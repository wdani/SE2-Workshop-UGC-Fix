# WPF preview — 0.6.0-preview.2 (not released)

The user requested a truly modern desktop UI after reviewing the dark WinForms 0.5 preview.
This is a new **WPF** front-end with navigation, responsive content, readable cards,
reduced visual noise, status and owner indicators, and separate recovery/preflight dialogs.

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
