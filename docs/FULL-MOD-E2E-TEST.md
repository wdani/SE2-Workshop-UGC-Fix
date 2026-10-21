# Single-pass full SE2 Workshop metadata recovery test (v0.4.0-preview.6)

## Why this replaces tiny one-off probes

We have already proven two live Steam states on 2026-10-21:
- Owner's existing `3810512193` has healthy metadata: 1,479 bytes.
- Dedicated newly created `3824227768` has **exactly 0 bytes** with `GetQueryUGCMetadata=true`, owner/app/item verified.

The empty test item has **no ModHUB-generated original project identity**. For a genuine full recovery, use a real ModHUB project that the game publishes, not a fabricated ProjectGUID.

## Recommended concrete fixture: a custom SE2 LCD image

The owner has already created the local ModHUB project `SE2 UGC E2E TEST - Recovery`, visible in both Mod HUB and the VRAGE3 Editor. Its `Assets` folder is initially empty. **Do not create another project**.

Use a single local PNG containing a clearly readable `SE2 UGC E2E TEST` message on a contrasting background. In the editor's **project** `Assets` folder (not read-only `Vanilla`), drag in the PNG to import it. In the Vanilla asset search, find `LcdCoreConfiguration` and select its **Partial Override**, saving the override to the test project's own Assets. In the override Inspector, find `Images`, add an element, and drag the imported PNG into the new image entry. Save the project, then **Project > Start Game** and verify the new image appears in an LCD's `Text and Image` mode. Use the short Workshop description `Disposable SE2 UGC metadata-recovery E2E test. Not a gameplay mod.` and tag `Other`; publish through Mod HUB with the most restricted available visibility. Record the **new ModHUB-assigned Workshop ID** and preserve the true local `.vrgproj`.

This uses the documented SE2 LCD image workflow: https://spaceengineers2.wiki.gg/wiki/Guide_New_LCD_Image . The user's actual installed ModSDK UI remains authoritative if control wording differs.

Do not use earlier bare-CreateItem fixture `3824227768` for this project unless Mod HUB officially binds its original project to that item. Do not modify existing production mods.

## A. One-time real ModHUB mod creation

