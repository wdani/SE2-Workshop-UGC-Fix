"""Offline regression tests for the OPTIONAL future release notice gate.

All archives are synthetic in TemporaryDirectory; no app or Steam startup.
Run: python3 -m unittest discover -s tests/ReleaseAssetAuditTests -v
"""
from __future__ import annotations

import hashlib
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import zipfile


ROOT = Path(__file__).resolve().parents[2]
GATE = ROOT / "scripts/check-release-notices.py"
COMMIT = "67b0872dfa4a3e6627495c039097ac240b0efb14"
NOTICE = (
    "Steamworks.NET.AnyCPU 2026.165.2: MIT managed wrapper; "
    "Valve native steam_api64.dll has separate conditions. "
    "Microsoft .NET 10 self-contained runtime carries additional notices "
    "requiring independent review before public distribution.\n"
)


def fixture(path: Path, *, include=("LICENSE", "licenses/Steamworks.NET.AnyCPU-MIT.txt",
                                   "THIRD-PARTY-NOTICES.txt"),
            change=None, manifest_hash_error=False) -> None:
    files = {"SE2WorkshopUGCFix.Wpf.exe": b"MZ synthetic, not executable"}
    for name in include:
        files[name] = (NOTICE.encode() if name == "THIRD-PARTY-NOTICES.txt"
                       else (ROOT / name).read_bytes())
    files.update(change or {})
    manifest = [
        f"SOURCE_COMMIT={COMMIT}", "VERSION=0.6.0-preview.10",
        "ALGORITHM=SHA256", "UNSIGNED_TEST_ARTIFACT=true", ""
    ]
    for name, content in sorted(files.items()):
        h = hashlib.sha256(content).hexdigest()
        if manifest_hash_error and name == "SE2WorkshopUGCFix.Wpf.exe":
            h = "0" * 64
        manifest.append(h + "  " + name)
    with zipfile.ZipFile(path, "w", compression=zipfile.ZIP_DEFLATED) as z:
        for name, content in files.items():
            z.writestr(name, content)
        z.writestr("SHA256-MANIFEST.txt", "\n".join(manifest) + "\n")


class ReleaseNoticeGateTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory(prefix="se2-release-gate-")
        self.addCleanup(temp.cleanup)
        self.path = Path(temp.name) / "candidate.zip"

    def run_gate(self, allowed: bool, commit: str = COMMIT):
        p = subprocess.run([sys.executable, str(GATE), "--zip", str(self.path),
                            "--expected-commit", commit],
                           capture_output=True, text=True, timeout=20)
        self.assertEqual(p.returncode == 0, allowed, p.stdout + "\n" + p.stderr)
        return p.stdout + p.stderr

    def test_genuine_license_notices_pass_content_check_only(self):
        fixture(self.path)
        output = self.run_gate(True)
        self.assertIn("MIT project/wrapper notices match", output)
        self.assertIn("NO PUBLIC RELEASE APPROVAL", output)

    def test_missing_project_license_blocked(self):
        fixture(self.path, include=("licenses/Steamworks.NET.AnyCPU-MIT.txt",
                                    "THIRD-PARTY-NOTICES.txt"))
        self.run_gate(False)

    def test_missing_wrapper_notice_blocked(self):
        fixture(self.path, include=("LICENSE", "THIRD-PARTY-NOTICES.txt"))
        self.run_gate(False)

    def test_missing_third_party_index_blocked(self):
        fixture(self.path, include=("LICENSE", "licenses/Steamworks.NET.AnyCPU-MIT.txt"))
        self.run_gate(False)

    def test_forged_project_license_even_with_valid_hash_blocked(self):
        fixture(self.path, change={"LICENSE": b"MIT License\nForged contents"})
        self.run_gate(False)

    def test_forged_wrapper_license_even_with_valid_hash_blocked(self):
        fixture(self.path, change={"licenses/Steamworks.NET.AnyCPU-MIT.txt": b"MIT\nWrong"})
        self.run_gate(False)

    def test_empty_third_party_index_blocked(self):
        fixture(self.path, change={"THIRD-PARTY-NOTICES.txt": b""})
        self.run_gate(False)

    def test_missing_native_notice_mention_blocked(self):
        fixture(self.path, change={"THIRD-PARTY-NOTICES.txt": (
            "Steamworks.NET.AnyCPU 2026.165.2 Microsoft .NET 10\n").encode()})
        self.run_gate(False)

    def test_missing_dotnet_notice_mention_blocked(self):
        fixture(self.path, change={"THIRD-PARTY-NOTICES.txt": (
            "Steamworks.NET.AnyCPU 2026.165.2 Valve steam_api64.dll\n").encode()})
        self.run_gate(False)

    def test_source_commit_mismatch_blocked(self):
        fixture(self.path)
        self.run_gate(False, commit="a" * 40)

    def test_tampered_manifest_blocked(self):
        fixture(self.path, manifest_hash_error=True)
        self.run_gate(False)

    def test_gate_does_not_modify_archive(self):
        fixture(self.path)
        before = hashlib.sha256(self.path.read_bytes()).digest()
        self.run_gate(True)
        after = hashlib.sha256(self.path.read_bytes()).digest()
        self.assertEqual(before, after)


if __name__ == "__main__":
    unittest.main()
