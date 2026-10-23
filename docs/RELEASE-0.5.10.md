# WinThunar 0.5.10

Published [23 October 2026](https://github.com/TheTriumphantFox/WinThunar/releases/tag/v0.5.10)
for Windows 11 x64 from source `d35876e94af9b1691498b848875d711048777a0f` (PR #27).
All five public binary digests, both update feeds and the source tag match the verified build.
Project, assembly/file, manifest and release-script versions are aligned at
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

The release was rebuilt from clean merged `main` using `Publish-Release.ps1 -Version 0.5.10`.
Both MSI copies have the expected version, maintenance/migration policy, native action types,
transaction sequence and shortcut choices, with no deliberate failure action. The embedded
native DLL hash matches the freshly compiled source output, and its actual-token guard is present.

All thirteen historical update packages remain hash-identical. Applying the **316,787-byte**
0.5.9-to-0.5.10 delta to the retained public 0.5.9 base reconstructs all **528 archive entries**
with matching names, lengths and SHA-256 digests. The packaged assemblies match across the
versioned ZIP, public portable ZIP and full package; test hooks and source/guest tools are absent.

All five published binary sizes/SHA-256 digests match the verified local build. The modern
public feed offers matching 0.5.10 full/delta entries, the compatibility `RELEASES` feed offers
the matching full package, latest stable resolves to 0.5.10, and the tag points to the source
commit above. Public notes disclose same-account elevation and cached-MSI limits. Shipping
evidence is retained in `artifacts/verification-0.5.10/`; the installed fixture evidence remains
in the [rollback investigation](MSI-ROLLBACK.md).

| GitHub binary | Bytes | SHA-256 |
| --- | ---: | --- |
| `WinThunar.App-win.msi` | 93,401,088 | `52494de5986abce44e7654697f2c058d54b31b5b28c039bb809ac8212ff3e427` |
| `WinThunar.App-win-Setup.exe` | 119,478,562 | `428d8e8d91d48265d74e3e4a4bcd346b29109f92d2a75512b274df42c92e5a08` |
| `WinThunar.App-win-Portable.zip` | 111,930,436 | `8cba058f5a3556b19b3c687f9451c64f93a6cd3cc1186de5ad21020a88e72c68` |
| `WinThunar.App-0.5.10-delta.nupkg` | 316,787 | `74fb007c07ad08098fc6277c03996b9d8baecbc58038a41d2ec2b9340304c467` |
| `WinThunar.App-0.5.10-full.nupkg` | 111,947,042 | `7dc29e961a176592118c06d2900d10f8b47c0374fb83c9ed8f17ab9dfc16dd1f` |
