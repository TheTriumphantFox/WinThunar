# WinThunar

WinThunar is a clean-room, Windows-native recreation of the classic Thunar file manager experience.
It targets Windows 11 and uses C#, .NET 10, WinUI 3, and the Windows App SDK.

The project is open source under the MIT License. It does not contain code copied from Thunar.

## Current milestone

- Classic menu, toolbar, location bar, side pane, detail view, and status bar
- Places and detected drives
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
- Sequential transfer queue with a live progress strip and active-job cancellation
- Ten-action Undo/Redo history for create, rename, ordinary copy, move, and duplicate operations, guarded by destination-state verification
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
- Create-from-Templates, symbolic links, select-by-pattern, and invert-selection commands
- Previewed, collision-safe bulk rename with grouped Undo/Redo
- Preferences for thumbnails, sorting, recursive search, trash confirmation, tab restoration, and default view
- In-app Windows Recycle Bin browsing, confirmed Empty Trash, mapped network locations, direct UNC connections, terminal launch, and removable-drive eject
- Customizable toolbar visibility and persistent alternate keyboard shortcuts
- Optional image-preview side pane, single-click activation, and per-folder view/zoom/sort memory
- Integrated PowerShell command panel (`F4`) that follows the active folder without covering the file list
- Manifest-based plugin discovery from bundled and per-user plugin folders, with validation diagnostics and enable/disable controls
- Selection-aware custom actions with file patterns, file/folder targeting, confirmation prompts, and argument-list execution without a command shell
- Atomic, junction-safe bundled archive create/extract, media-tag editing, Git status/diff/log, and Windows sharing-management integrations

Directory junction transfer is intentionally blocked until link-safe behavior is implemented. Transfer
undo is intentionally withheld after Replace or directory Merge because the displaced data cannot yet
be restored safely.

## Build

```powershell
dotnet restore .\WinThunar.csproj -r win-x64
dotnet build .\WinThunar.csproj --configuration Debug -p:Platform=x64 --no-restore
dotnet run --project .\WinThunar.csproj --configuration Debug -p:Platform=x64
```

Run the isolated file-operation and browser-state tests:

```powershell
dotnet test .\tests\WinThunar.FileOps.Tests\WinThunar.FileOps.Tests.csproj --configuration Release
```

Developer Mode must be enabled in Windows Settings to register and launch the development package.

## Portable Windows release

Create a self-contained, unpackaged x64 release folder, ZIP, and SHA-256 checksum:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Publish-Release.ps1 -Version 0.1.12
```

The generated `artifacts\WinThunar-<version>-win-x64.zip` can be extracted and run with
`WinThunar.exe`; the target Windows 11 computer does not need a separate .NET installation.

The bundled plugin manifests live beside the executable in `Plugins`. Personal manifests and
custom actions are stored under `%LOCALAPPDATA%\WinThunar\Plugins`, so updating the portable app
does not overwrite them.

## License

WinThunar is available under the [MIT License](LICENSE).
