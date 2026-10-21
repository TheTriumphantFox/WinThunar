# WinThunar 0.5.5

Prepared 21 October 2026 for Windows 11 x64. Publication is pending. This release includes the
unpublished 0.5.4 updater candidate and the complete recovery, storage, search, and preview work.
The latest public release remains 0.5.3 until the GitHub upload completes.

## Changes since 0.5.3

- Updater initialization and installer-launch failures become retryable errors instead of
  preventing startup or escaping the update button handler. A failed launch retains its package.
- **Help > Interrupted Transfers...** and a persistent warning expose selectable recovery paths,
  errors, and safe retry. Recovery runs off the UI thread and protects active transfers/history.
- Recovery verifies the prepared destination content, including NTFS streams, before discarding
  displaced originals. Unavailable storage retains cleanup journals. Source cleanup after a
  replacement Move commits cannot roll the completed transfer back.
- `content:word` and `content:"a phrase"` scan supported text files up to 16 MiB. This PC can search
  its filesystem drive listing, sharing one result limit across the selected scope.
- Explicit `index:true` queries the read-only Windows Search catalog, including indexed document
  contents when Windows provides suitable filters. Status explains index coverage and freshness.
- **View > Preview Pane** supports selectable text and native PDF pages. PDFs have Previous/Next
  controls, a 64 MiB input limit, and a 1,024-pixel output cap even on scaled displays. Source file
  handles close before rendering. Detailed filenames retain visible space beside the preview.
- Notification registration is idempotent and detaches activation handlers after failure.
- New guarded SMB-interruption and search-performance harnesses preserve their evidence and
  complement the disposable NTFS VHD, process-termination, service, and desktop checks.

## Verification

- Service tests: **162 passed, one skipped, zero failed**. The skip needs Windows symbolic-link
  privilege. Includes nine updater regressions and recovery/content/index scope/cancellation cases.
- Normal-token storage stress: **30 passed, five skipped**. Disposable NTFS VHD: **40 passed,
  two skipped**. Disposable loopback SMB: **five passed**. All have zero failures in final runs.
- Four actual disk-full cases returned native error 112 after **8,388,608 reported copy bytes**,
  preserving originals/alternate streams and passing recovery, retry, and Undo.
- Four owned target-share interruptions returned native error 67 after **1,048,576 reported
  bytes**. Another case verifies source loss after a replacement Move commits.
- **50 desktop checks** pass, including PDF pages/errors/cancellation/handle release, text preview,
  recovery, This PC routing, and native Windows index queries. Rendered views were inspected.
- A native index query returned and validated an existing filesystem metadata row and accepted
  quoted content SQL. Windows did not index the generated fixture, so a positive generated
  PDF/Office content-indexing result remains unverified.
- Ten generated 10,000-file searches establish a warmed local service baseline; see the
  [reliability milestone](RELIABILITY-MILESTONE.md) for timings and evidence paths.

Packaging validation and public asset/feed verification will be recorded here after the release
build. Test-project builds report NU1900 when NuGet vulnerability metadata is unreachable;
restoration and compilation completed.

## Known limits

Undo remains window-local and multi-item replay is not crash-atomic. VHD and loopback SMB tests
use one host/SSD; remote-server/network outages, physical removal, separate-device history, and
power loss remain unverified. Ambiguous recovery copies and journals are preserved.

Indexed contents can be stale or absent and depend on Windows indexing and installed filters.
Default content search is limited to supported text formats. Office/general Shell preview handlers,
PDF text selection, and PDF password entry remain open.

Notification registration still fails on this machine with HRESULT 0x8007007E and the missing
`Microsoft.WindowsAppRuntime.Insights.Resource.dll` diagnostic, matching the
[Windows App SDK issue marked pending release](https://github.com/microsoft/WindowsAppSDK/issues/6774).
Manual accessibility/Shell/device/cloud and long-running UI/network checks remain open.

Read-only evidence confirms the earlier installed all-users 0.5.0-to-0.5.3 full-package upgrade.
A controlled 0.5.5 upgrade, per-user apply-on-close, installed delta updates, UAC cancellation,
and Windows Installer repair/uninstall remain unverified. The original MSI product entry reports
0.5.0.0 while Velopack's entry reports 0.5.3. See [updater verification](UPDATER-VERIFICATION.md).

## Packaging and publication

Project, assembly/file, manifest, release-script default, and README example versions are aligned
at **0.5.5** (four-part versions **0.5.5.0**). Build from the merged main checkout with:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Publish-Release.ps1 -Version 0.5.5
```

Preserve `artifacts/installer`, all previous packages, retained VHDs, and recovery evidence.
The earlier unpublished 0.6.0 assets remain archived outside the upload directory.
