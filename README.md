# SE2 Workshop UGC Fix

A community-maintained diagnostic and recovery tool for **Space Engineers 2 Steam Workshop developer metadata**.

> **Development status: UNRELEASED.** Preview.7 connects the existing read-only recovery planner to an **explicit owner-confirmed metadata-only Steam APPLY** using the writer tested during the real disposable ModHUB E2E batch. **The new ordinary-user recovery APPLY has NOT yet passed the full VM/Windows/live validation and must NOT be used on production mods.** No public release is being made.


## Windows UI smoke follow-up (preview.8)

Real user screenshots of preview.7 from 2026-10-23 confirmed that Windows x64 launch, owned Workshop ID `3824260687` Steam read, `Healthy` classification (1,611/5,000 developer metadata bytes), and disabled unnecessary repair worked. The displayed SHA256 `E3F3353B0275FDA4330AB00A3A904ABC7A16B00AA2BD702D69FC4B7D38AD0270` matched the original pre-E2E backup exactly. This verifies **read-only health checking**, not the newly wired normal RESTORE APPLY.

The same preview.7 screenshot exposed a UI regression: the `READ-ONLY UGC PROBE` caption was partly covered by the next summary row; the startup activity log incorrectly claimed recovery was preview-only. Preview.8 separates status, diagnostic toolbar and summary into dedicated nonoverlapping rows, disables unwanted auto-size for status/summary, corrects startup messaging and adds a native UI smoke overlap assertion. Steam recovery behavior is unchanged. **Preview.8 still requires its own Windows build/UI smoke.**

## Compact recovery workflow (preview.8 — NOT RELEASED)

The regular Windows UI has exactly one recovery path: paste an owned Workshop ID and press **CHECK METADATA**. If the developer metadata is empty or malformed, open **RECOVERY PREVIEW** and supply **either** a complete original Steam developer-metadata JSON backup for the *same* Workshop ID, **or** the original `.vrgproj` plus an **other working Workshop item owned by the same creator** (a vetted schema source). Original ModHUB metadata is optional. A `.vrgproj` alone can create an **experimental read-only preview**, but does **not** authorize uploading invented metadata.

A verified source shows the candidate and SHA256. To apply, the creator must type `RESTORE <ORIGINAL_WORKSHOP_ID>`, acknowledge a second warning, and pass fresh same-owner/unchanged-remote checks. Exactly one `SetItemMetadata` update is sent for the owned original Workshop item; an uncached byte-for-byte Steam readback verifies completion. Failed/ambiguous submissions **STOP** with no blind retry. The tool **never edits public Workshop titles/descriptions, mod content, user worlds, local `.vrgproj` IDs, or preview images**. Saved-world compatibility is a separate concern: ModHUB publishing was observed changing the local test project's GUID **before** E2E fault injection; the tool cannot repair old world references automatically.

**Evidence boundary:** the 2026-10-21 full real ModHUB disposable E2E batch on ID `3824260687` passed 3/3 actual Steam corruption/restore cases and 2/2 oversized offline checks, with original developer metadata restored. That batch did **not** itself exercise this newly wired ordinary-user APPLY. A new world with the real LCD test mod subsequently loaded successfully; an older world still referenced the original pre-publication project GUID and could not load. Do not present preview.8 as an independently live-validated consumer recovery release yet.

## Why this exists

Some Steam Workshop uploads leave a Space Engineers 2 mod inaccessible because the internal developer metadata is invalid, too large, or unexpectedly empty. Two distinct failure modes have been investigated:

- **Oversized valid JSON** (e.g., ModHUB putting the Workshop description into the internal metadata field). A previously tested Windows tool safely cleared *only* the duplicate internal description on a creator-owned item.
- **Empty developer metadata** on an otherwise existing Workshop item. In a separate owner-assisted recovery, original project identifiers plus the schema from a working creator-owned item were used to reconstruct a candidate under the original item ID.

An empty metadata API response is **not proof that a Workshop item has been deleted**, and a public Workshop description is **not** the same field as Steam developer metadata.

## Recovery cases

