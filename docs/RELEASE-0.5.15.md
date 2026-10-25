# WinThunar 0.5.15

Published [24 October 2026](https://github.com/TheTriumphantFox/WinThunar/releases/tag/v0.5.15) for Windows 11 x64 from source `5e01f89186f3de48ccf13cdf2a9b9a80e776ee73` (PR #37).
All five public binary digests, both updater feeds and the source tag match the verified build.
Project, assembly/file, manifest and release-script versions are aligned at 0.5.15
(four-part version 0.5.15.0).

## Changes

File containers reuse one accessible-name binding that follows their current content instead
of capturing each successive file entry. Names, metadata and asynchronously updated Git status
remain current across Details, Icons, Compact, search results and the split pane. Replacement
content changes the announced name and null content clears it without installing another binding.
Help > What's New summarizes the release.

The source-only desktop harness adds native content-transition checks, a stable instance key
per generated directory, recorded process outcomes and nonzero-exit rejection. Endurance
samples include GC heap/commit/fragmentation, pending-finalization and runtime information.
These test hooks are excluded from normal application packages.

## Verification

The pre-version-bump source passes **211 service tests with one symbolic-link privilege skip,
156 desktop checks and two theme-restart checks**. Its final focused fifteen-minute run passes
**1,186 cycles, 3,558 searches, 2,372 previews, 159 recorded desktop checks and two restart checks**,
with both processes exiting zero and a maximum sampled dispatcher gap of **288 ms**. The running
product/harness hashes match that source. No debugger, heap capture or forced collection is used
during its active workload; the existing diagnostic collections run only after work finishes.

After cleanup, four of 3,558 sampled entries remain alive and process handles are 3,748, versus
3,482 initially. Private memory is about 278 MiB initially, 372 MiB at workload end and 359 MiB
after cleanup. Managed memory after cleanup is about 40 MiB; the negative initial raw managed
sample is invalid and is not used for comparison. This is not proof of flat/day-long memory use.

An unchanged 0.5.14-source baseline reproduces native termination `0xC000027B`; the new focused
run does not reproduce it, but does not establish its exact root cause. A separate menu-focused
diagnostic run has different resource behavior and includes intrusive captures; it is not a
matching focused comparison. See [full evidence and limits](PRIORITY-VERIFICATION.md#container-binding-follow-up-24-october-2026).

The versioned 0.5.15 source passes **158 desktop checks, two theme-restart checks and 211
service tests with one existing symbolic-link privilege skip**. Light/Dark What's New renders
were inspected. Tests report NU1900 when NuGet vulnerability metadata is unavailable.
Package and public-release verification below is complete.

## Package and publication verification

The normal release build ran from clean merged `main` using `Publish-Release.ps1 -Version 0.5.15`.
Application assemblies match across the versioned ZIP, public portable ZIP and full package.
Both MSI copies match, carry version 0.5.15.0, retain scope/shortcut choices and repair/migration
safeguards, and embed the native guard from the final source build. Deliberate failure actions
and all checked smoke/fault/container/keyboard/watcher test hooks are absent from app packages.

The **505,042-byte** 0.5.14-to-0.5.15 delta reconstructs all **528 archive entries** with
matching names, lengths and SHA-256 digests. All **23 historical update packages** retain
their hashes. All five public binary sizes/SHA-256 digests match the verified build. The modern
feed offers matching 0.5.15 full/delta entries, the compatibility feed offers the matching full
package, latest stable resolves to 0.5.15 and the tag points to the source commit above.

Evidence is in `artifacts/verification-0.5.15/`. These are source/package checks; an installed
upgrade to 0.5.15 remains unverified.

| GitHub binary | Bytes | SHA-256 |
| --- | ---: | --- |
| `WinThunar.App-win.msi` | 93,450,240 | `801edb124cc00b9f849df82648d1c4df1188f2e7217e1e7e45c20228db9d3f12` |
| `WinThunar.App-win-Setup.exe` | 119,499,184 | `04e41698879aeb432e5fbb357f27ba7443ae38e0735735bcc1c95a0ecf90c24e` |
| `WinThunar.App-0.5.15-delta.nupkg` | 505,042 | `c195bb704bc64098c3d7312b396073d96a51f40295a28369024ab7be08150e8a` |
| `WinThunar.App-win-Portable.zip` | 111,951,058 | `5f4f898b3b73e9c68af26a9381efc1d610ebb20604452bafa63e6896350fe18c` |
| `WinThunar.App-0.5.15-full.nupkg` | 111,967,664 | `647e0426dc8509bfee3f5cdb9a4f324ce212211dfda5c3bf338999b4f547405a` |

## Limits

Installed upgrade to 0.5.15, general native-runtime/retention resolution, day-long or slow-network
stability, notification delivery, manual screen-reader/contrast/scaling review and broader
document/filter coverage remain unverified. Physical hardware/network and interactive UAC
cases remain deferred at the maintainer's request.

An automatic app update does not replace an older cached MSI. Use the latest MSI for installer
safeguards. Legacy per-user migration requires actual administrator elevation in the same
Windows account; preserve an external backup of the complete user-state directory before
legacy maintenance. Legacy all-users migration remains refused. Other installer/maintenance
contexts remain outside the verified path.
