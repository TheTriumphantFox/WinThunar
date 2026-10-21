# WinThunar 0.5.4 candidate

Prepared 21 October 2026. This version is **not published**. The latest published application
remains [0.5.3](https://github.com/TheTriumphantFox/WinThunar/releases/tag/v0.5.3).
Committing this candidate does not distribute it to installed users.

## Changes

Updater initialization failures no longer prevent the application window from opening. A malformed
`WINTHUNAR_UPDATE_FEED` previously threw `UriFormatException` during startup. Initialization and
pending-package failures now become a failed update state and can be retried through a later check.
An initialization failure is not mistaken for a portable installation.

If launching the downloaded update installer fails, the window reports the error and retains the
ready package for retry. Exceptions while scheduling an update on last-window close are contained.
The existing active-transfer guard and all-users elevation behavior remain in place.

Nine updater regression tests use an internal fake backend to exercise failure/retry behavior,
concurrent checks, portable copies, failed-check scheduling, and all-users exit behavior without
installing updates or exiting the test process. The production backend still uses Velopack 1.2.161.

## Verification and limits

- The startup regression failed against the previous source with `UriFormatException` and passes
  after the fix.
- The x64 application build passed without warnings/errors. The service suite passed with
  **134 passed, one skipped, zero failed**; symbolic-link creation requires Windows privilege.
- Read-only inspection separately confirmed the existing all-users **0.5.0 to 0.5.3** full-package
  upgrade from updater logs and all **524 deployed file hashes**. This is evidence for 0.5.3, not
  an installed 0.5.4 upgrade. No user application was launched, closed, replaced, or downgraded.
- The original Windows Installer product entry still reports 0.5.0.0 while Velopack's uninstall
  entry reports 0.5.3. Repair/uninstall behavior remains unverified.
- Controlled upgrade UI repetition, per-user apply-on-close, installed delta updates, UAC
  cancellation, and updater child-process failures after launch remain unverified. See
  [updater verification](UPDATER-VERIFICATION.md) for evidence and scope.
- Existing storage, accessibility, notification, and recovery limits remain; see the
  [roadmap](ROADMAP.md) and [stress notes](FILE-OPERATIONS-STRESS.md).

## Version and publication status

The project, assembly/file versions, package manifest, release-script default, and README build
example are aligned at **0.5.4** (four-part versions **0.5.4.0**). Release packaging and publication
have not been performed. Preserve all earlier packages in `artifacts/installer` for future deltas.
See [release publishing](RELEASES.md) when a release is authorized.
