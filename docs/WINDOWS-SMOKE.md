# Windows GUI runtime smoke — controlled developer test

**Goal:** prove the **v0.4.0-preview.3** WinForms interface actually initializes on a Windows x64 PC, beyond the previously successful Linux-hosted cross-compilation.

**Never distribute this local preview as a release.** It contains the existing v0.3 oversized-description Steam write feature, but the new missing/corrupt metadata recovery is **read-only preview**. Do not attempt repairs on production Workshop items.

## A. Package the local Windows test ZIP on the dev-worker VM

```bash
cd /srv/dev-worker/workspaces/se2-workshop-ugc-fix
git fetch origin
git switch feat/universal-metadata-recovery
git pull --ff-only
bash scripts/run-offline-tests-docker.sh
bash scripts/package-windows-smoke-docker.sh
```

This uses the official .NET 9 SDK Docker image to **publish** a self-contained Windows x64 EXE. The final ZIP is saved only on the VM to:

`artifacts/SE2-Workshop-UGC-Fix-0.4.0-preview.3-win-x64-LOCAL-TEST.zip`

The script prints SHA256. The `artifacts/` directory is ignored by Git; **no Release upload, Steam API initialization or workshop edit is performed on the VM**.

## B. Test on Windows

Copy the ZIP to the Windows PC using MobaXterm SFTP, extract it to a local directory, then run **`RUN-UI-SMOKE.cmd`**. The command waits for the hidden WinForms smoke process to finish and displays one of:

- `PASS: Windows GUI forms initialized and safe controls verified.`
- `FAIL: Windows UI smoke check did not pass.`

Smoke mode constructs the main and recovery-preview forms, verifies the unscanned REPAIR/RECOVERY buttons remain disabled, and exits. It **never initializes Steam, reads a Workshop item, or submits updates**.

Then, optionally run **`RUN-APP.cmd`**. Check the layout, input fields and recovery labels visually. With Steam running and SE2/ModHUB closed, the creator may use **CHECK METADATA** for a Workshop item they own, which reads its live state and stores a private local backup. **Do not press REPAIR** on a real/production item in this test. For a missing/corrupt item the preview wizard may inspect files and generate a PRIVATE, non-uploaded candidate.

**A passing smoke does not yet validate Steam integration, recovery, native graphics interactions, or safe live write behavior.**

## C. Evidence to collect

Record the commit SHA, test package SHA256, the smoke PASS/FAIL line, Windows version, any UI exception, and optionally a screenshot with **private IDs/paths/Steam details redacted**. Avoid uploading the private backup folder.

## Remaining before any release

1. Pass offline tests and Windows GUI smoke.
2. Test Steam read-only query/ownership with explicit owner permission.
3. Design and independently review the missing-metadata write path (still disabled).
4. Use a disposable creator-owned Workshop test item and verify metadata-only submit + uncached readback.
5. Only then consider beta release and public documentation.


## D. Read-only Steam metadata API probe (preview.3)

**Purpose:** determine whether Steamworks reports an existing owner-controlled Workshop item with no developer metadata as a successful empty string (`GetQueryUGCMetadata == true` and 0 UTF-8 bytes), or as a failed metadata retrieval (`false`). Steamworks documentation describes the boolean return but does not explicitly guarantee which result an absent-value item produces; never equate `false` with 0 bytes.

1. Run the included `RUN-UI-SMOKE.cmd` first after extracting the new preview.3 ZIP; it does not initialize Steam.
2. Start Steam, sign into the account that **owns** the tested Workshop item, close SE2 and ModHUB, then open `RUN-APP.cmd`.
3. Paste a known **healthy owner-controlled item**, e.g. Bulk Production x100 `3810512193`. Click **READ-ONLY UGC PROBE** beside the status message.
4. Confirm the result says **Present** with a nonzero byte count; use **COPY SAFE REPORT** and preserve it privately. The report does not contain raw metadata, owner SteamID or credentials.
5. Later, when a **disposable owner-controlled item with legitimately absent developer metadata** is available, run exactly the same read-only probe on that item. Do **not** deliberately delete/blank metadata on a production mod or use any write tool for this diagnostic step.
6. Interpret results:
   - `ConfirmedEmpty`: details query successful/uncached, `GetQueryUGCMetadata=true`, string length 0. Candidate preview may be investigated, **no upload**.
   - `MetadataCallFailed`: item details valid but `GetQueryUGCMetadata=false`, **UNKNOWN**, not proven 0-byte metadata.
   - `QueryFailed`, `CachedQuery`, `UnexpectedItem`, `WrongOwner`, `AmbiguousNull`: STOP and inspect the safe report; do not attempt repair.
   - `Present`: normal metadata, not the missing-data test case.

Only safe API status flags, the item ID, byte count and SHA256 are logged. **Never upload the PRIVATE JSON backups or run REPAIR on a production item.**

Official Steamworks API reference: https://partner.steamgames.com/doc/api/ISteamUGC#GetQueryUGCMetadata
