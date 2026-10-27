"""Synthetic, local-only negative-path tests for read-only release inspector.

No Steam, Windows, network, NuGet restore or modification of real ZIPs.
Run: python3 -m unittest discover -s tests/ReleaseAssetAuditTests -v
"""
from __future__ import annotations

import hashlib
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import warnings
import zipfile


ROOT = Path(__file__).resolve().parents[2]
AUDITOR = ROOT / "scripts/audit-release-assets.py"
COMMIT = "67b0872dfa4a3e6627495c039097ac240b0efb14"


def sample_zip(path: Path, *, changes: dict[str, bytes] | None = None,
               wrong_manifest: bool = False, version: str = "0.6.0-preview.10",
               extra: list[tuple[str, bytes | zipfile.ZipInfo]] | None = None):
    files = {"SE2WorkshopUGCFix.Wpf.exe": b"MZ fake test image",
             "RUN-WPF-SMOKE.cmd": b"@echo off"}
    if changes:
        files.update(changes)
    manifest = [
        f"SOURCE_COMMIT={COMMIT}", f"VERSION={version}",
        "ALGORITHM=SHA256", "UNSIGNED_TEST_ARTIFACT=true", ""
    ]
    for name, data in sorted(files.items()):
        manifest.append(f"{hashlib.sha256(data).hexdigest()}  {name}")
    if wrong_manifest:
        manifest[-1] = "0" * 64 + "  " + manifest[-1].split("  ", 1)[1]
    with zipfile.ZipFile(path, "w", compression=zipfile.ZIP_DEFLATED) as z:
        for name, data in files.items():
            z.writestr(name, data)
        z.writestr("SHA256-MANIFEST.txt", "\n".join(manifest) + "\n")
        if extra:
            for name, data in extra:
                if isinstance(data, zipfile.ZipInfo):
                    z.writestr(data, b"dummy")
                else:
                    z.writestr(name, data)


def sample_nupkg(path: Path, *, pkgid: str = "Steamworks.NET.AnyCPU",
                 version: str = "2026.165.2", license: str = "MIT",
                 missing_native: bool = False, override_nested: bool = False,
                 duplicated_id: bool = False):
    namespace = 'xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"'
    meta = f"<id>{pkgid}</id><version>{version}</version><license type='expression'>{license}</license>"
    if duplicated_id:
        meta += "<id>untrusted</id>"
    if override_nested:
        meta += "<dependencies><group><id>other</id><version>0.0.0</version></group></dependencies>"
    xml = f"<?xml version='1.0'?><package {namespace}><metadata>{meta}</metadata></package>"
    with zipfile.ZipFile(path, "w") as z:
        z.writestr("Steamworks.NET.AnyCPU.nuspec", xml)
        if not missing_native:
            z.writestr("runtimes/win-x64/native/steam_api64.dll", b"MZ dummy native")


class AssetAuditTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="se2-asset-audit-")
        self.addCleanup(self.temp.cleanup)
        self.dir = Path(self.temp.name)

    def run_audit(self, *, zip_path=None, nupkg=None, sha=COMMIT, passes=True):
        args = [sys.executable, str(AUDITOR)]
        if zip_path:
            args += ["--zip", str(zip_path), "--expected-commit", sha]
        if nupkg:
            args += ["--nupkg", str(nupkg)]
        p = subprocess.run(args, capture_output=True, text=True, check=False, timeout=20)
        self.assertEqual(p.returncode == 0, passes, p.stdout + "\n" + p.stderr)
        return p.stdout + p.stderr

    def test_integrity_pass_is_not_release_authorisation(self):
        path = self.dir / "valid.zip"
        sample_zip(path)
        output = self.run_audit(zip_path=path)
        self.assertIn("PASS: WPF ZIP member coverage", output)
        self.assertIn("NONE (release blocker)", output)
        self.assertIn("NOT A PUBLIC RELEASE APPROVAL", output)

    def test_manifest_hash_mismatch_rejected(self):
        path = self.dir / "tampered.zip"
        sample_zip(path, wrong_manifest=True)
        self.run_audit(zip_path=path, passes=False)

    def test_wrong_source_commit_rejected(self):
        path = self.dir / "different.zip"
        sample_zip(path)
        self.run_audit(zip_path=path, sha="b" * 40, passes=False)

    def test_wrong_version_rejected(self):
        path = self.dir / "old.zip"
        sample_zip(path, version="0.6.0-preview.9")
        self.run_audit(zip_path=path, passes=False)

    def test_duplicate_zip_member_rejected(self):
        path = self.dir / "duplicate.zip"
        with warnings.catch_warnings():
            warnings.simplefilter("ignore", UserWarning)
            sample_zip(path, extra=[("RUN-WPF-SMOKE.cmd", b"bad")])
        self.run_audit(zip_path=path, passes=False)

    def test_path_traversal_rejected(self):
        path = self.dir / "traversal.zip"
        sample_zip(path, extra=[("../private.txt", b"x")])
        self.run_audit(zip_path=path, passes=False)

    def test_windows_case_collision_rejected(self):
        path = self.dir / "case.zip"
        sample_zip(path, extra=[("run-wpf-smoke.cmd", b"x")])
        self.run_audit(zip_path=path, passes=False)

    def test_windows_reserved_name_rejected(self):
        path = self.dir / "reserved.zip"
        sample_zip(path, extra=[("CON.txt", b"x")])
        self.run_audit(zip_path=path, passes=False)

    def test_invalid_windows_stream_path_rejected(self):
        path = self.dir / "alternate-stream.zip"
        sample_zip(path, extra=[("data:secret", b"x")])
        self.run_audit(zip_path=path, passes=False)

    def test_symlink_rejected(self):
        path = self.dir / "symlink.zip"
        item = zipfile.ZipInfo("link-to-exe")
        item.create_system = 3
        item.external_attr = (0o120777 << 16)
        sample_zip(path, extra=[("link-to-exe", item)])
        self.run_audit(zip_path=path, passes=False)

    def test_nupkg_metadata_and_native_asset_pass(self):
        path = self.dir / "good.nupkg"
        sample_nupkg(path)
        output = self.run_audit(nupkg=path)
        self.assertIn("PASS: pinned Steamworks.NET.AnyCPU", output)
        self.assertIn("NuGet steam_api64.dll SHA256:", output)

    def test_nested_metadata_cannot_override_canonical_fields(self):
        path = self.dir / "nested.nupkg"
        sample_nupkg(path, override_nested=True)
        self.run_audit(nupkg=path)

    def test_wrong_nupkg_version_rejected(self):
        path = self.dir / "wrong-version.nupkg"
        sample_nupkg(path, version="2026.165.3")
        self.run_audit(nupkg=path, passes=False)

    def test_wrong_nupkg_license_rejected(self):
        path = self.dir / "wrong-license.nupkg"
        sample_nupkg(path, license="NOASSERTION")
        self.run_audit(nupkg=path, passes=False)

    def test_nupkg_duplicate_id_rejected(self):
        path = self.dir / "duplicate-id.nupkg"
        sample_nupkg(path, duplicated_id=True)
        self.run_audit(nupkg=path, passes=False)

    def test_missing_native_dll_rejected(self):
        path = self.dir / "no-native.nupkg"
        sample_nupkg(path, missing_native=True)
        self.run_audit(nupkg=path, passes=False)


if __name__ == "__main__":
    unittest.main()
