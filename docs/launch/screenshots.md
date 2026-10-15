# Screenshot and GIF shot list

Capture these on Windows 11 (Win+Shift+S for stills; ScreenToGif or ShareX for GIFs). Use a clean
demo folder with realistic but non-private files, light theme for the README hero and dark theme for
a second set, and a window around 1400x860 so text stays readable when scaled down.

Save finished files in `docs/images/` and keep each under about 2 MB (GIFs under 5 MB).

## Stills

1. **overview.png** (README hero). Two tabs open, split pane on (`F3`), detailed view, one pane
   showing Documents and the other Downloads, a few files selected so the status bar shows totals.
2. **transfer-queue.png.** Copy a large folder across drives so the byte progress, throughput, and ETA
   show, with one more job queued behind it.
3. **bulk-rename.png.** The preview dialog with several files and the new names visible.
4. **search.png.** `Ctrl+F` with live results partway through a search.
5. **terminal-panel.png.** `F4` PowerShell panel open under the file list.
6. **conflict-dialog.png.** The Replace / Skip / Rename / Cancel Remaining prompt.
7. **plugins.png.** The plugin list in Preferences with the bundled plugins enabled.
8. **icon-view-dark.png.** Icon view with thumbnails in dark theme.

## GIFs (5 to 12 seconds each, looping)

1. **tabs-and-split.gif.** Open a tab (`Ctrl+T`), split (`F3`), drag a file from one pane to the other.
2. **undo-redo.gif.** Rename a file, delete another, then undo both.
3. **bulk-rename.gif.** Select files, open bulk rename, type a pattern, watch the preview, confirm.
4. **search.gif.** Type in search and watch results stream in.

## Product Hunt gallery

First image: `docs/site/assets/social-preview.png` (1270x760 minimum is preferred; export a 1270x760
crop if needed). Then overview, transfer queue, bulk rename, and the GIFs.

## Tips

- Hide your username and real folder names (use a demo account or rename folders).
- Turn off notifications and the taskbar clock if it shows a date you do not want in the picture.
- Compress PNGs with a tool such as Squoosh or `oxipng`, and GIFs with `gifsicle -O3`.
