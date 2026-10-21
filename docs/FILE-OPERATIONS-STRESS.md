# File-operation stress harness

This harness tests WinThunar's transfer, history, and recovery services against generated data.
It is separate from the xUnit service tests and the WinUI desktop smoke build.

Run from the checkout on Windows with the .NET 10 SDK:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-FileOperationsStress.ps1
```

The script builds `tests/WinThunar.FileOps.Stress` and writes `report.json` beneath a new
`artifacts/fileops-stress-*` directory. Passing cases clean their owned fixtures; failed cases retain
them for inspection. Use `-KeepFixtures` to keep passing fixtures too, or `-OutputDirectory` to choose
a new output directory. The harness rejects an existing run directory.

## What it exercises

| Kind | Cases | Evidence |
| --- | ---: | --- |
| Native error injection | 12 | Windows errors 112 (disk full), 64 (network name deleted), and 1167 (device unavailable), after progress in a real native copy; Copy/Move, new item/Replace. |
| Cancellation | 2 | Before copying and after native progress; originals preserved and staging cleaned. |
| Cleanup/recovery interruption | 1 | A fixture staging remnant is held by a real Windows sharing lock; a device error is then injected into recovery. Journals survive both failures and cleanup succeeds on explicit retry. |
| Forced copy/delete history | 2 | File replacement and nested merge, two Undo/Redo cycles, with the same-volume rename shortcut disabled only in the test build. |
| Worker process termination | 12 | Kill during copying, replacement renames, nested merge, recovery itself, and Undo. Recover twice, check live-owner protection, and force both orders of overlapping transfer/history records. |
| Real cross-volume history | Optional | File and merge Move/Undo/Redo on a supplied distinct writable volume. Reported separately from forced fallback. |

The local fixtures contain an 8 MiB deterministic incoming file and an NTFS alternate stream.
Assertions compare metadata and content/stream hashes, preserve unrelated merge items, verify journal
retention/expiry, and reject leftover private staging files after successful recovery. Copy errors
are injected from the progress callback of `CopyFileEx`; native callback exceptions are caught before
returning to Windows. These fault points are compiled only with `FILEOPS_FAULT_TESTS` into the stress
executable. The installed application has no fault-hook type, phase markers, environment switch, or
command-line switch that enables them.

For termination tests, the worker flushes a checkpoint marker and waits. The parent verifies that
ordinary recovery leaves live transfers alone, terminates only the `Process` object it started, waits
for its exit, then recovers the fixture's private journal. Worker startup/checkpoint waits are bounded.
This tests process termination; it does not simulate power loss or storage write-cache loss.

## Real second-volume tests

Supply an existing writable directory on a different NTFS volume:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-FileOperationsStress.ps1 -SecondVolumeRoot D:\Scratch
```

Only newly created `WinThunar-Stress-<run-id>-<scenario>` directories beneath that directory are used.
Ownership markers and path containment are checked before cleanup. The tests verify distinct volume
identities; a second folder or drive-letter alias for the same volume cannot count as cross-volume
coverage. The default run does not choose a secondary drive automatically.

For a non-NTFS destination, `-BasicSecondVolume` runs two additional tests with no alternate streams
and timestamps representable at FAT precision. These verify basic file data, timestamps, directory
structure, replacement and merge Undo/Redo. They leave full NTFS stream/metadata scenarios skipped:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-FileOperationsStress.ps1 -SecondVolumeRoot E:\Scratch -BasicSecondVolume
```

An unavailable or unwritable supplied directory is a setup failure, not a passing storage test.
These cases do not validate copying existing NTFS alternate streams to a filesystem that cannot
represent them, or preservation of arbitrary NTFS timestamp precision on FAT.

## Findings and verification on 21 October 2026

The default run completed with **29 passed, zero failed, five skipped**. The service suite completed
with **123 passed, one skipped, zero failed** (the skip requires symbolic-link privilege). The normal
WinUI build passed without warnings/errors, and its assembly was checked for the absence of the
fault-hook type and checkpoint markers.

The harness reproduced and fixed an interrupted-Undo problem: when recovery restored an old
destination first, a later history record could discard the staged incoming copy and leave its other
copy at an untracked private path. New history records identify their complete staged move. Recovery
compares that copy with the preserved source, then finishes the move into its still-free destination.
If the copies differ or an older record does not establish this intent, all copies and the journal
remain available with an error for attention. Regression tests cover both outcomes.

Recovery also takes an exclusive process lease for its journal and atomically records the recovery
owner before changing files. A second process leaves an active recovery alone; killing that owner
releases the lease so the next process can resume.

No writable second NTFS volume was available for this run. An attempted basic test on `G:\` failed
during fixture-directory creation, before WinThunar's transfer service ran; it does not establish a
cross-volume regression or a successful cross-volume transfer. Actual distinct-volume coverage
therefore remains unverified on this machine.

## Remaining checks

The JSON report explicitly skips physical disk-full, SMB disconnection, and device-removal tests.
Injected Windows errors exercise failure handling with the fixture filesystem still accessible.
They cannot establish how every filesystem/provider behaves when the underlying storage disappears.
Use disposable virtual disks, a dedicated test share, and expendable media for those manual checks.
The harness never fills an existing volume, disconnects a share, ejects a device, or formats storage.

Multi-item history remains window-local and is not crash-atomic. A process crash can leave part of
a merge undone; successful recovery of individual records does not resume the window's history.
