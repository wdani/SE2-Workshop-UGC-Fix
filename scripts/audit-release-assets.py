#!/usr/bin/env python3
"""Read-only WPF ZIP/NuGet asset audit; never executes the application.

This DOES NOT establish publisher authenticity, native DLL entitlement, or
the contents of native files embedded in a .NET single-file executable.
"""
from __future__ import annotations

import argparse
import hashlib
from pathlib import Path, PurePosixPath
import re
import sys
import xml.etree.ElementTree as ET
import zipfile

PKG_ID = "Steamworks.NET.AnyCPU"
PKG_VERSION = "2026.165.2"
NATIVE = "runtimes/win-x64/native/steam_api64.dll"
EXPECTED_VERSION = "0.6.0-preview.10"
MAX_MEMBERS = 1500
MAX_UNCOMPRESSED = 1_000_000_000
MAX_MANIFEST = 4_000_000
MAX_NUSPEC = 1_000_000
WINDOWS_RESERVED = {
    "CON", "PRN", "AUX", "NUL",
    *(f"COM{i}" for i in range(1, 10)),
    *(f"LPT{i}" for i in range(1, 10)),
}


def sha256_stream(stream) -> str:
    digest = hashlib.sha256()
    for chunk in iter(lambda: stream.read(1024 * 1024), b""):
        digest.update(chunk)
    return digest.hexdigest()


def members(z: zipfile.ZipFile, *, allow_dirs: bool = False) -> dict[str, zipfile.ZipInfo]:
    result: dict[str, zipfile.ZipInfo] = {}
    total = 0
    windows_names: set[str] = set()
    if len(z.infolist()) > MAX_MEMBERS:
        raise ValueError("Excessive ZIP member count")
    for entry in z.infolist():
        name = entry.filename
        is_dir = entry.is_dir()
        normalized = name[:-1] if is_dir else name
        pp = PurePosixPath(normalized)
        parts = pp.parts
        win_key = normalized.casefold()
        if (not normalized or name in result or win_key in windows_names
                or (is_dir and (not allow_dirs or entry.file_size))
                or pp.is_absolute() or pp.as_posix() != normalized
                or "\\" in normalized or ":" in normalized or ".." in parts
                or any(part.rstrip(". ") != part or
                       part.split(".", 1)[0].upper() in WINDOWS_RESERVED
                       for part in parts)
                or any(ord(ch) < 32 or ord(ch) == 127 for ch in normalized)
                or (entry.external_attr >> 16) & 0o170000 == 0o120000):
            raise ValueError("Unsafe, duplicate or symlink ZIP member")
        total += entry.file_size
        if total > MAX_UNCOMPRESSED:
            raise ValueError("ZIP uncompressed size limit exceeded")
        result[name] = entry
        windows_names.add(win_key)
    return result


def audit_zip(path: Path, expected_commit: str | None) -> None:
    with zipfile.ZipFile(path) as z:
        entries = members(z)
        if "SHA256-MANIFEST.txt" not in entries:
            raise ValueError("Missing SHA256-MANIFEST.txt")
        if entries["SHA256-MANIFEST.txt"].file_size > MAX_MANIFEST:
            raise ValueError("Manifest exceeds size limit")
        lines = z.read("SHA256-MANIFEST.txt").decode("utf-8").splitlines()
        if len(lines) < 6 or not lines[0].startswith("SOURCE_COMMIT="):
            raise ValueError("Invalid manifest header")
        commit = lines[0].split("=", 1)[1]
        if not re.fullmatch("[0-9a-f]{40}", commit):
            raise ValueError("Invalid source commit")
        if expected_commit and commit != expected_commit:
            raise ValueError("Source commit mismatch")
        expected_header = [
            f"SOURCE_COMMIT={commit}", f"VERSION={EXPECTED_VERSION}",
            "ALGORITHM=SHA256", "UNSIGNED_TEST_ARTIFACT=true", "",
        ]
        if lines[:5] != expected_header:
            raise ValueError("Unexpected manifest format or version")
        known = {}
        for line in lines[5:]:
            if "  " not in line:
                raise ValueError("Malformed manifest hash entry")
            digest, name = line.split("  ", 1)
            if not re.fullmatch("[0-9a-f]{64}", digest) or not name or name in known:
                raise ValueError("Invalid or duplicate digest entry")
            known[name] = digest
        if set(known) != set(entries) - {"SHA256-MANIFEST.txt"}:
            raise ValueError("Not all ZIP members covered by SHA256 manifest")
        for name, digest in known.items():
            with z.open(entries[name]) as stream:
                if sha256_stream(stream) != digest:
                    raise ValueError("Member SHA256 mismatch")
        if "SE2WorkshopUGCFix.Wpf.exe" not in entries:
            raise ValueError("Expected Windows executable absent")
        if any("FIXTURE-LAB" in name.upper() or name == "SE2-Workshop-UGC-Fix.exe"
               for name in entries):
            raise ValueError("Fixture lab or legacy executable in consumer ZIP")
        present = [name for name in (
            "LICENSE", "licenses/Steamworks.NET.AnyCPU-MIT.txt",
            "THIRD-PARTY-NOTICES.txt") if name in entries]
        print("PASS: WPF ZIP member coverage, source and SHA256 content checks")
        print("  source:", commit)
        print("  members:", len(entries))
        print("  visible native DLL:", any(name.endswith("steam_api64.dll") for name in entries))
        print("  license/notice files:", ", ".join(present) or "NONE (release blocker)")
    with path.open("rb") as stream:
        print("  ZIP SHA256:", sha256_stream(stream))
    print("NOTE: Single-file .NET executable may embed native DLLs; no bundle extraction done.")


