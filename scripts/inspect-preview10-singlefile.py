#!/usr/bin/env python3
"""Read-only .NET 6+ single-file bundle inspector for the owner-tested preview.10.

This script NEVER executes the Windows EXE, .NET, Steam or bundled code. It
reads only an existing ZIP; its EXE is temporarily copied to a private
temporary directory for random-access inspection. No extraction of DLLs.
Not a signature, license entitlement or public release authorization.

Format pinned to Microsoft's .NET HostModel Manifest.cs, FileEntry.cs and
Bundler.cs (bundle major 6); reject unfamiliar versions rather than guess.
"""
from __future__ import annotations

import argparse
import hashlib
import mmap
from pathlib import Path, PurePosixPath
import re
import runpy
import shutil
import struct
import sys
import tempfile
import zipfile
import zlib

ROOT = Path(__file__).resolve().parents[1]
ZIP_NAME = "SE2-Workshop-UGC-Fix-0.6.0-preview.10-WPF-win-x64-LOCAL-TEST.zip"
DEFAULT_ZIP = ROOT / "artifacts" / ZIP_NAME
EXPECTED_ZIP_SHA256 = "634fd3d79d689b21379aaa348660fcc22065a75f34dc96022157923e2813973a"
EXPECTED_SOURCE_COMMIT = "67b0872dfa4a3e6627495c039097ac240b0efb14"
EXPECTED_NATIVE_SHA256 = "e6d9bafb9a41e42fba7b21553db49f8719027af3f0deb23ff86cfc44d60e776d"
EXPECTED_NATIVE_SIZE = 319128
EXE_MEMBER = "SE2WorkshopUGCFix.Wpf.exe"
SIGNATURE = bytes.fromhex(
    "8b1202b96a612038727b930214d7a03213f5b9e6efae3318ee3b2dce24b36aae"
)
MAX_EXE_BYTES = 750_000_000
MAX_FILES = 4000
MAX_BUNDLE_MANIFEST = 4_000_000
MAX_PATH_LENGTH = 4096
MAX_NATIVE_BYTES = 16_000_000


def file_sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as f:
        for data in iter(lambda: f.read(1024 * 1024), b""):
            digest.update(data)
    return digest.hexdigest()


class Cursor:
    def __init__(self, data, start: int, end: int):
        self.data = data
        self.pos = start
        self.end = end

    def read(self, count: int) -> bytes:
        if count < 0 or self.pos + count > self.end:
            raise ValueError("Truncated or invalid .NET bundle manifest")
        result = self.data[self.pos:self.pos + count]
        self.pos += count
        return result

    def num(self, fmt: str) -> int:
        return struct.unpack(fmt, self.read(struct.calcsize(fmt)))[0]

    def dotnet_string(self, max_bytes=MAX_PATH_LENGTH) -> str:
        # BinaryWriter.Write(string) uses 7-bit encoded byte length.
        length = 0
        for step in range(5):
            val = self.num("<B")
            length |= (val & 0x7f) << (7 * step)
            if not val & 0x80:
                break
        else:
            raise ValueError("Overlong .NET bundle string length")
        if not 0 < length <= max_bytes:
            raise ValueError("Unreasonable .NET bundle string length")
        return self.read(length).decode("utf-8", "strict")


def parse_bundle(blob) -> tuple[int, list[dict]]:
    if len(blob) < 128 or blob[:2] != b"MZ":
        raise ValueError("Expected Windows PE apphost (MZ header)")
    marker = blob.find(SIGNATURE)
    if marker < 8 or blob.find(SIGNATURE, marker + 1) >= 0:
        raise ValueError("Missing or ambiguous .NET bundle marker")
    header_at = struct.unpack("<q", blob[marker - 8:marker])[0]
    if not 64 <= header_at < len(blob) - 48:
        raise ValueError("Invalid single-file bundle header offset")
    # Do not let an invalid apphost offset turn into an unbounded parse.
    manifest_end = min(len(blob), header_at + MAX_BUNDLE_MANIFEST)
    cur = Cursor(blob, header_at, manifest_end)
    major, minor, count = cur.num("<I"), cur.num("<I"), cur.num("<i")
    if (major, minor) != (6, 0) or not 0 < count <= MAX_FILES:
        raise ValueError("Unsupported single-file bundle version or file count")
    _bundle_id = cur.dotnet_string(max_bytes=256)
    # Manifest.cs: header v2 location fields (4x Int64) + flags UInt64.
    for _ in range(4):
        cur.num("<q")
    cur.num("<Q")
    entries = []
    names = set()
    for _ in range(count):
        offset = cur.num("<q")
        size = cur.num("<q")
        packed = cur.num("<q")
        kind = cur.num("<B")
        name = cur.dotnet_string()
        path = PurePosixPath(name)
        if (kind > 5 or not 0 < size <= MAX_EXE_BYTES
                or packed < 0 or (packed and packed > MAX_EXE_BYTES)
                or offset <= 0
                or offset + (packed if packed else size) > header_at
                or path.as_posix() != name or path.is_absolute()
                or ".." in path.parts or "\\" in name
                or name.casefold() in names):
            raise ValueError("Invalid embedded bundle member metadata")
        names.add(name.casefold())
        entries.append({"name": name, "type": kind, "offset": offset,
                        "size": size, "compressed_size": packed})
    return major, entries


