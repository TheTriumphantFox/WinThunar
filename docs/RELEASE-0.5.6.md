# WinThunar 0.5.6

Prepared 21 October 2026 for Windows 11 x64. Publication is pending; the latest public release
remains 0.5.5 until the verified packages are uploaded.

## Changes since 0.5.5

**Edit > Preferences > Display > Theme** now offers **System**, **Light**, and **Dark**.
Choose **Apply** to update all open WinThunar windows, their native title bars, and application
dialogs. New windows and restarts retain the choice. **System** follows the Windows app color
setting, and **Cancel** keeps the existing choice.

Appearance settings live separately in `%LOCALAPPDATA%\WinThunar\appearance.json`, so another
window's session save cannot overwrite the theme. Missing or invalid settings fall back to
System. A failed write reports an error and preserves the active/saved choice. Applying an
unchanged selector in an older Preferences dialog preserves another window's theme change.

Application dialogs use the same theme override as their window and update while already open.
Secondary/error text uses theme resources rather than brushes captured from the system theme.
Preferences content scrolls to keep the expanded settings usable in smaller windows.

## Verification and limits

- **171 service tests passed, one skipped, zero failed**. Nine new appearance cases cover
  persistence, isolation from session writes, malformed/unknown settings, and failed replacement.
  The skip requires Windows symbolic-link privilege.
- **65 desktop checks and two isolated-process restart checks passed**, including real Apply/Cancel
  controls, existing/new windows, native title-bar settings, light/dark dialogs, stale selectors,
  an open dialog following another window's change, and a real locked-settings save failure.
- Light/dark browser and Preferences renders were inspected. The smoke app uses generated
  appearance settings, disables Explorer registration changes, and restarts only its own process.
- The normal x64 application build passed with zero warnings/errors and excluded test hooks.
  Test-project builds report NU1900 when NuGet vulnerability metadata is unreachable; restoration
  and compilation completed.

Release packaging, delta reconstruction, and public asset/feed verification will be recorded
after the build and upload. Existing file-operation code is unchanged from 0.5.5; the earlier
[reliability evidence](RELIABILITY-MILESTONE.md) remains available. General accessibility,
contrast themes, live Windows-setting changes, native Shell dialogs, notification availability,
and controlled installed-upgrade checks remain outside this theme verification. See the
[roadmap](ROADMAP.md) and [updater verification](UPDATER-VERIFICATION.md).

## Packaging

Project, assembly/file, package manifest, release-script default, and README example versions
are aligned at **0.5.6** (four-part versions **0.5.6.0**). Build from the merged main checkout:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Publish-Release.ps1 -Version 0.5.6
```

Preserve all previous packages in `artifacts/installer`, the retained VHDs, and ambiguous
recovery data. The historical unpublished 0.6.0 assets remain archived outside the upload folder.