def audit_nupkg(path: Path, expected_native_git_blob: str | None = None,
                expected_native_size: int | None = None) -> None:
    with zipfile.ZipFile(path) as z:
        # NuGet archives may legitimately record empty directory entries;
        # consumer WPF ZIPs do not and remain strict.
        entries = members(z, allow_dirs=True)
        nuspecs = [name for name in entries if name.lower().endswith(".nuspec")]
        if len(nuspecs) != 1:
            raise ValueError("Expected exactly one NuGet .nuspec")
        if entries[nuspecs[0]].file_size > MAX_NUSPEC:
            raise ValueError("NuGet metadata exceeds size limit")
        tree = ET.fromstring(z.read(nuspecs[0]))
        if tree.tag.rsplit("}", 1)[-1] != "package":
            raise ValueError("Invalid NuGet root element")
        metadata = next(
            (child for child in tree if child.tag.rsplit("}", 1)[-1] == "metadata"),
            None,
        )
        if metadata is None:
            raise ValueError("NuGet metadata missing")
        meta = {}
        for node in metadata:
            key = node.tag.rsplit("}", 1)[-1]
            if key in ("id", "version", "license"):
                if key in meta:
                    raise ValueError("Duplicate NuGet metadata field")
                meta[key] = (node.text or "").strip()
        if (meta.get("id", "").lower() != PKG_ID.lower()
                or meta.get("version") != PKG_VERSION
                or meta.get("license") != "MIT"):
            raise ValueError("NuGet package identity, version or license mismatch")
        if NATIVE not in entries:
            raise ValueError("Pinned NuGet package lacks expected win-x64 native DLL")
        native_size = entries[NATIVE].file_size
        if expected_native_size is not None and native_size != expected_native_size:
            raise ValueError("Native DLL size differs from upstream pinned source")
        # Git object IDs hash "blob <size>\\0" + content (NOT a file's raw SHA-1).
        # This is a source-byte comparison, not a signature or proof of Valve rights.
        upstream_blob = hashlib.sha1(f"blob {native_size}\\0".encode("ascii"))
        native_sha = hashlib.sha256()
        with z.open(entries[NATIVE]) as stream:
            for chunk in iter(lambda: stream.read(1024 * 1024), b""):
                native_sha.update(chunk)
                upstream_blob.update(chunk)
        upstream_blob_sha = upstream_blob.hexdigest()
        if expected_native_git_blob is not None:
            if not re.fullmatch("[0-9a-f]{40}", expected_native_git_blob):
                raise ValueError("Expected upstream Git blob ID is not SHA-1 format")
            if upstream_blob_sha != expected_native_git_blob:
                raise ValueError("Native DLL differs from pinned upstream Git blob")
        print("PASS: pinned Steamworks.NET.AnyCPU package identity/metadata")
        print("  package:", meta["id"], meta["version"])
        print("  managed wrapper license:", meta["license"])
        print("  NuGet steam_api64.dll SHA256:", native_sha.hexdigest())
        print("  NuGet steam_api64.dll Git blob:", upstream_blob_sha)
        print("  NuGet steam_api64.dll bytes:", native_size)
        if expected_native_git_blob is not None:
            print("  Pin confirmed: NuGet DLL matches known upstream Git blob")
    with path.open("rb") as stream:
        print("  NuGet package SHA256:", sha256_stream(stream))
    print("NOTE: DLL from NuGet is NOT proof of Valve redistribution rights.")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--zip", type=Path, help="Local unreleased WPF ZIP")
    parser.add_argument("--nupkg", type=Path, help="Locally obtained pinned NuGet .nupkg")
    parser.add_argument("--expected-commit", help="Git SHA to compare with ZIP manifest")
    parser.add_argument("--expected-native-git-blob",
                        help="Known upstream Git blob SHA-1 (requires --nupkg)")
    parser.add_argument("--expected-native-size", type=int,
                        help="Known upstream DLL size in bytes (requires --nupkg)")
    args = parser.parse_args()
    if not args.zip and not args.nupkg:
        parser.error("Provide --zip and/or --nupkg")
    if args.expected_commit and not args.zip:
        parser.error("--expected-commit requires --zip")
    if (args.expected_native_git_blob is not None or args.expected_native_size is not None) and not args.nupkg:
        parser.error("--expected-native-* options require --nupkg")
    if args.expected_native_size is not None and args.expected_native_size <= 0:
        parser.error("--expected-native-size must be positive")
    try:
        if args.zip:
            audit_zip(args.zip, args.expected_commit)
        if args.nupkg:
            audit_nupkg(args.nupkg, args.expected_native_git_blob, args.expected_native_size)
        print("NOT A PUBLIC RELEASE APPROVAL: provenance, licenses and publisher still unverified")
        return 0
    except (OSError, ValueError, UnicodeError, zipfile.BadZipFile, ET.ParseError) as exc:
        print("AUDIT FAILED:", exc, file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
