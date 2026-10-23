# SE2 ModHUB developer-metadata size bug — controlled reproduction (for modders and Keen)

**Purpose:** Determine precisely WHEN a public Workshop description is copied into the private `FullMetadata.BaseMetadata.Description` developer-metadata field, and whether ModHUB refuses to read the item as the internal JSON approaches 5,000 UTF-8 bytes. A *long visible Workshop description by itself has NOT been shown to trigger the bug.*

**State of evidence (existing case):** A real SE2 Workshop item had 5,343 bytes of valid developer metadata including 3,734 characters of internal `Description`. The ModHUB log failed to parse its metadata around the 5,000-byte boundary. Clearing **only that internal field** to empty reduced the payload to 1,479 bytes, after which the same existing ID could be read and the mod worked in-game again. The exact ModHUB UI sequence causing this internal field to populate is **unknown**; these steps investigate it, they do not assert a known reproducible trigger.

## Important safety rules

- Use a **NEW, disposable, nonessential test mod** created with ModHUB; never the published mod players rely on. Do not reuse the ID of an existing mod.
- If ModHUB lets you create/publish an empty mod project, use that. Otherwise add **minimal harmless test-only content** according to SDK requirements. Do not include a production game mod or modify a real save.
- Keep the item **private/unlisted** when supported, clearly named `SE2 UGC Metadata Repro - TEST ONLY`, and don't ask players to subscribe. Note: workshop visibility settings and ModHUB behavior may vary.
- Back up your `.vrgproj`, `.modhub-metadata`, entire test project, ModHUB logs, and all generated `original-developer-metadata-PRIVATE.json` snapshots. Do not post private raw developer metadata publicly without reviewing it.
- The GUI **DESCRIPTION PREFLIGHT** is an estimate; it cannot intercept or block ModHUB publication. After *every* publish, use the GUI **CHECK METADATA** on the test item's ID to measure real Steam bytes. **Stop immediately** when data approaches/exceeds 5,000 bytes or ModHUB shows an error.
- Do not intentionally trigger a broken state in someone else's mod. If your test item becomes unreadable, stop and preserve the evidence. Do not delete the test item until evidence is captured. Recovery is not guaranteed.

## Test phases — make just ONE controlled change at a time

| Stage | What to do | Measure and record |
| --- | --- | --- |
| A | Create disposable SE2 ModHUB project. Use `description-stage-A-short.txt`; publish as a NEW test item. | Record Workshop ID, ModHUB version, time, result and **CHECK METADATA** size. |
| B | Edit **only the public description via the Steam Workshop website**, e.g. `description-stage-B-2500.txt`. Do NOT reopen or publish ModHUB. | Repeat CHECK METADATA. Did developer metadata change at all? The public text may be independent. |
| C | Close/restart ModHUB. Open the **disposable** project. If offered `Steam Data Available`, record screenshot of choices; in a separate copy of test project try **Load from Steam**. | Before uploading anything, note whether ModHUB's local description field now contains the long public text. Save local project copy. |
| D | For this **test item only**, publish an update through ModHUB. Start with a moderate description (~2,500 chars), then if safe proceed gradually to 3,250/3,700 chars. **Between each version** close/restart ModHUB and measure developer metadata using the GUI. | After EVERY publish, capture 5,000-byte proximity; if broken, capture exact ModHUB logs/error and stop. Don't keep publishing blindly. |
| E | Optional: repeat the test with equivalent UTF-8 strings containing emojis/umlauts/quotes/BBCode. | Compare *bytes*, not visual character count. Distinguish escaped JSON byte size from visible text length. |

**Avoid conflating:** Changing the public Steam description directly (Stage B) and importing Steam data into ModHUB (Stage C) are *different operations*. If Stage B doesn't change developer metadata but C/D does, report that. If nothing ever exceeds 5,000 or no failure appears, report a **negative reproduction** with exact steps—also valuable to Keen.

## Record this for an actionable Keen bug ticket

- Tested ModHUB build/version (e.g. 2.4.0.95), date/time/timezone, Steam Workshop test item ID, Steam language/region if relevant.
- Steps with **which editor** was used for the description (Steam website vs ModHUB), whether `Load from Steam` or `Keep Local` was selected, and whether content was rebuilt/re-published.
- The input test text file name, character count and UTF-8 bytes. Keep a copy of its *exact contents* (BBCode, emojis, newlines).
- Screenshots of publish errors, item visibility, and ModHUB prompts; record `originalBytes` and `originalHash` from each `report-SAFE-TO-SHARE.json`.
- If the failure happens, attach ModHUB and SE2 logs, particularly around `SteamUGC.SubscribeItem(id)`, `Deserialization of metadata`, `Expected end of string`, and `Failed to find metadata for UGC item`.
- Don't post full `original-developer-metadata-PRIVATE.json` publicly; review and send only through an appropriate private support channel.

## Known-case repair versus reproduction

The tool's separate **REPAIR MY ITEM** action is NOT a prerequisite for this repro and should NOT be used during the experiment. First preserve evidence. The repair is offered only when the logged-in Steam account owns the item, the metadata matches the specific confirmed JSON pattern, the internal description is the cause, and the one-field candidate is below 5,000 bytes. Other failures require independent diagnosis. No automated production of corrupt metadata is included in this kit.
