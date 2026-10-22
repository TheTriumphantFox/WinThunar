# WinThunar 0.5.7

Release candidate for Windows 11 x64. Publication is pending; 0.5.6 remains the latest
published release until the GitHub assets and updater feed are verified.

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
`artifacts/verification-0.5.7/` and `artifacts/desktop-release-0.5.7/`. Package results will be
recorded here before publication is marked complete.

The isolated production-backend probe passed delta/full apply-on-exit, pending rediscovery,
corrupt-delta fallback, and corrupt-full rejection/retry; all 194 deployed files matched in each
successful scenario. In a separate Windows 11 evaluation guest, the public 0.5.5-to-0.5.6
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
does not launch or modify the user's installed WinThunar copy.
