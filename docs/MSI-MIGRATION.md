# Migrating an older MSI installation

## Automatic protection in 0.5.9

Published **0.5.9** adds native protection for **Just me** migrations from
legacy MSIs. Published 0.5.8 and earlier installers retain the failure described below.
An application update alone still does not replace an old cached MSI.

**Known installer limitation:** normal migration/maintenance retention passes, but a deliberately failed
unelevated migration restores state/files while failing to restore the old Windows Installer
registration. The 0.5.9 release retains this failure as an open issue. Do not treat state
retention as proof of complete transactional rollback. Keep an external state backup before
legacy-MSI maintenance; see the rollback findings below.

Published **[0.5.10](RELEASE-0.5.10.md)** includes the [23 October rollback remedy](MSI-ROLLBACK.md), requiring elevation in the
same account before legacy per-user migration. Installed unelevated refusal, elevated early
and late rollback, subsequent migration, both repairs and uninstall pass in the disposable
guest. The public 0.5.10 packages, both MSI copies, update feed and source tag are verified
separately. Existing 0.5.9 and earlier cached MSIs retain the behavior and limits below until
replaced with the corrected MSI; an application update does not retrofit them.

The new MSI inspects related products' cached cleanup IDs before any removal. For a legacy
per-user product, it requires a closed application/updater and rollback enabled, then
renames the complete `%LOCALAPPDATA%\WinThunar` directory to the sibling
`%LOCALAPPDATA%\WinThunar.MsiMigrationState`. This same-volume rename preserves the original
directory and files, including streams, ACLs and nested links, without traversing their
contents. Reparse points at the state or holding root are rejected.

