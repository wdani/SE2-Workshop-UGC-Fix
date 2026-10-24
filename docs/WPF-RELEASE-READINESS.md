# SE2 Workshop Recovery — WPF release-readiness gates

Status: **NOT RELEASED / NOT QUALIFIED**. This document is the feature-PR
qualification checklist, not a claim that the Steam write path has been
independently qualified under WPF.

## Current candidate: 0.6.0-preview.9 (internal, Windows x64)

This candidate uses the owner-approved preview.4 UI layout and the
unchanged WinForms Steam backend. It is intentionally built as a self-contained
local test ZIP from the feature branch, not as a public installer or GitHub release.

### Acceptance evidence (date: 2026-10-24)

| Scope | Evidence | Status |
| --- | --- | --- |
| Main WPF UI | User-approved Windows screenshot; separate inspection/status/metadata cards, small active-nav marker, slim scrollbar after diagnostics expand | **PASS visually** |
| Workshop URL text | Screenshot shows URL legible in native WPF input | **PASS visually** |
| Read-only Steam scan | Existing owned healthy LCD test item 3824260687; verified owner, 1,611 / 5,000 bytes, no repair offered | **PASS native Windows** |
| Description preflight | User screenshots show test text and size estimate, copy button gated by fresh estimate | **PASS visually** |
| Recovery offline viewer | Screenshots of all three steps, Browse + Preview + Restore disabled in preview.6 | **PASS visually** |
| Backup folder / safe report | User opened backup folder and pasted summary JSON into Notepad++; no private metadata body shown | **PASS native Windows** |
| Steam readback original restoration | Prior validated WinForms backend on owned test item; exact restored metadata hash and SE2 load | **PASS old backend only** |
| WPF native restore using a damaged item | No new deliberate Steam faults/writes | **NOT QUALIFIED** |
| Working-reference reconstructed recovery | Candidate and safety preflight tested offline; no live end-to-end | **NOT QUALIFIED** |
| Preview.8 compiled safe-report tests | Owner reported green dev-worker package/test output; separate individual log lines not supplied | **PASS (owner reported)** |
| Preview.9 Windows smoke / package | Owner explicitly reported `smoke test grün` after corrected preview.9 build; full package log/ZIP SHA not separately supplied | **PASS (owner reported)** |
| Native Steamworks redistribution | Tagged wrapper NuGet build includes win-x64 `steam_api64.dll`; Valve agreement section 1.1(b) is conditional and owner entitlement/source provenance unverified | **BLOCKED** |
| .NET 10 LTS release migration | Current preview.9 targets .NET 9, support ends **2026-11-10**; separate .NET 10 build/compatibility tests required | **BLOCKED** |
| Public distribution | Owner license choice, Valve native component entitlement, runtime notices, authenticated publisher, WPF write-path qualification and explicit go/no-go | **BLOCKED** |

### 2026-10-24 qualification checkpoint

The owner reported **"alles grün"** following the preview.8
build/test instructions. Record the preview.8 dev-worker safety test and
packaging result as **owner-reported PASS**. Individual console lines, final
outer ZIP SHA256, native Windows `RUN-WPF-SMOKE.cmd` exit result, and independent
public-release acceptance were **not separately provided**, so those claims
must not be inferred.

The subsequent negative-path review identified three potential fail-open
WPF UI states and addressed them in **preview.9**:

1. If the owner-only `Check metadata` flow fails *after* partially assigning
   scan details, immediately discard the scan snapshot, buttons and report paths
   before displaying an error. A failed check cannot re-enable repair.
2. A failed read-only Steam probe similarly discards all partial report/backup
   pointers rather than enabling stale report actions.
3. On any Recovery dialog exit (including a thrown exception), invalidate the
   previous scan and require a fresh check. A Recovery preview exception now
   also invalidates its proposal and typed confirmation before enabling UI.
4. Copying a malformed private/safe report disables the rejected clipboard
   button until a fresh scan; it does not silently retry or leak its contents.

No Steam backend, WPF main layout or previously validated metadata update logic
was changed. The source safety suite now checks these fail-closed error paths.
**Preview.9 Windows smoke was subsequently reported green by the owner.** This confirms the offline startup test, not a new live WPF Steam write.

### Licensing and documentation checkpoint (2026-10-24)

The README and SECURITY entrypoints now describe the current **unreleased WPF preview.9** rather than the obsolete WinForms candidate. The guarded live Steam metadata update path is stated explicitly: the main UI defaults to read-only, but an approved recovery can actually write developer metadata. This is not a claim that the WPF-triggered live write is qualified.

The pinned managed `Steamworks.NET.AnyCPU 2026.165.2` NuGet package states **MIT** licensing. The project's *own* license is still unchosen, and Steamworks native binaries/runtime assets require separate release review. See [Third-party dependencies](THIRD-PARTY-DEPENDENCIES.md) for provenance, notice and redistribution gates. No new application license or Valve redistribution entitlement was assumed.

