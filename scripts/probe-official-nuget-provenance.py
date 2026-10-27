#!/usr/bin/env python3
"""One-shot, isolated NuGet supply-chain comparison (no Steam, no app runs).

Downloads ONLY Steamworks.NET.AnyCPU 2026.165.2 from the official NuGet v3
feed into a temporary directory, verifies NuGet's published SHA512 sidecar,
then checks the native DLL against a version-tagged upstream Git blob and
its length. A PASS proves binary identity with that upstream Git object,
NOT Valve SDK legal redistribution rights or the content of a bundled EXE.
"""
from __future__ import annotations

import base64
import hashlib
from pathlib import Path
import runpy
import tempfile
import urllib.error
import urllib.parse
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
AUDITOR = ROOT / "scripts" / "audit-release-assets.py"
BASE = "https://api.nuget.org/v3-flatcontainer/steamworks.net.anycpu/2026.165.2/"
PKG = "steamworks.net.anycpu.2026.165.2.nupkg"
EXPECTED_NATIVE_SIZE = 319128
EXPECTED_NATIVE_GIT_BLOB = "0ca8f77b8a315d2d43ea3087623f61c8fbe9ca3e"
MAX_NUPKG_BYTES = 20_000_000
ALLOWED_HOSTS = frozenset({"api.nuget.org", "globalcdn.nuget.org", "www.nuget.org"})


def approved_https(url: str) -> bool:
    parsed = urllib.parse.urlsplit(url)
    return (parsed.scheme == "https" and parsed.hostname in ALLOWED_HOSTS
            and parsed.username is None and parsed.password is None
            and parsed.port in (None, 443))


class PinnedNuGetRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request, fp, code, msg, headers, newurl):
        if not approved_https(newurl):
            raise ValueError("Unapproved NuGet redirect destination")
        return super().redirect_request(request, fp, code, msg, headers, newurl)


def verified_fetch(url: str, max_bytes: int) -> bytes:
    # HTTPS validation and a locked-down redirect handler avoid fetching
    # from arbitrary hosts even before validating the response.
    if not approved_https(url):
        raise ValueError("Unapproved NuGet endpoint")
    opener = urllib.request.build_opener(PinnedNuGetRedirect)
    with opener.open(url, timeout=30) as response:
        final = urllib.parse.urlsplit(response.geturl())
        if not approved_https(response.geturl()):
            raise ValueError("Unapproved NuGet redirect destination")
        length = response.headers.get("Content-Length")
        if length and int(length) > max_bytes:
            raise ValueError("Remote NuGet response exceeds size limit")
        data = response.read(max_bytes + 1)
    if not data or len(data) > max_bytes:
        raise ValueError("Remote NuGet response empty or exceeds size limit")
    return data


def run_probe() -> None:
    package = verified_fetch(BASE + PKG, MAX_NUPKG_BYTES)
    sidecar = verified_fetch(BASE + PKG + ".sha512", 2000)
    expected = base64.b64decode(sidecar.strip(), validate=True)
    if len(expected) != 64 or hashlib.sha512(package).digest() != expected:
        raise ValueError("Official NuGet SHA512 sidecar does not match downloaded package")
    print("PASS: NuGet official v3 sidecar SHA512 matches downloaded archive")

    with tempfile.TemporaryDirectory(prefix="se2-nuget-provenance-") as td:
        local = Path(td) / PKG
        local.write_bytes(package)
        inspector = runpy.run_path(str(AUDITOR))
        inspector["audit_nupkg"](
            local,
            EXPECTED_NATIVE_GIT_BLOB,
            EXPECTED_NATIVE_SIZE,
        )
    print("PASS: pinned NuGet win-x64 native DLL matches upstream Git tagged blob")
    print("NOT APPROVED FOR REDISTRIBUTION: Valve entitlement, bundled EXE and")
    print("required Microsoft/runtime notices still need separate review.")


if __name__ == "__main__":
    try:
        run_probe()
    except (OSError, ValueError, urllib.error.URLError, EOFError) as exc:
        raise SystemExit("PROVENANCE CHECK STOPPED: " + str(exc))
