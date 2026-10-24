# Third-party dependencies and redistribution review

**Status: preliminary inventory for UNRELEASED `0.6.0-preview.9` (2026-10-24).**
The owner selected **MIT for the project's own authored code** on
2026-10-24: see [LICENSE](../LICENSE). This **does not** authorize
redistribution of proprietary Valve binaries or automatically qualify
a public executable release. Inspect actual output, package metadata,
third-party permissions and notices for each release candidate.

## Direct external dependencies

| Component | Source / pinned version | Reported license | Current assessment |
| --- | --- | --- | --- |
| Steamworks.NET.AnyCPU | NuGet `Steamworks.NET.AnyCPU` **2026.165.2**, referenced from `legacy-gui-v0.3.0/SE2WorkshopUGCFix/SE2WorkshopUGCFix.csproj` and transitively reused by WPF | **MIT** according to this exact NuGet package's metadata/README | Library license identified. Preserve upstream/corresponding copyright and license notices in distribution; confirm the contents of the actual deployed NuGet package and its native assets. |
| Steamworks.NET upstream | Riley Labrecque / `rlabrecque/Steamworks.NET`; wrapper underlying AnyCPU fork | MIT | Attribution/notice review remains required for the relevant fork and original upstream. |
| Microsoft .NET / WPF runtime | Current .NET 9 `net9.0-windows`, self-contained `win-x64` publish | **Review actual redistributed runtime files and notices** | **.NET 9 support ends 2026-11-10.** A production/public candidate should migrate and be requalified on **.NET 10 LTS** (supported until November 2028) rather than publish with an almost-expired runtime. Check embedded runtime patch level and third-party notices. |
| Steamworks native binaries | The exact **2026.165.2 source tag** configures `steam_api64.dll` at `runtimes/win-x64/native` in the NuGet package; the actual delivered WPF ZIP still needs file-by-file inspection | **Valve Steamworks SDK Access Agreement, not MIT** | Valve SDK clause 1.1(b) grants *licensees* conditional redistribution of SDK `redistributable_bin` object-code files **with licensee software**. Verify the project's rights under that agreement and provenance of the packaged DLL before public sharing. |

### Verified upstream references

- Exact currently pinned NuGet package and declared MIT license:
  https://www.nuget.org/packages/Steamworks.NET.AnyCPU/2026.165.2
- AnyCPU fork: https://github.com/Akarinnnnn/Steamworks.NET.AnyCPU
- Upstream wrapper license text: https://github.com/rlabrecque/Steamworks.NET/blob/master/LICENSE.txt
- Valve's native Steamworks API deployment instructions:
  https://partner.steamgames.com/doc/sdk/api
- Valve's Steamworks SDK terms and overview:
  https://partner.steamgames.com/doc/sdk

### Pinned source-tag and Valve agreement evidence (2026-10-24)