Official SE2 guide: [Space Engineers 2 Modding Overview](https://steamcommunity.com/sharedfiles/filedetails/?id=3484180972).

1. Launch **Space Engineers 2 Mod SDK** from Steam (the VRAGE3 ModHUB).
2. Choose **+ Add Mod**, give it a title that begins EXACTLY `SE2 UGC E2E TEST`, for example `SE2 UGC E2E TEST - Recovery`; create it.
3. In VRAGE3 Editor, make one **small, harmless, observable change** (for example a test asset derived from a built-in editor template). The official guide has project/asset template examples. Validate it locally with **Start Game**, confirm that it loads, and note how to recognize the change. Do not copy a production mod with real dependencies as a shortcut.
4. Publish that mod from **ModHUB** to Steam. Use a test-only description, and select the most restricted visibility available. Check its Workshop page/visibility directly. ModHUB will allocate a **new** Workshop item ID and link a real `.vrgproj` to it.
5. Preserve the original `.vrgproj`, any local ModHUB metadata, mod content, and current healthy Steam metadata. Never manually fabricate/edit GUIDs.
6. Do not force the previous `3824227768` (created by bare `CreateItem`) into a new ModHUB project. Keep it as the independent empty-value control, unless ModHUB itself has a supported way to attach an existing ID.

This is the **only manual test-fixture creation** step. Everything below runs in ONE confirmed suite.

## B. Single complete Windows test run

On Ubuntu dev-worker VM, from the standalone repository's `feat/universal-metadata-recovery` branch:

```bash
cd /srv/dev-worker/workspaces/se2-workshop-ugc-fix
git pull --ff-only
bash scripts/run-offline-tests-docker.sh && bash scripts/package-windows-smoke-docker.sh
```

The latest version is **v0.4.0-preview.6**, currently awaiting its first VM compile. Never start a new tester build without checking that the tests passed.

Copy the local `artifacts/SE2-Workshop-UGC-Fix-0.4.0-preview.6-win-x64-LOCAL-TEST.zip` via MobaXterm, extract, and run `RUN-UI-SMOKE.cmd`. Then, with Steam signed in and **SE2 and ModHUB closed**, open `RUN-FIXTURE-LAB.cmd` (the default `RUN-APP.cmd` does not enable test writes).

Enter the **new ModHUB-published test mod's Workshop ID**, not the original empty-item ID. Click **RUN FULL MOD E2E**, select its original `.vrgproj` and type the exact phrase shown (`RUN FULL E2E <item_id>`). Accept the final message that explicitly states that the test WILL modify the disposable item's developer metadata repeatedly.

Safety gates prior to any update: owns the item, current metadata is healthy, its author is the owner, item has a real ModHUB-linked `ProjectGUID` and Steam ModId, real local `.vrgproj` ID matches, title begins `SE2 UGC E2E TEST`, and no production item ID is used. The complete original raw developer metadata is written **and read back locally before the first modification**.

## C. What the full suite does automatically

| Stage | Actual behavior | Expected result |
| --- | --- | --- |
| Healthy baseline | Verify original project and Steam metadata, create private backup | PASS |
| Malformed JSON | Write short invalid JSON to test item, classify with real reader, invoke original-backup recovery planner and offline write preflight, restore exact backup | PASS-LIVE or STOP |
| Wrong identity | Write a version of the test item's own metadata with deliberately mismatched Workshop ID. Refuse normal recovery and then guardedly restore known original | PASS-LIVE or STOP |
| Exactly 0 bytes | Attempt metadata-only `SetItemMetadata("")`, fresh read back, test original-backup recovery planner, restore exact backup | PASS-LIVE, BLOCKED if Steam rejects empty writes, or STOP |
| Oversized description | Generate a >5 KB original-identity JSON, verify the specific diagnosis, **do not upload** (Steam limit) | PASS-OFFLINE |
| Other oversized data | Generate a >5 KB JSON without long internal description, verify rejection, **do not upload** | PASS-OFFLINE |
| Final | Verify final Steam metadata SHA256/bytes are **exactly equal to the baseline** | PASS or STOP |

One button starts the batch; no intermediate manual approvals between individual scenarios. One final report `e2e-report-SAFE-TO-SHARE.json` shows all outcomes, while the full private source/backups remain local.

The suite window also has **COPY FULL E2E SAFE REPORT**, so the user can paste one combined result rather than looking through separate logs.

**Recovery after interruption:** An `e2e-latest-backup-<ID>.txt` pointer is saved under `%LOCALAPPDATA%\\SE2WorkshopUGCFix` before the first write. If a failed run leaves the disposable item damaged, open the same fixture-lab suite, enter `RESTORE TEST <ID>` and use **EMERGENCY RESTORE FROM TEST BACKUP**. This is an explicit, separate confirmation. The recovery service checks the backed-up original project's real GUID, exact item binding and signed-in Steam owner, then restores **only** when the live metadata exactly matches one of the known faulty payloads produced by this suite. Unknown states are never overwritten. Do not delete the backup before verifying recovery.

### Steam write safeguards

- The E2E transport only calls `StartItemUpdate`, **`SetItemMetadata`**, and `SubmitItemUpdate` on the *verified test item*. It never updates public title, public description, content, preview, tags or visibility.
- Before **each write**, it verifies owner and item again. After **each write**, it re-reads uncached metadata.
- Every intentional mutation is immediately followed by exact-backup restore + fresh readback. The recovery planner is genuinely invoked for `Malformed` and `Missing` states.
- Never automatically retry a timed-out or ambiguous submit. If Steam state differs from both the baseline and known injected test data, stop: **do not overwrite unexpected remote changes**.
- If the test reports `OriginalRestored=false`, do **not** republish/overwrite the item via ModHUB until the private backup is inspected; leave the failing disposable item out of the production workflow.

Steam limits metadata to `k_cchDeveloperMetadataMax`; sending a >5,000-byte fault directly is **not a valid real-state reproduction** because Steam rejects it. Therefore oversize paths run as in-process tests using real JSON but never upload the prohibited payload. See [Steamworks ISteamUGC](https://partner.steamgames.com/doc/api/ISteamUGC).

## D. Game-level verification (once after the batch)

After the final metadata readback equals the baseline, open **ModHUB**, verify it still recognizes the original project ID, launch **Space Engineers 2**, activate the test mod and confirm its observable, harmless effect still works. This distinguishes mere Steam API readback from actual game compatibility.

## Corrected first genuine ModHUB test failure (preview.6)

The first attempted full batch against the published test item `3824260687` in preview.5 stopped **during scenario preparation**, with `Unexpected scenario classification oversized-other-rejection`. No Steam submit had occurred because all five synthetic scenario classifications are checked before any backup/write transport is started.

Root cause: the initial `UniversalRecovery.Diagnose` labelled *any* oversized JSON carrying a nonempty BaseMetadata.Description as `OversizedDescription`, even when an unrelated 5200-byte field alone caused the overflow. This was hidden by the earlier fake unit test's empty ModHUB description.

Fixed in preview.6: diagnosis evaluates a copy with internal description cleared, and labels `OversizedDescription` only if that could solve the byte-limit problem; the final exact-byte one-field patch validator remains authoritative. The real E2E oversized-other fixture now retains its original ModHUB description unmodified and verifies `OversizedOther`. Added a regression test using a genuinely nonempty description, plus explicit `PRECHECK FAILED — NO STEAM METADATA UPDATE WAS SENT` messages for future preparation errors.

**Do not create or publish another mod.** Reuse the same live ModHUB test item and original project from the failed preview.5 attempt after the new preview.6 VM tests/publish pass. This change is not yet VM-validated.

## E. Scope limitation

A historical damaged mod can differ from this isolated test mod in its age, schema, dependencies or missing files. The batch establishes a strong E2E baseline but does not prove universal compatibility with *all* mod versions. No existing production mod should be used for these deliberate failure injections.

**Status:** E2E runner source implemented in Draft PR; .NET and Windows runtime tests for this new version are still pending. A backup plus readback is required before any live mutation.
