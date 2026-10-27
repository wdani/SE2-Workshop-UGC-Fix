"""Synthetic tests for official pinned NuGet retrieval and source comparison.

No network calls: each test replaces the downloader with known fixture bytes.
Original Windows/Steam package untouched.
"""
from __future__ import annotations

import base64
import hashlib
import io
from contextlib import redirect_stdout
from pathlib import Path
import runpy
import tempfile
import unittest

from test_audit_release_assets import sample_nupkg

ROOT = Path(__file__).resolve().parents[2]
PROBE = ROOT / "scripts/probe-official-nuget-provenance.py"


class PinnedNuGetProvenanceTests(unittest.TestCase):
    def setUp(self):
        self.module = runpy.run_path(str(PROBE))
        self.probe = self.module["run_probe"]
        self.globals = self.probe.__globals__
        tmp = tempfile.TemporaryDirectory(prefix="se2-provenance-tests-")
        self.addCleanup(tmp.cleanup)
        path = Path(tmp.name) / "fixture.nupkg"
        sample_nupkg(path)
        self.package = path.read_bytes()
        binary = b"MZ dummy native"
        git_input = b"blob " + str(len(binary)).encode() + b"\0" + binary
        self.globals["EXPECTED_NATIVE_SIZE"] = len(binary)
        self.globals["EXPECTED_NATIVE_GIT_BLOB"] = hashlib.sha1(git_input).hexdigest()

    def fake_download(self, *, data=None, hash_sidecar=None):
        package = self.package if data is None else data
        good_digest = base64.b64encode(hashlib.sha512(package).digest())
        sidecar = good_digest if hash_sidecar is None else hash_sidecar
        self.globals["verified_fetch"] = (
            lambda url, limit: sidecar if url.endswith(".sha512") else package
        )

    def test_valid_pinned_nuget_sha512_and_git_blob(self):
        self.fake_download()
        buf = io.StringIO()
        with redirect_stdout(buf):
            self.probe()
        self.assertIn("PASS: NuGet official v3 sidecar SHA512", buf.getvalue())
        self.assertIn("Pin confirmed: NuGet DLL matches known upstream Git blob", buf.getvalue())
        self.assertIn("NOT APPROVED FOR REDISTRIBUTION", buf.getvalue())

    def test_wrong_sha512_sidecar_blocks(self):
        self.fake_download(hash_sidecar=base64.b64encode(b"0" * 64))
        with self.assertRaisesRegex(ValueError, "SHA512 sidecar"):
            self.probe()

    def test_correct_nuget_hash_but_wrong_upstream_git_blob_blocks(self):
        self.fake_download()
        self.globals["EXPECTED_NATIVE_GIT_BLOB"] = "f" * 40
        with self.assertRaisesRegex(ValueError, "Git blob"):
            self.probe()

    def test_wrong_nuget_package_version_still_blocked(self):
        with tempfile.TemporaryDirectory() as td:
            path = Path(td) / "wrong.nupkg"
            sample_nupkg(path, version="2026.165.3")
            self.fake_download(data=path.read_bytes())
            with self.assertRaisesRegex(ValueError, "metadata"):
                self.probe()

    def test_disallowed_nuget_url_rejected(self):
        allowed = self.module["approved_https"]
        self.assertTrue(allowed("https://api.nuget.org/v3-flatcontainer/file"))
        for url in ("http://api.nuget.org/file",
                    "https://evil.example.net/anything",
                    "https://api.nuget.org.evil.example.net/a",
                    "https://user:password@api.nuget.org/file",
                    "https://api.nuget.org:8443/file"):
            with self.subTest(url=url):
                self.assertFalse(allowed(url))

    def test_cross_host_redirect_blocked_before_network(self):
        handler = self.module["PinnedNuGetRedirect"]()
        with self.assertRaisesRegex(ValueError, "redirect"):
            handler.redirect_request(None, None, 302, "Found", {},
                                     "https://evil.example.net/payload")


if __name__ == "__main__":
    unittest.main()
