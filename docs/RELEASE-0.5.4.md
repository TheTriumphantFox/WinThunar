# WinThunar 0.5.4 candidate (superseded)

Prepared 21 October 2026. This version is **not published**. The latest published application
remains [0.5.3](https://github.com/TheTriumphantFox/WinThunar/releases/tag/v0.5.3).
Committing this candidate does not distribute it to installed users.

This unpublished candidate and the subsequent local reliability/search/preview work below are
superseded by **[0.5.5](RELEASE-0.5.5.md)**. Use that release's notes for current version and
publication status; 0.5.4 was never published.

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

Additional local work adds **Help > Interrupted Transfers...**, a persistent recovery warning,
safe retry, and destination-content proof before displaced data is discarded. Unavailable storage
cannot silently erase a staging journal, and post-commit source cleanup cannot roll a completed
replacement Move back. See the [reliability milestone](RELIABILITY-MILESTONE.md) for findings.

Explicit `content:` terms search supported text files up to 16 MiB. Search from This PC spans
its filesystem drive listing. The preview pane supports bounded read-only text, and detailed
filenames keep visible space when panes narrow. A new performance harness measures generated
local search workloads. PDFs up to 64 MiB now have native page previews with Previous/Next
navigation and a 1,024-pixel output cap. Explicit `index:true` queries the read-only Windows
Search catalog, including indexed document contents when Windows provides the relevant filters.
Status warns that index coverage/freshness differs from filesystem traversal; see [search](SEARCH.md).

Notification initialization is idempotent and detaches activation handlers after registration
failure. Diagnostics include the HRESULT. Registration still fails here with the upstream
[self-contained Windows App SDK issue](https://github.com/microsoft/WindowsAppSDK/issues/6774),
which is marked pending release; no unverified DLL substitution or SDK upgrade was made.

## Verification and limits

- The startup regression failed against the previous source with `UriFormatException` and passes
  after the fix.
- The x64 application build passed without warnings/errors. The service suite passed with
  **162 passed, one skipped, zero failed**; symbolic-link creation requires Windows privilege.
- Normal-token stress passed **30/0/5** (passed/failed/skipped). Disposable-VHD stress passed
  **40/0/2**, including native disk-full error 112 after 8 MiB copy progress. Disposable loopback
  SMB passed all **five** cases, including loss after commit. Desktop smoke assertions passed.
- Read-only inspection separately confirmed the existing all-users **0.5.0 to 0.5.3** full-package
  upgrade from updater logs and all **524 deployed file hashes**. This is evidence for 0.5.3, not
  an installed 0.5.4 upgrade. No user application was launched, closed, replaced, or downgraded.
- The original Windows Installer product entry still reports 0.5.0.0 while Velopack's uninstall
  entry reports 0.5.3. Repair/uninstall behavior remains unverified.
- Controlled upgrade UI repetition, per-user apply-on-close, installed delta updates, UAC
  cancellation, and updater child-process failures after launch remain unverified. See
  [updater verification](UPDATER-VERIFICATION.md) for evidence and scope.
- Remote-server/network outage, physical removal, separate-device, accessibility, notification,
  and multi-item crash-atomicity limits remain; see the
  [roadmap](ROADMAP.md) and [stress notes](FILE-OPERATIONS-STRESS.md).

## Version and publication status

The project, assembly/file versions, package manifest, release-script default, and README build
example are aligned at **0.5.4** (four-part versions **0.5.4.0**). Release packaging and publication
have not been performed. Preserve all earlier packages in `artifacts/installer` for future deltas.
See [release publishing](RELEASES.md) when a release is authorized.
