# WinThunar 0.5.6

Published [21 October 2026](https://github.com/TheTriumphantFox/WinThunar/releases/tag/v0.5.6)
for Windows 11 x64 from source `7ed9574` (PR #17). Superseded by
[0.5.7](RELEASE-0.5.7.md) on 22 October 2026; the verification below records the 0.5.6 publication.

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

The packaged app/MSI report **0.5.6.0**. Release assemblies match across archives, contain the
theme implementation, and exclude test hooks. MSI shortcut choices and local checksum sidecars
passed. All **five public binary sizes and SHA-256 digests** match the upload files, and the
public feed contains exactly the 0.5.6 full/delta entries. The **731,233-byte 0.5.5-to-0.5.6 delta**
reconstructed all **528 package entries** with matching names, lengths, and SHA-256 content hashes.
All five earlier packages retain their original hashes. Evidence and reports are preserved in
`artifacts/verification-0.5.6/verification.json` and `artifacts/desktop-release-0.5.6/`.

Existing file-operation code is unchanged from 0.5.5; the earlier
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
Generic previous-release installer assets were saved in
`artifacts/verification-0.5.6/previous-installer-assets/` before packaging. No installed application
was launched, closed, replaced, or downgraded by this workflow. A controlled installed 0.5.6
upgrade remains unverified.
