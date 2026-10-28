#!/usr/bin/env python3
"""Pinned NuGet package + documented official Catalog SHA512 provenance probe.

Fetches official NuGet.org registration/catalog metadata and exact .nupkg,
checks the catalog's SHA512 and package size, then compares win-x64 native
DLL to pinned upstream Git blob. Never launches Steam or the application.
No legal redistribution approval, code signing or EXE bundle validation.
"""
from __future__ import annotations

import base64
import binascii
import hashlib
import json
import zlib
from pathlib import Path
import re
import runpy
import tempfile
import urllib.error
import urllib.parse
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
AUDITOR = ROOT / "scripts" / "audit-release-assets.py"
PKG_ID = "steamworks.net.anycpu"
PKG_VERSION = "2026.165.2"
PKG_NAME = f"{PKG_ID}.{PKG_VERSION}.nupkg"
PKG_URL = f"https://api.nuget.org/v3-flatcontainer/{PKG_ID}/{PKG_VERSION}/{PKG_NAME}"
REG_URL = f"https://api.nuget.org/v3/registration5-semver2/{PKG_ID}/index.json"
EXPECTED_NATIVE_SIZE = 319128
EXPECTED_NATIVE_GIT_BLOB = "0ca8f77b8a315d2d43ea3087623f61c8fbe9ca3e"
MAX_NUPKG_BYTES = 20_000_000
MAX_METADATA_BYTES = 3_000_000
ALLOWED_HOSTS = frozenset({"api.nuget.org", "globalcdn.nuget.org", "www.nuget.org"})


def approved_https(url: str) -> bool:
    parsed = urllib.parse.urlsplit(url)
    return (parsed.scheme == "https" and parsed.hostname in ALLOWED_HOSTS
            and parsed.username is None and parsed.password is None
            and parsed.port in (None, 443) and not parsed.fragment)


class PinnedNuGetRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request, fp, code, msg, headers, newurl):
        if not approved_https(newurl):
            raise ValueError("Unapproved NuGet redirect destination")
        return super().redirect_request(request, fp, code, msg, headers, newurl)


def verified_fetch(url: str, max_bytes: int) -> bytes:
    if not approved_https(url):
        raise ValueError("Unapproved NuGet endpoint")
    opener = urllib.request.build_opener(PinnedNuGetRedirect)
    try:
        request = urllib.request.Request(url, headers={"Accept-Encoding": "gzip, identity"})
        with opener.open(request, timeout=30) as response:
            if not approved_https(response.geturl()):
                raise ValueError("Unapproved NuGet redirect destination")
            length = response.headers.get("Content-Length")
            if length and int(length) > max_bytes:
                raise ValueError("Remote NuGet response exceeds size limit")
            encoding = response.headers.get("Content-Encoding", "identity").lower()
            if encoding not in ("identity", "gzip"):
                raise ValueError("Unsupported NuGet content encoding")
            data = response.read(max_bytes + 1)
    except urllib.error.HTTPError as exc:
        raise ValueError(f"NuGet HTTP {exc.code} for {url}") from exc
    if not data or len(data) > max_bytes:
        raise ValueError("Remote NuGet response empty or exceeds size limit")
    if encoding == "gzip":
        inflater = zlib.decompressobj(wbits=31)
        result = inflater.decompress(data, max_bytes + 1)
        if len(result) > max_bytes or inflater.unconsumed_tail:
            raise ValueError("Decompressed NuGet data exceeds size limit")
        result += inflater.flush(max_bytes + 1 - len(result))
        if len(result) > max_bytes or not inflater.eof:
            raise ValueError("NuGet gzip response truncated or oversized")
        data = result
    return data


def read_json(url: str) -> dict:
    result = json.loads(verified_fetch(url, MAX_METADATA_BYTES))
    if not isinstance(result, dict):
        raise ValueError("NuGet metadata must be a JSON object")
    return result


def approved_catalog_url(url: str) -> bool:
    parsed = urllib.parse.urlsplit(url)
    return (approved_https(url) and parsed.hostname == "api.nuget.org"
            and parsed.query == ""
            and re.fullmatch(r"/v3/catalog0/data/[0-9.]+/[a-z0-9.\-]+\.json",
                             parsed.path) is not None)


