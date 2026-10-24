# Security and responsible reporting

**Development status:** UNRELEASED `0.6.0-preview.9` Windows WPF candidate. The
application contains a **real Steam Workshop developer-metadata write path**,
inherited from its validated WinForms backend. The WPF main interface, offline
smoke and read-only operations have been checked; a live **WPF-triggered**
RESTORE has **not** independently passed an end-to-end test.

## What the program can change

On an eligible creator-owned item the program may, only after explicit
approval, submit an update to **internal developer metadata** using
`SteamUGC.SetItemMetadata` and `SteamUGC.SubmitItemUpdate`.
These operations are **not read-only** and may affect mod availability.
They are distinct from the public Steam Workshop description, title, uploaded
mod content, images, visibility and saved game worlds, which this program must
not change. The app cannot repair project GUID references embedded in old worlds.

Before any eligible update, the code must verify that:

- Steam is running with the correct Space Engineers 2 application ID
  (`1133870`) and the signed-in user owns the Workshop item.
- The remote metadata query succeeded; transient API failures must never be
  interpreted as an empty/missing payload.
- The item identity, original data, source integrity, local private backups,
  candidate and hashes satisfy the recovery preflight.
- The owner explicitly approves the item-specific write; recovery additionally
  requires the exact `RESTORE <WORKSHOP_ID>` phrase and a second warning.
- Immediately prior to submitting the update, the tool rechecks remote owner,
  state and unchanged metadata, and after submitting performs a fresh exact
  readback. If the outcome is unclear or times out, **STOP** rather than
  automatically retrying.

If identity, ownership, backups, metadata consistency or authorisation are
uncertain, the safe outcome is **no Steam write**.

The `RUN-WPF-SMOKE.cmd` and `RUN-WPF-REVIEW-RECOVERY.cmd` modes are
offline/read-only UI checks and must never make a Steam query or write.
The normal main app is **read-only by default**, not read-only under all
conditions. Do not deliberately damage a healthy Workshop item to exercise
these review modes.

## Private information and diagnostics

Do **not** attach original/candidate developer metadata, private backup JSON,
Steam session tokens, account identifiers, private file paths, saved worlds or
full logs containing private information to public issues or discussion
threads. The **Copy safe report** button whitelists the summary-only
`report-SAFE-TO-SHARE.json` schema, but users should still review the
diagnostic values before sharing them. Store private backups securely on the
local PC, and keep original unmodified copies when diagnosing a problem.

The program requires neither Steam passwords nor API keys. Avoid installing
unknown unsigned copies from third-party download sites. The current local
development ZIP includes hashes for integrity checks but is **not digitally
signed**, and the embedded manifest cannot authenticate an unknown publisher.

## Responsible reporting and disclosure

Report sensitive vulnerabilities privately to the repository owner through
GitHub account `wdani`; do not publish proof-of-concept secrets, private
Workshop metadata or exploitable reproduction data in a public Issue.
Non-sensitive functional bugs may be reported using GitHub Issues.

This project is independent of Keen Software House and Valve. The
[release-readiness checklist](docs/WPF-RELEASE-READINESS.md) and
[third-party dependency review](docs/THIRD-PARTY-DEPENDENCIES.md)
must be completed before publication, including the owner's explicit final
go/no-go decision. This repository remains **UNRELEASED**.
