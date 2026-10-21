# File-operation behavior

WinThunar follows Thunar's core interaction model while mapping Trash to the Windows Recycle Bin.
This page describes release 0.5.3 and identifies older 0.5.0 behavior where it differs.

## Commands

| Action | Shortcut | Behavior |
| --- | --- | --- |
| Cut | `Ctrl+X` | Places Windows storage items on the clipboard with Move requested. |
| Copy | `Ctrl+C` | Places Windows storage items on the clipboard with Copy requested. |
| Paste | `Ctrl+V` | Copies or moves clipboard items into the current folder. |
| Duplicate | `Ctrl+D` | Creates `name (copy 1)`, incrementing the number when needed. |
| Rename | `F2` | Renames one item, or opens the previewed Bulk Rename dialog for multiple selected items. |
| Move to Trash | `Delete` | Sends selected items to the Windows Recycle Bin; confirmation is off by default and can be enabled in Preferences. |
| Restore | Trash context menu | Restores selected Recycle Bin items to their original paths with normal conflict handling. |
| Delete Permanently | `Shift+Delete` | Bypasses the Recycle Bin only after explicit confirmation. |
| Undo | `Ctrl+Z` | Reverses the latest recorded create, rename/bulk rename, ordinary copy/move, duplicate, or supported link operation. |
| Redo | `Ctrl+Shift+Z` | Replays the latest undone operation. |

## Conflicts and transfers

- File conflicts offer Replace, Skip, Rename, and Cancel Remaining.
- Replacing two directories merges them and resolves conflicts for their children.
- Copy writes to a uniquely named destination-side staging file and promotes it only after the native Windows copy succeeds.
- Replace stages the complete incoming item, moves the old destination to a private backup, and restores that backup if promotion fails.
- Move uses a native same-volume rename when possible and falls back to Windows `CopyFileEx` plus source deletion across volumes, preserving NTFS metadata and alternate streams.
- Selecting both a folder and its child processes only the folder.
- Copying a folder into itself is rejected.

## Filesystem links: 0.5.0 and 0.5.3 behavior

**Release 0.5.0** does not natively copy directory links or junctions. It can offer to let
the Windows copy engine finish a transfer after confirmation. That fallback can follow
the link's contents as Explorer does; it is not a link-preserving copy. Do not infer native
link preservation in the older 0.5.0 binary from the changes below.

Release **0.5.3** adds these behaviors:

- Supported symbolic links and NTFS junctions are copied as links, including dangling and
  relative symbolic links; the transfer does not traverse their targets. Same-volume moves
  rename the link, and cross-volume moves recreate it before removing the source.
- Replacing a directory link replaces the link itself instead of merging into its target.
  Unsupported reparse tags can be offered to Windows after confirmation.
- Link transfer undo verifies reparse data: changes to target contents do not block undo,
  but retargeting the link does. Link deletion and recovery cleanup leave the target intact.
- Creating/recreating symbolic links requires Windows Developer Mode or the symbolic-link
  privilege. Failure leaves the source and any replaced destination intact.

The separate ZIP archive service still rejects directory reparse points during archive
creation; link-preserving file transfers do not add link support to ZIP archives.

## Queue, history, and drag-and-drop

- Copy and move requests enter a sequential queue, so another request can be queued while one runs.
- The transfer strip shows the active item, byte progress, throughput, estimated time remaining, overall item progress, queued-job count, and a Cancel button.
- Cancel stops the active job at its next cancellation point; already completed child transfers are journaled as well as completed top-level items.
- Closing a window with running or queued transfers asks before cancelling those jobs.
- Undo/Redo keeps up to ten operations in memory for the current window and preserves exact
  destination names. Closing that window loses its history; it is not stored in session JSON.
- Transfer Undo/Redo verifies recorded destination/source snapshots before replay, and create
  Undo checks the created item before removal. In release 0.5.0, Copy/link Undo uses the Recycle Bin. Single
  and bulk rename history use the rename validation/plan checks; not every Undo command has
  the recursive content-snapshot guard.
