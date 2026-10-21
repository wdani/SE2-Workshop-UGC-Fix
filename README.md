# SE2 Workshop UGC Fix

A community-maintained diagnostic and recovery tool for **Space Engineers 2 Steam Workshop developer metadata**.

> **Development status: pre-release.** This repository currently includes an **offline recovery-planning core and synthetic regression tests**. The original Windows GUI v0.3 is being audited for migration. **The new missing-metadata restore flow is not enabled or validated as a public release.**

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
```

Code: [src/SE2WorkshopUGCFix.Recovery/UniversalRecovery.cs](src/SE2WorkshopUGCFix.Recovery/UniversalRecovery.cs).

Design and pending live integration: [docs/RECOVERY-SAFETY.md](docs/RECOVERY-SAFETY.md).

**Compilation/CI status:** The migrated recovery core has not yet passed .NET build/Windows runtime verification. Do not distribute it as a working Steam restore utility.

## History

This code was initially developed in [Space-Engineers-Modding draft PR #10](https://github.com/wdani/Space-Engineers-Modding/pull/10) and is being relocated to a dedicated public repository. See [#8](https://github.com/wdani/Space-Engineers-Modding/issues/8) for incident history.

## License

Not yet chosen. Public visibility alone does not grant a software license. Please do not redistribute or relicense until the repository owner chooses a license.
