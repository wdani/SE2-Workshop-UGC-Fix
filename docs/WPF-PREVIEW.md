# WPF preview — 0.6.0-preview.7 (not released)

The user requested a truly modern desktop UI after reviewing the dark WinForms 0.5 preview.
This is a new **WPF** front-end with navigation, responsive content, readable cards,
reduced visual noise, status and owner indicators, and separate recovery/preflight dialogs.

## Safe-report clipboard and backup-folder handling (preview.7)

The 2026-10-24 owner screenshot confirmed the preview.6 offline Recovery Assistant
shows **OFFLINE VISUAL REVIEW · NO STEAM ACCESS**, with all three Browse actions,
candidate preview, and Restore controls visibly disabled. The offline UI acceptance
is therefore complete; this does **not** constitute a live Steam restore test.

For preview.7 the main dashboard **layout** remains exactly as approved, but the
backup/report button implementations have been hardened:

- Opening the private backup folder now reports a missing directory or failed
  Windows Explorer launch instead of silently doing nothing. A missing directory
  disables the stale button until a new check is run. No files are deleted or
  modified.
- Copying a safe report now checks the exact filename
  `report-SAFE-TO-SHARE.json`, the on-disk size (64 KiB maximum), the JSON schema,
  and a strict allowlist of summary-only fields from the two existing backend
  report types (normal metadata scan or read-only Steam API probe). Unexpected
  fields, especially private original/candidate metadata, block clipboard copy.
  A missing file also disables the stale button and shows an error.
- No raw developer-metadata JSON, private backup paths, or candidates are copied
  through the report action. Private original backup remains on the user's PC.
- Offline WPF smoke tests exercise both supported safe-summary schema shapes,
  reject an injected private `originalRaw` field and refuse a
  `original-developer-metadata-PRIVATE.json` filename. Regression source gates
  check the clipboard guard is wired to the actual button.
- All safety checks run without Steam API access; no Workshop modification,
  fixture creation, or deliberate corruption is needed.

### Preview.7 manual acceptance on Windows

1. On the dev-worker VM run the existing
   `bash scripts/package-wpf-preview-docker.sh` from
   `feat/wpf-modern-desktop`. Expect the
   `SE2-Workshop-UGC-Fix-0.6.0-preview.7-WPF-win-x64-LOCAL-TEST.zip`
   artifact **only after all tests/publish pass**.
2. Extract to a new folder; run `RUN-WPF-SMOKE.cmd` and inspect the exit status.
3. Open `RUN-WPF-PREVIEW.cmd`, run **Check metadata** on existing healthy
   owner-controlled item `3824260687`, then click **Open backups**.
   Windows Explorer should open the private timestamped scan directory,
   containing the original `...PRIVATE.json` file and
   `report-SAFE-TO-SHARE.json`.
4. Click **Copy safe report**, paste into a local Notepad file, and verify
   it contains basic diagnostic JSON only: workshop ID, byte count, SHA256,
   ownership check, classification, and repair eligibility. **Do not publish
   private backup JSON**; no raw metadata is expected in this safe report.
5. Optionally invoke **API probe** on the same healthy item and confirm
   **Copy safe report** still works for this alternative report schema.
6. Visually check error-message clarity only if a file genuinely goes missing.
   Do **not** delete or rename real private backups just to provoke errors.

**Validation pending:** this preview.7 source is committed on GitHub, but
its .NET package build and native Windows smoke are not yet confirmed. Keep
preview.6 as the known-tested fallback and PR #3 in Draft.

## Windows dialog visual acceptance and offline-control clarification (preview.6)

On 2026-10-24 the user supplied four native Windows screenshots of preview.5:
the Description size check with sample input, a calculated result, the Recovery
Assistant offline UI review at the sources/review stage, and the confirmation
stage after scrolling.

Observed: the 21-character ASCII description produces 21 UTF-8 bytes,
23 JSON-encoded bytes, 1477 baseline bytes and an approximately 1500-byte
total; Copy Result is disabled before estimation and enabled afterwards.
The preflight dialog is fully displayed. The recovery dialog has readable
source, review and confirmation sections and uses its vertical scrollbar.

**UI defect identified:** offline-only Recovery review had three apparently
enabled Browse buttons even though their click handlers refused to open files.
Preview.6 marks all three buttons as disabled, coordinates their state with
busy/offline checks, and changes the banner to the explicit
"OFFLINE VISUAL REVIEW · NO STEAM ACCESS". The native UI smoke and
static gates now verify all controls, including these three buttons,
are non-actionable in the offline review mode.

