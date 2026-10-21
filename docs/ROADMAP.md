# WinThunar roadmap

## Release and source status

Checked 21 October 2026. The latest published application is
[0.5.0](https://github.com/TheTriumphantFox/WinThunar/releases/tag/v0.5.0), published 14 October 2026.
The maintainer's local source checkout declares **0.5.3**, a prepared release candidate with
unreleased application work. That application work is not yet in GitHub's `main` branch.
Documentation-only commits do not distribute a new application version. See [candidate notes](RELEASE-0.5.3.md).

The checked items in sections 1–5 below are in release 0.5.0. Unreleased additions are
listed separately; a checked source task does not mean it has reached installed users.

| Capability | Published 0.5.0 | Maintainer's unreleased local source |
| --- | --- | --- |
| Directory links/junctions in file transfers | Offer Windows fallback after confirmation; link contents may be followed. | Preserve supported symbolic links/junctions without traversing targets. |
| Jump-list tasks and local transfer notifications | Not included. | Implemented; Windows notification availability remains environment-dependent. |
| Git status badges | Not included; Git commands are available. | Three primary layouts plus right-side details; ordinary folder listings only. |
| F6 regions, Ctrl+Tab tab cycling, keyboard column resizing, metadata/status names | Earlier keyboard and accessibility support only. | Added; full screen-reader and high-contrast review remains open. |
| Desktop smoke harness | Not included. | Available through `scripts/Test-DesktopSmoke.ps1`. |
| Replacement and merge Undo/Redo | Withheld for the whole batch. | Retains displaced items and merge children; guarded replay and exact retained Redo data. |
| Search filters | Literal filename substring. | Names/phrases/wildcards plus type, modified-date, and size filters; streamed traversal. |
| System light/dark theme | Forced light page and fixed command colors. | Follows Windows theme; system resources for command/preview/drop colors. |

## Unreleased additions

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

These items need a new version and a published GitHub Release before installed users receive
them. See [release publishing](RELEASES.md) and [accessibility status](ACCESSIBILITY.md).

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
- Directory links/junctions use the Windows fallback in 0.5.0; native preservation is listed
  above as unreleased source work.

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
- Taskbar jump list and notifications: implemented in unreleased source; see the status table.
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
- Version-control overlays: implemented as badges in unreleased source; see the status table.

## Remaining gaps

- [ ] Publish the unreleased application additions with a new version.
- [ ] Complete the manual accessibility checks documented in [ACCESSIBILITY.md](ACCESSIBILITY.md).
- [ ] Verify SMB disconnection, physical device removal, disk-full after partial writes, and history across separate physical devices. Real disk-full allocation failures and distinct-NTFS-volume history pass on a disposable VHD backed by the system SSD; see the [stress-harness results](FILE-OPERATIONS-STRESS.md).
- [ ] Verify notifications on installed and portable builds; registration remains unavailable on this machine.
- [ ] Broader Shell/device browsing, cloud status, document previews, and indexed/content search.
- [ ] Add and verify native handling of unsupported reparse-point types before claiming support.

Release ZIP/MSI packages currently target x64. The project lists other build platforms,
but no x86 or ARM64 download is promised by this roadmap.
