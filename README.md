# SE2 Workshop UGC Fix

A community-maintained diagnostic and recovery tool for **Space Engineers 2 Steam Workshop developer metadata**.

> **Current development status (2026-10-24): UNRELEASED WPF `v0.6.0-preview.9`.**
> Windows native `RUN-WPF-SMOKE.cmd` was reported **PASS by the owner**, following green local build/security tests. The owner also confirmed the main WPF desktop UI, read-only check on their healthy Workshop LCD test item, the description-size checker, the locked offline Recovery dialog, private backup-folder opening, and copying a summary-only safe report. **This is not a public release, not a general recovery guarantee, and not an approval to modify anybody else's Workshop item.**

## Current WPF preview

The Windows x64 client has a compact, dark **WPF** interface, retaining the existing guarded Steam metadata backend. It provides:

- **Check metadata:** owner and identity checks, condition, UTF-8 byte size, and a private backup of the original developer-metadata payload when the Steam read succeeds.
- **API probe:** a separate read-only Steam metadata diagnostic; no repair or restore is unlocked by a probe.
- **Description check:** a local UTF-8/JSON size estimator; editing input invalidates its previous result.
- **Open backups** and **Copy safe report:** private backups remain on the local computer. Clipboard exports accept only a validated summary report, not raw metadata.
- **Recovery assistant:** only for eligible verified-owner missing/malformed metadata. It can prepare an exact-original-backup recovery candidate or experimental source-based preview. Applying an eligible recovery is a **real Steam metadata update** requiring source verification, fresh remote checks, item-specific typed approval, and an additional confirmation. The separate `--review-recovery-ui` launcher is deliberately **offline and locked**.

**Evidence boundaries:** The original-backup RESTORE through the earlier WinForms-based ordinary UI was genuinely accepted on a disposable, creator-owned LCD mod on 2026-10-23, with exact Steam readback and successful in-game use. The current WPF UI reuses that backend, but its **live WPF RESTORE action has not independently passed a real-write acceptance test**. Recovery reconstructed from a working reference is not generally validated. Never deliberately corrupt a healthy Workshop item to test UI appearance or diagnostics.

### Build and run (developer-only)

