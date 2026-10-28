"""Synthetic official NuGet registration/catalog SHA512 + upstream Git tests.

All downloads are mocked (no real HTTP), all archives temporary.
"""
from __future__ import annotations

import base64
import hashlib
import io
import json
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
        self.catalog_url = "https://api.nuget.org/v3/catalog0/data/2026.10.10.01.02.03/steamworks.net.anycpu.2026.165.2.json"

    def fake_download(self, *, data=None, hash_digest=None, package_version=None,
                      pkg_link=None, catalog_id=None):
        package = self.package if data is None else data
        good_digest = base64.b64encode(hashlib.sha512(package).digest()).decode()
        digest = good_digest if hash_digest is None else hash_digest
        url = self.globals["PKG_URL"]
        index = {
            "items": [{
                "items": [{
                    "catalogEntry": {
                        "@id": self.catalog_url,
                        "id": "Steamworks.NET.AnyCPU",
                        "version": "2026.165.2",
                    },
                    "packageContent": url if pkg_link is None else pkg_link,
                }]
            }]
        }
        catalog = {
            "id": "Steamworks.NET.AnyCPU" if catalog_id is None else catalog_id,
            "version": "2026.165.2" if package_version is None else package_version,
            "packageHashAlgorithm": "SHA512",
            "packageHash": digest,
            "packageSize": len(package),
        }
        def get(url, max_bytes):
            if url == self.globals["REG_URL"]:
                return json.dumps(index).encode()
            if url == self.catalog_url:
                return json.dumps(catalog).encode()
            if url == self.globals["PKG_URL"]:
                return package
            raise ValueError("Unexpected mocked NuGet download URL")
        self.globals["verified_fetch"] = get

    def test_valid_pinned_nuget_sha512_and_git_blob(self):
        self.fake_download()
        buf = io.StringIO()
        with redirect_stdout(buf):
            self.probe()
        self.assertIn("official NuGet catalog SHA512", buf.getvalue())
        self.assertIn("Pin confirmed: NuGet DLL matches known upstream Git blob", buf.getvalue())
        self.assertIn("NOT APPROVED FOR REDISTRIBUTION", buf.getvalue())

    def test_wrong_catalog_sha512_blocks(self):
        self.fake_download(hash_digest=base64.b64encode(b"0" * 64).decode())
        with self.assertRaisesRegex(ValueError, "catalog SHA512"):
            self.probe()

    def test_correct_catalog_hash_but_wrong_upstream_git_blob_blocks(self):
        self.fake_download()
        self.globals["EXPECTED_NATIVE_GIT_BLOB"] = "f" * 40
        with self.assertRaisesRegex(ValueError, "Git blob"):
            self.probe()

    def test_wrong_nuget_package_version_still_blocked(self):
        with tempfile.TemporaryDirectory() as td:
            path = Path(td) / "wrong.nupkg"
            sample_nupkg(path, version="2026.165.3")
            self.fake_download(data=path.read_bytes())
            with self.assertRaisesRegex(ValueError, "NuGet package identity, version or license mismatch"):
                self.probe()

    def test_catalog_identity_mismatch_blocked(self):
        self.fake_download(catalog_id="Unrelated.Package")
        with self.assertRaisesRegex(ValueError, "catalog identity"):
            self.probe()

    def test_registration_unexpected_package_url_blocked(self):
        self.fake_download(pkg_link="https://example.net/evil.nupkg")
        with self.assertRaisesRegex(ValueError, "Unexpected NuGet package URL"):
            self.probe()

    def test_registration_endpoint_matches_official_nuget_service_index(self):
        # Official https://api.nuget.org/v3/index.json resource
        # RegistrationsBaseUrl/3.6.0 as verified 2026-10-28.
        self.assertEqual(
            self.globals["REG_URL"],
            "https://api.nuget.org/v3/registration5-gz-semver2/"
            "steamworks.net.anycpu/index.json",
        )

    def test_safe_paginated_registration_page_is_supported(self):
        self.fake_download()
        real_read_json = self.globals["read_json"]
        subpage = ("https://api.nuget.org/v3/registration5-gz-semver2/"
                   "steamworks.net.anycpu/2026.165.0-2026.165.3.json")
        def paginated(url):
            if url == self.globals["REG_URL"]:
                return {"items": [{"@id": subpage}]}
            if url == subpage:
                return {
                    "items": [{
                        "catalogEntry": {
                            "@id": self.catalog_url,
                            "id": "Steamworks.NET.AnyCPU",
                            "version": "2026.165.2",
                        },
                        "packageContent": self.globals["PKG_URL"],
                    }]
                }
            return real_read_json(url)
        self.globals["read_json"] = paginated
        buf = io.StringIO()
        with redirect_stdout(buf):
            self.probe()
        self.assertIn("pinned NuGet win-x64 DLL matches", buf.getvalue())

    def test_wrong_registration_page_base_is_rejected(self):
        self.fake_download()
        self.globals["read_json"] = lambda url: {
            "items": [{"@id": (
                "https://api.nuget.org/v3/registration5-semver2/"
                "steamworks.net.anycpu/page.json"
            )}]
        }
        with self.assertRaisesRegex(ValueError, "Unapproved NuGet registration page URL"):
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