| Condition | Planned behavior |
| --- | --- |
| Valid, within 5,000 UTF-8 bytes | Read-only report and backup |
| Valid but oversized due to embedded description | Verified one-field-only patch, with explicit confirmation |
| Empty developer metadata (successful Steam query, 0 bytes) | Recovery assistant using an original backup or verified project/template |
| Malformed/truncated metadata | Preserve raw evidence; attempt reconstruction only from checked sources |
| Identity conflict, unrecognized format, or unsafe state | Stop without changing Steam |

## Without an existing working mod

A creator may recover the original metadata from an older **full Steam developer-metadata backup**. When no such backup and no working reference item exists, a **synthetic reference proposal** can be prepared from an original `.vrgproj` file and manually supplied metadata, but **it is preview-only** until the exact format is independently verified. We **never invent** an original Workshop ID, project GUID, or owner.

The tool does not need to invent or overwrite the **public Workshop description**. The **internal** description can be left empty instead to avoid the metadata size limit.

## Safety and privacy

- No Steam passwords, API keys, tokens or browser cookies are requested.
- Never paste private developer metadata or local save files into public issues.
- Every actual update must verify Steam ownership, original identity, live state, backups, candidate SHA256, and affirmative consent.
- No automatic retries after ambiguous submission/timeouts.
- An authorized repair changes **only developer metadata** on the original item; game content, visibility, public description, title, thumbnail and tags must remain unchanged.
- This project is not affiliated with or endorsed by Keen Software House, Valve, or Steam.

## Development

Pure offline test harness (.NET 9, synthetic data, **no Steam access**):

```bash
dotnet run --project tests/RecoverySafetyTests/RecoverySafetyTests.csproj
dotnet run --project legacy-gui-v0.3.0/tests/MetadataSafetyTests/MetadataSafetyTests.csproj
python3 scripts/verify-recovery-safety.py
```

On a developer VM with Docker but without .NET installed:

```bash
bash scripts/run-offline-tests-docker.sh
```

Cross-compile the Windows WinForms app from Linux (build check only, **cannot run WinForms on Linux**):

```bash
bash scripts/build-windows-gui-docker.sh
```

To build an **unreleased local Windows smoke-test ZIP** and run the form-initialization test on Windows, see [Windows smoke instructions](docs/WINDOWS-SMOKE.md).

This may download Windows targeting packs and Steamworks.NET from NuGet during restore. It does not run Steam or make any Workshop changes. Actual **Windows runtime testing** is still mandatory before a release.

This runs the unit tests inside the official .NET 9 SDK container; it does **not** initialize Steam or modify any Workshop item. For a Windows compile test, build `legacy-gui-v0.3.0/SE2WorkshopUGCFix/SE2WorkshopUGCFix.csproj` with .NET 9 on Windows.

Code: [src/SE2WorkshopUGCFix.Recovery/UniversalRecovery.cs](src/SE2WorkshopUGCFix.Recovery/UniversalRecovery.cs).

Design and pending live integration: [docs/RECOVERY-SAFETY.md](docs/RECOVERY-SAFETY.md).

**Compilation/CI status:** The new integrated GUI and recovery core have not yet passed .NET build/Windows runtime verification. Do not distribute this development branch as a working Steam restore utility.

## History