The accepted main dashboard stays unchanged. No Steam metadata writer,
owner verification or backup restoration code is altered.

**Validation status:** Source-only checks inspected after the changes.
Preview.6 Docker package and Windows smoke remain pending; do not infer
that screenshot validation of preview.5 proves the new preview.6 release.

## Functional dialog hardening (preview.5)

The owner approved the preview.4 **main dashboard** after a native Windows
read-only check of their healthy owned test item `3824260687`. The screenshot
showed **healthy metadata, owner verified, 1,611 / 5,000 bytes** and repair and
recovery locked. The slim main scrollbar only appeared when Activity and
Diagnostics was expanded; the owner explicitly accepted this behavior. The
main dashboard layout, font sizes and three distinct cards are now frozen
unless a demonstrated defect calls for a change.

This version addresses two dialog-level correctness issues without editing any
Steam metadata transport or backend recovery paths:

1. **Description estimate freshness**: editing the description invalidates the
   previous estimate and disables copying it. An exception now sets an explicit
   failed-estimate status, and Clear resets that status/color.
2. **Recovery source freshness**: changing any original backup, project, ModHUB
   file or reference ID instantly clears the prior verified preview and
   confirmation and shows an explicit "Sources changed" message.
3. **Locked offline Recovery UI review**: launch
   `RUN-WPF-REVIEW-RECOVERY.cmd` from the preview.5 ZIP to inspect the recovery
   window with NO Steam session, NO query, NO preview submission, NO write, and
   all source/confirmation controls disabled. The real recovery path still
   requires a fresh owner-verified eligible item, unchanged remote metadata,
   original source checks and explicit approval.
4. Offline `RUN-WPF-SMOKE.cmd` now exercises the description calculation,
   invalidates it upon editing, verifies recovery-source invalidation and
   asserts that the offline UI-only recovery dialog has **no actionable
   controls**, as well as retaining previous Workshop URL and layout checks.

### Native Windows functional checks for preview.5

- `RUN-WPF-SMOKE.cmd`: should PASS on Windows before opening the main preview.
- `RUN-WPF-PREVIEW.cmd`: confirm the preview.4 main dashboard is visually unchanged.
- Click **Description**. Paste sample text (including accents or Unicode),
  click **Estimate size**, then change that text. Confirm **Copy result**
  disables immediately until estimation is repeated; **Clear** resets status.
- Run `RUN-WPF-REVIEW-RECOVERY.cmd`: verify the recovery dialog can be viewed,
  its controls remain visibly locked and NO Steam login, query, update or
  backing file is required. This is *not* a valid candidate reconstruction
  or restore test; it is visual-only.
- Read-only CHECK METADATA on the already healthy test item is optional.
  **Do not inject new Steam failures or perform a restore just for this UI pass.**

**Qualification:** preview.5 source is committed and guarded by source checks,
but the new .NET publish and Windows smoke have not been performed yet. The
existing WinForms original-backup restore is the only real end-to-end write
acceptance. Keep PR in Draft; no public release or merge into main.

## Hybrid design and native Workshop input (preview.4)

The user visually compared the real Windows screenshots of preview.2 and preview.3:
**preview.2's three separate inspection, ready/status and metadata cards are preferred.**
Only preview.3's thin blue active-navigation marker and restrained footer treatment
are carried forward. The footer dot is **blue** and signals a local manually controlled
tool, **not** Steam connection or verified item ownership. Owner identity is checked
separately for each Workshop item.

The user then spotted unpleasant rendering when typing a Workshop ID or full URL.
For the **single-line WorkshopInput only**, preview.4 stops using the shared
rounded `FormFieldStyle` custom TextBox content-host template. It instead uses
WPF's **native TextBox template** for selection, caret, horizontal scrolling and
vertical text layout, surrounded by an independent rounded, focus-highlighted
border. Explicit text/selection/caret colors and disabled spellcheck maintain
dark UI readability. Multiline preflight/recovery text controls retain their
existing style and are not changed.

The native Windows offline UI smoke enters and selects a long Workshop URL,
verifies it round-trips unchanged and no write actions unlock, in addition to
the already-existing default-height/diagnostics checks. It makes **no Steam calls**.

**Historical note:** preview.4 was built and visually validated on Windows; preview.5 remains pending.
Use the existing `feat/wpf-modern-desktop` GitHub branch; on the VM, preserve
local uncommitted preview.3 changes before switching back (see below). The
previous preview.2 ZIP and the user's local preview.3 build may be kept for
comparison. Do not release or modify live Workshop metadata for UI testing.

### Rebuild from the existing VM workspace

