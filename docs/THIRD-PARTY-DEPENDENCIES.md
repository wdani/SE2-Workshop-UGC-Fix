# Third-party dependencies and redistribution review

**Status: preliminary inventory for UNRELEASED `0.6.0-preview.9` (2026-10-24).**
This document does **not** choose a license for the SE2 Workshop UGC Fix
application or authorize public distribution. Check the actual publish
output, package metadata and notices again for each release candidate.

## Direct external dependencies

| Component | Source / pinned version | Reported license | Current assessment |
| --- | --- | --- | --- |
| Steamworks.NET.AnyCPU | NuGet `Steamworks.NET.AnyCPU` **2026.165.2**, referenced from `legacy-gui-v0.3.0/SE2WorkshopUGCFix/SE2WorkshopUGCFix.csproj` and transitively reused by WPF | **MIT** according to this exact NuGet package's metadata/README | Library license identified. Preserve upstream/corresponding copyright and license notices in distribution; confirm the contents of the actual deployed NuGet package and its native assets. |
| Steamworks.NET upstream | Riley Labrecque / `rlabrecque/Steamworks.NET`; wrapper underlying AnyCPU fork | MIT | Attribution/notice review remains required for the relevant fork and original upstream. |
| Microsoft .NET / WPF runtime | .NET 9 `net9.0-windows`, self-contained `win-x64` publish | **Review actual redistributed runtime files and notices** | WPF and .NET components are bundled by the self-contained publish. Verify the corresponding official license/third-party notices and runtime patch level when preparing a public build. |
| Steamworks native binaries | Steam API native redistributables (potentially included as assets in the NuGet publish output) | **Separate from the managed wrapper's MIT license** | Determine exactly which `steam_api64.dll` and other native binaries ship. Verify applicable Valve Steamworks SDK redistribution conditions and origin before distributing. Do not treat the MIT wrapper license as covering Valve's binaries. |

### Verified upstream references

- Exact currently pinned NuGet package and declared MIT license:
  https://www.nuget.org/packages/Steamworks.NET.AnyCPU/2026.165.2
- AnyCPU fork: https://github.com/Akarinnnnn/Steamworks.NET.AnyCPU
- Upstream wrapper license text: https://github.com/rlabrecque/Steamworks.NET/blob/master/LICENSE.txt
- Valve's native Steamworks API deployment instructions:
  https://partner.steamgames.com/doc/sdk/api
- Valve's Steamworks SDK terms and overview:
  https://partner.steamgames.com/doc/sdk

The NuGet page identifies **Steamworks.NET.AnyCPU 2026.165.2 as MIT-licensed**;
this is the library's license, **not** a license for the application's own
source, Steam, Space Engineers 2 data/assets or the Steam API DLL.
A newer package version may exist, but upgrading is a separate dependency,
compatibility and security review—not a documentation-only change.

## Project ownership and use of game data

- The repository owner has **not yet selected a license** for the application's
  original source. **No top-level `LICENSE` is currently present**.
  Public source visibility does not by itself grant reuse rights.
- This independent utility is **not affiliated with or endorsed by Keen
  Software House, Valve or Steam**.
- No permission to redistribute original Keen game artwork, icons, UI assets,
  block models, workshop metadata of other creators, or other proprietary game
  files is implied by the licensing of a .NET wrapper.
- The normal consumer ZIP must not contain developer-only fixture tools,
  private original/candidate metadata, test credentials or saved worlds.

## Release blocking actions

1. **Owner choice required:** select and explicitly approve the project's own
   source/binary license, or another documented public distribution agreement;
   do not guess or add a `LICENSE` on the owner's behalf.
2. **Preserve MIT notices:** inspect the actual NuGet package's embedded
   license/metadata and ship the proper copyright and MIT license text
   attributable to the deployed Steamworks.NET/AnyCPU components. If extra
   dependencies require notices, include those as well.
3. **Review native Valve files:** identify hashes, versions, provenance and
   redistribution entitlement for each native Steamworks binary in the
   self-contained ZIP. Seek authoritative clarification if distribution rights
   are uncertain.
4. **Check runtime assets:** inspect the complete actual `win-x64` publish and
   dependency graph (`dotnet list package --include-transitive`, package
   assets, license notices); include all relevant notices.
5. **Signing and delivery:** assess publisher signing, trusted release origin,
   package checksum publication, user warnings, vulnerability checks and
   rollback. A self-contained unsigned local-test ZIP with an internal SHA256
   manifest **does not** provide publisher authentication.
6. **Scope and confirmation:** keep live Steam restoration and reconstructed
   working-reference recovery out of public efficacy claims until appropriately
   qualified; no deliberate live Steam fault injection just to clear a UI gate.

These blockers intentionally remain open in
[WPF-RELEASE-READINESS.md](WPF-RELEASE-READINESS.md).
