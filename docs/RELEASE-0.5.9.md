# WinThunar 0.5.9 — unpublished candidate

Prepared 22 October 2026 for review. **0.5.9 has not been published to GitHub Releases**;
the latest published application and updater feed remain 0.5.8. Version, assembly/file,
manifest and release-script defaults are aligned at 0.5.9 (four-part version 0.5.9.0).

## Changes since 0.5.8

- Adds a source-only, disposable-guest regression tool for original MSI installation,
  native application update, migration to a corrected MSI, repair and uninstall.
- Records a confirmed legacy-MSI migration failure: the original MSI's cleanup deletes
  settings, plugins and recovery files when a newer MSI removes it, even though the new
  payload and cached installer are correct and the MSI returns success.
- Documents an independently verified external backup/restore workaround, preserving the
  direct failure separately from the workaround's success. README, roadmap, maintenance
  and upgrade-lab notes now distinguish these results.

The tool is excluded from application packages. Application and installer behavior retain
0.5.8's policy; **automatic protection during legacy-MSI migration is not implemented**.
An application update still does not replace an older cached MSI.

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

These checks used older published binaries, not an installed 0.5.9 candidate. Interactive
maintenance, all-users/UAC, other legacy versions/custom paths, actual recovery replay and
live Undo remain open. Preserve the complete user-state directory outside the app/state
directories before legacy-MSI maintenance or migration; see the lab instructions.

## Candidate validation

The candidate passed **171 service tests, one skipped, zero failed**, and a normal x64
Release build with **zero warnings/errors**. The skip requires Windows symbolic-link
privilege. The test project reported NU1900 because NuGet vulnerability metadata was
unavailable. Script parsing, version alignment and whitespace checks passed. Evidence is
retained in `artifacts/verification-0.5.9/`.

The guest checks above are retained evidence from the preceding migration investigation;
they were not rerun against this candidate. No 0.5.9 release packages, GitHub tag or public
feed are created by this PR preparation. Packaging, public asset/delta verification and
publication require a separate approved release step.
