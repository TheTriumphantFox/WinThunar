# WinThunar 0.5.7

Published [22 October 2026](https://github.com/TheTriumphantFox/WinThunar/releases/tag/v0.5.7)
for Windows 11 x64 from source `24042d5049bb15f36d08bc4f3bec93e3bce974b9` (PR #19).
This is the latest stable release; the public updater feed offers 0.5.7 full/delta packages.

## Changes since 0.5.6

Closing a window saves its own tabs, active folder, selected tab, and view mode. Previously,
another window could replace the shared session, causing the remaining window's tabs to be
lost after closing and restarting, including a restart after applying an update.

The save runs for both normal and programmatic window closure, before the final window
schedules a downloaded update. Cancelled closes retain the existing transfer safeguards.

This release also adds a read-only installed-updater capture script, an isolated production
updater probe, and a guarded disposable Windows guest runbook. The probe uses a unique package
identity and generated settings. Guest actions reject the originating host before any writes.

## Verification and limits

The regression reproduced the session loss before the fix. The 0.5.7 candidate passed **171
service tests, one skipped, zero failed**, **67 desktop checks** and **two isolated-process theme
restart checks**, plus a normal x64 build with zero warnings/errors. The service-test restore
reported NU1900 because NuGet vulnerability metadata was unreachable; compilation and tests
completed. The skip requires Windows symbolic-link privilege. Evidence is preserved in
`artifacts/verification-0.5.7/` and `artifacts/desktop-release-0.5.7/`.

The packaged app and both MSI variants report **0.5.7.0**. Release assemblies match across
the versioned ZIP, public portable ZIP and full package, contain the close-session fix, and
exclude smoke/fault hooks. Both MSI variants retain shortcut choices; local checksum sidecars
passed. The versioned local MSI and finalized upload MSI have different container bytes and
separate hashes; both were checked independently.

All **five public binary sizes and SHA-256 digests** match the verified upload files. The public
feed contains exactly the 0.5.7 full/delta entries, latest stable resolves to 0.5.7, and the tag
points to the verified source commit. The **520,657-byte 0.5.6-to-0.5.7 delta** reconstructed all
**528 package entries** with matching names, lengths and SHA-256 content hashes. All **seven
historical update packages** retain their original hashes. See
`artifacts/verification-0.5.7/verification.json` and `published-release.json`.

The isolated production-backend probe passed delta/full apply-on-exit, pending rediscovery,
corrupt-delta fallback, and corrupt-full rejection/retry; all 194 deployed files matched in each
successful scenario. The four scenarios were repeated successfully for this release; cleanup
confirmed no owned probe process or registration remained. Evidence is in
`artifacts/updater-probe-release-0.5.7/`. In a separate Windows 11 evaluation guest, the public 0.5.5-to-0.5.6
per-user delta updated all 524 deployed files and applied only after the final window closed.
That guest run exposed the session bug fixed here; it did not run the 0.5.7 installer.

Two installer findings remain open and are **not fixed in 0.5.7**:

- Repairing the original 0.5.5 MSI after updating to 0.5.6 returned success but restored the old
  `WinThunar.deps.json` and `WinThunar.pri`, leaving a mixed payload. Avoid repairing an older MSI
  after an automatic update. Original MSI product metadata can also retain its older version.
- Silent per-user uninstall removed the app, shortcuts and registrations and preserved the
  generated document, but also removed `%LOCALAPPDATA%\WinThunar` session/update settings.
  Recovery/Undo data retention was not exercised; preserve that directory before uninstalling
  if it contains settings or unresolved recovery data you need.

All-users UAC approval/cancellation, Restart Now, unseeded full-package UI upgrades, broader
Shell/accessibility/cloud coverage, positive PDF/Office indexed-content results, and real remote
storage/device interruption remain open. Notification registration still fails with `0x8007007E`
on the test host. See [updater verification](UPDATER-VERIFICATION.md), the
[guest evidence](RELEASED-UPGRADE-LAB.md), and the [roadmap](ROADMAP.md).

## Packaging

Project, assembly/file, package manifest and release-script versions are aligned at **0.5.7**
(four-part versions **0.5.7.0**). Build from the merged main checkout:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Publish-Release.ps1 -Version 0.5.7
```

Preserve all historical packages in `artifacts/installer`, the unpublished 0.6.0 archive,
retained VHDs, ambiguous recovery data, and guest snapshots/evidence. This release workflow
does not launch or modify the user's installed WinThunar copy. The final read-only capture
still reports **0.5.3.0**, no WinThunar process, and the same five observed file hashes as before
publication; see `artifacts/verification-0.5.7/host-after-publication/`.
