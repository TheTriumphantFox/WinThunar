# File-operation behavior

WinThunar follows Thunar's core interaction model while mapping Linux Trash to the Windows Recycle Bin.

## Commands

| Action | Shortcut | Behavior |
| --- | --- | --- |
| Cut | `Ctrl+X` | Places Windows storage items on the clipboard with Move requested. |
| Copy | `Ctrl+C` | Places Windows storage items on the clipboard with Copy requested. |
| Paste | `Ctrl+V` | Copies or moves clipboard items into the current folder. |
| Duplicate | `Ctrl+D` | Creates `name (copy 1)`, incrementing the number when needed. |
| Rename | `F2` | Renames one selected item after validating Windows filename rules. |
| Move to Trash | `Delete` | Sends selected items to the Windows Recycle Bin without a default confirmation. |
| Restore | Trash context menu | Restores selected Recycle Bin items to their original paths with normal conflict handling. |
| Delete Permanently | `Shift+Delete` | Bypasses the Recycle Bin only after explicit confirmation. |
| Undo | `Ctrl+Z` | Reverses the latest safely reversible create, rename, copy, move, or duplicate operation. |
| Redo | `Ctrl+Shift+Z` | Replays the latest undone operation. |

## Conflicts and transfers

- File conflicts offer Replace, Skip, Rename, and Cancel Remaining.
- Replacing two directories merges them and resolves conflicts for their children.
- Copy writes to a uniquely named destination-side staging file and promotes it only after the native Windows copy succeeds.
- Replace stages the complete incoming item, moves the old destination to a private backup, and restores that backup if promotion fails.
- Move uses a native same-volume rename when possible and falls back to Windows `CopyFileEx` plus source deletion across volumes, preserving NTFS metadata and alternate streams.
- Selecting both a folder and its child processes only the folder.
- Copying a folder into itself is rejected.
- Directory links and junctions are rejected until link-preserving behavior is available.

## Queue, history, and drag-and-drop

- Copy and move requests enter a sequential queue, so another request can be queued while one runs.
- The transfer strip shows the active item, byte progress, throughput, estimated time remaining, overall item progress, queued-job count, and a Cancel button.
- Cancel stops the active job at its next cancellation point; already completed child transfers are journaled as well as completed top-level items.
- Closing a window with running or queued transfers asks before cancelling those jobs.
- Undo/Redo keeps the ten most recent safely reversible operations and preserves exact destination names.
- Before Undo or Redo, WinThunar verifies a recursive metadata-and-content snapshot and refuses to delete or move an item that changed; copy Undo uses the Recycle Bin.
- Undo removes a created folder, blank document, template document, or archive only when its recursive metadata-and-content snapshot is unchanged; Redo recreates file content exactly.
- Replace and directory Merge are not entered into Undo history because restoring displaced content requires a backup journal.
- Trash and permanent delete are not entered into Undo history. Trash items have a dedicated Restore command that removes the Recycle Bin metadata record after a successful restore.
- In-progress staged copies and replacements have persistent recovery records. On the next launch, WinThunar removes incomplete incoming data and restores any destination backup or source recovery item left by an interrupted transfer.
- Dropping on a visible folder row targets that folder; dropping on blank list space targets the current folder.
- A same-volume drop moves by default, while a cross-volume drop copies. Hold `Ctrl` to force Copy or `Shift` to force Move.

## Verification

The discoverable xUnit test project creates unique trees beneath the system temporary directory. It covers
name validation, case-only rename, native metadata and alternate-stream preservation, rollback-safe Replace,
partial-operation journaling, state-guarded Undo data, junction-safe atomic archives, null-safe JSON loading,
folder merge, exact-path history replay, self-copy rejection, duplicate naming, bounded Undo/Redo, and the
sequential queue. It validates the cleanup root before recursively removing test data. Moving to the real
Recycle Bin and pointer-driven UI gestures remain excluded from automation.