def native_file_sha256(blob, entry: dict) -> str:
    if entry["size"] > MAX_NATIVE_BYTES:
        raise ValueError("Embedded native DLL exceeds safe inspection limit")
    offset = entry["offset"]
    count = entry["compressed_size"] or entry["size"]
    digest = hashlib.sha256()
    actual = 0
    if entry["compressed_size"]:
        # .NET Bundler.cs uses DeflateStream (raw DEFLATE, no zlib wrapper).
        decompressor = zlib.decompressobj(wbits=-15)
        for index in range(offset, offset + count, 65536):
            chunk = blob[index:min(index + 65536, offset + count)]
            data = decompressor.decompress(chunk, MAX_NATIVE_BYTES + 1 - actual)
            actual += len(data)
            if actual > MAX_NATIVE_BYTES or decompressor.unconsumed_tail:
                raise ValueError("Embedded DLL decompression size exceeded")
            digest.update(data)
        tail = decompressor.flush(MAX_NATIVE_BYTES + 1 - actual)
        actual += len(tail)
        digest.update(tail)
        if not decompressor.eof or decompressor.unused_data:
            raise ValueError("Corrupt or trailing compressed DLL data")
    else:
        for index in range(offset, offset + count, 65536):
            data = blob[index:min(index + 65536, offset + count)]
            actual += len(data)
            digest.update(data)
    if actual != entry["size"]:
        raise ValueError("Embedded DLL decoded length mismatch")
    return digest.hexdigest()


def audit_archive(path: Path, zip_sha256: str, source_commit: str,
                  native_sha256: str, native_size: int) -> dict:
    if not re.fullmatch("[0-9a-f]{64}", zip_sha256):
        raise ValueError("Invalid trusted ZIP SHA256 pin")
    if file_sha256(path) != zip_sha256:
        raise ValueError("ZIP differs from owner-audited trusted preview.10 checksum")
    # Also independently re-check internal ZIP member hashes and commit.
    audit = runpy.run_path(str(ROOT / "scripts/audit-release-assets.py"))
    audit["audit_zip"](path, source_commit)
    with zipfile.ZipFile(path) as archive:
        members = audit["members"](archive)
        exe_info = members[EXE_MEMBER]
        if not 0 < exe_info.file_size <= MAX_EXE_BYTES:
            raise ValueError("EXE member exceeds safe inspection size")
        with tempfile.TemporaryDirectory(prefix="se2-bundle-readonly-") as td:
            exe_path = Path(td) / EXE_MEMBER
            with archive.open(exe_info, "r") as source, exe_path.open("xb") as dest:
                shutil.copyfileobj(source, dest, length=1024 * 1024)
            if exe_path.stat().st_size != exe_info.file_size:
                raise ValueError("Copied EXE member size mismatch")
            with exe_path.open("rb") as file, mmap.mmap(
                    file.fileno(), length=0, access=mmap.ACCESS_READ) as blob:
                major, entries = parse_bundle(blob)
                found = [entry for entry in entries
                         if PurePosixPath(entry["name"]).name.casefold() == "steam_api64.dll"]
                if len(found) != 1:
                    raise ValueError("Bundle lacks exactly one steam_api64.dll; cannot verify")
                native = found[0]
                if native["type"] != 2:
                    raise ValueError("Bundled Steam DLL is not classified NativeBinary")
                if native["size"] != native_size:
                    raise ValueError("Embedded Steam DLL differs from pinned NuGet size")
                actual_sha256 = native_file_sha256(blob, native)
                if actual_sha256 != native_sha256:
                    raise ValueError("Embedded Steam DLL SHA256 differs from pinned NuGet")
                native_total = sum(e["type"] == 2 for e in entries)
                print("PASS: embedded steam_api64.dll matches exact pinned NuGet SHA256")
                print("  bundle major version:", major)
                print("  bundle entries:", len(entries))
                print("  native binary entries:", native_total)
                print("  Steam native path:", native["name"])
                print("  Steam native bytes:", native["size"])
                print("  Steam native SHA256:", actual_sha256)
                print("  Steam native compressed:", bool(native["compressed_size"]))
                print("  contains coreclr.dll:", any(
                    PurePosixPath(e["name"]).name.casefold() == "coreclr.dll" for e in entries))
                return {"bundle_major": major, "entries": len(entries),
                        "steam_sha256": actual_sha256, "steam_size": native["size"]}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--zip", type=Path, default=DEFAULT_ZIP,
                        help="Original unmodified preview.10 LOCAL-TEST ZIP on Dev-Worker")
    args = parser.parse_args()
    try:
        audit_archive(args.zip, EXPECTED_ZIP_SHA256, EXPECTED_SOURCE_COMMIT,
                      EXPECTED_NATIVE_SHA256, EXPECTED_NATIVE_SIZE)
        print("NOT PUBLIC RELEASE APPROVAL: Valve SDK rights, exact Windows runtime")
        print("notices, binary publisher and WPF Steam-write safety still unqualified.")
        return 0
    except (OSError, ValueError, UnicodeError, zipfile.BadZipFile,
            struct.error, zlib.error) as exc:
        print("BUNDLE AUDIT FAILED:", exc, file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
