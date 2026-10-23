# WinThunar roadmap

## Release and source status

Checked 22 October 2026. The latest published application is
[0.5.9](https://github.com/TheTriumphantFox/WinThunar/releases/tag/v0.5.9), published 22 October 2026.
The changes are merged into `main` through PR #25 (source `44d13d6`), and the GitHub updater feed
offers 0.5.9. Documentation-only commits do not distribute an application version.
See [release notes](RELEASE-0.5.9.md) for package verification and remaining limits.

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

Installed copies can discover 0.5.9 through GitHub Releases. See [release publishing](RELEASES.md)
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

- [x] Publish the application additions as 0.5.3 with the verified GitHub update feed.
- [x] Publish 0.5.5 with the complete updater/recovery/search/preview changes, verified assets/feed, and a reconstructed 0.5.3-to-0.5.5 delta.
- [x] Publish 0.5.6 with persistent theme selection, window/dialog propagation, verified assets/feed, and a reconstructed 0.5.5-to-0.5.6 delta.
- [x] Publish 0.5.7 with closing-window session persistence, isolated updater/guest tools, verified assets/feed, and a reconstructed 0.5.6-to-0.5.7 delta.
- [x] Publish 0.5.8 with corrected new-MSI cleanup/repair policy, native guest evidence, verified assets/feed and a reconstructed 0.5.7-to-0.5.8 delta; existing cached MSIs are not retrofitted.
- [x] Publish 0.5.9 with per-user migration state protection, interrupted-migration startup guard, verified assets/feed/tag and all 528 reconstructed 0.5.8-to-0.5.9 delta entries. Forced-rollback MSI registration failure remains disclosed.
- [x] Confirm the existing all-users 0.5.0 to 0.5.3 full-package upgrade from updater logs and all 524 deployed file hashes; see [updater verification](UPDATER-VERIFICATION.md).
- [ ] Repeat the installed upgrade under controlled UI conditions; verify per-user apply-on-close, installed delta updates, and Windows Installer repair/uninstall metadata. The logged all-users full-package upgrade succeeded; the original MSI product entry still reports 0.5.0.0.
  - Earlier read-only post-publication inspection found installed 0.5.3 with no later update check. The subsequent migration-lab captures found installed 0.5.8.0 before/after with unchanged observed hashes; these captures do not establish its UI upgrade path. A disposable unique-identity console probe verifies production-backend delta/full apply-on-exit, corrupt-delta fallback, corrupt-full rejection/retry, pending-package rediscovery, and updated-process startup; see [updater verification](UPDATER-VERIFICATION.md). Released-app UI, Setup/MSI, and UAC coverage remain open.
  - A [released-app guest bundle and test matrix](RELEASED-UPGRADE-LAB.md) are prepared, with seven public binary digests verified and the originating host rejected by every guest action. The user-approved Windows 11 evaluation VM is activated, with an unelevated test user, UAC enabled, and a clean snapshot with all NICs disabled. The seeded per-user released 0.5.5-to-0.5.6 delta path passed first/last-window close behavior and all 524 deployed file hashes. The complete persistence case failed because the secondary window overwrote the original two-tab session; remaining matrix cases are pending.
- [x] Save a closing window's own tabs, active folder and view before scheduling apply-on-exit. Published in 0.5.7; the source regression reproduced and corrected the guest finding, and 67 desktop checks plus 2 restart checks and a normal x64 build passed. See the [controlled upgrade observations](UPDATER-VERIFICATION.md#controlled-released-app-delta-observations-22-october-2026).
- [ ] Prevent an older MSI repair from producing a mixed payload after a Velopack update. Controlled per-user `/fomus` repair returned 0 and kept the DLL/manifest at 0.5.6, but reverted `WinThunar.deps.json` and `WinThunar.pri` to public 0.5.5 contents. The failure and all payload evidence are preserved; see [repair observations](UPDATER-VERIFICATION.md#msi-repair-produces-a-mixed-payload).
- [ ] Review uninstall settings/recovery retention. Controlled silent per-user `/x` removed the app, both registrations and both shortcuts, preserved the generated document, and left Explorer working, but also deleted the session/update settings directory. Recovery/Undo data and positive Shell cleanup were not exercised.
  - The [MSI maintenance policy](MSI-MAINTENANCE.md), published in 0.5.8 installers, corrects the cleanup ID and adds a repair guard for an updated installation, including a missing DLL. Both base/current MSIs pass eleven native condition cases. A corrected-MSI per-user guest run passed same-version repair, native 0.5.7 apply with all 524 deployed files matching, three rejected older-repair cases without mutation, and uninstall retention plus enabled Shell cleanup. Existing cached MSIs are not changed by application updates.
- [ ] Preserve user state automatically during legacy-MSI migration. The [original 0.5.5 MSI -> native 0.5.7 update -> published 0.5.8 MSI test](MSI-MIGRATION.md) returned 0, matched all 524 target files and replaced the cached MSI/registration, but old-MSI cleanup deleted all five seeded settings/plugin/recovery files. Interactive/all-users coverage and real recovery/Undo retention remain open.
  - [x] Implement and publish automatic per-user protection in 0.5.9. Private-candidate migration retains all five seeded state files without helper restoration; all 524 deployed files match after migration and both repairs. Uninstall retains state/document and removes app/registrations/shortcuts/enabled Shell integration. Native filesystem checks cover locks, stale holds and competing-state refusal. All-users legacy migration is refused before removal.
  - [ ] Resolve the forced-rollback MSI registration failure retained as a known issue in 0.5.9. The retained sequence restores state and all 524 application files, but Windows Installer cannot restore the old product registration in the unelevated guest; see [rollback evidence](MSI-MIGRATION.md#forced-rollback-finding--known-limitation). A component-sequencing experiment failed payload rollback and was rejected.
    - [x] Implement and verify a [23 October source remedy](MSI-ROLLBACK.md) requiring actual elevation in the same account before legacy per-user removal. The original failure is freshly reproduced and preserved. Native/MSI policy checks, installed unelevated refusal, elevated early/late rollback, subsequent migration, both repairs and uninstall pass; all evidence archives are hash-verified. This is not included in published 0.5.9.
    - [ ] Publish the guarded migration policy and verify the public installer. The same-account elevated path does not establish non-legacy MSI rollback, all-users migration or standard-user elevation through another account.
  - [x] Verify an explicit external backup/restore workaround from the pre-migration checkpoint. All five state files and 524 deployed files match after restoration and both repair modes. Uninstall removes the app, registrations, shortcuts and explicitly enabled Shell integration while retaining state and the generated document. This is test-helper behavior; direct MSI migration still loses state.
- [ ] Complete the manual accessibility checks documented in [ACCESSIBILITY.md](ACCESSIBILITY.md).
- [x] Verify real disk-full after main-stream writes on a disposable NTFS VHD, including Copy/Move and new/Replace recovery, retry, and Undo.
- [x] Reproduce and fix lost staging-journal tracking during owned loopback-SMB share removal; verify source loss after replacement Move commit.
- [x] Add an actionable recovery warning/dialog and safe retry while protecting live transfers/history and ambiguous copies.
- [ ] Verify remote SMB server/network interruption, physical device removal, and history across separate physical devices. VHD and loopback SMB tests use one SSD; see the [stress-harness results](FILE-OPERATIONS-STRESS.md).
- [ ] Verify notifications on installed and portable builds; registration remains unavailable on this machine.
- [x] Add bounded text-content search, filesystem drive search from This PC, and read-only text previews.
- [x] Add bounded native PDF preview with page navigation and explicit read-only Windows Search index queries.
- [ ] Verify generated PDF/Office content-indexing matches and representative document/filter coverage; index inclusion and freshness are controlled by Windows.
- [ ] Broader Shell/device browsing, cloud status, Office/general document previews, and fresh document-content extraction.
- [ ] Extend local search performance measurements to sustained UI/thumbnail and slow-network endurance checks.
- [ ] Add and verify native handling of unsupported reparse-point types before claiming support.

Release ZIP/MSI packages currently target x64. The project lists other build platforms,
but no x86 or ARM64 download is promised by this roadmap.
