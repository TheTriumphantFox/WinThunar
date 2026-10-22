# WinThunar 0.5.8

Release candidate prepared 22 October 2026 for Windows 11 x64. Publication and release-package
verification are pending; 0.5.7 remains the latest published application until that completes.

## Changes since 0.5.7

Newly generated MSIs preserve the separate `%LOCALAPPDATA%\WinThunar` settings, plugin and
recovery directory on uninstall. The installer previously used the display title as its
native cleanup ID and could delete that directory despite using `WinThunar.App` for the app.

The MSI also rejects maintenance before file changes when the installed payload is newer
or its updater registration reports a different version. This prevents an older repair
from mixing old unversioned files with newer application binaries. Both install scopes have
their own registration lookup; a missing app DLL remains protected by the version marker.
Fresh installation, same-version repair and full uninstall remain allowed.

The release pipeline applies this policy after adding shortcut choices. Native condition
checks and a guarded disposable-guest regression runner are included as source tools.
Application behavior otherwise retains 0.5.7's window-session fix and 0.5.6's theme preference.

## Verification and limits

The candidate passed **171 service tests, one skipped, zero failed**, and a normal x64
Release build with zero warnings/errors. The skip requires Windows symbolic-link privilege.
The restricted test runner initially timed out connecting to testhost; the normal-access
retry passed. Test output retained NU1900 because NuGet vulnerability metadata was unavailable.
Evidence is retained in `artifacts/verification-0.5.8/`. Desktop checks were not repeated for
this packaging-only change; the preceding application release's UI coverage is historical.

Before this candidate, corrected copies of the published 0.5.5 and 0.5.7 MSIs each passed
eleven native Windows Installer condition cases under Windows PowerShell 5.1. Repeat patching
was rejected without mutation and original release files retained their hashes.

A disposable per-user Windows guest installed the corrected 0.5.5 MSI and passed standard
and forced same-version repair. The native updater applied the verified 0.5.7 full package;
all **524 deployed files** matched. Older standard, forced and missing-DLL repair attempts
were rejected with no payload mutation. Uninstall removed the app, both registrations,
both shortcuts and enabled Shell/default-folder integration while preserving five seeded
settings/plugin/opaque-recovery files and a generated document byte-for-byte. Exported
evidence matched its guest length and SHA-256. See [maintenance verification](MSI-MAINTENANCE.md).

These are native silent MSI/updater tests of the policy, not an installed 0.5.8 upgrade.
Opaque recovery-file retention does not establish journal replay or live Undo preservation.
Interactive maintenance, all-users UAC/uninstall and the broader released-app matrix remain open.

**An application update does not replace an older cached MSI.** Existing installations made
with 0.5.7 or earlier MSIs retain their original repair and uninstall behavior. Migration to
a corrected MSI is unverified; preserve settings and unresolved recovery data before using
an older installer's maintenance/uninstall path. Original MSI product metadata can continue
to report its originally installed version.

Manual accessibility/Shell/cloud review, positive PDF/Office indexed-content fixtures,
notification registration and real remote-storage/physical-device interruption coverage
remain open. Transfer Undo is window-local and is not crash-atomic across multiple items.

## Packaging

Project, assembly/file, package manifest and release-script versions are aligned at **0.5.8**
(four-part versions **0.5.8.0**). Build from the merged main checkout:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Publish-Release.ps1 -Version 0.5.8
```

Keep all historical packages in `artifacts/installer`, retained recovery/stress data and the
guest snapshots/evidence. Release verification does not launch or change the host installation.
