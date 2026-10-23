# MSI maintenance policy

Included in the MSI published with **[0.5.8](RELEASE-0.5.8.md)** on 22 October 2026.
The 0.5.7 and earlier installers retain the repair and settings-removal limitations. This
policy applies to corrected MSIs, not to older MSIs already cached on installed machines.

The **unpublished 0.5.9 candidate** extends this policy with native automatic per-user
migration protection and rollback. See [migration protection](MSI-MIGRATION.md#candidate-automatic-protection)
for its scope, fail-safe behavior and private-candidate verification. The 0.5.8 public MSI
does not contain that protection.

## Corrected packaging

`scripts/Publish-Release.ps1` now runs `Set-MsiMaintenancePolicy.ps1` after adding shortcut
choices and before copying/checksumming the MSI. The postprocessor checks the expected
Velopack 1.2.161 identity, application file version, cleanup action and action sequence.
An unexpected template or a repeated patch fails without committing database changes.

- **Uninstall retention:** the upstream template assigns the display title `WinThunar` to
  `RustAppId`. Its native cleanup consequently removes `%LOCALAPPDATA%\WinThunar`, even
  though the executable lives in `WinThunar.App`. The postprocessor assigns the package ID
  `WinThunar.App` instead. Session, preferences, user plugins and unresolved transfer records
  are intended to remain in their separate user-state directory after uninstall.
- **Repair after updates:** an execute-sequence error action rejects maintenance when the
  installed DLL is newer than the MSI, or the updater's registered version differs from the
  MSI's application version. DLL discovery uses the registered MSI component key path;
  version registration is checked separately for the per-user and all-users scopes. The
  registration check also protects an installation with a missing DLL. The guard runs
  immediately after costing, before file/registry changes and the MSI transaction.
- **Ordinary maintenance:** fresh installation and same-version repair are allowed. Full
  uninstall is allowed after an update. The guard is absent from the UI sequence, so opening
  maintenance does not prevent selecting Remove. Partial feature changes after an update
  are blocked because they can also restore older payload files.

This is a refusal to perform an unsafe older repair, not a repair of the updated payload.
An automatic application update does not replace the original Windows Installer cache.
Existing installations need a corrected MSI installation before they gain this policy;
their old MSI limitations remain. Direct migration can invoke the old uninstall cleanup
and delete user state even when installing the new MSI succeeds. Preserve the complete
state directory before older-MSI maintenance; see the [migration findings](MSI-MIGRATION.md).
The original MSI product registration can still report the version originally installed.

## Verification

Run the host checks in Windows PowerShell. They patch a separate copy, evaluate the guard
using Windows Installer's native condition engine, and execute no installation sequence:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-MsiMaintenancePolicy.ps1 -MsiPath .\artifacts\installer\WinThunar.App-win.msi
```

Both the published 0.5.5 and 0.5.7 inputs passed **11 native condition cases**. Coverage
includes fresh installation, same-version repair, newer files, missing DLLs, both registry
scopes, unrelated installations in the other scope, partial removal and full uninstall.
Repeat patching was rejected without mutation; both original MSI hashes were unchanged.
The patcher also ran successfully under Windows PowerShell 5.1, used by release packaging.
These checks do not establish an installed repair or uninstall outcome.

The separate disposable per-user guest run passed:

| Native check | Result |
| --- | --- |
| Corrected 0.5.5 MSI installation | Exit 0 |
| Same-version `/fomus` and `/fa` | Both exit 0; payload bytes unchanged |
| Native 0.5.5 to 0.5.7 full-package apply | Exit 0; all 524 deployed files match the target package |
| Older-MSI `/fomus` and `/fa` | Both exit 1603 with the repair-guard diagnostic; payload unchanged |
| Older-MSI repair with DLL missing | Exit 1603 using the updated-version registration; no payload mutation; DLL restored from the held fixture |
| Uninstall after update | Exit 0; app directory, both registrations, both shortcuts and enabled Shell/default-folder integration removed |
| Uninstall retention | Five seeded settings/plugin/opaque-recovery files and the test document retain identical hashes |

The guest's native MSI log confirms both searches found the newer DLL and the per-user
`DisplayVersion=0.5.7` before the guard rejected repair. The first uninstall test attempt
stopped before uninstall because its runner did not reliably wait for the GUI executable's
Shell-registration exit code. The corrected runner explicitly waits for that process and
was checked against the source hash before the passing uninstall run. The old runner and
all failed input files remain in the guest.

`Test-MsiMaintenanceGuest.ps1` supplies independent native installed checks, restricted by
origin and expected guest MachineGuids, input hashes and case ownership. Its stages are:

1. Install a corrected base MSI and run same-version `/fomus` and `/fa` repairs.
2. Apply the newer package in the disposable guest, then run `-Stage Repair` to compare all
   package files and reject older `/fomus`, `/fa`, and repair with the DLL temporarily missing.
3. Run `-Stage Uninstall` to enable folder-default Shell integration, seed user settings,
   plugins and opaque unresolved-recovery data, and check retention plus app/registration/
   shortcut/Shell cleanup.
4. Run `-Stage Export` and verify exported archive length and SHA-256 before restoring the VM.

The guest runner checks installer retention of opaque recovery input and retained bytes;
it does not replay recovery journals or test live Undo. Silent MSI/native updater checks do
not replace interactive installer, update UI, UAC or all-users uninstall coverage.

Host evidence is in `artifacts/msi-maintenance-20261022/native-base-final/` and
`native-current-final/`. Guest inputs and observations are under the existing owned
`artifacts/windows-upgrade-vm-20261021-212229-9192ed86/` directory. Direct VirtualBox file
copies reported success but produced zero-byte files; hash checks rejected them before any
installer launch. Those failed inputs were preserved, and verified read-only ISO input
was copied to a separate guest-local directory instead.

The passing evidence archive, `maintenance-evidence-20261022.zip`, was exported with its
guest byte count **657,834** and SHA-256
`f751a6968fff450055dc7a2c2f68feb594b2166e4a8036ab19d667a4812a7ebf`
verified on the host. Its extracted case contains native logs, file-by-file comparisons,
state hashes and results. The guest case ID is `1d3e59f1c8a94e76a4c835cbccf001a3`.
The passing guest is preserved in snapshot `guarded-msi-maintenance-passed-20261022` and
left Saved to free host memory. The clean and earlier failure snapshots remain available.

The subsequent [legacy-MSI migration case](MSI-MIGRATION.md) found successful 0.5.8 payload
and cache replacement but deletion of all five seeded user-state files during old-MSI
removal. Interactive maintenance, automatic migration retention, all-users UAC/uninstall,
actual recovery replay and live Undo preservation remain open.
An independent external backup/restore branch passed migration, both repair modes and
uninstall retention/positive Shell cleanup; that workaround is implemented by the guest
test helper, not the published MSI. See [migration verification](MSI-MIGRATION.md).
The policy regression did not change release assets/feed or the installed host app;
the subsequent [0.5.8 publication](RELEASE-0.5.8.md) was verified separately.
Read-only host inspection now finds 0.5.7.0; its updater log records a 07:43–07:44 local
upgrade before this work, so the previous session's 0.5.3 host baseline is historical.
