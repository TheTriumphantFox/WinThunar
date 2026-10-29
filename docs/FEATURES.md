# WinThunar features

The full feature reference, moved here from the README on 26 October 2026. For the
latest changes see [What's new](WHATS-NEW.md).

## Foundation features retained from 0.5.0

- Classic menu, toolbar, location bar, side pane, detail view, and status bar
- Places, detected drives, a This PC drive overview (Up from a drive root), and OneDrive when signed in
- Real directory enumeration
- Back, Forward, Up, Home, Refresh, direct-path navigation, and folder opening
- Multiple selection and selection totals
- Thunar-style hidden-file filtering with `View > Show Hidden Files` and `Ctrl+H`
- Create folders and blank documents
- Rename and Thunar-style duplicate naming (`name (copy 1)`)
- Windows clipboard Cut, Copy, and Paste for files and folders
- Replace, Skip, Rename, or Cancel Remaining conflict decisions
- Delete moves to the Windows Recycle Bin; `Shift+Delete` requires permanent-delete confirmation
- Destination-side staged copies and rollback-safe replacement, so a failed incoming transfer cannot destroy the existing item
- Native Windows cross-volume copies that preserve file attributes, security metadata, extended attributes, and NTFS alternate data streams
- Sequential transfer queue with byte progress, throughput, ETA, queued-job count, and active-job cancellation
- Persistent recovery records for interrupted staged copies and replacements
- Ten-action Undo/Redo history for create, template/archive creation, rename, ordinary copy, move, and duplicate operations, guarded by destination-state verification
- Drag-and-drop into the current folder or a visible child folder
- Thunar-style drop rules: move on the same volume, copy across volumes, `Ctrl` forces copy, and `Shift` forces move
- Detailed, icon, and compact folder views (`Ctrl+2`, `Ctrl+1`, and `Ctrl+3`)
- Browser tabs with `Ctrl+T`, `Ctrl+W`, and independent Back/Forward history
- Split-pane browsing (`F3`) with an independent path, history, selection, and file-command target
- Persistent custom bookmarks and restart restoration for tabs, selected tab, folders, split state, view mode, and hidden-file preference
- Recursive active-pane search (`Ctrl+F`) with live batched results, query cancellation, hidden-file filtering, and safe junction handling
- Windows shell icons and content thumbnails in all folder views
- Live folder monitoring plus free-space reporting in the status bar
- Sortable and drag-resizable Name, Size, Type, and Date columns; configurable column visibility; persistent zoom and folders-first ordering
- Shortcuts and expandable tree side-pane modes
- Renameable, reorderable persistent bookmarks
- Pathbar and editable-toolbar location selector styles
- Native Windows Properties and Open With dialogs, new windows, and Open in New Tab
- The full Windows shell context menu, including third-party extensions and Send To, from `Show More Options` or Shift+right-click
- Create-from-Templates, symbolic links, select-by-pattern, and invert-selection commands
- Previewed, collision-safe bulk rename with grouped Undo/Redo
- Preferences for thumbnails, sorting, recursive search, trash confirmation, tab restoration, and default view
- In-app Windows Recycle Bin browsing, conflict-safe Restore, confirmed Empty Trash, mapped network locations, direct UNC connections, terminal launch, and removable-drive eject
- Customizable toolbar visibility and persistent alternate keyboard shortcuts
- Optional image-preview side pane, single-click activation, and per-folder view/zoom/sort memory
- Integrated PowerShell command panel (`F4`) that follows the active folder without covering the file list
- Manifest-based plugin discovery from bundled and per-user plugin folders, with validation diagnostics and enable/disable controls
- Selection-aware custom actions with file patterns, file/folder targeting, confirmation prompts, and argument-list execution without a command shell
- Atomic, junction-safe bundled archive create/extract, media-tag editing, Git status/diff/log, and Windows sharing-management integrations


## Daily-default improvements in 0.5.3

- Transfer Undo/Redo includes Replace and directory Merge. Displaced items and exact Redo data
  are retained until the window's ten-action history expires; history still does not survive restart.
- Native copy, stream checks, and Undo handle long Windows paths. Disposable NTFS VHD tests verify
  cross-volume history and real disk-full allocation failures; physical removal and SMB interruption remain open.
- Search combines words, quoted names, wildcards, file types, modified dates, and sizes:
  `name:"annual report" type:pdf modified:last7days size:>1MB`.
- The interface follows Windows light/dark settings and uses system resources in the command panel,
  preview text, and drop highlight. The **System colors** theme adds the Windows 11 Mica backdrop and
  tints selections, the folder tree and the selected tab with the Windows accent color, following
  accent changes live. Full screen-reader and contrast-theme review remains open.
- Folder enumeration checks cancellation between entries; filename search streams directory entries;
  selection totals use the listing's metadata rather than reopening selected paths.

## Desktop integration and accessibility in 0.5.3

These additions are included in 0.5.3. Notification availability and manual accessibility
verification remain subject to the limits below.

The taskbar jump list offers **New window**, **Home**, and **Downloads**. When a transfer finishes
while its window is in the background, WinThunar sends a local Windows notification; selecting it
opens the destination folder. If Windows notifications are unavailable or disabled, the in-app
status and error dialogs continue to report the outcome.

Git repositories show status badges in all three primary-pane layouts and the right-side
detailed list. Badges are not populated in search results or virtual locations such as Trash. The badges are
`✓` clean, `M` modified, `+` added, `−` deleted, `R` renamed, `?` untracked, `·` ignored, and `!`
conflicted. Folder badges summarize their descendants. Git is optional; queries run in the
background, refresh on navigation, on changes in the open folder and once a minute, do not
write the index, and disable repository fsmonitor commands. Tooltips and accessible item names
include the status text. Turning off **Show Git status badges** in Preferences stops WinThunar
starting Git at all.

Use **F6 / Shift+F6** to move between tabs, location, side pane, file panes, and the visible
command input; **Ctrl+Tab / Ctrl+Shift+Tab** switches folder tabs. Focus a column divider and
use **Left / Right** to resize it. Keyboard focus selects the active split pane. The interface
exposes file metadata and Git status to accessibility tools. Most browser chrome uses system
color resources, but fixed colors remain in parts of the interface; high-contrast behavior
and screen-reader use still need manual verification. See the
[accessibility status](ACCESSIBILITY.md) for implemented support and remaining checks.

