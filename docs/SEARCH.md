# Search behavior

The navigation and result rules below apply to release 0.5.3.
Query filters and streaming traversal are included in **0.5.3**; release 0.5.0 matches
one literal filename substring and does not understand filters or wildcards.

`Ctrl+F` or the Search toolbar button searches the active pane's current folder. Subfolders
are included by default. To search only the current folder, open **Edit > Preferences**,
clear **Include subfolders in search**, and select **Apply**.

Typing restarts a 350-millisecond delay before a query starts. `Enter` starts the query
immediately. Starting the next query cancels the previous traversal, and generation checks
keep cancelled searches from publishing stale results. An empty or whitespace-only query
returns to the ordinary folder listing.

## Matching, limits, and cancellation

- In 0.5.3, words are case-insensitive filename substrings combined with AND.
  Quote a phrase to match consecutive words. `*` and `?` match an entire filename.
  This is not regular-expression or file-content search.
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

## Query filters in 0.5.3

| Query | Meaning |
| --- | --- |
| `report type:pdf` | PDF files with report in their names. |
| `name:"annual report"` | Names containing the consecutive phrase annual report. |
| `*.txt` | Text filenames; `?` matches one character. |
| `type:folder` / `type:file` | Only directories / only files. |
| `ext:jpg` / `type:.jpg` | A filename extension, case-insensitive; no content inspection. |
| `modified:today` / `modified:yesterday` | Local calendar dates. |
| `modified:last7days` | Today and the preceding six calendar days. |
| `modified:>=2026-10-01` | On or after an ISO date; `<`, `<=`, `>`, and `=` also work. |
| `modified:2026-10-01..2026-10-21` | An inclusive date range. `date:` is an alias. |
| `size:>10MB` / `size:1KB..5MB` | File size comparisons or an inclusive range. Directories are excluded. |

Size units are bytes or binary KB/MB/GB/TB (1 KB = 1,024 bytes); decimals use a period.
All words and filters must match. Invalid filters produce an inline status explanation.
Results retain filesystem enumeration order and appear in batches without sorting a whole
directory first. Search still traverses accessible filesystem folders and caps output at 5,000;
Windows Search index integration, content search, and This PC search remain future work.
