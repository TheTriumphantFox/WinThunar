# Search behavior

`Ctrl+F` or the Search toolbar button searches the active pane's current folder and all ordinary
subfolders. Typing pauses for 350 milliseconds before a query starts; another keystroke cancels the
older query so stale results cannot overwrite the current search.

## Safety and result behavior

- Matches are case-insensitive and compare the literal file or folder name.
- Results arrive in batches while traversal continues and remain usable with normal file commands.
- The current `Show Hidden Files` setting controls hidden, system, and dot-prefixed paths.
- Directory junctions and symbolic links can appear as matches but are not traversed, preventing loops.
- Inaccessible folders are skipped and counted in the final status message.
- Results stop at 5,000 entries and clearly report when the limit is reached.
- Changing folders, closing Search, pressing `Escape`, or starting another query cancels active work.
- Hovering a result displays its complete path.

Search does not create an index or write metadata into searched folders.
