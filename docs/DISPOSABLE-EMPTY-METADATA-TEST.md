# Isolated 0-byte Steam UGC metadata test plan (NO production-mod changes)

Status: PLANNED — not performed. Owner must approve creation of a disposable item and any subsequent item updates. This document **does not** authorize tools to perform Steam writes.

## Baseline already observed

On 2026-10-21, owner used GUI v0.4.0-preview.3 `READ-ONLY UGC PROBE` on their existing **Bulk Production x100** item `3810512193`. Its sanitized report showed:

| Field | Value |
| --- | --- |
| Steam details result | `k_EResultOK` |
| Details query succeeded, not cached | true / true |
| Returned ID + consumer app matched | true / true |
| Signed-in owner matched | true |
| `GetQueryUGCMetadata` bool | `true` |
| Raw output was null | `false` |
| UTF-8 bytes | `1479` |
| Assessment | `Present` |
| Steam item updates | none |

This proves a healthy control, **NOT** 0-byte behavior and **NOT** a successful missing-metadata restore.


## Explicit single-use fixture creator — Windows preview.4

A developer-only creator has been implemented in the local Windows test build. It is **not part of the default RUN-APP.cmd flow**. Launch `RUN-FIXTURE-LAB.cmd` only when you want to create a brand-new test Workshop item on your Steam account.

1. First run `RUN-UI-SMOKE.cmd`. Stop if it fails. Use the SAME fresh preview.4 package.
2. Keep Steam signed in, close Space Engineers 2 and ModHUB.
3. Start `RUN-FIXTURE-LAB.cmd`, then click **CREATE TEST ITEM**.
4. Read the in-app warning: creating an item is a real Steam account mutation. Steam's default visibility is NOT verified private, and newly created items might not be queryable.
5. Enter the exact confirmation `CREATE EMPTY SE2 TEST ITEM`; a second warning asks for final Yes/No approval.
6. The creator reserves one unique local attempt file *before* calling `SteamUGC.CreateItem`. It performs **no** `StartItemUpdate`, `SetItemMetadata`, `SetItemContent`, `SetItemVisibility` or `SubmitItemUpdate`. The saved receipt includes the new Workshop ID if Steam confirms creation.
7. Close the dialog. The new ID is entered into the main form; click **READ-ONLY UGC PROBE**. Share only the resulting safe report. If Steam returns an error / the created item is not queryable, **STOP**: that is not proof of a missing metadata value.
8. If the creator returns timeout, failure or no receipt, **DO NOT RETRY**. The attempt is reserved until a deliberate manual review of `%LOCALAPPDATA%\\SE2WorkshopUGCFix\\TestFixture\\`; Steam might already have created the item. This is deliberate duplicate-prevention.

Only ONE attempt per installation is permitted by the development tool. It never deletes created items. It never calls any existing-item update method. Before treating a new item as private, independently inspect its visibility in Steam; the creator does not set visibility.

**This implementation still needs a fresh developer VM build and real Windows smoke test** before the creator is used. Any Steam account state mutation remains dependent on the on-screen user confirmation.

## Safe source of a truly missing-metadata test item

Prefer an intentionally disposable Workshop item **created by its owner without uploading developer metadata**. Steamworks' official `ISteamUGC.CreateItem` documentation describes creating a new Workshop item with no content attached yet; it does not guarantee how querying an unpublished/new item behaves. The user should first approve the experiment and accept that creating an item, even private, changes their Steam Workshop account.

Do **not**:
- modify, wipe, or replace the developer metadata on any existing production mod;
- use AngryWolf's original SEEM item, Bulk Production x100, or any other active mod;
- give this program a Steam password/token or bypass account permissions;
- invoke `SetItemMetadata("")` on an existing valuable item;
- share raw developer metadata or complete user save files publicly.

A newly created, never-published item might not be returned by normal Workshop queries. That outcome is a **non-diagnostic query failure** — not proof of missing metadata.

## Phased qualification

1. **Owner explicitly authorizes disposable fixture creation** through a separately reviewed workflow, or already has an unused test item with no metadata.
2. Capture a non-sensitive fixture ID and owner details. Prefer private/unlisted visibility where supported.
3. Run the same untouched preview.3 `READ-ONLY UGC PROBE` against that item. Confirm the returned item ID and SE2 app ID. Record the safe JSON report.
4. Classification rules:
   - `ConfirmedEmpty`: a successful noncached details query + `GetQueryUGCMetadata=true` + 0 UTF-8 bytes. The app may generate a preview if an original project GUID is available; **no automatic upload**.
   - `MetadataCallFailed`: details valid but metadata function returned `false`; classification remains **UNKNOWN**. Before changing code, investigate with a second fresh read and independent evidence.
   - `QueryFailed`, `UnexpectedItem`, `WrongOwner`, `CachedQuery`, `AmbiguousNull`: test inconclusive; stop.
   - `Present`: not an empty-metadata fixture; do not blank it just to force the case.
5. Only after 0-byte read semantics and item-binding are evidenced, separately review a genuine metadata-only write flow for a disposable fixture, with original SHA, owner check, explicit typed item ID and uncached SHA readback. No automatic retries on ambiguous results.
6. Verify stages independently: API readback, ModHUB appearance, in-game recognition, actual upload availability, and in-game usability. These are distinct outcomes.

## Universal fallback design target

A creator may have **no second working mod**. If the same-item original metadata backup exists, validate and use it. If only an old `.vrgproj` exists, synthesize a **PREVIEW ONLY** candidate based on a **version-verified, openly documented schema profile**; never invent the original ProjectGUID or silently infer remote owner. A hardcoded unverified schema remains non-writable until a disposable-item test or developer-confirmed compatibility establishes it.

The *public Workshop description* is not the internal developer metadata description. Restoring metadata must not overwrite public title, description, content, thumbnails or tags.

## References

- [Steamworks ISteamUGC API](https://partner.steamgames.com/doc/api/ISteamUGC) — `CreateItem`, `SetItemMetadata`, `GetQueryUGCMetadata`
- [Windows read-only probe procedure](WINDOWS-SMOKE.md)

This is a test **plan**, not instructions to perform a mutation on a live production item.
