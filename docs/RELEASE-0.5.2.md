# WinThunar 0.5.2 release candidate

Prepared 21 October 2026. This is a local release candidate, not a published GitHub Release.
Installed copies continue to receive 0.5.0 until 0.5.2 is published.

## Changes since 0.5.1

The storage stress harness exercises native-copy failures, cancellation, sharing locks, and worker
termination. It exposed an interrupted-Undo recovery bug: overlapping records could discard staged
incoming data and leave its preserved copy at an untracked private path. Recovery now verifies and
finishes supported staged history moves, retains ambiguous data and journals for attention, and
uses an exclusive process lease to prevent concurrent restart recovery.

This candidate includes the earlier [0.5.1 source changes](RELEASE-0.5.1.md).

## Resulting behavior

Replacing files or merging folders can now be undone from the window's history. Old destination
content is retained, completed merge children are recorded even after cancellation, and exact incoming
data is retained for Redo. Before changing anything, replay checks destination data, move sources,
backups, and Redo data, including NTFS alternate streams. An ordinary replay failure attempts rollback.
Closing the window or expiring its ten-action history releases its retained data and disk space.

Search combines names, quoted phrases, wildcard filenames, file types, modified dates, and sizes.
For example: `name:"annual report" type:pdf modified:last7days size:>1MB`. Invalid filters explain
their syntax in the status bar. Traversal streams entries rather than sorting each whole folder first.

The interface follows Windows light/dark settings. The command panel, preview path, and drop highlight
use system colors. Directory enumeration checks cancellation between entries; selection totals use
cached listing metadata. Closing/updating waits for an active Undo/Redo instead of releasing its data.

This candidate also includes the previously local link-preserving transfers, keyboard/accessibility
additions, Git badges, jump-list tasks, notification implementation, and isolated desktop smoke harness.

## Verification and limits

- Final service run: **123 passed, one skipped, zero failed** (124 total). The test project
  built with zero warnings/errors. The skipped test needs Windows symbolic-link privilege.
- Generated-fixture service tests cover nested merge/replacement Undo/Redo, partial cancellation,
  changed backups and alternate streams, replay rollback/retry, recovery between replacement renames,
  history expiry, search parsing, and a 5,100-file search with batching, limits, and cancellation.
- Recovery records identify their owning process and its start time. Opening another window
  skips live owners instead of expiring another window's active transfer or retained Undo data.
- The [storage stress harness](FILE-OPERATIONS-STRESS.md) passes 29 injected-error, sharing-lock,
  forced-fallback and worker-termination scenarios, with five physical/real-volume checks skipped.
  It found and fixed overlapping history-record recovery and added an exclusive restart-recovery lease.
- Desktop smoke checks cover three layouts, light/dark rendering with the command panel, accessible
  names and selected keyboard transitions, combined search filters, and queue-to-window Undo/Redo.
- Symbolic-link creation can be skipped when the Windows account lacks Developer Mode/privilege.
- Notification registration remains unavailable on this test machine with a resource-module loading
  error. Notification delivery/activation is not verified; in-app status and error dialogs remain available.
- Screen readers, Windows contrast themes, display scaling, real elevation, real Recycle Bin/pointer
  workflows, disconnected shares, removable-drive removal, disk-full conditions, and cross-volume
  history on separate physical volumes still require manual verification. No performance benchmark
  or universal data-safety claim follows from these tests.
- Search remains filesystem-only and capped at 5,000 results. Shell namespace/device browsing,
  cloud status columns, general document previews, indexed/content search, and interception of
  Win+E / `SHOpenFolderAndSelectItems` remain outside this candidate.
- Multi-item history replay is not crash-atomic. Restart recovery handles staged items and displaced
  originals, while a merge can remain partially undone after abrupt termination. Undo is window-local.

## Release preparation

Version, assembly/file version, package manifest, release-script default, and README build example
are aligned at 0.5.2. Build through `scripts/Publish-Release.ps1 -Version 0.5.2`; preserve all previous
packages in `artifacts/installer` for delta updates. Review this candidate before publishing and retain
the distinction between local preparation and a published application throughout the documentation.

The earlier local preview was built and package-verified as **0.6.0**, before the maintainer
selected 0.5.1 for the preceding source commit. Its ZIP/MSI hashes, MSI properties, and packaged application DLL
were checked, and it excluded the desktop smoke harness. Its 0.5.0 full package was preserved,
and its delta was 780,699 bytes. Those historical reports remain in `artifacts/verification-0.6.0/`;
they do not establish that 0.5.2 packages have been built or installed.

Before publishing 0.5.2, build new packages with the requested version and ensure the upload feed
contains the intended release. The unpublished 0.6.0 preview packages must not be uploaded as part
of a 0.5.2 release. Preserve earlier published installer packages for delta updates. Publication
and an installed-upgrade check remain separate release steps.
