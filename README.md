# WinThunar

**A classic file manager experience for Windows 11.**

WinThunar is a free, open-source file manager for Windows 11 inspired by Thunar, the Xfce
file manager. It gives you a menu bar, a location bar and a side pane, plus tabs, split panes,
a bulk rename with a preview, and a PowerShell panel that follows the folder you are in.

**[Download for Windows 11 (MSI)](https://github.com/TheTriumphantFox/WinThunar/releases/latest/download/WinThunar.App-win.msi)**
&nbsp;|&nbsp; [Portable ZIP](https://github.com/TheTriumphantFox/WinThunar/releases/latest/download/WinThunar.App-win-Portable.zip)
&nbsp;|&nbsp; [Release notes and all downloads](https://github.com/TheTriumphantFox/WinThunar/releases/latest)

Windows 11, x64. No separate .NET installation required. Installed copies update themselves;
portable copies are updated manually.

[![WinThunar showing tabs and two file panes with sample documents](docs/media/winthunar-split-panes.png)](docs/media/winthunar-demo.mp4)

**[Watch the 25-second demo (MP4)](docs/media/winthunar-demo.mp4)**
or [open the animated walkthrough (GIF)](docs/media/winthunar-demo.gif).

## What it adds to File Explorer

- **Two folders side by side.** Press `F3` to split the window. Each pane keeps its own
  path, history and selection, and drag-and-drop moves or copies between them.
- **See a rename before it happens.** Select several files and press `F2` to preview every new
  name before anything changes. A bulk rename undoes as one step.
- **A shell where your files are.** `F4` opens a PowerShell panel beside the file list that
  follows the active folder.
- **Search that understands what you want.** `Ctrl+F` combines names, types, dates, sizes and
  file contents, for example `name:"annual report" type:pdf modified:last7days size:>1MB`.
- **Undo for file operations.** Create, rename, copy, move, replace and merge can be undone,
  up to ten actions per window.
- **Classic menus and keyboard-first navigation.** A real menu bar, a pathbar or editable
  location bar, bookmarks, a tree side pane, and `F6` to move between areas of the window.

It also has tabs (`Ctrl+T`), detail, icon and compact views (`Ctrl+1`–`Ctrl+3`), thumbnails
and an image/text/PDF preview pane, Git status badges, the full Windows context menu, archive
create and extract, Recycle Bin browsing, network shares, light and dark themes, and plugins
for custom actions. See the [full feature list](docs/FEATURES.md).

### Coming from Linux?

WinThunar follows Thunar's habits where Windows allows: `Ctrl+H` toggles hidden files, `F3`
splits the view, duplicates are named `name (copy 1)`, and dragging moves on the same drive,
copies across drives, `Ctrl` forces a copy and `Shift` forces a move. It is an independent,
clean-room implementation built with C#, .NET 10 and WinUI 3. It is not an official Thunar or
Xfce project and contains no code copied from Thunar.

## Installing

Download `WinThunar.App-win.msi` from the link above and run it. The installer asks:

- **Just me or everyone.** Just me installs to `%LOCALAPPDATA%\WinThunar.App` without asking for
  administrator permission. Everyone installs to `C:\Program Files\WinThunar.App` for all accounts
  and needs administrator permission.
- **Which shortcuts to create:** a desktop shortcut, a Start menu shortcut, both, or neither.

The installer and app are not code-signed yet, so Windows SmartScreen may show
**Windows protected your PC**. Choose **More info**, check that the app name matches the file
you downloaded, then **Run anyway**. The [release notes](docs/RELEASE-0.6.0.md) list each
download's SHA-256 digest.

The portable ZIP runs without installing: extract it and start `WinThunar.exe`.

WinThunar can be removed from Settings > Apps > Installed apps. For a scripted install, pass
`DESKTOPSHORTCUT=""` or `STARTMENUSHORTCUT=""` to `msiexec` to skip a shortcut. Uninstalling
removes the program and any Explorer registration it made. Your settings, bookmarks, session
and personal plugins in `%LOCALAPPDATA%\WinThunar` are kept.

## Updating

The installed WinThunar checks GitHub Releases for a new version once a day. A new version
downloads in the background, usually as a small delta, and installs after you close the last
WinThunar window. An "Update ready" bar offers **Restart Now** to install it straight away, after
running transfers finish. When WinThunar is installed for everyone, installing an update needs
administrator permission, so the bar offers **Install Now** instead of installing on close.
Use `Help > Check for Updates...` to check on demand and `Edit > Preferences > Updates` to turn
automatic checks off. Portable copies do not update themselves.

## Using WinThunar as the default file manager

`Edit > Preferences > Windows Integration` can add "Open in WinThunar" to Explorer's folder menu
and make WinThunar open folders and drives. Both settings write only to your own account's
registry (`HKCU\Software\Classes`), record any value they replace, and restore it when turned
off. Windows still opens Explorer for Win+E and for apps that call `SHOpenFolderAndSelectItems`
directly.

The same changes can be scripted, and `--unregister-shell` is the recovery path if WinThunar
cannot start:

```powershell
.\WinThunar.exe --register-shell      # "Open in WinThunar" only
.\WinThunar.exe --register-default    # also open folders and drives in WinThunar
.\WinThunar.exe --unregister-shell    # undo both
```

Turn the setting off before moving or deleting a portable folder; the installed copy does this
when it is uninstalled. WinThunar accepts a folder or file path, `file:` URIs, and Explorer-style
`/select,<path>`, `/root,<path>`, `/n` and `/e` switches. A second launch opens a new window in
the running process.

When WinThunar is denied access, for example in `Program Files`, it offers to let Windows finish
the operation, which asks for administrator permission itself. Operations finished by Windows
are outside WinThunar's Undo history. See [file-operation behavior](docs/FILE-OPERATIONS.md)
for links, junctions, recovery and the other details.

## Known issues

WinThunar is under active development. The ones you are most likely to meet:

- Long sessions of heavy browsing, searching and previewing can keep using more memory and
  can end in an unexpected close. Both are being investigated.
- Win+E still opens File Explorer, even when WinThunar is the default folder handler.
- Transfer-complete notifications don't appear because of a
  [Windows App SDK issue](https://github.com/microsoft/WindowsAppSDK/issues/6774).
- Undo history is per window and is not kept after WinThunar closes.

The [roadmap](docs/ROADMAP.md) lists everything that remains open.

## Help improve it

Try it on a few sample folders and tell us what makes everyday file management easier or
harder. [Share first-tester feedback](https://github.com/TheTriumphantFox/WinThunar/issues/new?template=tester-feedback.yml),
or [report a bug or request a feature](https://github.com/TheTriumphantFox/WinThunar/issues).
Useful reports include the WinThunar version, steps to reproduce, and what you expected.
Please remove personal paths and filenames from screenshots and logs before sharing them.

To build from source or contribute code, see [CONTRIBUTING.md](CONTRIBUTING.md).

## Documentation

- [What's new](docs/WHATS-NEW.md) and the [latest release notes](docs/RELEASE-0.6.0.md)
- [Full feature list](docs/FEATURES.md)
- [Browser views, tabs, split panes, and preferences](docs/BROWSER-WORKSPACE.md)
- [Search behavior and limits](docs/SEARCH.md)
- [File operations, Undo, and recovery](docs/FILE-OPERATIONS.md)
- [Accessibility status](docs/ACCESSIBILITY.md)
- [Roadmap and remaining gaps](docs/ROADMAP.md)
- [Building, packaging, and publishing releases](docs/RELEASES.md)

## License

WinThunar is available under the [MIT License](LICENSE). Its icon is an original design
covered by the same license.
