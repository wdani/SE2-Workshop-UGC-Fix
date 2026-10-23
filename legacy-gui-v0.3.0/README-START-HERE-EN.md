# SE2 Workshop UGC Fix v0.3.0 – Instructions for mod authors

**For Space Engineers 2 (Steam app 1133870) only.** This free, self-contained Windows utility repairs **one known type of broken Steam UGC developer metadata** caused by a duplicated, overly long internal ModHUB description. It is not a general Steam Workshop repair utility.

## Requirements

- Windows 10/11 x64 and Steam running with **the account that published your mod**.
- Space Engineers 2 and VRAGE3 ModHUB **closed** during the check and repair.
- **No .NET SDK, PowerShell scripts, Steam password, API key or administrator permissions are required for a compiled release EXE.**

## How to use

1. Download the official release ZIP, extract it, and run `SE2-Workshop-UGC-Fix.exe`. The file is unsigned; check the published SHA-256 and source code before running. Microsoft SmartScreen may display a warning.
2. Paste your **existing** Steam Workshop URL or its numeric item ID.
3. Click **1 – CHECK METADATA**. This reads Steam data without changing anything and saves a copy to `%LOCALAPPDATA%\SE2WorkshopUGCFix\Backups`.
4. Review the results. **2 – REPAIR MY ITEM** stays grey/disabled unless the tool confirms the exact recognized error *and* Steam confirms your account is the item owner.
5. If and only if a valid repair is offered, review the confirmation dialog and click **Yes** to authorize the *single* metadata-only Steam update. Nothing is submitted just by checking.
6. After a confirmed success, restart ModHUB and check the mod appears correctly. Test it in a **fresh disposable world**. Test existing saves only on backup copies.

If the tool shows **NO FIX PROPOSED**, that's not a failure: the detected metadata doesn't match this one safe repair. **Do not attempt to bypass the guardrails or replace your original Workshop item.** If Steam times out or the update isn't confirmed, do not blindly retry. Run a fresh read-only check and inspect backups first.

## NEW: Description Preflight (read-only, does not publish)

Click **DESCRIPTION PREFLIGHT** to paste a *planned* Workshop description. This estimates how large the **internal developer metadata** could become **IF** ModHUB also embeds that description in its internal JSON.

- If you previously clicked **CHECK METADATA**, the estimate uses your item's last retrieved metadata structure as a template; otherwise it uses a clearly labelled **reference baseline** from a previously repaired SE2 mod (not a measurement of your new mod).
- The result shows raw UTF-8 bytes, JSON-encoded size (escaping can increase it), projected internal developer-metadata size, and warnings as it approaches 5,000 bytes.
- **CRITICAL:** Long PUBLIC Workshop descriptions by themselves are separate and do not establish a bug. Preflight is a *conditional estimate*, not proof that ModHUB will embed the description and not a guarantee of a safe upload. This tool cannot intercept ModHUB publication. Always use CHECK METADATA after any publication to see the real Steam values.
- Your pasted text is not uploaded, saved or added to safe reports by this calculator.

For reproducing the specific bug with a **disposable test mod only**, see `REPRODUCE-WITH-DISPOSABLE-MOD-EN.md` and the prepared `TEST-DESCRIPTIONS/` files. Do not risk an existing Workshop ID or important saves.

## What exactly changes?

The app handles valid SE2 ModHUB JSON developer metadata larger than 5,000 UTF-8 bytes *only when* an embedded `FullMetadata.BaseMetadata.Description` is the cause and clearing it produces a valid payload below the limit. It **does not** change public Workshop descriptions, actual mod files, preview images, title, tags, visibility or Steam Workshop item ID.

A prior affected mod had 5,343 bytes of developer metadata and was restored under its original Workshop ID after removing the duplicated internal description. This does not mean every UGC error has the same cause.

## Diagnostics and privacy

- **OPEN BACKUP FOLDER** opens the local folder containing `original-developer-metadata-PRIVATE.json` and, if applicable, `candidate-metadata-PRIVATE.json`.
- **COPY SAFE REPORT** copies `report-SAFE-TO-SHARE.json` to the clipboard. This summary includes technical status, hashes and Workshop item ID, **not the full private metadata**. Review it before sharing.
- Never publicly share full private metadata, Steam credentials, Steam session files or files from other mods without inspecting them.
- Source code is available in the developer archive. Users who don't trust unfamiliar executables can inspect and build it themselves.

## Developer: build from source

Windows 10/11 x64 and the official [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) are required **only to build** this application. The `Steamworks.NET.AnyCPU` NuGet dependency is retrieved at build time.

Double-click `BUILD-EXE.cmd`. It will:
1. Run the actual portable C# metadata-patch and preflight safety tests.
2. Publish a Windows x64, self-contained single-file EXE.
3. Create `Release\SE2-Workshop-UGC-Fix-v0.3.0-Windows-x64.zip` containing the EXE, these instructions and an SHA-256 checksum.

The program must be compiled and tested on a real Windows/Steam machine before distributing to other modders. The build has not been executed in the development container.

## Safety limits

- Only Space Engineers 2 UGC items returned by Steam with consumer AppID `1133870` are accepted.
- The current Steam owner must match the signed-in account.
- The tool only accepts the recognized SE2 JSON schema and matching ModId/ProjectID.
- Read responses must be fresh (uncached), and the original data is backed up before any write.
- A syntactic and semantic comparison ensures no field other than internal `Description` changes.
- Source and candidate backups are verified again before submitting.
- The original remote metadata is freshly fetched and compared before writing, with a fresh read-back after submitting.
- No automatic retries if outcome is unknown.

Steamworks documentation: https://partner.steamgames.com/doc/api/ISteamUGC
