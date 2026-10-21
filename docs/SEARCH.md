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

The following unreleased sections supersede those 0.5.3 limits in the current source candidate.

## Unreleased text-content and This PC search

The 0.5.5 source candidate adds `content:word` and `content:"a phrase"`. Content terms are literal,
case-insensitive, combined with AND, and can be combined with the existing filename/metadata
filters. For example, `type:txt content:"annual report" content:approved` searches inside text,
while `report type:txt` continues to inspect names/metadata without opening contents.

Content search reads supported plain-text, Markdown, CSV/TSV, JSON/XML, configuration, and common
source-code/script extensions. It supports UTF-8 and BOM-marked Unicode text. Reads are streamed
and cancellable between blocks, with a **16 MiB per-file limit**, up to 16 content terms of at most
1,024 characters, and the existing **5,000-result limit**. Files growing beyond the read bound,
unsupported extensions, binary/NUL data, invalid UTF-8, locked/unavailable files, and online-only
or reparse-point files are skipped. The summary reports skipped content files. This is not
PDF/Office extraction or Windows Search indexing; byte encodings without a supported Unicode
representation may be skipped. No file contents are written or indexed.

From This PC, search uses the filesystem drive roots in the current drive listing. Repeated
queries retain that scope, overlapping roots are traversed once, and all roots share one result
limit. Unavailable roots count as inaccessible folders. Results are ordinary filesystem items;
blank result space remains an invalid Paste/drop destination. Closing search restores the drive
listing. Trash remains outside search. Cancelling cannot force a blocked storage provider to
return from an operating-system read immediately.

Run the generated local performance baseline with:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-FileSearchPerformance.ps1
```

It creates 10,000 small generated text files in a new artifacts directory, runs five filename and
five content searches, records first-batch/total times and allocation diagnostics, and verifies
cancellation. Successful fixtures are deleted only after ownership/path checks; reports and failed
fixtures remain. This service workload does not establish UI rendering speed, remote-provider
latency, or long-session memory stability. See the [milestone](RELIABILITY-MILESTONE.md).

## Unreleased Windows index search

Add **`index:true`** to explicitly query the local Windows Search catalog. Examples:

| Query | Meaning |
| --- | --- |
| `index:true report type:pdf` | Indexed PDFs whose current filename contains report. |
| `index:true content:"annual report" type:pdf` | PDFs with that phrase in their indexed document contents, when Windows has a suitable content filter. |
| `index:true content:approved type:docx` | Word documents with approved in their indexed contents. Office installation/filter availability and indexing configuration control coverage. |
| `index:false report` | Ordinary filesystem search; this is also the default when index: is omitted. |

The current folder, This PC drive scopes, and **Include subfolders in search** still apply.
Filename/wildcard predicates narrow the native query. Existing type/size/date filters are checked
against current filesystem metadata. The reader validates returned paths against the requested
scope, excludes descendants reached through reparse-point folders, respects hidden ancestor
folders, deduplicates paths, and skips missing/inaccessible entries. It requests read-only access
and never changes Windows index settings or opens document contents to verify indexed terms.

**Index results can be missing or stale.** Only indexed locations and formats are available.
Unlike filesystem `content:` substring matching, indexed content uses Windows word/phrase
matching and installed document filters. A PDF/Office match is not a fresh extraction from the
current file. Online-only documents may appear from cached index metadata without downloading
them. Search status identifies the Windows index and its coverage limitation. Remove `index:true`
to return to filesystem traversal; WinThunar does not silently switch engines.

The output limit remains 5,000 results. At most 50,000 native candidates are checked; a separate
warning asks for a narrower query if that bound is reached before the result limit. Queries run
off the UI thread, request a five-second connection timeout and a ten-second command timeout,
and observe cancellation between native operations and candidate checks. Provider/metadata calls
can delay cancellation; timeout properties are not a guarantee against a stalled storage provider.
Provider failures report an actionable explanation. Owned COM rowsets/connections close when
enumeration completes, fails, reaches a limit, or is cancelled.

Regression tests cover SQL quoting/wildcards, scope boundaries, hidden ancestors, nested roots,
stale entries, current metadata, duplicate paths, result/candidate limits, and cancellation/disposal.
The native desktop check queries generated fixtures without changing index configuration and
separately probes at most one existing indexed file's metadata without logging its path/content.
No positive generated PDF/Office content-indexing result has been established yet.
