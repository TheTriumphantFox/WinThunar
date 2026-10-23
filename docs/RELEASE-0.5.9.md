# WinThunar 0.5.9

Published [22 October 2026](https://github.com/TheTriumphantFox/WinThunar/releases/tag/v0.5.9)
for Windows 11 x64 from source `44d13d6d6d7c1a6715a73aaa1739105be8b0459d` (PR #25).
This is the latest stable release; the public updater feed offers 0.5.9 full/delta packages.
Version, assembly/file, manifest and release-script defaults are aligned at 0.5.9
(four-part version 0.5.9.0).

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

## Release verification

The merged release source passed **173 service tests, one skipped, zero failed**, a normal x64
self-contained Release publish, native custom-action compilation with warnings treated as
errors, and real filesystem/native MSI policy checks. The skip requires Windows symbolic-link
privilege. The test project reported NU1900 because NuGet vulnerability metadata was
unavailable. Script parsing, version alignment and whitespace checks passed. Evidence is
retained in `artifacts/verification-0.5.9/ship/`; the earlier private-candidate checks remain
in `artifacts/migration-protection/` and `artifacts/verification-0.5.9/`.

The release was rebuilt from clean merged `main` using `Publish-Release.ps1 -Version 0.5.9`.
Both MSI copies have the expected native policy, action sequence and shortcut choices;
deliberately failing test actions are absent. All eleven historical packages remain
hash-identical. Reconstructing 0.5.9 from the public 0.5.8 full package and the **547,840-byte**
delta matched all **528 archive entries** by name, length and SHA-256.

All five published binary sizes/digests match the verified build. The modern public feed
offers matching 0.5.9 full/delta entries; the compatibility `RELEASES` feed offers the matching
full package. Latest stable resolves to 0.5.9, its tag points to the verified source commit,
and the public release notes retain the known forced-rollback registration failure.

| GitHub binary | Bytes | SHA-256 |
| --- | ---: | --- |
| `WinThunar.App-win.msi` | 93,425,664 | `7383ed9b5990ae3854ada2eaeadfcdf23f4a792b6d7e3c4d165c860d9fcd7b92` |
| `WinThunar.App-win-Portable.zip` | 111,930,354 | `516a79eeea72f8d25d30ab3796aef3e57013979335f1a85c44ad767fe5b24dec` |
| `WinThunar.App-win-Setup.exe` | 119,478,479 | `994bcf59fbe282ddac463ca5fab4268f2a24b00ab1771a9d5542ecff6d6c5249` |
| `WinThunar.App-0.5.9-full.nupkg` | 111,946,959 | `23ab65b1e3d298d6ec0a1f310b3546fe9ea19e940ff44cbf595197e069e85d66` |
| `WinThunar.App-0.5.9-delta.nupkg` | 547,840 | `a5976deae24f8f36ef25a353d1eb42ffaeaf49e5dfc322139e2cfd558ae23d9d` |

Installed-guest checks used private 0.5.9 packages under
`artifacts/migration-protection/packages-final/`, before the final release build. They are
not relabeled as installed tests of the public binaries. Actual recovery replay, live Undo,
interactive/all-users UAC and complete MSI registration rollback remain unverified.