### Public release blockers ranked by urgency (2026-10-24)

1. **Runtime migration — HIGH PRIORITY:** current `net9.0-windows`
   self-contained app bundles .NET 9. Microsoft's official support ends
   **10 November 2026** (less than three weeks from this audit).
   For public shipping, plan a **separate .NET 10 LTS candidate** rather than
   retarget the proven preview.9 in place. Retest .NET C# safety suites,
   Windows GUI smoke/real healthy item reads, ZIP manifest and runtime patch
   version. .NET 10 receives support until November 2028.
2. **Valve native binaries — PUBLIC RELEASE BLOCKED:** exact version tag
   `2026.165.2` of the AnyCPU wrapper packs `steam_api64.dll` under
   `runtimes/win-x64/native`. Its managed MIT license does not grant
   redistribution of Valve proprietary SDK files. Valve's
   [Steamworks SDK Access Agreement](https://partner.steamgames.com/documentation/sdk_access_agreement/)
   clause 1.1(b) permits licensees to redistribute approved
   `redistributable_bin` object-code files alongside their licensee software,
   subject to agreement conditions. **Owner access/acceptance and provenance
   are unknown.** Inspect the *actual* ZIP and native asset hashes before any
   public distribution. Do not treat availability on NuGet as authorisation.
3. **Application license and notices — OWNER DECISION REQUIRED:** choose the
   original project's license or documented proprietary terms. Preserve
   third-party MIT notices and runtime license files; do not add a `LICENSE`
   to the repo without the owner's choice.
4. **Distribution integrity:** local ZIP includes unsigned hashes, not
   Authenticode signing. Choose an authenticated origin and appropriate
   checksum/version/rollback strategy; the app is currently an internal test.
5. **Function claims:** a live **WPF-triggered** RESTORE and reconstruction
   from a different reference item remain unqualified. Retain safeguards;
   make no general recovery success promise and do not intentionally damage
   a working test item merely for a screenshot.

See [third-party dependency evidence](THIRD-PARTY-DEPENDENCIES.md) and
Microsoft's [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy).
No item above permits a public release by itself.

### Source and build integrity

- The dev-worker build script checks the repository origin and exact
  `feat/wpf-modern-desktop` branch. It now refuses changed tracked files
  and untracked WPF/test/build source, so reported `Source HEAD` reflects
  the actual input files rather than hidden local edits.
- The Docker .NET 9 build runs source safety gates, 83 recovery checks,
  23 legacy metadata checks and the new **standalone compiled
  SafeReportGuardTests** before publishing the self-contained `win-x64` app.
  (These counts are prior observations, not proof the new build has passed.)
- The preview ZIP includes `SHA256-MANIFEST.txt` listing its source commit and
  individual file hashes. Packaging validates expected launchers and the manifest.
  The printed ZIP SHA-256 allows comparison after Windows transfer.
- **No cryptographic code signing**, release provenance attestation, or
  automatic supply-chain signing is implemented. A hash file inside the ZIP
  detects accidental mismatch but cannot authenticate an untrusted distributor.
  The ZIP must remain labeled **UNRELEASED LOCAL UI TEST**.
- The consumer build disables destructive fixture lab classes; the archive
  check excludes the legacy executable/fixture-lab files. Binary inspection,
  dependency licensing, release signing and independent review are separate
  future gates.

### Privacy and safety expectations

- Reports exported through the clipboard must be exactly the
  `report-SAFE-TO-SHARE.json` diagnostic schema generated by a known scan
  or read-only probe; full original metadata and candidate files are PRIVATE.
- Before copy: check report filename, file size, JSON parser, AppID, Workshop
  ID, no-write flags, expected field names/types, no duplicate fields.
- Real Steam writes require verified ownership, unchanged remote state,
  private original backups, item-specific approval, one update and fresh
  readback. UI review mode cannot query Steam or authorize writes.
- **Never** introduce an artificial Steam failure merely to pass a
  screenshot, clipboard, negative-path or packaging check.
- Do not upload private backup JSON, file paths or Steam account secrets.

### Next operator checkpoint

```bash
cd /srv/dev-worker/workspaces/se2-workshop-ugc-fix
git switch feat/wpf-modern-desktop
git pull --ff-only
bash scripts/package-wpf-preview-docker.sh
```

On successful output:
`artifacts/SE2-Workshop-UGC-Fix-0.6.0-preview.9-WPF-win-x64-LOCAL-TEST.zip`.
Earlier: the owner reported green preview.9 native `RUN-WPF-SMOKE.cmd` after the fixed build. Preserve that tested code revision; there is no need to rebuild only for this documentation. If a later release candidate is made, capture the complete console summary, source SHA and outer ZIP SHA256. Only read-only checks on the known healthy test item are needed for further UI review.

Only after the owner explicitly approves the final release candidate, all
qualified write paths and packaging/legal gates may a release be considered.
Do not mark PR #3 ready, merge to main, or publish while these remain open.