The installer registers a rollback restore before protection and flushes protection with
`InstallExecute` inside the transaction. `RemoveExistingProducts` then removes the old app
before the new payload is installed. A deferred action restores state before finalization;
rollback also restores it if installation fails. This uses Microsoft's supported
[placement after InstallExecute and before InstallFinalize](https://learn.microsoft.com/en-us/windows/win32/msi/removeexistingproducts-action).
All state actions impersonate the installing user. Legacy **all-users** migration is refused
before mutation because protection of other users' profiles has not been established.

The native actions never overwrite or recursively delete competing state. An empty recreated
state directory may be removed to restore the original; a nonempty competing directory causes
failure with the held original retained. A previous holding directory also stops a new
migration. The new application refuses normal startup while any holding entry exists, so
an interrupted migration cannot silently create replacement settings. Installer hooks and
explicit Shell-registration recovery switches still work.

### Interrupted-migration recovery

Wait for Windows Installer to finish, and close every WinThunar window and updater before
manual recovery. Keep both `%LOCALAPPDATA%\WinThunar.MsiMigrationState` and any newly created
`%LOCALAPPDATA%\WinThunar` directory. Make independently verified external backups of both
before changing them. If `WinThunar` does not exist, restore the complete held directory by
renaming it to `WinThunar` in the same parent directory. If both exist, preserve both and
resolve their contents deliberately; never delete the held original or overwrite competing
files just to clear the startup guard. Then retry maintenance with the corrected MSI.

Abrupt power loss may leave the holding directory for this manual recovery. Old application
versions do not have the startup guard. The implementation does not retrofit the old MSI's
standalone repair/uninstall behavior. Interactive UI, all-users migration, other legacy
versions/custom paths, actual recovery replay and live Undo require independent coverage.

### Source checks

`scripts/Build-MsiMigrationActions.ps1 -OutputDirectory <new-output> -Test` builds the native
actions and exercises real directory/file identity, alternate streams, empty directories,
absent state, restoration conflicts, stale holds, sharing locks and rollback restoration.
The symbolic-link root check skips when Windows does not permit link creation.
`Test-MsiMaintenancePolicy.ps1` additionally verifies the embedded action types, transaction
sequence and native migration conditions without executing installation actions.
The native test executable's `--preflight <candidate.msi> <installed-legacy-product-code>`
mode opens a read-only MSI session and calls the real preflight implementation directly.
In the disposable guest it verified recognition of the installed legacy cache, refusal of
all-users migration, and refusal with rollback disabled, without scheduling state changes.

### Private 0.5.9 installed verification

From the preserved original-0.5.5-MSI/native-0.5.7 checkpoint, the private 0.5.9 MSI passed
direct migration **without `PreserveUserState` or any helper backup/restore**. All five
seeded settings/plugin/opaque-recovery files retained their hashes and all **524 deployed
files** matched the candidate package. The cached corrected MSI/registration replaced the
old product. Native logs confirm protection before old cleanup and restoration afterward.

Both `/fomus` and `/fa` repairs retained all five state hashes and matched all 524 deployed
files. Uninstall retained the state and generated document while removing the app, both
registrations, both shortcuts and explicitly enabled Shell/default-folder integration.
This is silent per-user installer evidence; opaque recovery-file retention does not establish
actual recovery replay or live Undo.

The exported success archive is **921,688 bytes**, SHA-256
`668cbb57a1061ddc877ebe3231aa1eae68e5ade2c4a3b3b4a1e1d37d94e176ba`.
Its guest size/hash were verified on the host before restoring the VM. Evidence and the
private input ISO are under
`artifacts/windows-upgrade-vm-20261021-212229-9192ed86/migration-protection/`.
Snapshot `automatic-msi-059-migration-maintenance-passed-20261022` preserves the result.

### Forced rollback finding — known limitation

A private MSI variant deliberately fails with a type-19 action immediately after
`RemoveExistingProducts`. The protection action executed before removal, and rollback
restored all five original state hashes and all 524 updated application files, with no
holding directory left. The overall rollback case is nevertheless **Failed**: the old MSI's
standard product registration is missing, `ProductState` is Advertised (1), and its
`LocalPackage` lookup fails. The native updater registration still points at that old product.

The rollback log contains skipped registry restoration errors 1401/1402, system error 5,
under the old product's Installer UserData/InstallProperties keys. This resembles the
[reported per-user MSI rollback registration problem](https://github.com/wixtoolset/issues/issues/8574);
that comparison is not proof of the underlying cause in WinThunar. No MSI-owned registry
permissions were changed and the failure has not been relabeled as a passing rollback.

The first failure archive is **421,423 bytes**, SHA-256
`269ae581d059db079c0bc128986d7aac138f4a77f9817d298e93f4f367b2c119`,
exported and host-hash-verified as `rollback-early-initial-evidence.zip` alongside the success
evidence. An OpenPackage automation preflight attempt returned action-not-executed; it is
preserved as a harness limitation and does not establish a native guard result.

An isolated experiment flushed `ProcessComponents` before old-product removal. It retained
state but did not restore all application files during rollback, so it was rejected and is
**not** the source/candidate sequence. Its failed evidence archive is **420,519 bytes**,
SHA-256 `e624d12af596050471e36119e0797017563aebd0907d5641e337ba8d93faf42b`,
also exported and hash-verified. The source retains the sequence with verified normal
migration and file/state rollback. All-user migration and complete registration rollback
remain open issues, separate from the implemented automatic state preservation.

The retained sequence was also tested with a deliberate failure after
`InstallExecuteAgain` executed the new payload and normal state restoration. All five
original state hashes and all 524 original updated application files were restored; the
holding directory was absent. The overall case again **Failed** the old-product registration
check. Its archive is **544,246 bytes**, SHA-256
`916bbf3e8d90166dc855d2399f3e49f0b8b1def778f19be25f6ed4a060f37530`,
exported and hash-verified as `rollback-late-evidence.zip`. It includes passing native
all-users/rollback-disabled preflight results. A zero-byte guestcontrol probe transfer was
rejected by SHA-256 before execution; the verified read-only ISO replacement was used.

Snapshot `automatic-msi-059-late-rollback-registration-failed-20261022` retains this failure.
The VM is restored to `automatic-msi-059-migration-maintenance-passed-20261022` and left
Saved, with all NICs disabled. The normal success, both failure branches and rejected
sequence experiment remain separate evidence; no host app or public release was changed
by those guest checks. The subsequent [0.5.9 publication](RELEASE-0.5.9.md) is verified
separately against the final release binaries.

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
and `TargetPackage`. Public inputs must match preserved release metadata; private candidates
and deliberately failing variants require separate recorded hashes and must not be published.

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
