# Open-source direction, trust and verifiable releases

**Community direction confirmed by the project owner on 2026-10-24:**
SE2 Workshop UGC Fix is intended as a **free, openly inspectable,
community-maintained open-source project**. Users should be able to review the
scripts, source code, Steam API calls, owner and backup checks and reports
before deciding whether to run the tool, and contribute fixes.

**Important legal distinction:** this document records the owner's **intent**.
It is **not a software license**. Until the owner selects and adds a
recognized open-source license, public GitHub visibility alone does not grant
general rights to redistribute or modify the original project code.
Do not describe an unlicensed development ZIP as a completed open-source release.

## What will be openly inspectable

- Normal program code: the WPF Windows UI, inherited WinForms Steam adapter,
  Steam API calls, and read-only and write preflights.
- Local build/packaging scripts and offline regression suites, with no
  requirement for a paid hosted build service or hidden remote service.
- Source commit, release tag, dependencies and build instructions corresponding
  to every published binary.
- Explicitly documented permissions and limits: how identity, ownership,
  original metadata, private backups, confirmation and Steam readback are checked.
- Relevant known limitations, security advisories, release differences and a
  route for community issue reports and pull requests.

Private original Workshop metadata, project secrets, Steam authentication,
user backups and saved worlds are **not** part of the open-source release.

## Source availability is not proof that a downloaded EXE is safe

A transparent repository lets others **audit** what the maintainer intends to
build. A malicious or modified download can still differ from the published
source. For public trust, each eventual release should provide:

1. A GitHub tag pointing to an immutable source commit and an explicit list
   of packaged dependencies (including `Steamworks.NET.AnyCPU` version and
   native `steam_api64.dll` provenance).
2. All source and PowerShell/Bash build scripts needed to reproduce or
   independently check the build. Document .NET SDK version/digest, NuGet
   package checksums, RID (`win-x64`), publish properties and build environment.
3. A public SHA-256 digest for the **whole** release ZIP and hashes of its
   contents. Current preview ZIP embeds an **unsigned internal manifest**;
   this is not a digital signature or proof of who built it.
4. Actual checks or traceable logs from compiled tests, Windows GUI smoke
   and a read-only functional test. Distinguish independent real-world
   acceptance from owner-reported green tests.
5. Where feasible, an independent reproducible-build comparison and
   authenticated release distribution / code signing. Investigate any
   mismatch; Windows code signing alone also does not guarantee benign code.
6. A release process that excludes developer-only fault-injection fixtures,
   other creators' metadata, private backups, tokens and native components
   without confirmed redistribution permission.

As one **manual local check** for a future published ZIP, Windows PowerShell
can run `Get-FileHash -Algorithm SHA256 -Path .\\SE2-Workshop-UGC-Fix-*.zip`.
Compare it to an independently published expected digest from a trusted
release page. Do **not** treat a checksum placed only inside the same
untrusted ZIP as independent evidence.

## Open-source license choice still required

Two OSI-approved options were discussed:

- **MIT**: minimal attribution/license-notice conditions, easy community
  adoption and compatibility with proprietary integrations, but downstream
  forks may legally distribute closed-source versions.
- **GPLv3**: distributions of derivatives generally must preserve the
  applicable source-availability/copyleft terms, but using proprietary
  native SDK libraries such as Valve Steamworks can introduce additional
  linking/compatibility and explicit-exception questions. Such an exception
  cannot be added on behalf of third-party GPL contributors without rights.

The owner confirmed an open-source community goal **but has not explicitly
selected MIT versus GPLv3 (or another OSI-approved license)**.
No `LICENSE` file or blanket redistribution permission should be inserted
without settling that choice. Any selection applies to the owner's project
code only, **not** Valve's proprietary binaries, Keen's game assets or
independent third-party dependencies.

Relevant references:
- Open Source Initiative: https://opensource.org/osd
- GNU GPL FAQ on proprietary libraries:
  https://www.gnu.org/licenses/gpl-faq.en.html#GPLIncompatibleLibs
- Valve Steamworks SDK agreement:
  https://partner.steamgames.com/documentation/sdk_access_agreement/
- Third-party evidence: [THIRD-PARTY-DEPENDENCIES.md](THIRD-PARTY-DEPENDENCIES.md)
- Release gates: [WPF-RELEASE-READINESS.md](WPF-RELEASE-READINESS.md)

## Status

The accepted, owner-smoke-tested **`0.6.0-preview.9`** remains a local test,
**not a public release**. Before first distribution, choose the final
open-source license; review native Valve redistribution rights and exact ZIP
assets; migrate to a qualified supported .NET runtime; and finish
WPF-triggered recovery qualification and publication safety.
