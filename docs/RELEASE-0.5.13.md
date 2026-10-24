# WinThunar 0.5.13

Release preparation, 24 October 2026. This version is not published yet; installed copies
continue to use the public 0.5.12 feed until 0.5.13 is published and verified.
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

Package, MSI policy, delta reconstruction and public-feed checks are pending. Evidence is in
`artifacts/verification-0.5.13/`. The earlier installed 0.5.11-to-0.5.12 evidence is separate
from these release checks and does not establish an installed upgrade to 0.5.13.

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
