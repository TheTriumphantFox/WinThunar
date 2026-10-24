# WinThunar 0.5.13

Published [24 October 2026](https://github.com/TheTriumphantFox/WinThunar/releases/tag/v0.5.13)
for Windows 11 x64 from source `23d989f2ac3c7b7d89b88a3b944daf5da071c25d` (PR #33).
All five public binary digests, both updater feeds and the source tag match the verified build.
Project, assembly/file, manifest and release-script versions are aligned at 0.5.13
(four-part version 0.5.13.0).

## Changes

This is a maintenance release. Help > What's New identifies 0.5.13 and retains the summaries
of DOCX body-text preview/search, thumbnail reuse and Restart Now session restoration.
No new browsing or document-format support is claimed.

The [24 October installed verification](PRIORITY-VERIFICATION.md#installed-public-0511-restart-now-retest-24-october-2026)
closes the public 0.5.11 Restart Now handler retest. After a secondary window saves a single-tab
session, the original window's real Restart Now button updates to public 0.5.12 and relaunches
with both original tabs and the active folder. Compact view, the Source bookmark, Light theme
and a generated document survive. All 528 reconstructed package entries and 524 deployed files
match the public full package. This uses an explicitly seeded base cache and offline feed.

The reusable source tool `scripts/Test-ReleasedRestartGuest.ps1` prepares a clean per-user
guest fixture, verifies delta reconstruction and the secondary-window save, compares the
restarted payload and retained state, and exports evidence. All five actions refuse the
originating host before writes or launches. The [lab instructions](RELEASED-UPGRADE-LAB.md#released-restart-now-regression-helper)
describe the actual UI steps and preserve incomplete/failed attempts. The helper is a source
tool, excluded from application packages.

## Verification

The release service suite passes **211 tests**, with one existing symbolic-link permission
skip and no failures. The isolated desktop suite passes **87 checks and two theme-restart
checks**, including the 0.5.13 maintenance summary and retained 0.5.12 DOCX summary in Light
and Dark; both renders were inspected. Guest-helper syntax and all five originating-host
refusals pass. One restricted service-runner attempt could not connect to testhost and ran
zero tests; its aborted report is retained separately from the passing rerun. Builds/tests
report NU1900 when NuGet vulnerability metadata is unavailable.

The normal release build ran from clean merged `main` using `Publish-Release.ps1 -Version 0.5.13`.
Application assemblies match across the versioned ZIP, public portable ZIP and full package.
Both MSI copies have matching hashes, version 0.5.13.0, scope/shortcut controls and the existing
repair/migration safeguards. The embedded native guard matches the final source build; deliberate
failure actions are absent. Application packages exclude smoke/fault entry points and the new
guest-test helper.

The **345,381-byte** 0.5.12-to-0.5.13 delta reconstructs all **528 archive entries** with matching
names, lengths and SHA-256 digests. All **19 historical update packages** retain their hashes.
All five public binary sizes/SHA-256 digests match the verified build. The modern feed offers
matching 0.5.13 full/delta entries, the compatibility feed offers the matching full package,
latest stable resolves to 0.5.13 and the tag points to the source commit above.

Evidence is in `artifacts/verification-0.5.13/`. The earlier installed 0.5.11-to-0.5.12 evidence
is separate from these release checks and does not establish an installed upgrade to 0.5.13.

| GitHub binary | Bytes | SHA-256 |
| --- | ---: | --- |
| `WinThunar.App-win.msi` | 93,429,760 | `ac89c3d2a48bca9f429393808c70c099e038427cda6741d2f00fc7286bc2bfcf` |
| `WinThunar.App-win-Portable.zip` | 111,948,580 | `d4624c0dd5e1025f8b23e0e2d10ff166d70a016c70ad5f970c3d11c443059160` |
| `WinThunar.App-win-Setup.exe` | 119,496,706 | `b7300afe3cd2d8a10892d64ffbf5f7354af84959f7261157917d9ee416d3bfd2` |
| `WinThunar.App-0.5.13-full.nupkg` | 111,965,186 | `469695adde28edfd89fc4731223bd8d69aa6269ae7d467fbc91766c0419a1da7` |
| `WinThunar.App-0.5.13-delta.nupkg` | 345,381 | `40fc59bfecd8b4ec1626081aa776bbe8872d90861f5dc0c405bc624172639685` |

## Limits

DOCX preview/search extracts bounded stored body text; Word page layout, separate document
stories, password-protected documents, legacy DOC/DOCM and other Office formats remain outside
this reader. See [document support and limits](DOCUMENT-TEXT.md).

An automatic app update does not replace an older cached MSI. Use the latest MSI for installer
safeguards. Legacy per-user migration requires actual administrator elevation in the same
Windows account; preserve an external backup of the complete user-state directory before
legacy maintenance. Legacy all-users migration remains refused. Other maintenance contexts,
unseeded delta/cache behavior, Setup and public-network installed UI discovery remain open.

Manual accessibility, installed/portable notification delivery, Office indexing filters,
broader previews and slow-network/day-long endurance remain open. Physical hardware/network
and interactive UAC cases remain deferred at the maintainer's request.