- Undo removes a created folder, blank document, template document, or archive only when its recursive metadata-and-content snapshot is unchanged; Redo recreates file content exactly.
- In release 0.5.0, a transfer batch containing Replace or directory Merge is not entered into Undo history.
  Release **0.5.3** retains displaced items and completed merge children for Undo,
  including completed work in a cancelled transfer. Undo restores replaced content and keeps
  unrelated merge contents. Redo uses the exact retained incoming data, so later edits to a copy's
  original source do not change Redo. Destination, move-source, retained-backup, and Redo snapshots
  are checked before any replay; content guards include NTFS alternate streams.
- In 0.5.3, Undo of Copy/Link parks the incoming item in a private `.winthunar-undo-*.tmp`
  directory alongside the top-level destination, outside the transferred folder. Replacement backups
  use the same kind of directory. These consume disk space until that history entry expires,
  a new operation discards Redo, or its window closes. They are not persistent history or Recycle Bin
  items. Startup recovery expires abandoned retained data. Do not edit these private directories.
- Replay failures attempt inverse actions in reverse order. Recovery records protect displaced
  originals across interrupted replacement renames. A multi-item replay is not a filesystem-wide
  atomic transaction; abrupt process termination can leave part of a merge undone. Restart recovery
  protects individual staged items but does not resume the window's Undo history.
- Trash and permanent delete are not entered into Undo history. Trash items have a dedicated Restore command that removes the Recycle Bin metadata record after a successful restore.
- In-progress staged copies and replacements have persistent recovery records. On the next launch, WinThunar removes incomplete incoming data and restores any destination backup or source recovery item left by an interrupted transfer.
- Dropping on a visible folder row targets that folder; blank list space targets an ordinary
  writable current folder. Blank search-result space is not a destination; a Trash drop can
  move items to the Recycle Bin, and dropping Trash items into a folder restores them there.
- A same-volume drop moves by default, while a cross-volume drop copies. Hold `Ctrl` to force Copy or `Shift` to force Move.

## Verification

The discoverable xUnit test project creates unique trees beneath the system temporary directory. It covers
name validation, case-only rename, native metadata and alternate-stream preservation, rollback-safe Replace,
partial-operation journaling, state-guarded Undo data, junction-safe atomic archives, null-safe JSON loading,
folder merge, exact-path history replay, self-copy rejection, duplicate naming, bounded Undo/Redo, and the
sequential queue. It validates the cleanup root before recursively removing test data. The
service tests use synthetic Recycle Bin records for restoration; they do not require moving
personal files into the real Recycle Bin. Pointer-driven drag/drop and real Windows elevation/Recycle Bin interactions still
need manual checks. The relative-symbolic-link test reports a skip when Windows denies link
creation. See the [accessibility status](ACCESSIBILITY.md) for the separate desktop smoke checks.

The 0.5.3 source includes a separate [stress harness](FILE-OPERATIONS-STRESS.md) for injected native
storage errors, real sharing locks, copy/delete fallback, and forced termination at transfer/recovery/
history checkpoints. It reports real-volume and physical-storage coverage separately. Recovery now
leases its journal to one process and protects staged incoming history data when overlapping records
restore their paths in either order. A mismatched stage or an ambiguous older record is retained for
attention instead of being discarded.

## Unreleased recovery improvements

The 0.5.5 source candidate adds a persistent warning for recovery failures and
**Help > Interrupted Transfers...**. The dialog shows selectable details, including the source,
destination, retained-copy paths, and journal path. Reconnect unavailable storage or close apps
locking these files, then select **Retry recovery**. Active transfers and window Undo data stay
protected; a failed recovery claimed by the current process can be retried safely.

Recovery runs off the UI thread. It verifies containing folders before treating private staging
data as absent, so a disconnected share cannot cause silent journal deletion. New replacement and
history-move journals record the complete staged content before destructive renames. A destination
that changed, or an older ambiguous journal without proof of promotion, leaves all displaced copies
and the journal intact for review. There is no force-discard action in this dialog.

Real loopback-SMB tests reproduced and fixed missing staging-journal tracking, including loss of
the source share after a replacement Move commits. Real VHD tests now cover error 112 after 8 MiB
of main-stream progress. These additions do not make multi-item history crash-atomic or persistent.
See the [reliability milestone](RELIABILITY-MILESTONE.md).
