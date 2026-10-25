# WinThunar 0.5.14

Published [24 October 2026](https://github.com/TheTriumphantFox/WinThunar/releases/tag/v0.5.14) for Windows 11 x64 from source `e28cde2f4af9dddd76db51c39218c51fe26e84e9` (PR #35).
All five public binary digests, both updater feeds and the source tag match the verified build.
Project, assembly/file, manifest and release-script versions are aligned at 0.5.14
(four-part version 0.5.14.0).

## Changes

F6 / Shift+F6 identifies whole keyboard regions, including side-pane children, the visible
search/location or pathbar editor, split navigation, preview text/PDF controls and command
output when input is disabled. Closing focused search, preview, command or split controls
restores file-list focus. Pane-local focus handlers select the file-command pane and ignore
stale notifications. Split and command buttons expose explicit action names through automation.
Help > What's New summarizes this release.

An old folder's debounced filesystem watcher can no longer start a refresh while navigation
to a new folder is busy. Previously that refresh could cancel the requested navigation while
`CurrentPath` still named the old folder. The native watcher regression fails against the old
view-model code, passes with the guard, and confirms normal watching resumes afterward.

The source-only endurance harness saves atomic progress checkpoints so native termination
does not erase all completed-work and resource counters. No diagnostic collection or checkpoint
writing is added to the normal application.

## Verification

The versioned 0.5.14 release source passes **128 desktop checks, two theme-restart checks
and 211 service tests with one existing symbolic-link privilege skip**. The Light/Dark
What's New renders were inspected. Tests report NU1900 when NuGet vulnerability metadata
is unavailable. The pre-version-bump focused five-minute
workload passes 131 recorded checks and two restart checks, with 445 cycles, 1,335 searches,
890 previews and a maximum observed dispatcher gap of 265 ms. A pre-change focused baseline
also passes five minutes. The public package verification below is complete.

Both focused builds retain entries after diagnostic cleanup. Two attempted 15-minute runs
using an intermediate global-focus observer terminate in native WinUI with `0xC000027B`.
Their dumps and counters are preserved. The final implementation uses pane-local handlers;
the shorter passing run does not establish a crash fix or a successful 15-minute final retest.
See [full evidence and limits](PRIORITY-VERIFICATION.md#keyboard-and-endurance-follow-up-24-october-2026).

## Package and publication verification

The normal release build ran from clean merged `main` using `Publish-Release.ps1 -Version 0.5.14`.
Application assemblies match across the versioned ZIP, public portable ZIP and full package.
Both MSI copies match, carry version 0.5.14.0, retain scope/shortcut choices and the repair/migration
safeguards, and embed the native guard from the final source build. Deliberate failure actions
and all checked smoke/fault/keyboard/watcher entry points are absent from application packages.

The **701,691-byte** 0.5.13-to-0.5.14 delta reconstructs all **528 archive entries** with
matching names, lengths and SHA-256 digests. All **21 historical update packages** retain their
hashes. All five public binary sizes/SHA-256 digests match the verified build. The modern feed
offers matching 0.5.14 full/delta entries, the compatibility feed offers the matching full package,
latest stable resolves to 0.5.14 and the tag points to the source commit above.

Evidence is in `artifacts/verification-0.5.14/`. These are source/package checks; an installed
upgrade to 0.5.14 remains unverified.

| GitHub binary | Bytes | SHA-256 |
| --- | ---: | --- |
| `WinThunar.App-win.msi` | 93,442,048 | `bdea4ab7e07352e19d252b8f0ba293c9d3ab9d8d2a32219a7859b1a17d24c11e` |
| `WinThunar.App-win-Setup.exe` | 119,498,752 | `b92d00d6efc77d8a6dc99a7fe31abc88df77f94eb6f004fbd4c6a4edace8c13a` |
| `WinThunar.App-0.5.14-delta.nupkg` | 701,691 | `ed69aadfa98a0d548b8d69458f9a5f97fd7ae9295b0842cdd4a74e6eb830a4c0` |
| `WinThunar.App-0.5.14-full.nupkg` | 111,967,232 | `08a61691bcc516d25a9578317c31e5fbbba845e38cad38244763edee7e98f0fb` |
| `WinThunar.App-win-Portable.zip` | 111,950,626 | `c9f010d1145874b8c8880578f8acd043dfe2f0dc4d02635ec56632155525da36` |

## Limits

Manual screen-reader, contrast and scaling review, longer/slow-network endurance, notification
delivery and broader document/filter support remain open. Physical hardware/network and
interactive UAC cases remain deferred at the maintainer's request. Installed upgrade to this
version is not established by source tests or package verification.

An automatic app update does not replace an older cached MSI. Use the latest MSI for installer
safeguards. Legacy per-user migration requires actual administrator elevation in the same
Windows account; preserve an external backup of the complete user-state directory before
legacy maintenance. Legacy all-users migration remains refused. Other installation/maintenance
contexts remain outside the verified path.