```bash
cd /srv/dev-worker/workspaces/se2-workshop-ugc-fix
git stash push -u -m "preserve local preview3 comparison"
git switch feat/wpf-modern-desktop
git pull --ff-only
bash scripts/package-wpf-preview-docker.sh
```

Expected new ZIP if tests and publish pass:
`artifacts/SE2-Workshop-UGC-Fix-0.6.0-preview.4-WPF-win-x64-LOCAL-TEST.zip`.
After extracting on Windows, run `RUN-WPF-SMOKE.cmd` then
`RUN-WPF-PREVIEW.cmd`. Visually type an ID, paste a full long Workshop URL,
test cursor position and text selection, and optionally inspect the
**already healthy owned** LCD mod read-only. No Steam corruption required.

## Compact UI refinement (preview.2)

The owner's first native-Windows screenshot confirmed that WPF runs and the initial
visual redesign is an improvement, but still too card-heavy and unnecessarily
scrollable. This preview rebalances it as a concise **desktop utility**:

- A narrower 188-DIP sidebar and 48-DIP top toolbar.
- Inline item input + two read-only buttons, compact status strip and flat
  four-value metadata summary; reduced padding, typography and visual noise.
- Diagnostics starts collapsed and expands independently on demand.
- Default overview retains wheel/keyboard overflow access for small windows but
  uses slim, subtle WPF scrollbars when scrolling is actually needed.
- Windows offline smoke now opens the invisible main view, lays it out and fails
  if its default-window `OverviewScroller.ScrollableHeight` exceeds 1 DIP.
  This is an actual *runtime* gate, not a claim based on static XAML inspection.
- Status accent follows result severity. Steam transport, authorization checks
  and confirmed recovery algorithms were **not changed**.

**Validation pending for this exact preview.2 source:** rebuild in dev-worker,
run native `RUN-WPF-SMOKE.cmd`, inspect initial and expanded diagnosis at default
size and 100%, 125%, 150% scaling. Test smaller windows for graceful overflow;
do not hide overflow just to suppress a scrollbar.

## Architecture and safety

- The WPF project references the **existing consumer-mode WinForms executable
  assembly** as a transitional backend. It uses `InternalsVisibleTo` solely to
  call existing tested diagnostic and recovery methods. No copy of the Steam write
  algorithm was created.
- The old WinForms project remains the established fallback. Existing 0.5 preview
  build scripts and validation are unchanged. No default/release entrypoint changes.
- The WPF assembly has no direct Steam UGC mutation API calls.
- Developer item-creation/fixture-injection sources are disabled via
  `EnableFixtureLab=false` on the project reference; the WPF ZIP contains no lab launcher.
- Only the original backup RESTORE has passed real Steam + SE2 post-restore testing.
  Experimental other-item template recovery is not equivalent to that proof.
- There is **no automatic Steam query**, and WPF UI smoke starts without Steam.
- Each changing action still uses the **original owner/identity/unchanged-remote
  gates**, explicit confirmation, one update, fresh readback and no blind retries.

## Build locally on dev-worker

```bash
cd /srv/dev-worker/workspaces/se2-workshop-ugc-fix
git fetch origin
git switch --track origin/feat/wpf-modern-desktop
bash scripts/package-wpf-preview-docker.sh
```

If the branch already exists locally, use `git switch feat/wpf-modern-desktop`
followed by `git pull --ff-only`. This command runs legacy synthetic safety
tests and source checks, publishes WPF with .NET 9 targeting win-x64 and creates
one ZIP under `artifacts/`. It does **not** run Windows, initialize Steam, or
change a Workshop item. Package build requires Docker/NuGet access.

## Windows acceptance

1. Extract only the WPF preview ZIP to a fresh local folder.
2. Run `RUN-WPF-SMOKE.cmd`, expect a PASS. Failure details are saved in
   `%LOCALAPPDATA%\SE2WorkshopUGCFix\Diagnostics\last-wpf-smoke.txt`.
3. Start `RUN-WPF-PREVIEW.cmd`. Visually inspect navigation, disabled button
   readability, scaling at 100% / 125% / 150%, and the separate preflight window.
4. Optionally read the existing **healthy owner-controlled** disposable test item
   `3824260687` using CHECK METADATA. No writes or new damaged fixtures.
5. Capture feedback/screenshots; retain the original WinForms candidate until
   the entire WPF behavior has independent verification.

**Not a release:** no CI compilation or actual Windows smoke has been
confirmed for this new branch. Do not merge it directly into main or distribute
the executable until build/tests, UI inspection, and explicit release approval.
