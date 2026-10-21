# WinThunar roadmap

## Release and source status

Checked 21 October 2026. The latest published application is
[0.5.3](https://github.com/TheTriumphantFox/WinThunar/releases/tag/v0.5.3), published 21 October 2026.
The application changes are merged into `main` through PR #13, and the GitHub updater feed
offers 0.5.3. Documentation-only commits do not distribute an application version.
See [release notes](RELEASE-0.5.3.md) for package verification and remaining limits.

The current source candidate is **0.5.5**, with updater startup/installer-launch failure handling
and nine updater regression tests. Additional local work adds an Interrupted Transfers view,
conservative recovery/SMB cleanup, text-content/Windows-index search, This PC search, and text/PDF previews.
It is **not published**; installed copies still receive 0.5.3.
See the [0.5.5 candidate notes](RELEASE-0.5.5.md) and [reliability milestone](RELIABILITY-MILESTONE.md).

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

Installed copies can discover 0.5.3 through GitHub Releases. See [release publishing](RELEASES.md)
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
- [x] Confirm the existing all-users 0.5.0 to 0.5.3 full-package upgrade from updater logs and all 524 deployed file hashes; see [updater verification](UPDATER-VERIFICATION.md).
- [ ] Repeat the installed upgrade under controlled UI conditions; verify per-user apply-on-close, installed delta updates, and Windows Installer repair/uninstall metadata. The logged all-users full-package upgrade succeeded; the original MSI product entry still reports 0.5.0.0.
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
