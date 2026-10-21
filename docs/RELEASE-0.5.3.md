# WinThunar 0.5.3 release candidate

Prepared 21 October 2026. This is a source release candidate, not a published GitHub Release.
Installed copies continue to receive 0.5.0 until a new version is packaged and published.
This candidate includes the earlier [0.5.2](RELEASE-0.5.2.md) and [0.5.1](RELEASE-0.5.1.md)
source changes; neither preceding candidate was published.

## Changes since 0.5.2

Real-storage verification exposed native Windows API failures when private Undo paths exceeded
260 characters. Alternate-stream enumeration rejected unchanged retained backups as changed,
and Copy Undo could fail while reading parked data. Native copy, stream, identity, access, and
link opens now use normalized extended Windows paths, including UNC form. Regression tests
cover long-path Copy/Move, replacement Undo/Redo, alternate streams, stream-edit rejection,
volume/access checks, and retained-data cleanup.

The new `scripts/Test-FileOperationsRealStorage.ps1` builds the stress harness, requests UAC,
and creates a new 256 MiB NTFS VHD beneath a unique artifacts directory. Only the disk identified
from that exact new image is partitioned and formatted. Volume identity, ownership, mount path,
size limits, and a distinct source volume are checked before filling the disposable target.
Ordinary second-volume stress runs never enable filling. The script detaches and preserves the
image, including failed fixtures and recovery data, and records setup/detachment status.

Artifact directories are excluded from default build inputs before WinUI enumerates XAML.
Retained detached volume mount points therefore do not break the next application build.
Stress reports also retain the local fixture path when fixture construction itself fails.

## Verification and limits

- Normal Debug x64 application build: zero warnings/errors. The final application assembly is
  **0.5.3.0** and contains no stress fault-hook type or checkpoint markers.
- Service suite: **125 passed, one skipped, zero failed** (126 total). The skip requires Windows
  symbolic-link privilege. The two long-path regressions failed before the fix and pass afterward.
- Ordinary stress suite under the account's normal token: **29 passed, five skipped, zero failed**.
- Disposable-VHD suite: **35 passed, two skipped, zero failed**. This includes the 29 baseline
  scenarios, real cross-volume file/merge Move/Undo/Redo over two cycles, and four real disk-full
  cases (Copy/Move, new destination/Replace). Metadata and alternate streams were checked.
- All real disk-full cases returned native Windows error **112**, without injecting an error,
  and passed original-data, recovery, retry, and Undo checks. Allocation failed before copied
  bytes were reported; failure after partial writes remains unverified.
- The disposable NTFS volumes share the system SSD. Separate-physical-device behavior, SMB
  disconnection, and physical removal remain unverified. The VHD suite uses the UAC token and
  does not establish ordinary-user permission handling; normal-token service/stress runs are separate.
- Both test images, including the reproduced-failure image, are detached and preserved. A detached
  manifest was explicitly rejected before fixture creation. See the [stress notes](FILE-OPERATIONS-STRESS.md)
  for report paths, safeguards, and remaining checks.
- Multi-item Undo remains window-local and is not crash-atomic. Ambiguous recovery data and
  journals remain intact. Process-termination tests do not simulate power loss.
- Earlier limits still apply: manual screen-reader/contrast/scaling and Shell workflows,
  notifications on installed/portable builds, general document previews, cloud/device browsing,
  and indexed/content search remain open. See the [roadmap](ROADMAP.md).

## Release preparation

Project version, assembly/file version, package manifest, release-script default, and README
build example are aligned at **0.5.3**. Packaging and publication are separate steps:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Publish-Release.ps1 -Version 0.5.3
```

Preserve all earlier packages in `artifacts/installer` for delta updates. The historical unpublished
0.6.0 preview artifacts are not 0.5.3 packages and must not be uploaded as part of a 0.5.3 release.
No 0.5.3 ZIP/MSI/Velopack package or installed-upgrade result is established by this source commit.
Before publishing, build the requested packages, inspect the upload feed, and verify the installed
upgrade. See [release publishing](RELEASES.md).
