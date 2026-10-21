# Security and responsible reporting

This utility can eventually update developer metadata on a Steam Workshop item belonging to the signed-in creator. This is a **high-impact operation**. Currently the migrated recovery engine is **offline-only** and has no Steam write path.

Please do **not** attach private workshop developer metadata, Steam session tokens, user IDs associated with private accounts, saved worlds, or full logs containing private paths to public GitHub issues.

To report security concerns, contact the repository owner privately through GitHub account `wdani` rather than posting sensitive data publicly. Ordinary functional bugs without secrets can use GitHub Issues.

Never run unsigned third-party builds or scripts without reviewing their origin and integrity. The project does not require Steam passwords or API keys. Keep originals and backups before attempting any repair.

Unknown API states, identity conflicts, and failed ownership validation must result in **no Steam changes**.
