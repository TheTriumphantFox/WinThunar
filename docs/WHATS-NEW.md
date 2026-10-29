# What's new in WinThunar

Release-by-release summaries moved here from the README on 26 October 2026. Each release
also has its own notes in this folder; the [roadmap](ROADMAP.md) lists what remains open.

## Published in 0.6.0

WinThunar stays steady through long sessions: a full day of browsing, searching and previewing
finishes without a crash, with memory and handles levelling off. Git status badges run in the
background, so a slow Git start no longer freezes the window, and **Show Git status badges** in
Preferences turns Git off entirely. See the [0.6.0 release notes](RELEASE-0.6.0.md).
The public packages, source tag and GitHub update feed are verified.

## Published in 0.5.15

File rows reuse their accessible-name bindings across browsing and search, preserving current
names, metadata and Git status when containers are recycled. The source passes a focused
fifteen-minute workload with 3,558 searches and 2,372 previews; retained memory and the earlier
native-crash root cause remain under investigation. See the [0.5.15 release notes](RELEASE-0.5.15.md).
The public packages, source tag and GitHub update feed are verified.

## Published in 0.5.14

F6 and Shift+F6 now reach the visible search/location controls, side-pane children, split
navigation, previews and command output. Closing focused auxiliary panes restores file-list
focus, and split/command buttons have explicit accessible names. An old folder's watcher can
no longer cancel a busy navigation to a new folder. See the [0.5.14 release notes](RELEASE-0.5.14.md)
for verified packages and the remaining endurance limits.

## Published in 0.5.13

This maintenance release records a passing installed two-window Restart Now test from public
0.5.11 to public 0.5.12, adds a reusable guest-test helper, and updates Help > What's New.
DOCX text preview/search and thumbnail improvements from 0.5.12 are retained.
See the [0.5.13 release notes](RELEASE-0.5.13.md) for scope and publication status.

## Published in 0.5.12

DOCX files gain a read-only body-text preview and direct `content:` search without an Office
installation or Windows indexing filter. For example, `type:docx content:"annual report"` reads
the current document body. See [DOCX support and limits](DOCUMENT-TEXT.md).
Thumbnail loading also uses bounded workers and reuses unchanged images for equivalent
layout/zoom requests; see the [resource verification](PRIORITY-VERIFICATION.md#thumbnail-resource-follow-up).
These additions are included in the published 0.5.12 downloads.
See the [0.5.12 release notes](RELEASE-0.5.12.md) for verification and publication status.

## Published in 0.5.11

**Keep your tabs when you restart to update.** After opening and closing another WinThunar
window, **Restart Now** restores the tabs, active folder and view from the window where you
clicked the button when tab restoration is enabled. Restart still waits for file transfers and
Undo/Redo operations in every window. The fix is included in the published 0.5.11 downloads.

Use the new **Help > What's New...** section to read the update summary inside the app.
See the [0.5.11 release notes](RELEASE-0.5.11.md) for changes and release status.
See the [verification report](PRIORITY-VERIFICATION.md#installed-delta-and-restart-now-session-fix)
for the reproduced issue, passing regression and remaining checks.

## Published in 0.5.10

**Safer migration from an older per-user MSI.** The installer requires administrator elevation
in the same Windows account before removing the old product, protecting the tested recovery
path if installation fails. Automatic app updates do not replace Windows' cached repair/uninstall
installer; use the latest MSI to get these safeguards. See the
[0.5.10 release notes](RELEASE-0.5.10.md) for details.

## Release and source status

The latest published application release is
**[0.6.0](https://github.com/TheTriumphantFox/WinThunar/releases/tag/v0.6.0)** (29 October
2026), from source `bf7eadc` (PR #43). Its changes are merged into `main`, and the GitHub
updater feed offers 0.6.0 to installed copies. Use **Help > Check for Updates...** to check
immediately; portable copies are updated manually. Documentation-only updates do not update the
app. See the [0.6.0 release notes](RELEASE-0.6.0.md) for verified packages and remaining
limits.
The version string alone does not establish that a local build matches published binaries.

Release **[0.5.10](RELEASE-0.5.10.md)** provides a guarded legacy-MSI migration
path: the installer requires actual administrator elevation in the same Windows account
before old-product removal. Unelevated refusal and elevated early/late rollback, migration,
both repair modes and uninstall pass in the disposable guest. All five public binary digests,
the source tag/feed and all 528 reconstructed delta entries match the verified release build.
Application updates do not replace an older cached MSI; use the corrected installer for this policy.

Release **[0.5.9](RELEASE-0.5.9.md)** adds
automatic state protection during per-user legacy-MSI migration and an interrupted-migration
startup guard. All-users legacy migration is refused before removal; see the
[migration scope and recovery instructions](MSI-MIGRATION.md).
The release retains a known forced-rollback MSI registration failure: normal migration
preserves state, but full installer failure recovery is not verified. Read the migration
findings before legacy-MSI maintenance and keep an external state backup.

Release **0.5.8** adds safer MSI repair and uninstall retention for newly built
installers. It does not replace an older MSI already cached on an installed machine; see the
[0.5.8 release notes](RELEASE-0.5.8.md) for the migration and verification limits.
Direct MSI migration from an original 0.5.5 installation updated to 0.5.7 deleted seeded
settings, plugins and recovery data despite installing 0.5.8 successfully. Preserve the complete
user-state directory before older-MSI maintenance; see the [migration findings](MSI-MIGRATION.md).

Release **0.5.7** fixes lost tabs after closing multiple windows by saving the closing window's
own session before an update applies. It passed 171 service tests (one skipped), 67 desktop checks,
two restart checks, and four isolated updater scenarios. Installations made with its older MSI
retain mixed-payload repair and settings-removal risks; read the
[installer findings](RELEASE-0.5.7.md#verification-and-limits) before repair or uninstall.

Release **0.5.6** adds **System / Light / Dark** under **Edit > Preferences > Display > Theme**.
Apply updates all windows, native title bars, and WinThunar dialogs. The choice survives restart;
Cancel preserves it, and separate appearance settings prevent another window from overwriting it.

Release **0.5.5** adds updater error handling, an Interrupted Transfers
review/retry view, conservative recovery for unavailable storage, text-content search, This PC
filesystem search, read-only text/PDF previews with page navigation, and explicit `index:true`
Windows Search queries with coverage warnings. Disposable tests cover real disk-full after
main-stream writes and owned loopback-SMB interruption. It includes the unpublished 0.5.4 candidate.
See the [reliability milestone](RELIABILITY-MILESTONE.md) for results and limits.

