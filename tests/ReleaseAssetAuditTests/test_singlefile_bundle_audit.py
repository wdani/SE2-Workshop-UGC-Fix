"""Offline, synthetic tests for the READ-ONLY .NET bundle manifest inspection.

No Steam, no Windows binary execution, no downloaded DLLs. The miniature
"MZ" byte fixtures are deliberately not runnable Windows executables.
"""
from __future__ import annotations

import hashlib
import io
import os
from pathlib import Path
import runpy
import struct
import tempfile
import unittest
import zipfile
import zlib
from contextlib import redirect_stdout

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts" / "inspect-preview10-singlefile.py"
MODULE = runpy.run_path(str(SCRIPT))
parse_bundle = MODULE["parse_bundle"]
native_hash = MODULE["native_file_sha256"]
audit_archive = MODULE["audit_archive"]
SIG = MODULE["SIGNATURE"]
COMMIT = MODULE["EXPECTED_SOURCE_COMMIT"]


def bstring(text: str) -> bytes:
    encoded = text.encode()
    length = len(encoded)
    result = bytearray()
    while length >= 128:
        result.append((length & 0x7f) | 128)
        length >>= 7
    result.append(length)
    return bytes(result) + encoded


def fake_bundle(native: bytes = b"MZ synthetic steam DLL", *, name: str = "steam_api64.dll",
                filetype: int = 2, compressed: bool = False,
                major: int = 6, minor: int = 0, duplicate: bool = False,
                corruption: bool = False) -> bytes:
    host = bytearray(b"MZ" + bytes(510))
    records = [(name, native, filetype)]
    if duplicate:
        records.append(("runtimes/win-x64/native/steam_api64.dll", native, 2))
    current = len(host)
    manifest_entries = []
    for path, data, kind in records:
        if compressed:
            compressor = zlib.compressobj(wbits=-15)
            payload = compressor.compress(data) + compressor.flush()
            if corruption:
                payload = payload[:-1] + bytes([payload[-1] ^ 0xff])
        else:
            payload = data
        manifest_entries.append(
            struct.pack("<qqqB", current, len(data), len(payload) if compressed else 0, kind)
            + bstring(path)
        )
        host.extend(payload)
        current += len(payload)
    header_pos = current
    manifest = (struct.pack("<IIi", major, minor, len(records))
                + bstring("DUMMYBUNDLEID")
                + struct.pack("<qqqqQ", 0, 0, 0, 0, 0)
                + b"".join(manifest_entries))
    host.extend(manifest)
    host[128:136] = struct.pack("<q", header_pos)
    host[136:168] = SIG
    return bytes(host)


def build_zip(path: Path, apphost: bytes) -> str:
    contents = {"SE2WorkshopUGCFix.Wpf.exe": apphost,
                "RUN-WPF-SMOKE.cmd": b"@echo off\r\n"}
    manifest = [
        f"SOURCE_COMMIT={COMMIT}",
        "VERSION=0.6.0-preview.10",
        "ALGORITHM=SHA256",
        "UNSIGNED_TEST_ARTIFACT=true",
        "",
    ]
    for name in sorted(contents):
        manifest.append(hashlib.sha256(contents[name]).hexdigest() + "  " + name)
    with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED) as archive:
        for name, data in contents.items():
            archive.writestr(name, data)
        archive.writestr("SHA256-MANIFEST.txt", "\n".join(manifest) + "\n")
    return hashlib.sha256(path.read_bytes()).hexdigest()


class SinglefileBundleAuditTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory(prefix="se2-bundle-tests-")
        self.addCleanup(temp.cleanup)
        self.zip_path = Path(temp.name) / "synthetic.zip"
        self.native = b"MZ synthetic steam DLL"
        self.native_sha = hashlib.sha256(self.native).hexdigest()

    def scan(self, exe: bytes, *, expected_hash: str | None = None,
             expected_size: int | None = None, zip_hash: str | None = None):
        actual_zip_hash = build_zip(self.zip_path, exe)
        return audit_archive(self.zip_path, actual_zip_hash if zip_hash is None else zip_hash,
                             COMMIT, self.native_sha if expected_hash is None else expected_hash,
                             len(self.native) if expected_size is None else expected_size)

    def test_plain_native_DLL_matches_exact_manifest_and_SHA256(self):
        buf = io.StringIO()
        with redirect_stdout(buf):
            result = self.scan(fake_bundle(self.native))
        self.assertEqual(result["steam_sha256"], self.native_sha)
        self.assertIn("PASS: embedded steam_api64.dll", buf.getvalue())

    def test_compressed_native_DLL_matches_raw_deflate_decoded_SHA256(self):
        with redirect_stdout(io.StringIO()):
            result = self.scan(fake_bundle(self.native, compressed=True))
        self.assertEqual(result["steam_size"], len(self.native))

    def test_bundle_audit_is_readonly_for_existing_zip(self):
        exe = fake_bundle(self.native)
        digest = build_zip(self.zip_path, exe)
        before = self.zip_path.read_bytes()
        with redirect_stdout(io.StringIO()):
            audit_archive(self.zip_path, digest, COMMIT, self.native_sha, len(self.native))
        self.assertEqual(self.zip_path.read_bytes(), before)

    def test_trusted_zip_SHA256_pin_must_match(self):
        with self.assertRaisesRegex(ValueError, "ZIP differs"):
            self.scan(fake_bundle(self.native), zip_hash="f" * 64)

    def test_mismatched_embedded_native_sha256_is_blocked(self):
        with self.assertRaisesRegex(ValueError, "SHA256 differs"):
            with redirect_stdout(io.StringIO()):
                self.scan(fake_bundle(self.native), expected_hash="0" * 64)

    def test_mismatched_embedded_native_bytecount_is_blocked(self):
        with self.assertRaisesRegex(ValueError, "NuGet size"):
            with redirect_stdout(io.StringIO()):
                self.scan(fake_bundle(self.native), expected_size=len(self.native) + 1)

    def test_bundle_without_native_DLL_is_blocked(self):
        with self.assertRaisesRegex(ValueError, "lacks exactly one"):
            with redirect_stdout(io.StringIO()):
                self.scan(fake_bundle(self.native, name="random-runtime.dll"))

    def test_duplicate_steam_DLL_basename_is_blocked(self):
        with self.assertRaisesRegex(ValueError, "lacks exactly one"):
            with redirect_stdout(io.StringIO()):
                self.scan(fake_bundle(self.native, duplicate=True))

    def test_steam_DLL_must_be_native_binary_type(self):
        with self.assertRaisesRegex(ValueError, "NativeBinary"):
            with redirect_stdout(io.StringIO()):
                self.scan(fake_bundle(self.native, filetype=1))

    def test_old_unsupported_bundle_version_is_blocked(self):
        with self.assertRaisesRegex(ValueError, "Unsupported"):
            parse_bundle(fake_bundle(self.native, major=2))

    def test_unknown_future_bundle_version_is_blocked(self):
        with self.assertRaisesRegex(ValueError, "Unsupported"):
            parse_bundle(fake_bundle(self.native, major=7))

    def test_missing_bundle_signature_is_blocked(self):
        exe = bytearray(fake_bundle(self.native))
        exe[136:168] = b"X" * 32
        with self.assertRaisesRegex(ValueError, "marker"):
            parse_bundle(bytes(exe))

    def test_bad_bundle_header_pointer_is_blocked(self):
        exe = bytearray(fake_bundle(self.native))
        exe[128:136] = struct.pack("<q", len(exe) + 1)
        with self.assertRaisesRegex(ValueError, "header offset"):
            parse_bundle(bytes(exe))

    def test_truncated_manifest_is_blocked(self):
        with self.assertRaisesRegex(ValueError, "manifest"):
            parse_bundle(fake_bundle(self.native)[:-12])

    def test_corrupt_compressed_Dll_cannot_report_pass(self):
        exe = fake_bundle(self.native, compressed=True, corruption=True)
        with self.assertRaises((ValueError, zlib.error)):
            with redirect_stdout(io.StringIO()):
                self.scan(exe)

    def test_duplicate_bundle_marker_is_blocked(self):
        exe = bytearray(fake_bundle(self.native))
        exe[175:207] = SIG
        with self.assertRaisesRegex(ValueError, "ambiguous"):
            parse_bundle(bytes(exe))


if __name__ == "__main__":
    unittest.main()
