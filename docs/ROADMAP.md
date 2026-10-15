# WinThunar roadmap

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
- Safe handling for directory links and junctions (they are currently handed to the Windows copy
  engine, which follows them as Explorer does; native link-preserving copies remain to do)

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
- Taskbar jump list and notifications
- [x] Optional, reversible default handler for folders and drives (Win+E and apps that call
  `SHOpenFolderAndSelectItems` still open Explorer)
- [x] Portable/self-contained x64 GitHub release
- [x] Per-user installer with background delta updates from GitHub Releases (Velopack)
- [x] Transfer crash recovery journal and bounded thumbnail loading
- Accessibility and keyboard-navigation review (files and side-pane locations now have screen-reader names; a full review remains)

## 5. Plugin ecosystem

- [x] Manifest discovery, validation diagnostics, and enable/disable controls
- [x] Custom actions and command placeholders
- [x] Archive integration
- [x] Media-tag editing
- [x] Version-control commands
- [x] Shared-folder management commands
- Version-control overlays
