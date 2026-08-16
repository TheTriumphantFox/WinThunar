# Browser workspace behavior

## Views

WinThunar provides the three classic Thunar folder layouts:

| View | Shortcut | Behavior |
| --- | --- | --- |
| Icon View | `Ctrl+1` | Large folder/file glyphs in a wrapping grid. |
| Detailed List | `Ctrl+2` | Name, size, type, and modified-date columns. |
| Compact List | `Ctrl+3` | Small entries filled vertically into columns. |

Selection, drag-and-drop, double-click opening, keyboard commands, and the file context menu work in
all three layouts. The selected layout is restored at the next launch.

## Tabs

- `Ctrl+T` or the plus button opens a tab at the current folder.
- `Ctrl+W` or a tab close button closes the selected tab.
- WinThunar keeps at least one tab open.
- Every tab owns its own Back and Forward path chain.
- Open tab paths and the selected tab are restored after restart.

## Bookmarks and session state

- `Bookmarks > Add Current Folder` (`Ctrl+Shift+D`) adds the current path to the sidebar.
- `Bookmarks > Remove Current Folder` removes its matching bookmark.
- Missing bookmark and tab paths are ignored safely during restoration.
- Session JSON is written atomically beneath the app's Windows local-application-data directory.
- A missing, unreadable, or malformed session file falls back to safe defaults.

The current session stores folder paths, view mode, hidden-file preference, bookmarks, open tabs, and
the selected tab. It does not contain file contents or credentials.

## Split pane

- `F3` or `View > Split View` opens and closes the right browser pane.
- The right pane owns its own location box plus Back, Up, and Home controls.
- A blue inset border identifies the pane that receives selection-based file commands.
- Create, rename, cut, copy, paste, duplicate, trash, delete, and drag/drop use the active pane.
- File operations refresh both visible panes so changes are reflected on either side.
- The open/closed state and right-side path are restored after restart.
