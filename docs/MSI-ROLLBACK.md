# Legacy MSI rollback investigation

Verified private source candidate, 23 October 2026. Published **0.5.9 remains unchanged** and retains the
[known rollback limitation](MSI-MIGRATION.md#forced-rollback-finding--known-limitation).
The source change below is a private candidate, not a published installer fix.

## Reproduction

The offline disposable Windows 11 guest was restored to
`legacy-055-msi-updated-057-before-migration-20261022`: original public per-user 0.5.5 MSI,
native 0.5.7 application update, five seeded state/plugin/opaque-recovery files.
A private unmodified 0.5.9 rollback variant failed deliberately immediately after
`RemoveExistingProducts`. It reproduced the registration failure:

- All 524 updated application files and five original state files were restored.
- The holding directory was absent.
- The old product's uninstall entry was missing, `ProductState` was Advertised (1), and
  `LocalPackage` lookup failed. The required fully installed state is Default (5).

The result is **Failed**, retained in snapshot `rollback-registration-reproduced-20261023`.
Export `reproduced-evidence.zip` is 422,333 bytes, SHA-256
`c3d81d1da2364a0f7591f7bce2eee50246bc9f270aeca16ce57c2ba11f18e2a3`;
the host copy matches the guest length and digest. Evidence and private test inputs are in
`artifacts/windows-upgrade-vm-20261021-212229-9192ed86/rollback-investigation-20261023/`.

The earlier logs' access-denied registry restoration resembles the independently reported
[per-user Windows Installer rollback issue](https://github.com/wixtoolset/issues/issues/8574).
This is a comparison, not proof of an engine or compiler root cause. Moving the failure before
removal would not exercise rollback. Changing Windows Installer registry permissions or
disabling rollback is not part of this candidate.

## Candidate behavior

The native preflight requires an elevated effective Windows token for legacy per-user
migration, before registering state actions or entering `InstallInitialize`. An unelevated
attempt receives guidance to close the installer and rerun it elevated using the **same
Windows account**, selecting **Just me**. Switching to another administrator account would
target a different user's profile and is not the supported procedure.

The guard reads the impersonating thread token first, falling back to the process token only
when there is no thread token. Caller-supplied `Privileged` or `MsiRunningElevated` properties
cannot satisfy it. Legacy all-users migration and rollback-disabled migration remain refused.
The existing state-holding and restoration transaction is retained. Fresh installations,
ordinary application updates, and maintenance of an already corrected MSI use their existing
paths. This guard does not fix Windows Installer itself or retrofit old cached packages.

## Verification

The native DLL and filesystem test executable build with warnings as errors. Real directory/file
identity, alternate streams, empty directories, absent state, stale holds, conflicts, locks and
restoration checks pass; the symbolic-link fixture skips without the needed Windows privilege.
The separate MSI policy check passes all eleven maintenance-condition cases, action types and
transaction ordering, and repeat-patch rejection without changing the original MSI. The guest
runner rejects the originating host with and without its new elevation option.

The strengthened guest runner records original and resulting product state, cached-MSI path
and digest, uninstall metadata, deployed-file hashes and retained state. `RejectMigration`
requires the expected refusal before removal or state protection. `Rollback` requires the
injected failure after real removal, original payload/state restoration, full old-product
registration, unchanged cache and uninstall metadata, and no target registration or hold.
`ElevatedMigration` must match the actual token and is accepted only for migration,
maintenance and evidence export; initial installation/application updates stay unelevated.

The native guest preflight passes the all-users, rollback-disabled and forged-privilege
property refusal checks. The installed unelevated refusal case also passes: all 524 payload
files and five state files match, uninstall metadata is unchanged, the old product remains
fully installed (5), and its cached MSI path/digest are unchanged. No removal/state-protection
action ran. Export `guard-evidence.zip` is 255,008 bytes, SHA-256
`76f120de2b6f8301e5568cdcdbff101c1a81f3d99e5bf539fc15c19877fee4ca`;
the host copy matches the guest length and digest.

The same account (`WINTHUNAR-LAB\WTLab`) then ran the corrected suite elevated, with UAC
enabled and all VM NICs disabled. The cases ran sequentially: early rollback, late rollback,
then a normal migration retry and maintenance. Each rollback had to restore the original
payload, state, registration, cache and uninstall metadata before the next case could start.

| Installed check | Result |
| --- | --- |
| Failure immediately after old-product removal | Passed; all 524 original updated files and five state files restored; old product state 5, original cache path/hash and uninstall metadata restored; no hold or target uninstall entry |
| Failure after `InstallExecuteAgain` writes the new payload and restores state | Passed with the same original-payload/state/registration/cache checks |
| Normal migration after both rollbacks | Passed; all 524 target files match; five state files retained without helper backup/restore; old registration replaced and corrected cached policy verified |
| Standard `/fomus` and forced `/fa` repairs | Both passed; all 524 target files and five retained-state hashes match after each |
| Uninstall with explicitly enabled Shell integration | Passed; state and generated document retained; app, both registrations, both shortcuts and all three owned Shell verbs removed; default folder handling restored |

Both elevated rollback logs have zero matches for the earlier skipped rollback, access-denied
1401/1402/1403/1404 registry restoration, or invalid-owner 1307 diagnostics. This establishes a
passing path for the tested elevated legacy migration, not a general repair of the MSI engine.
Fresh installation and non-legacy major-upgrade rollback are outside these installed cases.

| Case export | Bytes | SHA-256 |
| --- | ---: | --- |
| Early rollback | 418,608 | `bf566df52540171b9980abdcbfae0916d78abc28f08e8ba7c747988cf27d6aa9` |
| Late rollback | 534,896 | `cf4291056251e550f4f428f3df159b938dfd143004acfd066b7c17d09969ce0d` |
| Migration, both repairs and uninstall | 917,809 | `d8495225a7a1dacf80d8ecc965e6cc9cbff6ebe1bc0060881211fc03a7e1619b` |

All four case archives, including the earlier unelevated refusal, were bundled with the worker
report/transcript and retained harness-failure report. The bundle is **1,876,805 bytes**, SHA-256
`cd420e687d649facd906a6284f465d9aedb5781712c25feb3b4853fbec5bbe35`.
The host verified that length/digest and every nested case archive against the guest manifest.
Snapshot `elevated-legacy-msi-rollback-maintenance-passed-20261023` preserves the completed
suite; the VM is left Saved, with all NICs disabled.

The first elevated worker stopped before installer execution on a read-only working manifest
copied from ISO. Its report and partial case were preserved. The corrected worker uses an
explicit forced replacement of that case-owned manifest; switching was verified before retry.
An oversized encoded correction timed out before applying changes, and a subsequent guest-file
copy failed its digest check before execution. The final corrected worker ran from a small
read-only ISO with a verified digest. These are harness failures, not passing application tests.

Publication and public-binary verification remain outstanding. Standard Windows accounts that
can elevate only by supplying a different administrator account cannot use this same-account
migration path. Interactive maintenance/UAC cancellation, other legacy versions/custom paths,
actual recovery replay and live Undo remain separate verification work.
