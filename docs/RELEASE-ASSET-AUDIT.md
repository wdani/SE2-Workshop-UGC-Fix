# Read-only release asset and license audit

**Status (2026-10-24): tooling prepared; no public release authorised.**
This audit is intentionally separate from the previously tested
`0.6.0-preview.10` executable and its verified Steam read-only behavior.
The candidate's C# code, XAML, Docker build configuration, ZIP layout and
native Steam API usage are **not changed** in this audit workstream.

## Findings grounded in external documentation

1. The project owns its original source and selected the [MIT License](../LICENSE).
   Valve's official
   [open-source guidance](https://partner.steamgames.com/doc/sdk/uploading/distributing_opensource)
   lists MIT as a generally compatible permissive license for Steamworks
   integrations; this does **not** grant rights to distribute Valve's SDK DLL.
2. The exact pinned NuGet dependency is
   [Steamworks.NET.AnyCPU 2026.165.2](https://www.nuget.org/packages/Steamworks.NET.AnyCPU/2026.165.2),
   which reports MIT for the managed wrapper. The
   [version-tagged build project](https://github.com/Akarinnnnn/Steamworks.NET.AnyCPU/blob/2026.165.2/Standalone3.0/Steamworks.NET.csproj)
   configures `steam_api64.dll` at `runtimes/win-x64/native` in the nupkg.
   The exact [upstream MIT notice](../licenses/Steamworks.NET.AnyCPU-MIT.txt)
   is now preserved in this repository (originally from the upstream
   `2026.165.2` tag); this notice covers Steamworks.NET, **not Valve's DLL**.
3. Valve's
   [Steamworks SDK Access Agreement](https://partner.steamgames.com/documentation/sdk_access_agreement/)
   section 1.1(b) grants **eligible licensees**, subject to the agreement,
   redistribution of designated `redistributable_bin` SDK object-code files
   alongside the licensee's software. The authorisation for this independent
   utility and exact native-file provenance have **not been independently
   confirmed**. Neither a GitHub tag nor a NuGet download grants those rights.
4. The current .NET 10 WPF build uses
   `SelfContained=true`, `PublishSingleFile=true` and
   `IncludeNativeLibrariesForSelfExtract=true`. Therefore, a native DLL
   can be **embedded within the bundled EXE**; the absence of a separate
   `steam_api64.dll` ZIP member does not prove that no Valve code is shipped.
5. Microsoft's
   [.NET runtime asset licensing model](https://github.com/dotnet/runtime/blob/main/docs/project/licensing-assets.md)
   says that binary distributions need corresponding license and third-party
   notices. Determine the *exact* Windows runtime-pack notices and
   `Microsoft.NETCore.App` / WindowsDesktop runtime versions used by the
   self-contained package before release.

## New standalone inspector

`scripts/audit-release-assets.py` is a **read-only** Python 3 helper;
it does not start Steam, run .NET, extract or launch a Windows executable,
rewrite any ZIP, touch original/candidate metadata or contact external sites.

It accepts an existing local preview ZIP, validates the untrusted member
names, forbids duplicate/path-traversal/symlink entries, checks that the
embedded `SHA256-MANIFEST.txt` covers every other file, computes each member's
real SHA-256, and reports the entire ZIP hash and current notice-file presence.
The `--expected-commit` flag checks a trusted known source commit.

An optional, separately supplied NuGet `.nupkg` can be checked against
`Steamworks.NET.AnyCPU` / `2026.165.2` / MIT package metadata; the tool
lists SHA-256 and size for its win-x64 `steam_api64.dll` asset. This only
verifies the **NuGet package's contents**. It does not inspect the native
payload inside a bundled WPF EXE, compare that native payload with an
authorised Valve SDK copy, verify downloaded package authenticity, or
grant permission to redistribute anything.

For a future automated release review (not necessary for the owner now):

```bash
python3 scripts/audit-release-assets.py \
  --zip artifacts/SE2-Workshop-UGC-Fix-0.6.0-preview.10-WPF-win-x64-LOCAL-TEST.zip \
  --expected-commit 67b0872dfa4a3e6627495c039097ac240b0efb14
```

For the NuGet inventory, pass `--nupkg /path/to/steamworks.net.anycpu.2026.165.2.nupkg`
**only when the original package bytes are available**. Do not supply
workshop backups, Steam tokens, or other private files. Because the ZIP's
internal manifest is **unsigned**, even a full integrity PASS **cannot
authenticate the original publisher**. An external trusted checksum and
repeatable build are needed to support that claim.

## What remains to be done before a public Windows ZIP

| Topic | Current result | Release decision |
| --- | --- | --- |
| MIT license for original code | Chosen and committed | **Resolved** |
| Steamworks.NET managed MIT notice | Exact upstream tagged text archived | **Source notice prepared; packaging pending** |
| ZIP build-time SHA256 manifest | Present in preview.10 packaging source; actual ZIP hash not supplied | **Evidence pending** |
| Direct NuGet native DLL inventory | Tag proves configured packaging; actual package bytes not directly audited | **Pending** |
| Embedded native DLL inside WPF single-file EXE | Cannot infer payload from ZIP member names | **Pending independent bundle/asset inspection** |
| Valve SDK redistribution entitlement | Conditional license text known; owner's particular entitlement unverified | **Blocked** |
| Microsoft .NET 10 self-contained runtime notices | Distribution model documented; actual shipped runtime file list absent | **Pending** |
| Native WPF-triggered Steam RESTORE | Only prior WinForms write path qualified; WPF read-only preview.10 qualified | **Not yet qualified** |
| Authenticated public release | No signing, official download or public tag issued | **Blocked** |

**This audit does not require the owner to repeat Windows tests or corrupt a
healthy Workshop item.** The tested `preview.10` code and `preview.9` fallback
remain frozen until a separately reviewed release candidate is explicitly
approved.
