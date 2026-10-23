# WinThunar 0.5.10

Prepared 23 October 2026 for Windows 11 x64. Publication and final public-binary verification
are pending. Project, assembly/file, manifest and release-script versions are aligned at
0.5.10 (four-part version 0.5.10.0).

## Changes since 0.5.9

Legacy per-user MSI migration now requires actual administrator elevation in the **same
Windows account** before removing the old product. Unelevated attempts stop before state
protection or removal and explain how to rerun the installer, selecting **Just me**. The
guard checks the effective Windows token; caller-supplied MSI privilege properties cannot
bypass it. This avoids the reproduced unelevated rollback path that restored application
files and state while leaving the old Windows Installer product registration incomplete.

The complete-directory state protection and interrupted-migration startup guard introduced
in 0.5.9 remain in place. Legacy all-users migration and rollback-disabled migration remain
refused. Fresh installs, ordinary application updates, and maintenance of an already
corrected MSI keep their existing paths. Native build linking includes the Windows token API
dependency; no additional target-machine runtime is required.

The source-only guest runner now supports explicit elevated migration/maintenance tests and
an unelevated-refusal case. Rollback checks include fully installed product state, the
original cached MSI path/hash, unchanged uninstall metadata, original payload/state hashes,
and absence of the target uninstall entry and holding directory.

## Installed verification and scope

The [rollback investigation](MSI-ROLLBACK.md) freshly reproduced the original failure from
the original public per-user 0.5.5 MSI after a native 0.5.7 update. Private packages carrying
the new guard then passed:

- Unelevated refusal without changing all 524 application files, five seeded state files,
  uninstall metadata, fully installed product state or cached MSI.
- Same-account elevated failures immediately after old-product removal and after execution
  of the new payload/state restoration. Both restore all original file/state hashes, product
  state 5, original cache path/hash and uninstall metadata, with no hold or target entry.
- Subsequent normal migration without helper backup/restore, both `/fomus` and `/fa` repair,
  and uninstall retaining state/document while removing app, registrations, shortcuts and
  explicitly enabled Shell/default-folder integration.

The 1,876,805-byte suite bundle and all four nested archive digests are verified on the host.
Snapshot `elevated-legacy-msi-rollback-maintenance-passed-20261023` preserves the results; the
VM is Saved with all NICs disabled. These installed checks used private 0.5.9 application
packages with the final guard source; they are not installed tests of public 0.5.10 binaries.

## Limits

An automatic application update does **not** replace an older cached MSI. Use the corrected
MSI to obtain the migration policy. Existing legacy repair/uninstall limitations remain until
that installer is replaced. Preserve an external backup of the complete user-state directory
before legacy maintenance and follow the [interrupted-migration guidance](MSI-MIGRATION.md).

Standard Windows accounts that can elevate only by supplying a different administrator's
credentials cannot use this same-account migration path. Legacy all-users migration,
non-legacy major-upgrade rollback, interactive maintenance/UAC cancellation, other legacy
versions/custom paths, actual recovery replay and live Undo remain outside these checks.
This change establishes the tested elevated legacy-migration path; it does not fix the MSI
engine generally. Windows 11 x64 is the supported release target.

## Release verification

Pre-merge verification passed 173 service tests, one symbolic-link privilege skip, zero
failed; a normal self-contained x64 Release publish; native compilation with warnings as
errors; real filesystem preservation checks; script parsing, version alignment and whitespace
checks. Native symbolic-link fixtures also skip without privilege. The initial sandboxed
service run aborted on testhost connection timeout before tests ran; the unrestricted retry
passed. NuGet reported NU1900 while vulnerability metadata was unavailable.

The final clean-main release build, both MSI copies and embedded guard binary, retained
historical packages, 0.5.9-to-0.5.10 delta reconstruction, public binary digests, feeds and
source tag are verified separately during shipping. Results will be recorded after publication.
