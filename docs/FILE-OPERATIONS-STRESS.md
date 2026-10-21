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

## Disposable NTFS virtual disk and real disk-full checks

When no expendable second volume is available, run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-FileOperationsRealStorage.ps1
```

The script builds the harness before requesting UAC. The elevated continuation creates a **new,
256 MiB dynamically expanding VHD** in a new `artifacts/fileops-realstorage-*` directory. It obtains
the disk from that exact image with `Get-DiskImage | Get-Disk`, rejects boot/system disks, requires
the expected virtual bus and size and a blank partition table, and formats only the new partition.
It mounts the volume in that run's `volume` directory without assigning a drive letter. The host
must be NTFS with at least 2 GiB available. Existing images and run directories are never reused.

The harness runs its baseline checks plus real NTFS cross-volume file/merge Move/Undo/Redo and
four real disk-full cases (Copy/Move, new item/Replace). A separate provisioning manifest, unique
volume label, ownership token, mount identity, distinct source volume, and 64–512 MiB volume-size
bound are checked before allocating filler. Ordinary `-SecondVolumeRoot` never enables filling.
Filler is real non-sparse data inside each owned fixture; only that volume's free space is reduced
below the incoming file size. The checks observe Windows error 112/39 without injecting it, verify
the original source/destination including alternate streams, release their filler, and verify
recovery, retry, and Undo. Successful history scenarios check two Undo/Redo cycles.

The script detaches its exact image in `finally` and **preserves the VHD even on success**. Review
`provisioning.json` for setup/detach status, `provisioning.log`/`diskpart.log` for setup diagnostics,
and `stress/report.json` for transfer evidence. If detachment fails, the script fails too. Never
recursively delete a `volume` directory that might still be mounted. Failed fixtures/journals stay
in the image and the host-side `stress/fixtures` directory. To inspect a retained image, mount
that exact `disposable.vhd` read-only using Windows Disk Management; preserve its host journals.

These tests establish behavior on separate NTFS **volumes backed by the same physical SSD**.
They do not establish separate-device behavior, power-loss safety, SMB interruption, physical
removal, or non-elevated permission handling. The virtual-disk harness runs with the UAC token;
the ordinary stress/service checks should also be run with the account's normal token.

## Findings and verification on 21 October 2026

The default run completed with **29 passed, zero failed, five skipped**. The service suite completed
with **125 passed, one skipped, zero failed** after the long-path fix (the skip requires symbolic-link
privilege). The normal WinUI build passed without warnings/errors, and its assembly was checked for the absence of the
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

No existing writable second NTFS volume was available for the original run. An attempted basic test
on `G:\` failed during fixture-directory creation, before WinThunar's transfer service ran; it does not establish a
cross-volume regression or a successful cross-volume transfer.

The later disposable-VHD run, `artifacts/fileops-realstorage-20261021-090753-8d4c2065/stress/report.json`,
completed with **35 passed, zero failed, two skipped**. Both real cross-volume history scenarios
passed on distinct NTFS volumes, preserving metadata and alternate streams over two Undo/Redo cycles.
Four actual disk-full cases returned native Windows error **112** and passed original-content,
recovery, retry, and Undo checks. Windows rejected allocation before reporting any copied bytes;
these cases do not establish failure after partial writes. The VHD was detached successfully and
preserved. SMB disconnection and physical device removal remain skipped. Both volumes are on the
same SSD, so separate-physical-device behavior remains unverified.

The first transfer run on a disposable VHD exposed long-path failures: private Undo siblings exceeded
260 characters, native alternate-stream enumeration rejected valid retained data, and Copy Undo
could leave recoverable parked data. `FindFirstStreamW`, `CopyFileExW`, and native identity/access/link
opens now use normalized extended Windows paths, including UNC form. Two service regressions failed
before the fix and pass afterward; they check long-path Copy/Move, alternate streams, replacement
Undo/Redo, edit rejection, and cleanup. The failing VHD, host journals, and its report remain intact in
`artifacts/fileops-realstorage-20261021-090515-56cccee9/`.

Repository artifact directories are excluded from default build items before WinUI enumerates XAML.
Otherwise retained disconnected volume mount points can turn its recursive XAML glob into a literal
wildcard and fail the next application build. No fixture or mount point needs deleting to build.

## Remaining checks

The default JSON report explicitly skips real disk-full, SMB disconnection, and device-removal tests.
Injected Windows errors exercise failure handling with the fixture filesystem still accessible.
They cannot establish how every filesystem/provider behaves when the underlying storage disappears.
Use the opt-in virtual-disk script for bounded real disk-full checks, a dedicated test share for SMB,
and expendable media for physical removal. The ordinary harness never fills a supplied second
volume, disconnects a share, ejects a device, or formats storage. Only the separate opt-in script
formats a newly created image and enables filling its disposable volume.

Multi-item history remains window-local and is not crash-atomic. A process crash can leave part of
a merge undone; successful recovery of individual records does not resume the window's history.
