# WinThunar 0.5.9 — unpublished candidate

Prepared 22 October 2026 for review. **0.5.9 has not been published to GitHub Releases**;
the latest published application and updater feed remain 0.5.8. Version, assembly/file,
manifest and release-script defaults are aligned at 0.5.9 (four-part version 0.5.9.0).

**Known installer limitation:** normal per-user migration, both repairs and uninstall retain state,
but a forced migration failure exposes broken old-product registration during Windows
Installer rollback. State and application files are restored in the retained sequence;
complete MSI registration rollback is not verified. This failure remains open in 0.5.9;
keep an external state backup before legacy-MSI maintenance. See
[the failure evidence](MSI-MIGRATION.md#forced-rollback-finding--known-limitation).

## Changes since 0.5.8

- Adds a source-only, disposable-guest regression tool for original MSI installation,
  native application update, migration to a corrected MSI, repair and uninstall.
- Records a confirmed legacy-MSI migration failure: the original MSI's cleanup deletes
  settings, plugins and recovery files when a newer MSI removes it, even though the new
  payload and cached installer are correct and the MSI returns success.
- Documents an independently verified external backup/restore workaround, preserving the
  direct failure separately from the workaround's success. README, roadmap, maintenance
  and upgrade-lab notes now distinguish these results.
- Adds native automatic protection for per-user legacy-MSI migration. The whole state
  directory is held outside old cleanup and restored before transaction completion or on
  rollback. Locks, unexpected roots, competing state and stale holds fail without overwriting
  the original. Legacy all-users migration is refused before removal.
- Stops normal application startup while a migration holding entry remains, with the
  preserved-state location and recovery guidance. Installer hooks and Shell recovery
  switches remain available.

The guest tool and native test executable are excluded from application packages. The
native custom-action DLL is embedded in the MSI, with no extra target-machine runtime
requirement. An application update still does not replace an older cached MSI.

## Migration evidence and limits

The [migration lab](MSI-MIGRATION.md) used the original published per-user 0.5.5 MSI,
the native 0.5.7 full-package update and the published 0.5.8 MSI. Direct migration matched
all 524 target files and replaced the MSI registration/cache correctly, but deleted all
five seeded settings/plugin/opaque-recovery files. That case is **Failed**.

An independent external backup/restore branch matched all 524 payload files and the five
original state hashes after migration and both standard/forced repairs. Uninstall retained
those files and a generated document while removing the app, both registrations, both
shortcuts and explicitly enabled Shell/default-folder integration. Both evidence exports
match their guest lengths and SHA-256 digests; failure and passing-workaround snapshots
are retained and the VM is Saved.

Those historical checks used older published binaries. The private candidate verification
is recorded separately in the [migration lab](MSI-MIGRATION.md). Interactive
maintenance, all-users/UAC, other legacy versions/custom paths, actual recovery replay and
live Undo remain open. Preserve the complete user-state directory outside the app/state
directories before legacy-MSI maintenance or migration; see the lab instructions.

## Candidate validation

The updated candidate passed **173 service tests, one skipped, zero failed**, a normal x64
self-contained Release publish, native custom-action compilation with warnings treated as
errors, and real filesystem/native MSI policy checks. The skip requires Windows symbolic-link
privilege. The test project reported NU1900 because NuGet vulnerability metadata was
unavailable. Script parsing, version alignment and whitespace checks passed. Evidence is
retained in `artifacts/migration-protection/`; the earlier candidate checks remain in
`artifacts/verification-0.5.9/`.

Private 0.5.9 packages were built under `artifacts/migration-protection/packages-final/`
for installed-guest checks. They have not been published. No GitHub tag or public feed was
changed; public asset/delta verification and publication remain separate release work.
