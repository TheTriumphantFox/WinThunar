<p align="center">
  <img src="docs/site/assets/logo.svg" width="112" alt="WinThunar logo: a blue folder with a lightning bolt">
</p>

<h1 align="center">WinThunar</h1>

<p align="center">
  <strong>The Thunar file manager experience, built natively for Windows 11.</strong><br>
  Tabs, split panes, bulk rename, undo/redo, a transfer queue, and plugins. Free and open source.
</p>

<p align="center">
  <a href="https://github.com/TheTriumphantFox/WinThunar/releases/latest"><img alt="Latest release" src="https://img.shields.io/github/v/release/TheTriumphantFox/WinThunar?label=download"></a>
  <a href="https://github.com/TheTriumphantFox/WinThunar/actions/workflows/ci.yml"><img alt="CI" src="https://github.com/TheTriumphantFox/WinThunar/actions/workflows/ci.yml/badge.svg"></a>
  <a href="LICENSE"><img alt="MIT License" src="https://img.shields.io/badge/license-MIT-blue"></a>
  <img alt="Windows 11" src="https://img.shields.io/badge/Windows-11-0078D4">
</p>

<!-- TODO: add a screenshot or short GIF here once captured on Windows, for example
     <p align="center"><img src="docs/images/overview.png" width="860" alt="WinThunar with tabs and a split pane"></p> -->

WinThunar is a clean-room, Windows-native recreation of the classic Thunar file manager experience.
It targets Windows 11 and uses C#, .NET 10, WinUI 3, and the Windows App SDK.

The project is open source under the MIT License. It does not contain code copied from Thunar. It is an
independent project and is not affiliated with the Xfce project.

## Why WinThunar?

- **Familiar if you know Thunar.** The same menu, toolbar, location bar, side pane, and keyboard
  habits, with Thunar-style drag-and-drop rules and duplicate naming.
- **Careful with your files.** Copies are staged at the destination and replacements roll back on
  failure, so an interrupted transfer cannot destroy the file you already had.
- **Power features without the clutter.** Tabs, split panes, recursive search, previewed bulk rename,
  ten-step undo/redo, and a built-in PowerShell panel.
- **Extensible.** Plugins and custom actions are plain JSON manifests, no C# required.
- **Native and portable.** WinUI 3 with Windows shell icons, thumbnails, the Recycle Bin, and NTFS
  alternate data streams. Installer or portable ZIP, with no separate .NET install.

## Get it

