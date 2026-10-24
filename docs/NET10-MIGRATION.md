# .NET 10 LTS — separate Windows qualification candidate

**Status: SOURCE READY, TESTS / WINDOWS ACCEPTANCE NOT YET RUN.**
Created 2026-10-24 to address [Issue #4](https://github.com/wdani/SE2-Workshop-UGC-Fix/issues/4).
This is **not a public release**.

## Why this is separate

The owner-tested `v0.6.0-preview.9` Windows WPF app uses .NET 9, whose
support ends **2026-11-10**. The migration uses .NET 10 LTS and a
separate branch (`feat/wpf-net10-lts-qualification`). The source and
binary of the accepted `preview.9` remain unchanged on
`feat/wpf-modern-desktop` and in its existing local ZIP.

The new version is **`0.6.0-preview.10` (unreleased local test)**.
No new features, layout changes, Steam API changes, owner confirmation changes,
private backup changes, or recovery data conversions are proposed.

## What changed

- Main WPF and referenced WinForms projects: `net10.0-windows`
  (still x64, self-contained Windows target and `EnableFixtureLab=false`).
- Three existing offline C# safety suites: `net10.0`.
- Docker build: Microsoft `mcr.microsoft.com/dotnet/sdk:10.0`,
  publish `-f net10.0-windows -r win-x64`.
- Distinct output workspace `artifacts/wpf-user-preview-net10`
  and ZIP `SE2-Workshop-UGC-Fix-0.6.0-preview.10-WPF-win-x64-LOCAL-TEST.zip`.
- Static source gate checks framework alignment and packaging separation,
  in addition to all existing fail-closed Steam/report/recovery assertions.
- Package ZIP retains SHA256 per-file/source manifests and native
  Windows `RUN-WPF-SMOKE.cmd`. These are **unsigned**.

The dependency `Steamworks.NET.AnyCPU` remains pinned to **2026.165.2**.
The separately licensed native `steam_api64.dll` redistribution issue
is still open and **not** solved by changing runtime versions.

## Minimal test plan: no deliberate Steam modifications

**On dev-worker VM:**

```bash
cd /srv/dev-worker/workspaces/se2-workshop-ugc-fix
git fetch origin
git switch feat/wpf-net10-lts-qualification
git pull --ff-only
bash scripts/package-wpf-preview-docker.sh
```

The packaging command already performs source verification and three
compiled safety suites. It must stop if any fail. On success it prints
the source commit, SHA256 ZIP checksum and manifest validation.

**On Windows:** extract the new `preview.10` ZIP into its **own** folder,
not over the working preview.9. Run `RUN-WPF-SMOKE.cmd` once. Then, if desired,
open `RUN-WPF-PREVIEW.cmd` and do **one read-only Check metadata** against
the already-known healthy, creator-owned Workshop test item `3824260687`.
Confirm owner is verified, metadata healthy, and repair/restore remain
disabled. No source file selection or RESTORE attempt is required.

If testing fails, keep using the original preview.9 executable and preserve
the failure diagnostic. **Do not** create a damaged Workshop item or repeat
Steam metadata submissions to prove compatibility.

## Explicit qualification gates

- [ ] dev-worker static safeguards pass.
- [ ] compiled recovery, metadata and safe-report suites pass under .NET 10.
- [ ] self-contained `win-x64` package produced with correct ZIP manifest.
- [ ] native Windows offline smoke PASS.
- [ ] one healthy owner-only **read** works (no write or recovery).
- [ ] actual `steam_api64.dll` asset/third-party notices inspected for release.
- [ ] final release licensing, security and user go/no-go separately addressed.

Even after all minimal migration tests pass, native WPF-triggered live
RESTORE and alternate-reference reconstruction are separately unqualified.
Keep this PR in **Draft**; do not merge or publish automatically.
