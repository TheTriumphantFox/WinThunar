# WinThunar 0.5.11

Published [23 October 2026](https://github.com/TheTriumphantFox/WinThunar/releases/tag/v0.5.11)
for Windows 11 x64 from source `c87dd99947ed3fbaca75a20f2fa6bf52c5395435` (PR #29).
All five public binary digests, both update feeds and the source tag match the verified build.
Project, assembly/file, manifest and release-script versions are aligned at 0.5.11
(four-part version 0.5.11.0).

## What's new

**Keep your tabs when restarting to update.** After opening and closing another WinThunar
window, Restart Now saves the tabs, active folder and view from the window where you clicked
the button. With tab restoration enabled, that session is restored when the updated app opens.
Restart continues to wait for file transfers and Undo/Redo operations in every window.

**Read the update summary inside the app.** Help > What's New opens a scrollable summary
that follows the selected light/dark theme and links to release notes and downloads.

The tab-loss bug was reproduced in a public 0.5.9-to-0.5.10 installed delta update. The original
two-tab window restarted with the secondary window's single tab despite all 524 deployed files
matching the release. The fix saves the invoking window before the native updater exits, since
that exit bypasses the normal window-close save. A source regression fails before the fix and
passes afterward, including the global busy-history guard.

## Verification tooling and evidence

- Generalized released-upgrade bundles accept recorded public base/target versions, optional
  MSI-only inputs and preserved assets matched by public size/SHA-256.
- Optional desktop endurance runs exercise repeated search, thumbnails and image/text previews,
  recording dispatcher responsiveness, memory, handles and cleanup diagnostics.
- Eight native file/directory Copy/Move cases confirm safe refusal of an unsupported third-party
  name-surrogate reparse tag without modifying source or destination.
- Public 0.5.8-to-0.5.10 per-user full-update, MSI repair/uninstall, generated recovery-journal
  replay and positive TXT/RTF/PDF Windows Search evidence are recorded separately from this release.

The [priority verification report](PRIORITY-VERIFICATION.md) preserves the original public-package
failure and the passing source regression. The pre-release source suite passed 181 service tests
with one symbolic-link privilege skip, 70 desktop checks and two theme-restart checks. Light/dark
What's New renders were inspected. The release was rebuilt from clean merged `main` using
`Publish-Release.ps1 -Version 0.5.11`. The normal build excludes all smoke, endurance, updater
replacement and fault-injection hooks. Builds/tests reported NU1900 when NuGet vulnerability
metadata was unavailable; the release publish and package checks passed.

Both MSI copies have matching hashes, version 0.5.11.0, scope/shortcut choices and the existing
maintenance/migration guards. The embedded native DLL matches the freshly compiled source build;
no deliberate failure action is present. All **15 historical update packages** remain unchanged.
Applying the **683,926-byte** 0.5.10-to-0.5.11 delta reconstructs all **528 archive entries** with
matching names, lengths and SHA-256 digests. Assemblies match across the versioned ZIP, public
portable ZIP and full package.

All five public binary sizes/SHA-256 digests match the verified build. The downloaded modern
feed offers matching 0.5.11 full/delta entries, the compatibility feed offers the matching full
package, latest stable resolves to 0.5.11 and its tag points to the source commit above.
Evidence is retained in `artifacts/verification-0.5.11/`. The installed test of a future update
initiated by the public 0.5.11 Restart Now handler remains pending; the source regression and
earlier installed full/delta cases are recorded separately.

| GitHub binary | Bytes | SHA-256 |
| --- | ---: | --- |
| `WinThunar.App-win.msi` | 93,446,144 | `4665b8cbf37f10718d6f5527358c5916c362e4d332b27a30c1919a375b48c7e0` |
| `WinThunar.App-win-Portable.zip` | 111,932,585 | `8dbe7f1bb5c0889503437f99a351c4b91975909a0875bfe008e370cd1055eaec` |
| `WinThunar.App-win-Setup.exe` | 119,480,711 | `212dbf218d5212adb3fdf498eb816eab2c4d6c62cc233ba0799e07a82519cb74` |
| `WinThunar.App-0.5.11-full.nupkg` | 111,949,191 | `de7b3aac79cc8b7ef3cb6fc93340e91715c3ffe97a78b21be150b8d733d34dac` |
| `WinThunar.App-0.5.11-delta.nupkg` | 683,926 | `351aa4eac56dce7ecb205932666ee8900c4b09c15e8306b4d1ecd8093676db7b` |

## Scope and remaining limits

The fix is contained in the application running **before** Restart Now is clicked. Updating
from 0.5.10 or earlier still uses that version's old restart handler; it cannot repair an earlier
saved session retroactively. Once running 0.5.11, future Restart Now updates use the fixed handler.
When updating from an earlier version, close secondary windows first and the original window
last to use the tested apply-on-close session save.

Automatic app updates do not replace an older cached MSI. Use the latest MSI for installer
safeguards. Legacy per-user MSI migration still requires administrator elevation in the
**same Windows account**; preserve an external backup of the complete user-state directory
before legacy maintenance. Existing legacy all-users migration remains refused.

Hardware and interactive UAC checks remain pending at the maintainer's request. Manual
accessibility review, installed/portable notification delivery, Office/filter coverage, fresh
document extraction, additional native reparse types and slow-network/long-session checks
remain open. The five-minute endurance run completed 933 searches and 622 previews, but retained
elevated memory after cleanup; it does not establish flat memory use or day-long stability.
