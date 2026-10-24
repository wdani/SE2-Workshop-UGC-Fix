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


def sha256_stream(stream) -> str:
    digest = hashlib.sha256()
    for chunk in iter(lambda: stream.read(1024 * 1024), b""):
        digest.update(chunk)
    return digest.hexdigest()


def members(z: zipfile.ZipFile) -> dict[str, zipfile.ZipInfo]:
    result: dict[str, zipfile.ZipInfo] = {}
    total = 0
    if len(z.infolist()) > 1500:
        raise ValueError("Excessive ZIP member count")
    for entry in z.infolist():
        name = entry.filename
        pp = PurePosixPath(name)
        if (not name or name in result or pp.is_absolute()
                or "\\" in name or ".." in pp.parts or name.endswith("/")
                or (entry.external_attr >> 16) & 0o170000 == 0o120000):
            raise ValueError("Unsafe, duplicate or symlink ZIP member")
        total += entry.file_size
        if total > 1_000_000_000:
            raise ValueError("ZIP uncompressed size limit exceeded")
        result[name] = entry
    return result


def audit_zip(path: Path, expected_commit: str | None) -> None:
    with zipfile.ZipFile(path) as z:
        entries = members(z)
        if "SHA256-MANIFEST.txt" not in entries:
            raise ValueError("Missing SHA256-MANIFEST.txt")
        lines = z.read("SHA256-MANIFEST.txt").decode("utf-8").splitlines()
        if len(lines) < 6 or not lines[0].startswith("SOURCE_COMMIT="):
            raise ValueError("Invalid manifest header")
        commit = lines[0].split("=", 1)[1]
        if not re.fullmatch("[0-9a-f]{40}", commit):
            raise ValueError("Invalid source commit")
        if expected_commit and commit != expected_commit:
            raise ValueError("Source commit mismatch")
        if "ALGORITHM=SHA256" not in lines[:5] or "UNSIGNED_TEST_ARTIFACT=true" not in lines[:5]:
            raise ValueError("Unexpected manifest format")
        known = {}
        for line in lines[1:]:
            if not line or "  " not in line:
                continue
            digest, name = line.split("  ", 1)
            if not re.fullmatch("[0-9a-f]{64}", digest) or name in known:
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


def audit_nupkg(path: Path) -> None:
    with zipfile.ZipFile(path) as z:
        entries = members(z)
        nuspecs = [name for name in entries if name.lower().endswith(".nuspec")]
        if len(nuspecs) != 1:
            raise ValueError("Expected exactly one NuGet .nuspec")
        tree = ET.fromstring(z.read(nuspecs[0]))
        meta = {}
        for node in tree.iter():
            key = node.tag.rsplit("}", 1)[-1]
            if key in ("id", "version", "license"):
                meta[key] = (node.text or "").strip()
        if (meta.get("id", "").lower() != PKG_ID.lower()
                or meta.get("version") != PKG_VERSION
                or meta.get("license") != "MIT"):
            raise ValueError("NuGet package identity, version or license mismatch")
        if NATIVE not in entries:
            raise ValueError("Pinned NuGet package lacks expected win-x64 native DLL")
        with z.open(entries[NATIVE]) as stream:
            native_sha = sha256_stream(stream)
        print("PASS: pinned Steamworks.NET.AnyCPU package identity/metadata")
        print("  package:", meta["id"], meta["version"])
        print("  managed wrapper license:", meta["license"])
        print("  NuGet steam_api64.dll SHA256:", native_sha)
        print("  NuGet steam_api64.dll bytes:", entries[NATIVE].file_size)
    with path.open("rb") as stream:
        print("  NuGet package SHA256:", sha256_stream(stream))
    print("NOTE: DLL from NuGet is NOT proof of Valve redistribution rights.")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--zip", type=Path, help="Local unreleased WPF ZIP")
    parser.add_argument("--nupkg", type=Path, help="Locally obtained pinned NuGet .nupkg")
    parser.add_argument("--expected-commit", help="Git SHA to compare with ZIP manifest")
    args = parser.parse_args()
    if not args.zip and not args.nupkg:
        parser.error("Provide --zip and/or --nupkg")
    if args.expected_commit and not args.zip:
        parser.error("--expected-commit requires --zip")
    try:
        if args.zip:
            audit_zip(args.zip, args.expected_commit)
        if args.nupkg:
            audit_nupkg(args.nupkg)
        print("NOT A PUBLIC RELEASE APPROVAL: provenance, licenses and publisher still unverified")
        return 0
    except (OSError, ValueError, UnicodeError, zipfile.BadZipFile, ET.ParseError) as exc:
        print("AUDIT FAILED:", exc, file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