def fetch_catalog_sha512() -> tuple[bytes, int]:
    # A .nupkg.sha512 sidecar is NOT a documented public FlatContainer
    # resource. NuGet's catalog entry includes packageHash (SHA512) and
    # packageSize; discover its URL from the registration index.
    index = read_json(REG_URL)
    matches = []
    pages = index.get("items")
    if not isinstance(pages, list):
        raise ValueError("NuGet registration index missing pages")
    for page in pages:
        if not isinstance(page, dict):
            raise ValueError("Invalid registration page")
        leaves = page.get("items")
        if leaves is None:
            url = page.get("@id")
            parsed = urllib.parse.urlsplit(url if isinstance(url, str) else "")
            if (not approved_https(url if isinstance(url, str) else "")
                    or parsed.hostname != "api.nuget.org"
                    or not parsed.path.startswith(
                        f"/v3/registration5-semver2/{PKG_ID}/")
                    or not parsed.path.endswith(".json")):
                raise ValueError("Unapproved NuGet registration page URL")
            leaves = read_json(url).get("items")
        if not isinstance(leaves, list):
            raise ValueError("NuGet registration page missing leaves")
        for leaf in leaves:
            if not isinstance(leaf, dict):
                raise ValueError("Invalid NuGet registration leaf")
            entry = leaf.get("catalogEntry")
            if not isinstance(entry, dict):
                continue
            if entry.get("id", "").lower() == PKG_ID and entry.get("version", "").lower() == PKG_VERSION:
                if leaf.get("packageContent") != PKG_URL:
                    raise ValueError("Unexpected NuGet package URL")
                matches.append(entry)
    if len(matches) != 1:
        raise ValueError("Expected exactly one pinned NuGet registration entry")
    catalog_url = matches[0].get("@id")
    if not isinstance(catalog_url, str) or not approved_catalog_url(catalog_url):
        raise ValueError("Unapproved NuGet catalog URL")
    catalog = read_json(catalog_url)
    if catalog.get("id", "").lower() != PKG_ID or catalog.get("version", "").lower() != PKG_VERSION:
        raise ValueError("NuGet catalog identity/version mismatch")
    if catalog.get("packageHashAlgorithm") != "SHA512":
        raise ValueError("NuGet catalog hash algorithm is not SHA512")
    expected = base64.b64decode(catalog.get("packageHash", ""), validate=True)
    size = catalog.get("packageSize")
    if len(expected) != 64 or not isinstance(size, int) or isinstance(size, bool) or not 0 < size <= MAX_NUPKG_BYTES:
        raise ValueError("NuGet catalog SHA512 or package size invalid")
    return expected, size


def run_probe() -> None:
    digest, size = fetch_catalog_sha512()
    package = verified_fetch(PKG_URL, MAX_NUPKG_BYTES)
    if len(package) != size or hashlib.sha512(package).digest() != digest:
        raise ValueError("NuGet catalog SHA512 or package size mismatch")
    print("PASS: official NuGet catalog SHA512 + byte count match downloaded package")

    with tempfile.TemporaryDirectory(prefix="se2-nuget-provenance-") as td:
        local = Path(td) / PKG_NAME
        local.write_bytes(package)
        inspector = runpy.run_path(str(AUDITOR))
        inspector["audit_nupkg"](local, EXPECTED_NATIVE_GIT_BLOB, EXPECTED_NATIVE_SIZE)
    print("PASS: pinned NuGet win-x64 DLL matches version-tagged upstream Git blob")
    print("NOT APPROVED FOR REDISTRIBUTION: Valve rights, bundled EXE,")
    print("Microsoft/runtime notices and publisher authentication remain open.")


if __name__ == "__main__":
    try:
        run_probe()
    except (OSError, ValueError, TypeError, KeyError, AttributeError,
            binascii.Error, zlib.error, urllib.error.URLError, EOFError) as exc:
        raise SystemExit("PROVENANCE CHECK STOPPED: " + str(exc))
