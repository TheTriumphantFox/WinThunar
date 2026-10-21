# Browser workspace behavior

This page describes release 0.5.5, with selected earlier additions marked below. See the
[release/source status](ROADMAP.md#release-and-source-status) before comparing a local build
with the downloaded application.

## Views and navigation

WinThunar provides three layouts in the primary pane:

| View | Default shortcut | Behavior |
| --- | --- | --- |
| Icon View | `Ctrl+1` | A wrapping grid with Windows icons and available thumbnails. |
| Detailed List | `Ctrl+2` | Name, size, type, and modified-date columns. |
| Compact List | `Ctrl+3` | Small entries filled vertically into columns. |

Selection, drag/drop, opening, file commands, and context menus work in these layouts.
The right split pane always uses a detailed list; it does not switch to the primary pane's
icon or compact layout. Windows shell menus are available through **Show More Options** or
`Shift`+right-click; ordinary right-click opens WinThunar's menu.

Use **View > Configure Columns...** to choose visible detail columns; drag their dividers to
resize them. Sorting, folders-first order, zoom, thumbnails, the Shortcuts/Tree side pane,
and the Toolbar Style/Pathbar Style location selector can be configured through the View
menu. Up from a drive root opens This PC. Mapped network locations and direct UNC folder
paths are supported; **Go > Connect to Server...** accepts a complete UNC server/share path.

The last active layout is saved. **Edit > Preferences** can also remember view, zoom, and
sorting separately for each folder. Single-click activation and the image-preview side pane
are optional. Preferences take effect after **Apply**.

**Since 0.5.5:** **View > Preview Pane** supports images and bounded, selectable,
read-only text. Text previews show at most 32,768 characters from supported text/configuration/
source-code files; binary data, unsupported encodings, links, and online-only placeholders are
not read as text. Preview does not edit files or execute markup/scripts. PDFs up to **64 MiB**
render through Windows' PDF API with **Previous/Next page** buttons and an announced page count.
The source is read into a bounded snapshot and closed before rendering; each page reloads the
current file rather than holding it open. Output is capped at **1,024 physical pixels per side**,
including on scaled displays. Links, online-only placeholders, invalid pages, and oversized inputs
are rejected; malformed/password-protected documents report preview unavailability. Changing
selection or hiding the pane cancels the preview. PDF text selection/extraction, password entry,
Office rendering, and general Shell preview handlers remain open.
Metadata/path details scroll when the pane is short. Both detailed
lists reserve filename space when split/preview panes narrow the available width.

## Tabs and windows

- `Ctrl+T` or the plus button opens a primary-pane tab at the active folder, including the
  right pane's folder when that pane is active.
- `Ctrl+W` or a tab's close button closes that tab. At least one tab remains open.
- Each tab has its own Back/Forward history while the window is open. Those history chains
  are not saved across restart; only the folder paths and selected tab are saved.
- **Restore tabs on startup** in Preferences controls reopening the saved tab set.
  If it is disabled, the last folder can still be used as the initial folder.
- An explicit folder launch replaces saved-tab restoration for that new window.
- `Ctrl+N` opens a new window. Further launches reuse the existing process and open windows
  there; they do not automatically turn the folder request into a tab.
- **Since 0.5.3:** `Ctrl+Tab` / `Ctrl+Shift+Tab` cycles through the folder tabs.

## Split pane

- `F3` or **View > Split View** toggles the right browser pane.
- The right pane has its own location box, Back/Up/Home controls, and navigation history.
- The accent-colored inset border identifies the pane receiving selection-based commands.
- Create, rename, cut, copy, paste, duplicate, trash, delete, and drag/drop use the active pane.
- File operations refresh both visible panes. Copy and move progress uses a shared transfer strip.
- The split state and right-side path are restored when starting without an explicit folder
  request and when the saved right-side folder still exists.
- **Since 0.5.3:** keyboard focus moving into a file pane also makes that pane active;
  `F6` / `Shift+F6` cycles between the main keyboard regions.

## Bookmarks and session state

**Bookmarks > Add Current Folder** (`Ctrl+Shift+D`) adds the active path. The same menu can
remove or rename the current folder's bookmark and move it up or down. Names and ordering
are preserved. Missing bookmark or tab folders are ignored safely during restoration.

Session settings are written atomically to `%LOCALAPPDATA%\WinThunar\session.json`.
Missing, unreadable, or malformed JSON falls back to defaults. Saved settings include:

- Last folder, tab folders, selected tab, and split state/path.
- View, sort, zoom, column visibility/widths, thumbnails, hidden files, and per-folder views.
- Bookmark names/order, side-pane and location-selector style, image preview, and terminal visibility.
- Recursive-search, trash-confirmation, tab-restoration, and single-click preferences.
- Toolbar visibility/order, selected custom actions, and alternate keyboard shortcuts.

Session JSON contains paths and preferences, not file contents. It does not preserve the
selection, an in-progress search, navigation-history chains, Undo/Redo history, terminal
output, or running processes. Update preferences, personal plugins, and transfer-recovery
records are separate files/directories under `%LOCALAPPDATA%\WinThunar`.

## PowerShell and custom commands

`F4` or **View > Terminal Panel** toggles the integrated PowerShell command panel. Each
submitted command starts a new, noninteractive `powershell.exe` process in the active
folder and displays output/error text and an exit code. It is not a persistent interactive
shell: variables and directory changes from one command do not carry into the next command
or navigate the file pane. Commands requiring interactive input are not supported there.

**Go > Open Terminal Here** opens Windows Terminal when available, with a PowerShell fallback.
Selection-aware custom actions and bundled manifest actions are distinct from the command
panel. The bundled actions cover ZIP creation/extraction, one-file media-tag editing, Git
status/diff/log, and Windows folder Properties for sharing/security. Git inspection needs
Git installed; media metadata support depends on the Windows property handler for the file.

**Since 0.5.3:** optional Git badges are populated during ordinary folder browsing in
the three primary layouts and the right detailed list. Search results, Trash, and This PC
do not receive badges. A missing Git executable or an unavailable repository leaves them blank.
