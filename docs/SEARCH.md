# Search behavior

This page describes release 0.5.0 and the current source checkout.

`Ctrl+F` or the Search toolbar button searches the active pane's current folder. Subfolders
are included by default. To search only the current folder, open **Edit > Preferences**,
clear **Include subfolders in search**, and select **Apply**.

Typing restarts a 350-millisecond delay before a query starts. `Enter` starts the query
immediately. Starting the next query cancels the previous traversal, and generation checks
keep cancelled searches from publishing stale results. An empty or whitespace-only query
returns to the ordinary folder listing.

## Matching, limits, and cancellation

- Matches are case-insensitive literal substrings of the file or folder name. Queries are
  trimmed; this is not wildcard, regular-expression, or file-content search.
- Results arrive in batches of up to 40 while traversal continues. The limit is 5,000 results
  per search; the final status reports when that limit is reached.
- **Show Hidden Files** controls hidden, system, and dot-prefixed paths. Hidden folders are
  excluded from traversal when that setting is off.
- Directory junctions and symbolic links can appear as matches, but nested reparse-point
  directories are not traversed. This prevents loops during recursive search.
- Inaccessible directories are skipped and counted in the final summary.
- Navigating away, closing Search, pressing `Escape` in the search box, or starting another
  query cancels the current search. Closing Search returns to the folder listing.
- Hovering a result displays its complete path.

## Available commands

Selection-based actions use each result's real path, so opening, renaming, copying, moving,
and deleting can act on files from different subfolders. Creation, Paste into the current
view, and other commands that require a single writable destination folder are disabled
while viewing search results. A folder-row drop can still target a specific result folder;
blank search-result space is not a destination.

Search is unavailable in Trash and This PC: open an ordinary folder or drive first.
Search does not create an index or write metadata into the searched folders.