This code was initially developed in [Space-Engineers-Modding draft PR #10](https://github.com/wdani/Space-Engineers-Modding/pull/10) and is being relocated to a dedicated public repository. See [#8](https://github.com/wdani/Space-Engineers-Modding/issues/8) for incident history.

## License

Not yet chosen. Public visibility alone does not grant a software license. Please do not redistribute or relicense until the repository owner chooses a license.

### UI layout update (preview.2)

The Windows GUI now separates backups/reports from prevention/recovery controls into two accessible groups, uses larger action targets, and displays healthy metadata in green rather than an orange no-repair warning. The new UI layout still requires native Windows retesting.


## Current development stage

The `RecoveryReadOnlySession` verifies fresh Steam reads before/after building proposals. An independent `RecoveryWritePreflight` now verifies private backups, working-reference freshness, owner, identity and the typed `RESTORE <Workshop ID>` phrase entirely **offline**. This latter guard is **not connected to Steam** and is not an enabled repair feature.

Next qualification gates for preview.8: run the bundled .NET/static suite and Windows GUI cross-compilation, perform Windows UI smoke/read-only verification, and separately validate the newly wired owner-only APPLY on a pre-authorized disposable item. The existing genuine E2E batch has already validated the Steam transport, but not this exact user-approval flow. Do not deliberately corrupt an existing production mod to run these checks.


### Read-only Steam API diagnostic (preview.3, unreleased)

The Windows GUI has an explicit **READ-ONLY UGC PROBE** button beside the status line. It is useful if the regular `CHECK METADATA` fails: the probe records the **uncached Workshop details** response and **the independent boolean result of `GetQueryUGCMetadata`**. A failed metadata call (false) is `MetadataCallFailed / UNKNOWN`, **not** automatically interpreted as empty developer metadata. `ConfirmedEmpty` requires a successful metadata call (true) with a 0-byte returned string. All reports omit raw private developer metadata and signed-in Steam account IDs. The probe never enables a Steam write.

The Steamworks documentation does not definitively specify missing-value behavior for all client versions. Compare a known healthy owned item against a **disposable** owner-controlled item only. No existing production mod should be deliberately damaged as a test fixture.

Follow [Windows smoke and read-only probe instructions](docs/WINDOWS-SMOKE.md). The prior preview.6 core tests and actual disposable E2E passed; preview.8's newly wired owner-only APPLY still requires a fresh bundled build and Windows test.


### Disposable Workshop fixture lab (developer-only, preview.4)

A separate `RUN-FIXTURE-LAB.cmd` in the local Windows test ZIP enables a **user-confirmed, one-attempt** `SteamUGC.CreateItem` call for a brand-new, empty SE2 test item. It never invokes the metadata/content upload or changes existing Workshop items; it does affect the user's Steam account by creating a new entry. The default `RUN-APP.cmd` does not expose this feature.

Creation is guarded by an exact typed confirmation plus a final dialog, and a durable local attempt/receipt prevents automatic retries after ambiguous results. Visibility cannot be guaranteed private; no existing production item should be used as a fixture. See [disposable-item testing](docs/DISPOSABLE-EMPTY-METADATA-TEST.md).

**New creator implementation has not yet passed the next VM build and Windows smoke.**


## Integrated SE2 mod E2E test (preview.5, DEVELOPMENT)

To avoid dozens of manual tiny probes, the separate **fixture lab** now contains a **RUN FULL MOD E2E** test for a **real ModHUB-published, intentionally disposable mod** whose name begins `SE2 UGC E2E TEST`. This is different from the earlier bare CreateItem fixture: a genuine ModHUB project carries a real `.vrgproj`, GUID and published Workshop linkage.

In one user-confirmed run, the suite saves the original metadata and project, then exercises malformed metadata, invalid Workshop identity, empty metadata (when Steam accepts it), and two oversized rejection cases. After every live change it re-queries Steam, invokes the actual preview/write-preflight logic where applicable and restores the original metadata by exact readback. A single safe report summarizes the entire run. The test transport never sets public Workshop title, description, tags, visibility or content.

**Never run this destructive-injection suite on an active or valuable mod.** It requires an original same-item `.vrgproj`, matching user ownership, a specific test title prefix and a separate typed `RUN FULL E2E <WorkshopID>` confirmation. Emergency manual restore is restricted to known injected fault payloads and the original backup. `RUN-APP.cmd` does not expose the suite; only `RUN-FIXTURE-LAB.cmd` does.

Full instructions: [One-pass real ModHUB E2E test](docs/FULL-MOD-E2E-TEST.md).

**Status:** committed to Draft PR and pending VM/offline tests + native Windows smoke. Not a stable/public release or proof of broad game compatibility.


### First real full-suite attempt (preview.6 fix)

The initial live E2E attempt on the dedicated published ModHUB test item `3824260687` exposed a safe, pre-submit classifier defect: an unrelated oversized field was mistaken for an embedded-description overflow when the original published mod already had a description. No Steam metadata update occurred in that aborted attempt. The shared diagnosis and E2E sample tests are now corrected in preview.6; this version still needs a fresh VM test/build. [Incident and combined retest procedure](docs/FULL-MOD-E2E-TEST.md).
