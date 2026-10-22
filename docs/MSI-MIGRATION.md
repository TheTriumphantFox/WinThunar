# Migrating an older MSI installation

## Direct migration loses user state

Checked 22 October 2026 in the existing disposable Windows 11 x64 guest, using an
unelevated account with UAC enabled and external networking/shared folders disabled.

The original published **0.5.5 MSI** was installed per-user with both shortcuts and
default-folder Shell integration enabled. The native updater then applied the published
**0.5.7 full package**, without launching the application afterward. All **524 deployed
files** matched the package and five seeded settings/plugin/opaque-recovery files retained
their hashes. This is a silent native-updater fixture, not an interactive update-UI test.

Installing the published **0.5.8 MSI** over that installation produced:

| Check | Direct migration result |
| --- | --- |
| MSI exit code | 0 |
| Deployed 0.5.8 payload | All 524 package files match |
| Original 0.5.5 MSI product entry | Removed |
| New 0.5.8 MSI product entry | Present |
| Cached MSI | Exact published 0.5.8 digest; corrected cleanup ID and repair guard present |
| Seeded `session.json` and `updates.json` | Both deleted |
| Seeded personal plugin | Deleted |
| Seeded opaque recovery record and retained-original file | Both deleted |
| Overall safe migration | **Failed** |

The verbose MSI log shows `RemoveExistingProducts` invoking the old MSI's `RustCleanup`
action with `RustAppId=WinThunar` and `UPGRADINGPRODUCTCODE` identifying the new product.
The corrected new MSI cannot change that already cached old cleanup action. A successful
installer exit and consistent new binaries therefore do not establish state retention.

**Preserve the complete `%LOCALAPPDATA%\WinThunar` directory outside both `WinThunar` and
`WinThunar.App` before installing a corrected MSI over an older installation or invoking an
older installer's uninstall.** Close all WinThunar windows and wait for its updater to finish
first. Keep the backup and verify its contents before maintenance. Restore the preserved
state before launching the application, especially if unresolved transfer data is present.
Backing up only session preferences omits personal plugins and recovery data.

This finding covers one original per-user 0.5.5 MSI updated to 0.5.7 and then migrated to
the published 0.5.8 MSI. Other old versions, custom paths, all-users scope, interactive UI,
UAC, recovery replay and live Undo require independent verification.

## External backup/restore verification

The pre-migration checkpoint was restored for an independent branch using the same public
assets. The helper copied the complete fixture state to a separate case-owned directory
and verified every file hash before starting the MSI. The new MSI again removed native
state; that outcome was recorded before the helper restored the backup.

After restoration, all five original state files matched their seeded hashes, all 524
deployed files matched 0.5.8, the old product entry was removed, and the cached corrected MSI
matched the public asset digest. Both standard `/fomus` and forced `/fa` repair returned 0;
all 524 payload files and original state hashes matched after each repair.

Before uninstall, the helper explicitly enabled all three Shell verbs and default-folder
handling again and verified that this setup left retained state unchanged. Silent uninstall
returned 0, removed the application directory, both uninstall registrations, both shortcuts,
the three owned Shell verbs and default-folder handling, and preserved all five original
state files plus the generated document. The independent backup also remains in the case.

These results validate the explicit external backup/restore procedure for this fixture.
They do not make direct migration safe or add automatic state protection to the MSI.

## Repeatable guest tooling

`scripts/Test-MsiMigrationGuest.ps1` rejects the originating host and any machine other than
the declared disposable guest before writes or launches. Its `migration-input.json` records
origin/guest MachineGuids, a unique case ID, both MSI product codes, expected versions and
the names, sizes and SHA-256 digests of four assets: `BaseMsi`, `UpdatedPackage`, `TargetMsi`
and `TargetPackage`. Inputs must match the preserved public release metadata.

Use a new clean guest case for installation, then preserve a checkpoint after the update:

```powershell
.\Test-MsiMigrationGuest.ps1 -InputDirectory C:\LabInput -Stage Install
.\Test-MsiMigrationGuest.ps1 -InputDirectory C:\LabInput -Stage Update
# Preserve a VM checkpoint here before each independent migration scenario.
.\Test-MsiMigrationGuest.ps1 -InputDirectory C:\LabInput -Stage Migrate
.\Test-MsiMigrationGuest.ps1 -InputDirectory C:\LabInput -Stage Export
```

`VerifyUpdate` can independently recheck a completed native update without applying it
again. Stages refuse to overwrite prior evidence. Restore the pre-migration checkpoint
before testing the explicit external backup/restore workaround:

```powershell
.\Test-MsiMigrationGuest.ps1 -InputDirectory C:\LabInput -Stage Migrate -PreserveUserState
.\Test-MsiMigrationGuest.ps1 -InputDirectory C:\LabInput -Stage Repair
.\Test-MsiMigrationGuest.ps1 -InputDirectory C:\LabInput -Stage Uninstall
.\Test-MsiMigrationGuest.ps1 -InputDirectory C:\LabInput -Stage Export
```

`PreserveUserState` is a **test-helper workaround**, not a feature of the published MSI.
It retains an independently hashed backup outside the install/state directories, records
the native retention outcome before restoration, and restores the backup before further
checks. The helper rejects reparse points in the backup fixture. It does not replay opaque
recovery records or launch the app after they are seeded.

## Preserved direct-migration evidence

Host evidence is under
`artifacts/windows-upgrade-vm-20261021-212229-9192ed86/migration-20261022/`.
The direct-failure archive is **442,180 bytes**, SHA-256
`303feb6c4fd02a670a567d84f51c440132452a1853904cfbb29aa95e5c8efb35`.
The exported length and hash match the guest archive. It includes native logs,
file-by-file comparisons, state hashes, registrations, cached policy and stage outcomes.

The passing backup-assisted archive is **923,418 bytes**, SHA-256
`0db8159c1ab4a99e535f1092f430479e5b1611d7f0760f2600467a579d0d8216`.
Its exported length/hash also match the guest archive. `session-result.json` records both
branches separately; the direct failure is not relabeled by the workaround's success.

Snapshot `legacy-msi-to-058-state-loss-observed-20261022` preserves the failure.
Snapshot `legacy-055-msi-updated-057-before-migration-20261022` preserves the verified
pre-migration state. Earlier clean, repair-failure and maintenance-policy snapshots remain.
Snapshot `legacy-msi-058-backup-restore-maintenance-passed-20261022` preserves the final
passing workaround/maintenance state; the VM is left **Saved** to release host memory.

The first update verification stopped on a PowerShell 5.1 array-enumeration bug in the new
helper; a corrected verifier passed without reapplying the update. An invalid VirtualBox
helper-file transfer was rejected by SHA-256 before execution. Both failures remain in the
evidence; the replacement helper was delivered through the guest execution channel and
hash-verified. Neither issue is counted as an application or installer failure.

Read-only host captures used the same explicit real-user AppData path before and after
the test. The host already had 0.5.8.0 before this case; the observed DLL, manifest, updater,
updater log and update settings retained identical hashes at identical paths afterward.
No host WinThunar process was launched by the lab.
