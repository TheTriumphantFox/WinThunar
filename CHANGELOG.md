# Changelog

Release notes for WinThunar. Downloads are on the
[Releases page](https://github.com/TheTriumphantFox/WinThunar/releases).

## 0.5.0 - 2026-10-14

- MSI installer built with Velopack. It asks for "Just me" (no administrator permission) or
  "Everyone", and which shortcuts to create.
- Automatic updates from GitHub Releases: checked daily, downloaded in the background (usually a
  small delta), and installed after the last window closes. `Help > Check for Updates...` checks
  on demand, and `Edit > Preferences > Updates` turns checks off. Portable copies do not update
  themselves.
- Fixed the window icon, which was missing from release builds.

## 0.4.1 - 2026-10-14

- Optional "Open in WinThunar" Explorer menu entry and an optional, reversible default handler for
  folders and drives, in `Edit > Preferences > Windows Integration`. Changes are written only to
  the current user's registry and restored when turned off.
- WinThunar accepts a folder or file path, `file:` URIs, and Explorer-style `/select`, `/root`,
  `/n`, and `/e` switches. A second launch opens a new window in the running process.
- `--register-shell`, `--register-default`, and `--unregister-shell` command-line switches.

## 0.3.1 - 2026-10-08

- Thunar-style drag and drop: move on the same volume, copy across volumes, `Ctrl` forces copy,
  `Shift` forces move, with folder rows as drop targets.

## 0.3.0 - 2026-10-08

- Interactions aligned more closely with Thunar's.

## 0.2.1 - 2026-10-08

- Improved file and folder creation.

## 0.2 - 2026-10-07

- Fixes to file interactions and shortcut activation.

## 0.1.1 - 2026-08-16

- First published release, as a portable ZIP.
