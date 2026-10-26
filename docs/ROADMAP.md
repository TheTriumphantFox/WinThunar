# WinThunar roadmap

## Release and source status

Checked 24 October 2026. The latest published application is
[0.5.15](https://github.com/TheTriumphantFox/WinThunar/releases/tag/v0.5.15), published 24 October 2026.
The changes are merged into `main` through PR #37 (source `5e01f89`), and the GitHub updater feed
offers 0.5.15. Documentation-only commits do not distribute an application version.
See [release notes](RELEASE-0.5.15.md) for package verification and remaining limits.

Release **[0.5.15](RELEASE-0.5.15.md)** reuses file-container accessible-name bindings and adds
native content-transition regressions and isolated test diagnostics. The versioned source
passes 211 service tests with one skip, 158 desktop checks and two restart checks. The earlier
focused fifteen-minute source run passes 3,558 searches and 2,372 previews; retained memory and
the native crash's exact cause remain open. Five public binary digests, both MSI policies and
embedded guard, source tag/feeds, all 528 reconstructed delta entries and 23 historical
packages are verified. Installed upgrade to this version and broader manual/network coverage
remain unverified.

Release **[0.5.14](RELEASE-0.5.14.md)** fixes F6 region selection/focus restoration and stale
folder-watcher navigation, adds accessible split/command action names and a current What's New.
It passes 211 service tests with one skip, 128 desktop checks and two theme-restart checks.
Five public binary digests, both MSI policies/embedded guard, source tag/feeds, all 528
reconstructed delta entries and 21 retained historical packages are verified. The earlier
focused five-minute source workload passes; memory retention, native-crash/longer endurance,
manual accessibility and an installed upgrade to this version remain open.

Release **[0.5.13](RELEASE-0.5.13.md)** is a maintenance update with a current What's New and
the reusable installed Restart Now verification helper. The public 0.5.11-to-0.5.12 two-window
retest preserves both tabs and active folder after a secondary save. The 0.5.13 release passes
211 service tests with one skip, 87 desktop checks and two theme-restart checks. Five public
binary digests, both MSI policies/embedded guard, source tag/feeds, all 528 reconstructed delta
entries and 19 unchanged historical packages are verified. An installed upgrade to 0.5.13 and
the broader remaining matrix are not established by these package checks.

Release **[0.5.12](RELEASE-0.5.12.md)** adds bounded DOCX body-text preview and fresh filesystem
content search, thumbnail worker bounds/reuse and an updated Help > What's New. Verification
passes 211 service tests with one skip, 85 desktop checks and two theme-restart checks. Five
public binary digests, both MSI policies/embedded guard, source tag/feeds, all 528 reconstructed
delta entries and 17 unchanged historical packages are verified. Other Office formats/layout,
index-filter coverage and slow-network/day-long stability remain open.

Release **[0.5.11](RELEASE-0.5.11.md)** saves the invoking window's session before Restart Now
and adds Help > What's New. Verification passed 181 service tests with one skip, 70 desktop
checks, two theme-restart checks and a clean x64 release build. Five public binary digests,
both MSI copies/embedded policy, source tag/feeds, all 528 reconstructed delta entries and
15 historical packages are verified. The 24 October installed 0.5.11-to-0.5.12 Restart Now
retest retains both tabs and the active folder after a secondary window's save; see the
[released-handler evidence](PRIORITY-VERIFICATION.md#installed-public-0511-restart-now-retest-24-october-2026).
Earlier handlers cannot acquire this fix before exiting.

Release **[0.5.10](RELEASE-0.5.10.md)** requires actual same-account elevation before legacy
per-user MSI migration. Unelevated refusal, elevated early/late rollback with restored old
registration/cache, subsequent migration, both repairs and uninstall pass in the private
guest suite. All five public binary digests, both MSI copies/embedded guard, source tag/feed,
all 528 reconstructed delta entries and thirteen retained historical packages are verified.
Service verification passed 173 tests with one skip. Existing cached MSIs are not retrofitted;
legacy all-users migration and non-legacy major-upgrade rollback remain outside this path.

Release **[0.5.9](RELEASE-0.5.9.md)** includes automatic
per-user legacy-MSI migration protection, rollback restoration and an interrupted-migration
startup guard. Legacy all-users migration is refused before removal. Five public binary
hashes, both MSI policies, the source tag/feed, all 528 reconstructed delta entries and
eleven historical packages were verified. The release passed 173 service tests with one skip.
Incomplete old-MSI registration restoration during forced unelevated rollback remains a
known issue in 0.5.9; the state-retention fix does not establish complete installer rollback.

Release **0.5.8** corrects uninstall cleanup and guards older repair in newly built MSIs.
Verification passed 171 service tests with one skip, eleven native condition cases per base
MSI, and the separate per-user policy guest regression. Both release MSI policies, five public
binary hashes, the source tag/feed, all 528 reconstructed delta entries and nine historical
packages were verified. Existing cached MSIs remain unchanged. A subsequent per-user MSI
migration replaced payload/cache correctly but deleted seeded user state during old-MSI
removal; automatic retention and broader UI/UAC coverage are still open.

Release **0.5.7** saves the closing window's own session before final-window update scheduling.
Verification passed 171 service tests with one skip, 67 desktop checks, two restart checks and
four isolated updater scenarios. All five public binary hashes, the updater feed, source tag,
and all 528 entries reconstructed from the 520,657-byte 0.5.6-to-0.5.7 delta were verified.
Older-MSI repair mixed payloads and per-user uninstall settings deletion remain open.

Release **0.5.6** adds persistent **System / Light / Dark** selection in Preferences, shared by
windows, native title bars, and application dialogs. Verification passed 171 service tests with
one skip, 65 desktop checks, and two restart checks. All five public binary hashes, the updater
feed, and all 528 entries reconstructed from the 731,233-byte 0.5.5-to-0.5.6 delta were verified.

Release **0.5.5** includes updater startup/installer-launch failure handling
and nine updater regression tests, an Interrupted Transfers view,
conservative recovery/SMB cleanup, text-content/Windows-index search, This PC search, and text/PDF previews.
The unpublished 0.5.4 candidate was folded into this release. All five public binary hashes,
the updater feed, and reconstruction of all 528 package entries using the 0.5.3-to-0.5.5 delta were verified.
See the [0.5.5 release notes](RELEASE-0.5.5.md) and [reliability milestone](RELIABILITY-MILESTONE.md).

The checked foundation items in sections 1–5 were already in 0.5.0. The additions listed
below are now in published 0.5.3; test harnesses are source tools excluded from app packages.

| Capability | Published 0.5.0 | Published 0.5.3 / source tools |
| --- | --- | --- |
| Directory links/junctions in file transfers | Offer Windows fallback after confirmation; link contents may be followed. | Preserve supported symbolic links/junctions without traversing targets. |
| Jump-list tasks and local transfer notifications | Not included. | Implemented; Windows notification availability remains environment-dependent. |
| Git status badges | Not included; Git commands are available. | Three primary layouts plus right-side details; ordinary folder listings only. |
| F6 regions, Ctrl+Tab tab cycling, keyboard column resizing, metadata/status names | Earlier keyboard and accessibility support only. | Added; full screen-reader and high-contrast review remains open. |
| Desktop smoke harness | Not included. | Available through `scripts/Test-DesktopSmoke.ps1`. |
| Replacement and merge Undo/Redo | Withheld for the whole batch. | Retains displaced items and merge children; guarded replay and exact retained Redo data. |
| Search filters | Literal filename substring. | Names/phrases/wildcards plus type, modified-date, and size filters; streamed traversal. |
| System light/dark theme | Forced light page and fixed command colors. | Follows Windows theme; system resources for command/preview/drop colors. |

## Additions published with 0.5.3

- [x] Native symbolic-link and junction preservation, link-state checks, and recovery cleanup.
- [x] Taskbar jump-list tasks and background-transfer outcome notifications.
- [x] Git status badges with optional, bounded background queries.
- [x] Additional keyboard navigation and accessible names.
- [x] Desktop smoke checks for rendered layouts and selected accessibility behavior.
- [x] Retain displaced content for Undo after Replace and directory Merge, including partial transfers.
- [x] Verify content/alternate-stream state before Undo/Redo and expire retained history data.
- [x] Name/type/date/size search filters, streaming traversal, and cancellable folder enumeration.
- [x] System light/dark theme and expanded desktop checks for filtered search and window Undo/Redo.
- [x] Storage-error/process-termination stress harness and recovery fixes for overlapping history records and concurrent restart recovery.
- [x] Disposable NTFS VHD verification of cross-volume history and actual disk-full allocation failures; native long-path copy/stream/history fixes.

Installed copies can discover 0.5.15 through GitHub Releases. See [release publishing](RELEASES.md)
and [accessibility status](ACCESSIBILITY.md) for package behavior and remaining checks.

## 1. Browser foundation

- [x] Classic Thunar window layout
- [x] Places and Devices side pane, This PC, and OneDrive
- [x] Details, icon, and compact views
- [x] Browser tabs with per-tab navigation history
- [x] Split panes with independent navigation and active-pane file commands
- [x] Navigation history
- [x] Bookmarks and remembered sessions
- [x] Windows-native shell icons, thumbnails, file associations, live folder monitoring, and removable-drive discovery
- [x] Sort controls, configurable detailed-list columns, zoom, free-space status, pathbar mode, and tree side pane
- [x] Configurable toolbar visibility, alternate keyboard shortcuts, single-click activation, image preview, and per-folder view memory

## 2. Thunar-style file jobs

- [x] Create, rename, cut, copy, move, trash, and permanent delete
- [x] Clipboard interoperability with Windows storage items
- [x] Thunar-style duplicate names and replace, skip, rename, or cancel conflict decisions
- [x] Intermediate files for incomplete copies
- [x] Isolated tests using generated disposable directory trees
- [x] Windows Recycle Bin browser for restoration plus confirmed Empty Trash
- [x] Explicit sequential transfer queue with progress and active-job cancel controls
- [x] Undo and redo for the ten most recent safely reversible file operations
- [x] Drag-and-drop rules based on source and destination volumes, including folder-row targets
- [x] Elevation only when an operation requires it (copies, moves, trash, delete, new folder, and
  rename that are denied access can be finished by the Windows copy engine with a UAC prompt)
- Directory links/junctions use the Windows fallback in 0.5.0; native preservation is included in 0.5.3.

## 3. Discovery and power features

- [x] Recursive active-pane search with live results, cancellation, filtering, and result limits
- [x] Previewed, collision-safe bulk rename with grouped Undo/Redo
- [x] Custom actions with file-pattern filters, confirmation, and argument-list execution
- [x] Native Windows Properties including Security/permissions
- [x] Windows shell context menu ("Show More Options" and Shift+right-click) with third-party extensions, Send To, and Open With
- [x] Mapped network shares, direct UNC connections, and Windows network browser
- [x] Removable-device eject and safe removal request
- [x] Native Open With, document templates, symbolic links, selection patterns, and invert selection
- [x] Preferences for implemented display, search, trash-confirmation, and session behavior
- [x] Integrated PowerShell command panel plus external Windows Terminal launch

## 4. Windows integration and distribution

- [x] “Open in WinThunar” shell command
- [x] Command-line folder and `/select` launches with single-instance window reuse
- Taskbar jump list and notifications: included in 0.5.3; see the status table and environment limits.
- [x] Optional, reversible default handler for folders and drives (Win+E and apps that call
  `SHOpenFolderAndSelectItems` still open Explorer)
- [x] Portable/self-contained x64 GitHub release
- [x] MSI installer with per-user/all-users scope and GitHub Release updates (Velopack);
  all-users installation and applying its updates require administrator permission
- [x] Transfer crash recovery journal and bounded thumbnail loading
- Accessibility and keyboard-navigation review: partial source/smoke coverage; full manual
  screen-reader, high-contrast, scaling, and input review remains open.

## 5. Plugin ecosystem

- [x] Manifest discovery, validation diagnostics, and enable/disable controls
- [x] Custom actions and command placeholders
- [x] Archive integration
- [x] Media-tag editing
- [x] Version-control commands
- [x] Shared-folder management commands
- Version-control overlays: included as badges in 0.5.3; see the status table.

## Remaining gaps

Ranked 26 October 2026. The ranking favours what a daily user would hit first and the
evidence that WinThunar has no measured user base yet: no tester reports have arrived since the
19 October [launch](LAUNCH.md), and each release so far has drawn only single-digit downloads.
Work through P0 before P1. Frozen items stay open but get no time until a user reports them.
The checklist and evidence trail that used to be here are kept in the
[verification history](VERIFICATION-HISTORY.md).

### P0 — Stability under sustained use

- [ ] Find the cause of the native `0xC000027B` termination in `Microsoft.UI.Xaml.dll`.
  The unchanged 0.5.14 source reproduces it within three minutes; the 0.5.15 focused
  fifteen-minute run does not, but the cause has not been found, so the crash is not
  confirmed fixed. See [the endurance evidence](PRIORITY-VERIFICATION.md#container-binding-follow-up-24-october-2026).
- [ ] Bound memory and handle growth during browsing, search and previews. The focused 0.5.15
  run ends at 359 MiB after cleanup against 278 MiB at startup; the menu-focused diagnostic
  reaches 857 MiB and 18,065 handles during the workload.
- [ ] Ship only after a one-hour focused workload and a one-hour menu-focused workload both
  finish with no crash and memory that levels off.
- [ ] Move logic out of `MainPage.xaml.cs` where the crash and memory work touches it.

### P0 — Real users

- [ ] Pause releases until the first outside feedback arrives, apart from stability fixes.
- [x] Recapture the screenshots and demo from the current release, rewrite the README for
  first-time visitors, and update the stale release references in [PROMOTION.md](PROMOTION.md)
  and [LAUNCH.md](LAUNCH.md) (26 October 2026).
- [ ] Post the prepared [0.5.15 follow-up](PROMOTION.md#follow-up-post-for-0515) and
  [Linux and Xfce](PROMOTION.md#linux-and-xfce-communities) introductions.
- [x] Follow up on the pending AlternativeTo review: approved, confirmed 26 October 2026.
- [x] Test the Scoop bucket: install, launch and uninstall pass in the lab VM (26 October 2026).
- [ ] Submit the prepared WinGet manifest, and the Scoop manifest to Scoop Extras; see
  [distribution channels](DISTRIBUTION.md).
- [ ] Sign releases, so downloads stop showing SmartScreen's unknown-publisher warning.
- [ ] Hold Show HN, press pitches and download-site submissions until the stability P0 passes.
- [ ] Ask five people directly to use WinThunar as their file manager for a day, and collect
  their reports through the tester-feedback form. Those reports set the P1 order.

### P1 — Daily-use gaps

- [ ] Fix problems that testers report.
- [ ] Find out whether Win+E and apps that call `SHOpenFolderAndSelectItems` can open
  WinThunar; they still open Explorer when WinThunar is the default folder handler.
- [ ] Notifications on installed and portable builds. Registration fails with `0x8007007E`
  (missing Insights resource DLL), waiting on the upstream
  [Windows App SDK fix](https://github.com/microsoft/WindowsAppSDK/issues/6774).

### P1 — Release cost

- [ ] Release on a weekly batch, not per change.
- [ ] Record publication in the release PR, not in a second PR.
- [ ] Re-verify the full delta and historical-package set only when packaging or updater code
  changes; otherwise check the new assets, the feed and the tag.

### P2

- [ ] One manual accessibility pass with Narrator, high contrast and 150% scaling, using the
  checks in [ACCESSIBILITY.md](ACCESSIBILITY.md).
- [ ] An installed upgrade to the current release through the public feed, without a seeded
  cache. This is the path real users take; earlier installed runs used a seeded cache and an
  offline feed.

### Frozen until reported

These stay open and are documented, but get no time until a user hits them:

- Older-MSI repair that leaves mixed files after an update; see
  [repair observations](UPDATER-VERIFICATION.md#msi-repair-produces-a-mixed-payload).
- Legacy-MSI migration beyond the guarded per-user path, all-users and UAC installer matrix,
  and uninstall retention in other installation contexts.
- Remote SMB interruption, physical device removal and history across separate devices
  (already deferred at the maintainer's request).
- Office/DOCX index filters, other Office formats, Shell preview handlers and cloud status.
- Preserving additional reparse-point types; unsupported ones are safely refused.
- Day-long and slow-network endurance, beyond the one-hour P0 bar.

Release ZIP/MSI packages currently target x64. The project lists other build platforms,
but no x86 or ARM64 download is promised by this roadmap.
