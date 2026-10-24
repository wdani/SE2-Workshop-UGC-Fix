# Open-source direction, trust and verifiable releases

**Community direction confirmed by the project owner on 2026-10-24:**
SE2 Workshop UGC Fix is intended as a **free, openly inspectable,
community-maintained open-source project**. Users should be able to review the
scripts, source code, Steam API calls, owner and backup checks and reports
before deciding whether to run the tool, and contribute fixes.

**License decision confirmed 2026-10-24:** the owner has approved the
permissive **MIT License**, including use in commercial derivatives.
The top-level [LICENSE](../LICENSE) grants broad rights to use, copy, modify,
publish, sublicense and sell the **project-authored code** as long as its
copyright and license notices are retained. This does **not** automatically
license Valve's proprietary Steamworks DLL, Microsoft runtime components,
external artwork or independent third-party material. It also does **not**
make a local-test ZIP a publicly qualified release.

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

## Open-source license: MIT selected

The owner explicitly accepts that others may reuse the source, build new
versions, and even sell derivative tools. The choice is therefore **MIT**,
not a copyleft requirement that all downstream forks remain open.

- The original application's MIT terms are in [LICENSE](../LICENSE).
- Include that copyright and license notice when distributing copies or
  substantial portions of this project's code.
- No charge is required to use the original project, and third parties
  cannot imply official endorsement by Keen, Valve or the original author.
- MIT legally permits downstream redistribution, including commercial and
  closed-source derivatives; derivative authors must still comply with their
  applicable obligations and third-party licenses.
- Third-party dependencies remain separately licensed, especially
  proprietary native Steamworks SDK files.

**The source license is now decided. Publication is not yet approved.**
Native SDK redistribution, a supported .NET runtime, package notices,
Windows functional qualification and release provenance still need review.

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
