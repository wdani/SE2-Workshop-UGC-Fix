#!/usr/bin/env python3
"""Fail-closed NOTICE CONTENT gate for FUTURE release candidates only.

Read-only: does not edit ZIPs, run the executable, contact Steam/network,
grant Valve redistribution rights or authorize public publication.
The auditor currently targets the v0.6.0-preview.10 manifest format; retarget
and requalify it explicitly for later candidate versions.
"""
from __future__ import annotations

import argparse
from pathlib import Path
import runpy
import sys
import zipfile

ROOT = Path(__file__).resolve().parents[1]
AUDITOR = ROOT / "scripts" / "audit-release-assets.py"
REQUIRED_LICENSES = (
    "LICENSE",
    "licenses/Steamworks.NET.AnyCPU-MIT.txt",
)
INDEX = "THIRD-PARTY-NOTICES.txt"
MAX_NOTICE_BYTES = 200_000
INDEX_TERMS = (
    "Steamworks.NET.AnyCPU",
    "2026.165.2",
    "Valve",
    "steam_api64.dll",
    "Microsoft",
    ".NET 10",
)


def check_notices(path: Path, expected_commit: str) -> None:
    # Reuse the already tested integrity and source-commit gate so that a
    # notice cannot be added to an unsigned or tampered archive.
    audit = runpy.run_path(str(AUDITOR))
    audit["audit_zip"](path, expected_commit)

    with zipfile.ZipFile(path, "r") as z:
        names = set(audit["members"](z))
        missing = (set(REQUIRED_LICENSES) | {INDEX}) - names
        if missing:
            raise ValueError(
                "RELEASE NOTICE GATE BLOCKED: required ZIP files missing: "
                + ", ".join(sorted(missing))
            )

        for name in REQUIRED_LICENSES:
            canonical_path = ROOT / name
            canonical = canonical_path.read_bytes()
            with z.open(name) as stream:
                bundled = stream.read(len(canonical) + 1)
            if not canonical or bundled != canonical:
                raise ValueError(
                    "RELEASE NOTICE GATE BLOCKED: license differs from "
                    f"version-controlled {name}"
                )

        index_member = z.getinfo(INDEX)
        if not 100 <= index_member.file_size <= MAX_NOTICE_BYTES:
            raise ValueError("RELEASE NOTICE GATE BLOCKED: notice index size invalid")
        notice = z.read(INDEX).decode("utf-8")
        missing_terms = [term for term in INDEX_TERMS if term not in notice]
        if missing_terms:
            raise ValueError(
                "RELEASE NOTICE GATE BLOCKED: third-party index lacks "
                + ", ".join(missing_terms)
            )

    print("PASS: MIT project/wrapper notices match repository and third-party index is present")
    print("IMPORTANT: NO PUBLIC RELEASE APPROVAL; Valve rights, bundled native")
    print("DLL provenance, runtime notices completeness, authentication and")
    print("owner go/no-go require independent manual verification.")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--zip", type=Path, required=True, help="Existing local candidate ZIP")
    parser.add_argument("--expected-commit", required=True,
                        help="Externally known source SHA, not read from ZIP")
    args = parser.parse_args()
    try:
        check_notices(args.zip, args.expected_commit)
        return 0
    except (OSError, ValueError, UnicodeError, zipfile.BadZipFile) as exc:
        print("BLOCKED:", exc, file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