The upstream repository tag [`2026.165.2`](https://github.com/Akarinnnnn/Steamworks.NET.AnyCPU/tree/2026.165.2)
contains the [packaging project](https://github.com/Akarinnnnn/Steamworks.NET.AnyCPU/blob/2026.165.2/Standalone3.0/Steamworks.NET.csproj).
Its explicit `Pack="true"` entry maps
`com.rlabrecque.steamworks.net/Plugins/steam_api64.dll` to
`runtimes/win-x64/native/steam_api64.dll`. It also lists redistributable
native assets for win-x86, Linux, Android and macOS. The published package
version is pinned in our existing .NET project, **without upgrading anything**.

The wrapper's [MIT LICENSE](https://github.com/Akarinnnnn/Steamworks.NET.AnyCPU/blob/2026.165.2/LICENSE.txt)
names *Copyright (c) 2013–2026 Riley Labrecque*. Its copyright/permission
notice must accompany a substantial distributed copy of the wrapper. The
license of that **managed C# code does not cover Valve's proprietary SDK**.

Valve's **[Steamworks SDK Access Agreement](https://partner.steamgames.com/documentation/sdk_access_agreement/)**
specifically licenses approved **licensees**, under clause 1.1(b), to
redistribute files from the SDK's `redistributable_bin` folder together with
their own *licensee software* in object form. The agreement also limits SDK
use, references Valve trademarks, forbids unauthorised reverse engineering,
and gives **no automatic Steam distribution entitlement**. We have **not**
verified whether the project owner has accepted the agreement or meets
its distribution conditions, whether the package's native file provenance
matches an authorised SDK redistributable, or which binaries were actually
bundled into the self-contained final ZIP. Thus the right to redistribute
this native file **remains BLOCKED**, not presumed from package availability.
Valves [Steamworks API deployment docs](https://partner.steamgames.com/doc/sdk/api)
confirm that Windows API consumers generally ship `steam_api64.dll`
alongside their application.

**.NET runtime support horizon:** Microsoft's [official policy](https://dotnet.microsoft.com/en-us/platform/support/policy)
lists **2026-11-10** as the end of support for .NET 9 and .NET 8, and
**November 2028** for .NET 10 LTS. Because the executable is built
*SelfContained=true*, the chosen runtime and its patch level travel with
the application. A release candidate needs a separately tested .NET 10
migration and current serviced runtime; do not retroactively call the
already-tested .NET 9 preview unsupported today (2026-10-24).

The NuGet page identifies **Steamworks.NET.AnyCPU 2026.165.2 as MIT-licensed**;
this is the library's license, **not** a license for the application's own
source, Steam, Space Engineers 2 data/assets or the Steam API DLL.
A newer package version may exist, but upgrading is a separate dependency,
compatibility and security review—not a documentation-only change.

## Project ownership and use of game data

- The owner selected the permissive **MIT License** for the project's
  original source on 2026-10-24, allowing free/open and commercial reuse.
  See the top-level [LICENSE](../LICENSE); preserve its required notice.
  This license does not supersede independent Valve or other third-party rights.
- This independent utility is **not affiliated with or endorsed by Keen
  Software House, Valve or Steam**.
- No permission to redistribute original Keen game artwork, icons, UI assets,
  block models, workshop metadata of other creators, or other proprietary game
  files is implied by the licensing of a .NET wrapper.
- The normal consumer ZIP must not contain developer-only fixture tools,
  private original/candidate metadata, test credentials or saved worlds.

## Release blocking actions

1. **Project source license resolved:** MIT was explicitly chosen by the
   owner and the [LICENSE](../LICENSE) file has been added. Include the
   appropriate project copyright/permission notice with distributions.
   **Do not** infer any Valve DLL redistribution permission from this decision.
2. **Preserve MIT notices:** inspect the actual NuGet package's embedded
   license/metadata and ship the proper copyright and MIT license text
   attributable to the deployed Steamworks.NET/AnyCPU components. If extra
   dependencies require notices, include those as well.
3. **Review native Valve files:** identify hashes, versions, provenance and
   redistribution entitlement for each native Steamworks binary in the
   self-contained ZIP. Seek authoritative clarification if distribution rights
   are uncertain.
4. **Migrate off .NET 9 before release:** create a separate, testable `.NET 10 LTS` candidate, re-run compiled tests, Windows startup/Steam read-only checks and compatibility tests, inspect the actual self-contained `win-x64` publish and dependency graph (`dotnet list package --include-transitive`), identify runtime patch level and include required notices. Do **not** change the owner-tested preview.9 candidate in place.
5. **Signing and delivery:** assess publisher signing, trusted release origin,
   package checksum publication, user warnings, vulnerability checks and
   rollback. A self-contained unsigned local-test ZIP with an internal SHA256
   manifest **does not** provide publisher authentication.
6. **Scope and confirmation:** keep live Steam restoration and reconstructed
   working-reference recovery out of public efficacy claims until appropriately
   qualified; no deliberate live Steam fault injection just to clear a UI gate.

These blockers intentionally remain open in
[WPF-RELEASE-READINESS.md](WPF-RELEASE-READINESS.md).
