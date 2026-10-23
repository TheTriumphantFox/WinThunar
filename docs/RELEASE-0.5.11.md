# WinThunar 0.5.11

Prepared for Windows 11 x64. Publication and public-package verification are pending.

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
What's New renders were inspected. Final release build, package, delta and public-feed checks
will be recorded after completion.

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