The supported development branch is `feat/wpf-modern-desktop`, reviewed in [Draft PR #3](https://github.com/wdani/SE2-Workshop-UGC-Fix/pull/3). From the registered dev-worker VM:

```bash
git switch feat/wpf-modern-desktop
git pull --ff-only
bash scripts/package-wpf-preview-docker.sh
```

The script runs offline/static safeguards, compiled .NET recovery/privacy tests, publishes a self-contained Windows x64 program and creates the **local-test-only** archive:

`artifacts/SE2-Workshop-UGC-Fix-0.6.0-preview.9-WPF-win-x64-LOCAL-TEST.zip`

Extract to a **new folder** on Windows and run `RUN-WPF-SMOKE.cmd`; use `RUN-WPF-PREVIEW.cmd` for the main program or `RUN-WPF-REVIEW-RECOVERY.cmd` for a fully locked offline dialog inspection. The ZIP includes an **unsigned** per-file SHA-256 manifest, which is not a code signature or proof that an untrusted distributor is safe.

See [WPF release readiness](docs/WPF-RELEASE-READINESS.md), [security information](SECURITY.md), and [third-party dependencies](docs/THIRD-PARTY-DEPENDENCIES.md).

### Community open-source direction and transparency

The project owner confirmed that **the program is intended to be free and
fully open source for the Space Engineers community**. Anyone should be able
to inspect the source, scripts, Steam API calls and safety checks before
running a download, and contribute improvements. See the
[open-source transparency and verifiable-build plan](docs/OPEN-SOURCE-TRANSPARENCY.md).
Private Workshop metadata and user backups will never be published as part
of the source.

**Source visibility is not binary authentication.** A trustworthy public
release must link source commit, packaged dependencies, build instructions
and independently published checksums. In particular, proprietary Valve
Steamworks runtime DLLs remain separately licensed. No public binary has
been released or signed.

### Release and license status

**Not licensed for public redistribution yet.** The repository owner has not chosen a license for this application's code. The MIT license covering the separately obtained Steamworks.NET.AnyCPU wrapper **does not assign a license to this application's original code**. Before any external distribution, the owner must choose a project license and review required third-party notices, native Steamworks components and distribution conditions. The app is not affiliated with or endorsed by Keen Software House, Steam or Valve.

---

## Archived development record — historical releases and experiments

The remaining text below documents earlier WinForms prototypes, specialized disposable-item experiments and past test instructions. Historical version numbers and "not yet validated" remarks describe those earlier stages, **not the current WPF preview.9 status**. Do not run an old fixture-lab or real Steam mutation merely because an archived section describes it.

### Historical 0.5.0-preview.1 WinForms UI and candidate packaging

The earlier 0.5.0-preview.1 WinForms candidate used a darkened WinForms interface and separated normal user builds from explicit developer-only fixture-lab builds. Neither ZIP was a public release. It was later superseded by the WPF implementation described above.



## Windows UI smoke follow-up (preview.9)

Real user screenshots of preview.7 from 2026-10-23 confirmed that Windows x64 launch, owned Workshop ID `3824260687` Steam read, `Healthy` classification (1,611/5,000 developer metadata bytes), and disabled unnecessary repair worked. The displayed SHA256 `E3F3353B0275FDA4330AB00A3A904ABC7A16B00AA2BD702D69FC4B7D38AD0270` matched the original pre-E2E backup exactly. This verifies **read-only health checking**, not the newly wired normal RESTORE APPLY.

The same preview.7 screenshot exposed a UI regression: the `READ-ONLY UGC PROBE` caption was partly covered by the next summary row; the startup activity log incorrectly claimed recovery was preview-only. Preview.8 separates status, diagnostic toolbar and summary into dedicated nonoverlapping rows, disables unwanted auto-size for status/summary, corrects startup messaging and adds a native UI smoke overlap assertion. Steam recovery behavior is unchanged. **Later confirmed: preview.8 Windows UI launched, with fixed probe/status overlap.**

## Focused GUI acceptance test (preview.9, NOT RELEASED)

Preview.8's Windows startup and geometry were visually verified. The only remaining *live* functional difference from the validated special E2E runner is the ordinary GUI `RECOVERY PREVIEW → RESTORE` click path. Preview.9 therefore adds **one guarded, opt-in disposable fixture preparation** to the existing `RUN-FIXTURE-LAB.cmd` UI. It retains the same official ModHUB project identity, item ownership, real title and original project checks as the fully successful five-scenario suite. It saves `e2e-original-PRIVATE.json` and the authentic `.vrgproj` plus an emergency rescue pointer *before* making one single Steam metadata update to a confirmed `empty-metadata` state. It intentionally **does not auto-restore**, so the actual ordinary GUI must run that step. The existing `RESTORE TEST <ID>` emergency action remains available for the specific known fault. If Steam's response is uncertain, stop rather than repeat.

For the established disposable item `3824260687`: from an **unmodified, healthy Steam state**, run fixture lab and choose its genuine `.vrgproj`. Type the exact `PREPARE SINGLE RESTORE 3824260687`, click `PREPARE ONE GUI RESTORE TEST`, approve the confirmation. Then close the fixture lab, run the regular app, scan the same ID, open recovery preview, browse to the **saved** `e2e-original-PRIVATE.json`, build the candidate, type `RESTORE 3824260687`, approve, and verify the original SHA256 readback. If it fails/ambiguous, avoid repeat submissions and use the known-state emergency restore from the fixture lab after inspection. **Never use this on a production item**. It is expected to change the disposable Steam item exactly twice (inject and normal restore) if successful. This is NOT an automatic background action: each actual submit requires explicit operator consent.

## Compact recovery workflow (preview.9 — NOT RELEASED)

The regular Windows UI has exactly one recovery path: paste an owned Workshop ID and press **CHECK METADATA**. If the developer metadata is empty or malformed, open **RECOVERY PREVIEW** and supply **either** a complete original Steam developer-metadata JSON backup for the *same* Workshop ID, **or** the original `.vrgproj` plus an **other working Workshop item owned by the same creator** (a vetted schema source). Original ModHUB metadata is optional. A `.vrgproj` alone can create an **experimental read-only preview**, but does **not** authorize uploading invented metadata.

A verified source shows the candidate and SHA256. To apply, the creator must type `RESTORE <ORIGINAL_WORKSHOP_ID>`, acknowledge a second warning, and pass fresh same-owner/unchanged-remote checks. Exactly one `SetItemMetadata` update is sent for the owned original Workshop item; an uncached byte-for-byte Steam readback verifies completion. Failed/ambiguous submissions **STOP** with no blind retry. The tool **never edits public Workshop titles/descriptions, mod content, user worlds, local `.vrgproj` IDs, or preview images**. Saved-world compatibility is a separate concern: ModHUB publishing was observed changing the local test project's GUID **before** E2E fault injection; the tool cannot repair old world references automatically.

**Evidence as of 2026-10-23:** the genuine disposable E2E batch (3/3 live Steam faults, 2/2 offline oversized checks), the later **ordinary preview.9 OriginalBackup RESTORE through the normal GUI**, its exact SHA256 Steam readback, and the user's subsequent confirmation that the LCD mod functions in SE2 all passed. Other source paths and old-world project GUID references remain outside the proven scope.

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

Next qualification gate for 0.5.0-preview.1: build both distinct modern UI variants, pass both native Windows smoke tests, visually check scaling, and confirm normal read-only health diagnostics. Normal exact-backup GUI RESTORE was already live-tested in preview.9; do not deliberately corrupt another item merely to test the new UI.


### Read-only Steam API diagnostic (preview.3, unreleased)

The Windows GUI has an explicit **READ-ONLY UGC PROBE** button beside the status line. It is useful if the regular `CHECK METADATA` fails: the probe records the **uncached Workshop details** response and **the independent boolean result of `GetQueryUGCMetadata`**. A failed metadata call (false) is `MetadataCallFailed / UNKNOWN`, **not** automatically interpreted as empty developer metadata. `ConfirmedEmpty` requires a successful metadata call (true) with a 0-byte returned string. All reports omit raw private developer metadata and signed-in Steam account IDs. The probe never enables a Steam write.

The Steamworks documentation does not definitively specify missing-value behavior for all client versions. Compare a known healthy owned item against a **disposable** owner-controlled item only. No existing production mod should be deliberately damaged as a test fixture.

Follow [Windows smoke and read-only probe instructions](docs/WINDOWS-SMOKE.md). The prior preview.6 core tests and actual disposable E2E passed; preview.9's ordinary exact-backup GUI APPLY subsequently PASSED live, including functional SE2 mod verification.


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