Download the installer (`.msi`) or the portable ZIP from the
**[latest release](https://github.com/TheTriumphantFox/WinThunar/releases/latest)**, then see
[Installing](#installing) for details. Installed copies update themselves.

## Keyboard shortcuts

| Action | Shortcut |
|---|---|
| New tab / close tab | `Ctrl+T` / `Ctrl+W` |
| Toggle split pane | `F3` |
| Search the current folder | `Ctrl+F` |
| Show or hide hidden files | `Ctrl+H` |
| Icon / detailed / compact view | `Ctrl+1` / `Ctrl+2` / `Ctrl+3` |
| Toggle the PowerShell panel | `F4` |
| Delete to Recycle Bin / permanent delete | `Delete` / `Shift+Delete` |
| Force copy / force move when dropping | `Ctrl` / `Shift` while dragging |

<details>
<summary><b>Full feature list</b> (click to expand)</summary>

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

</details>

## Installing

Download `WinThunar.App-win.msi` from the
[latest release](https://github.com/TheTriumphantFox/WinThunar/releases/latest) and run it. The target
Windows 11 computer does not need a separate .NET installation. The installer asks:

- **Just me or everyone.** Just me installs to `%LOCALAPPDATA%\WinThunar.App` without asking for
  administrator permission. Everyone installs to `C:\Program Files\WinThunar.App` for all accounts
  and needs administrator permission.
- **Which shortcuts to create:** a desktop shortcut, a Start menu shortcut, both, or neither.

WinThunar can be removed from Settings > Apps > Installed apps. For a scripted install, pass
`DESKTOPSHORTCUT=""` or `STARTMENUSHORTCUT=""` to `msiexec` to skip a shortcut.

Uninstalling removes the program and, if this copy was registered with Explorer, its "Open in
WinThunar" and default-file-manager settings. Your settings, bookmarks, session, and personal
plugins in `%LOCALAPPDATA%\WinThunar` are kept.

The portable ZIP is still available for running WinThunar without installing it.

## Updating

The installed WinThunar checks GitHub Releases for a new version once a day, starting shortly
after launch. A new version downloads in the background, usually as a small delta, and installs
after you close the last WinThunar window. An "Update ready" bar offers **Restart Now** to install
it straight away; that button waits until running transfers finish or are cancelled. When WinThunar
is installed for everyone, installing an update needs administrator permission, so it never installs
on close: the bar's **Install Now** button asks for permission instead. Use
`Help > Check for Updates...` to check on demand, and `Edit > Preferences > Updates` to turn
automatic checks off. Portable copies do not update themselves.

## Using WinThunar as the default file manager

`Edit > Preferences > Windows Integration` can add "Open in WinThunar" to Explorer's folder menu
and make WinThunar the handler for opening folders and drives. Both settings write only to your
own account's registry (`HKCU\Software\Classes`), record any value they replace, and restore it
when turned off. They are available in the installed and portable builds; MSIX packages cannot register shell
handlers this way.

The same changes can be scripted, and `--unregister-shell` is the recovery path if WinThunar
cannot start:

```powershell
.\WinThunar.exe --register-shell      # "Open in WinThunar" only
.\WinThunar.exe --register-default    # also open folders and drives in WinThunar
.\WinThunar.exe --unregister-shell    # undo both
```

Turn the setting off before moving or deleting the portable folder; the installed copy does this
when it is uninstalled. If the registered copy has
already been moved, launching WinThunar from its new location repairs the registration.

WinThunar accepts a folder or file path, `file:` URIs, and Explorer-style `/select,<path>`,
`/root,<path>`, `/n`, and `/e` switches. A second launch opens a new window in the running
process. Windows still opens Explorer for Win+E and for apps that call
`SHOpenFolderAndSelectItems` directly.

When WinThunar is denied access (for example in `Program Files`), it offers to let the Windows copy
engine finish the copy, move, trash, delete, new-folder, or rename, which asks for administrator
permission itself. WinThunar's own engine does not copy directory links or junctions yet; it offers
the same hand-off, and Windows copies the linked folder's contents as Explorer does. Operations
finished by Windows are not in WinThunar's Undo history. Transfer
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

## Windows release

The installer is built with [Velopack](https://velopack.io) (MIT). Install its packaging tool once:

```powershell
dotnet tool install -g vpk --version 1.2.161
```

Create a self-contained, unpackaged x64 release folder, portable ZIP, installer, and SHA-256
checksums:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Publish-Release.ps1 -Version 0.5.0
```

This writes `artifacts\WinThunar-<version>-win-x64.zip` and
`artifacts\WinThunar-<version>-win-x64.msi`, each with a `.sha256` file. The script adds the
shortcut checkboxes to Velopack's MSI with `scripts\Set-MsiShortcutChoices.ps1`. The installer needs
a three-part version; pass `-SkipInstaller` for a ZIP-only build. Keep `artifacts\installer`
between releases: it holds every version's Velopack packages, which later releases need for
delta updates. The ZIP can be extracted and run with `WinThunar.exe`; the target Windows 11
computer does not need a separate .NET installation.

Installed copies find new versions through GitHub Releases. Publish one by uploading the Velopack
packages with a GitHub token that can create releases (keep the token out of the repository and
your shell history):

```powershell
vpk upload github --outputDir .\artifacts\installer --repoUrl https://github.com/TheTriumphantFox/WinThunar --token $env:GITHUB_TOKEN --publish --releaseName "WinThunar 0.5.0" --tag v0.5.0
```

To try an update before publishing it, install the previous version, then start WinThunar with
`WINTHUNAR_UPDATE_FEED` set to a local `artifacts\installer` folder (or a URL serving one) and use
`Help > Check for Updates...`.

The bundled plugin manifests live beside the executable in `Plugins`. Personal manifests and
custom actions are stored under `%LOCALAPPDATA%\WinThunar\Plugins`, so reinstalling or replacing
the app does not overwrite them.

## Icon

WinThunar's icon is an original design (a folder with a lightning bolt) and is covered by the
project's MIT License. It is drawn by `scripts\New-AppIcons.ps1`, which writes `Assets\AppIcon.ico`
and the MSIX logo images; change the shapes there and re-run it rather than editing the images.

## License

WinThunar is available under the [MIT License](LICENSE).
