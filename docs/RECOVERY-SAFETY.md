# Recovery safety contract

This design document defines conditions for a future creator-authorized Steam restore UI. The current offline core performs **no Steam API calls**.

## Classification

1. Steam API query **failed**: transient/permission/network error, **never** classify as 0-byte remote metadata.
2. Steam API succeeded and metadata is `""`: missing developer metadata. Preserve Steam details, owner and ID.
3. Metadata nonempty and valid: verify envelope, identity and UTF-8 length.
4. Malformed/truncated JSON: save raw evidence, source and error; do not guess where it was truncated.
5. Conflicts in Workshop ID, GUID, ModId, Steam author, app ID or unrecognized schema: stop.

## Reconstruction sources in descending trust order

1. **Same item:** complete verified developer metadata backed up before failure. Validate original item ID, Steam author and safe size.
2. **Original local project + working schema:** use original `.vrgproj` for GUID and Workshop-bound ModId; optional `ModProject.modhub-metadata` for title, version and category tags. The second working item must have owner-confirmed, valid live developer metadata. Copy **schema**, not Workshop identity, title, version, tags, preview or dependencies.
3. **No backup or comparison:** synthetic candidate from a documented compatible schema is **preview-only**. Never allow APPLY until schema validation and independent approval. A neutral **internal** description of `""` is safer than a public-description replacement.

Local ModHUB metadata with zero/null IDs can be used as **secondary data only**; it cannot replace original Workshop identity.

## Required live Steam gates (NOT implemented in the offline core)

1. Ensure app `1133870`, signed-in account owns target item, and live details of target are available; do not use public page visibility as owner proof.
2. Uncached Steam metadata query must **succeed**. Record raw UTF-8 bytes + SHA256 before any mutation.
3. Load original project and template from user-chosen files; show full source attribution and redacted preview. Avoid disclosing full creator metadata in shareable reports.
4. Verify candidate exact original Workshop ID/ModId/ProjectGUID, author, envelope, schema compatibility, size **under** 5,000 UTF-8 bytes, no unintended data-field replacements, and project binding.
5. Save old raw metadata, source input hashes and candidate to **private** backup. Show precise field-level change and warning that reconstruction is not byte-identical to the lost original.
6. Requery target **immediately before** apply. Require old raw hash and owner unchanged. When comparison item is used, require original template hash still equal to the version used in proposal.
7. Explicit opt-in with phrase `RESTORE <ORIGINAL_WORKSHOP_ID>`. No invisible automatic fixes.
8. Call **only** `SteamUGC.SetItemMetadata` on the same original ID and `SteamUGC.SubmitItemUpdate`. No workshop content, public description, preview, title, visibility, tags, deletion or other item changes.
9. Wait for actual submit result and uncached readback of candidate SHA256; on ambiguous outcome, stop and **do not auto-retry**.
10. Confirm visibility in ModHUB, in-game download in SE2, functionality in a **new disposable world**, and old save recovery on *backup copies only*. These are distinct milestones.

## Testing before release

- Synthetic unit tests for healthy, empty, malformed, oversized and identity-conflict cases.
- Negative tests for wrong owner, wrong project GUID, wrong ModId, stale template, oversized candidate, nonempty malformed data, malformed local project file.
- Confirm Unicode and JSON escaping costs in UTF-8 length.
- Execute .NET 9 tests and Windows build on appropriate self-hosted runners.
- Validate with a disposable, personally owned Workshop test item under **explicit** human authorization. Never test on somebody else's production Workshop entry.

See also [Security](../SECURITY.md).
