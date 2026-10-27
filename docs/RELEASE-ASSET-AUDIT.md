# Read-only release asset and license audit

**Status (2026-10-24): tooling prepared; no public release authorised.**
This audit is intentionally separate from the previously tested
`0.6.0-preview.10` executable and its verified Steam read-only behavior.
The candidate's C# code, XAML, Docker build configuration, ZIP layout and
native Steam API usage are **not changed** in this audit workstream.

## Findings grounded in external documentation

1. The project owns its original source and selected the [MIT License](../LICENSE).
   Valve's official
   [open-source guidance](https://partner.steamgames.com/doc/sdk/uploading/distributing_opensource)
   lists MIT as a generally compatible permissive license for Steamworks
   integrations; this does **not** grant rights to distribute Valve's SDK DLL.
2. The exact pinned NuGet dependency is
   [Steamworks.NET.AnyCPU 2026.165.2](https://www.nuget.org/packages/Steamworks.NET.AnyCPU/2026.165.2),
   which reports MIT for the managed wrapper. The
   [version-tagged build project](https://github.com/Akarinnnnn/Steamworks.NET.AnyCPU/blob/2026.165.2/Standalone3.0/Steamworks.NET.csproj)
   configures `steam_api64.dll` at `runtimes/win-x64/native` in the nupkg.
   The exact [upstream MIT notice](../licenses/Steamworks.NET.AnyCPU-MIT.txt)
   is now preserved in this repository (originally from the upstream
   `2026.165.2` tag); this notice covers Steamworks.NET, **not Valve's DLL**.
3. Valve's
   [Steamworks SDK Access Agreement](https://partner.steamgames.com/documentation/sdk_access_agreement/)
   section 1.1(b) grants **eligible licensees**, subject to the agreement,
   redistribution of designated `redistributable_bin` SDK object-code files
   alongside the licensee's software. The authorisation for this independent
   utility and exact native-file provenance have **not been independently
   confirmed**. Neither a GitHub tag nor a NuGet download grants those rights.
4. The current .NET 10 WPF build uses
   `SelfContained=true`, `PublishSingleFile=true` and
   `IncludeNativeLibrariesForSelfExtract=true`. Therefore, a native DLL
   can be **embedded within the bundled EXE**; the absence of a separate
   `steam_api64.dll` ZIP member does not prove that no Valve code is shipped.
5. Microsoft's
   [.NET runtime asset licensing model](https://github.com/dotnet/runtime/blob/main/docs/project/licensing-assets.md)
   says that binary distributions need corresponding license and third-party
   notices. Determine the *exact* Windows runtime-pack notices and
   `Microsoft.NETCore.App` / WindowsDesktop runtime versions used by the
   self-contained package before release.

## New standalone inspector

`scripts/audit-release-assets.py` is a **read-only** Python 3 helper;
it does not start Steam, run .NET, extract or launch a Windows executable,
rewrite any ZIP, touch original/candidate metadata or contact external sites.

It accepts an existing local preview ZIP, validates the untrusted member
names, forbids duplicate/path-traversal/symlink entries, checks that the
embedded `SHA256-MANIFEST.txt` covers every other file, computes each member's
real SHA-256, and reports the entire ZIP hash and current notice-file presence.
The `--expected-commit` flag checks a trusted known source commit.

An optional, separately supplied NuGet `.nupkg` can be checked against
`Steamworks.NET.AnyCPU` / `2026.165.2` / MIT package metadata; the tool
lists SHA-256 and size for its win-x64 `steam_api64.dll` asset. This only
verifies the **NuGet package's contents**. It does not inspect the native
payload inside a bundled WPF EXE, compare that native payload with an
authorised Valve SDK copy, verify downloaded package authenticity, or
grant permission to redistribute anything.

For a future automated release review (not necessary for the owner now):

```bash
python3 scripts/audit-release-assets.py \
  --zip artifacts/SE2-Workshop-UGC-Fix-0.6.0-preview.10-WPF-win-x64-LOCAL-TEST.zip \
  --expected-commit 67b0872dfa4a3e6627495c039097ac240b0efb14
```

For the NuGet inventory, pass `--nupkg /path/to/steamworks.net.anycpu.2026.165.2.nupkg`
**only when the original package bytes are available**. Do not supply
workshop backups, Steam tokens, or other private files. Because the ZIP's
internal manifest is **unsigned**, even a full integrity PASS **cannot
authenticate the original publisher**. An external trusted checksum and
repeatable build are needed to support that claim.

## Audit hardening and synthetic negative tests (2026-10-27)

The inspector now rejects ambiguous Windows ZIP names (case-insensitive
collisions, NTFS alternate-data-stream colons, Windows-reserved device names,
path normalization inconsistencies, traversal, backslashes and symlinks).
It verifies the **exact** preview.10 manifest version and required header,
refuses missing/unlisted/mismatched digest entries, and bounds metadata size.
For NuGet packages, it reads `<metadata>` **direct children only** so nested
unrelated XML cannot replace package identity, version or license; safe empty
NuGet directory records are permitted without weakening the strict consumer
ZIP check.

Added **18 synthetic `unittest` cases** under
`tests/ReleaseAssetAuditTests/test_audit_release_assets.py`, including
valid ZIP/NuGet fixtures, forged manifest digests, wrong source SHA, mismatched
version/license, missing DLL, duplicate ZIP entries, Windows case collisions,
path traversal, reserved device names, alternate data streams, symlinks,
nested NuGet metadata and safe directory records.

Run only the Python tests, with no Docker/Steam/Windows calls:

```bash
python3 -m unittest discover -s tests/ReleaseAssetAuditTests -v
```

**Evidence status:** these cases are implemented but **have not yet been run
on the owner's dev-worker VM or the actual preview.10 ZIP**. Do not mark their
results PASS without a real test transcript. The project still has no new
binary release authorization; integrity checks and legal rights are distinct.

## Owner's real preview.10 ZIP audit — verified 2026-10-27

The owner ran the read-only inspector on the Dev-Worker VM against the
**existing local** `preview.10` archive, passing
`--expected-commit 67b0872dfa4a3e6627495c039097ac240b0efb14`.
The owner supplied the actual output:

```text
PASS: WPF ZIP member coverage, source and SHA256 content checks
  source: 67b0872dfa4a3e6627495c039097ac240b0efb14
  members: 7
  visible native DLL: False
  license/notice files: NONE (release blocker)
  ZIP SHA256: 634fd3d79d689b21379aaa348660fcc22065a75f34dc96022157923e2813973a
NOTE: Single-file .NET executable may embed native DLLs; no bundle extraction done.
NOT A PUBLIC RELEASE APPROVAL: provenance, licenses and publisher still unverified
```

**Evidence classification:** **PASS for internal archive/member SHA256 integrity
and expected source commit**, based on the owner's live local execution.
Seven members are present. The archive does not contain visible native
Steamworks DLL files, **but .NET single-file bundling may contain those inside
the EXE**. The ZIP lacks `LICENSE`, the prepared
`licenses/Steamworks.NET.AnyCPU-MIT.txt` and
`THIRD-PARTY-NOTICES.txt`: a **concrete public redistribution blocker**, not
a failure of the tested Steam read-only program.

**Next release-candidate actions (not retroactive edits to the tested ZIP):**
Include project MIT license, exact pinned managed-wrapper copyright/MIT
notice, and reviewed .NET/runtime/Valve third-party notices in the new
release packaging; extend the pre-release acceptance gate to require them.
Before offering any downloads, independently inspect embedded native DLL
provenance/Valve distribution conditions and capture a trusted, externally
published ZIP hash. This local audit is **not** third-party code-signing.

## Planned community usability package — before public release

Owner requested (2026-10-27) an **original compact program logo** and a
**well-designed illustrated beginner guide**, not text-only documentation.
Track through [#8: logo/Windows icon](https://github.com/wdani/SE2-Workshop-UGC-Fix/issues/8)
and [#9: illustrated offline HTML/PDF guide](https://github.com/wdani/SE2-Workshop-UGC-Fix/issues/9).
Plan concept work in parallel, final icons/screenshots **after feature/UI
acceptance and before first public release**, followed by a separate
packaging/UI smoke check. Do not modify the owner-tested preview.10 app to
insert branding now; avoid unauthorised Keen/Steam logos and use sanitized
screenshot examples.

## Pinned upstream Windows native DLL provenance checkpoint — 2026-10-27

The exact [`2026.165.2` tagged Steamworks.NET.AnyCPU project](https://github.com/Akarinnnnn/Steamworks.NET.AnyCPU/blob/2026.165.2/Standalone3.0/Steamworks.NET.csproj)
at line 55 includes
`../com.rlabrecque.steamworks.net/Plugins/steam_api64.dll` in
`runtimes/win-x64/native`. The corresponding **public tagged GitHub
repository object** is:

| Field | Provenance evidence |
| --- | --- |
| Repository | `Akarinnnnn/Steamworks.NET.AnyCPU` |
| Tag | `2026.165.2` |
| Source path | `com.rlabrecque.steamworks.net/Plugins/steam_api64.dll` |
| File size | **319,128 bytes** |
| Git **blob ID** (SHA-1 Git object hash) | `0ca8f77b8a315d2d43ea3087623f61c8fbe9ca3e` |

This is from the GitHub API's metadata for that exact tagged source
file. A Git blob ID is **not the same as a plain-file SHA256** and is
not a legal attestation. The byte comparison calculates SHA-1 over
`blob <size>\\0<file bytes>`, the standard Git object input, in addition
to independent plain-file SHA256 for the NuGet asset. An optional strict
pin was added to `scripts/audit-release-assets.py`:

```bash
python3 scripts/audit-release-assets.py \
  --nupkg /path/to/steamworks.net.anycpu.2026.165.2.nupkg \
  --expected-native-size 319128 \
  --expected-native-git-blob 0ca8f77b8a315d2d43ea3087623f61c8fbe9ca3e
```

**Status: source asset metadata VERIFIED, actual NuGet asset bytes NOT YET
AVAILABLE FOR THIS COMPARISON.** The new check is optional because the
original synthetic test fixtures have intentionally fake DLL bytes. Four
additional regression tests cover a matching Git object ID, mismatching
upstream identity, wrong size and malformed expected IDs. **These 4 new
tests remain unrun on the owner's Dev-Worker VM.**

Even if the NuGet binary matches GitHub's pinned source byte-for-byte,
this proves correspondence with a particular upstream repository object,
**not** correspondence to an authorised Valve SDK distribution, the final
Windows single-file bundle, or this project's entitlement to redistribute it.

## New optional release notice-content gate (2026-10-27)

`scripts/check-release-notices.py` is a separate **read-only, fail-closed**
check for **a future release candidate**, not a patch to the already-tested
preview.10 package. First it invokes the existing ZIP/source SHA256 verifier.
Then it requires these exact files **inside the verified ZIP**:

- `LICENSE` — byte-for-byte equal to the repository's approved project MIT
  license.
- `licenses/Steamworks.NET.AnyCPU-MIT.txt` — byte-for-byte equal to
  the pinned upstream copyright/MIT license text kept in the repository.
- `THIRD-PARTY-NOTICES.txt` — a non-trivial UTF-8 notices index referring
  at minimum to `Steamworks.NET.AnyCPU 2026.165.2`, Valve
  `steam_api64.dll`, and Microsoft `.NET 10`.

Invoke when a **new candidate ZIP including reviewed notices** exists:

```bash
python3 scripts/check-release-notices.py \
  --zip /path/to/SE2-Workshop-UGC-Fix-CANDIDATE.zip \
  --expected-commit THE_TRUSTED_40_CHARACTER_GIT_SHA
```

The expected commit must come from the release review, not from the untrusted
ZIP. The current auditor checks the `0.6.0-preview.10` manifest: if the
future release candidate has a different version, update/review the manifest
version constraint in `scripts/audit-release-assets.py` **separately**.
No release ZIP/notice index has been created or released in this step.
The existing real preview.10 ZIP **must fail this notice gate**, because
the owner already confirmed its notices were absent. This is correct.

**Important limitations:** Even a `PASS` means **required notice files
are present and the two known MIT texts match repository source**, not that
all native/.NET notices are complete, Valve redistribution has been authorised,
the executable matches a trusted build, the publisher is authenticated, or
the owner approved public release. Those gates remain **manual, blocking
and independent**. Checking a few component names in the index is not a
substitute for legal/technical review.

Added **12 further synthetic tests** in
`tests/ReleaseAssetAuditTests/test_release_notice_gate.py`. They test exact
MIT text matches, missing license/index files, doctored notices with matching
ZIP hashes, missing native/runtime notice references, mismatched source SHA,
tampered manifest and byte-for-byte read-only ZIP behavior. The original 18
ZIP/NuGet tests passed on the owner VM on 2026-10-27. **These 12 new tests
are committed but not yet independently executed on the owner's VM**;
do not report them as passed until there is test evidence.

## What remains to be done before a public Windows ZIP

| Topic | Current result | Release decision |
| --- | --- | --- |
| MIT license for original code | Chosen and committed | **Resolved** |
| Steamworks.NET managed MIT notice | Exact upstream tagged text archived; real ZIP contains **no LICENSE/THIRD-PARTY-NOTICES** | **Packaging blocked** |
| ZIP build-time SHA256 manifest | Real preview.10 ZIP, 7 members, source commit verified, full ZIP SHA256 recorded | **PASS (owner local audit)** |
| Direct NuGet native DLL inventory | Tag proves configured packaging; actual package bytes not directly audited | **Pending** |
| Embedded native DLL inside WPF single-file EXE | Cannot infer payload from ZIP member names | **Pending independent bundle/asset inspection** |
| Valve SDK redistribution entitlement | Conditional license text known; owner's particular entitlement unverified | **Blocked** |
| Microsoft .NET 10 self-contained runtime notices | Distribution model documented; actual shipped runtime file list absent | **Pending** |
| Native WPF-triggered Steam RESTORE | Only prior WinForms write path qualified; WPF read-only preview.10 qualified | **Not yet qualified** |
| Authenticated public release | No signing, official download or public tag issued | **Blocked** |

**This audit does not require the owner to repeat Windows tests or corrupt a
healthy Workshop item.** The tested `preview.10` code and `preview.9` fallback
remain frozen until a separately reviewed release candidate is explicitly
approved.
