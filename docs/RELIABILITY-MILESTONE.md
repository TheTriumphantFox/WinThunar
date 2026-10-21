# Reliability and discovery milestone

Local source work on 21 October 2026 for 0.5.5, following the unpublished 0.5.4 updater candidate. These additions are
included in published **[0.5.5](RELEASE-0.5.5.md)**. The installed application was not changed by this workflow.

## Completed work

- **Interrupted Transfers:** a persistent warning and **Help > Interrupted Transfers...** expose
  errors, source/destination paths, retained-copy paths, and the journal path in selectable,
  read-only details. Recovery runs off the UI thread. Retry protects active transfers and Undo
  history while allowing another attempt at a failed recovery claimed by this process.
- **Conservative recovery:** a new journal records the complete staged destination snapshot
  before destructive replacement/history renames. Before deleting displaced originals, recovery
  checks the completed destination, including alternate streams. Edited destinations and older
  records without proof retain all copies and report an issue.
- **Storage availability:** an absent leaf on an unavailable share/device no longer counts as
  successful cleanup. Recovery verifies containing folders before treating staging data as absent.
  Confirmed source cleanup is recorded before an empty merge-source folder is removed. Cleanup
  failure after a replacement Move commits cannot roll that completed transfer back.
- **Text discovery:** explicit `content:` terms search supported text files, and This PC can
  search the filesystem drives in its current listing. Scopes survive successive queries and
  share one result limit. Closing search restores the drive listing.
- **Preview:** the existing pane supports bounded, read-only text and native PDF pages as well
  as images. PDF input is limited to 64 MiB, output to 1,024 physical pixels per side, with
  Previous/Next controls and source handles released before rendering. Details scroll in small
  windows. Both detailed lists reserve space for filenames when panes narrow.
- **Windows index:** explicit `index:true` queries the read-only local catalog with SQL literal
  quoting, scope validation, current metadata checks, hidden-folder handling, deduplication,
  candidate/result limits, and an explicit coverage/freshness warning. Indexed content uses
  Windows' document filters rather than WinThunar reopening document bytes.
- **Notifications:** registration is idempotent, failed registration detaches its activation
  handler, and diagnostics retain the HRESULT. The underlying runtime registration issue remains.
- **Repeatable evidence:** disposable SMB interruption and search-performance scripts supplement
  the VHD, native service, process-termination, and desktop smoke suites.

## Verification

| Check | Result | Scope |
| --- | --- | --- |
| Service suite | 162 passed, one skipped, zero failed | Skip needs Windows symbolic-link privilege. Includes thirteen new indexed-search cases beyond the prior 149-pass milestone. |
| Normal-token stress | 30 passed, five skipped, zero failed | Native injected errors, sharing locks, copy/delete, owned worker termination, history ordering, nonblocking live journal registration. |
| Disposable NTFS VHD | 40 passed, two skipped, zero failed | Distinct-volume history and eight actual disk-full cases. |
| Disposable loopback SMB | Five passed, zero failed | Four target interruptions after progress, plus source loss after a replacement Move commits. |
| Desktop smoke | Assertions passed; renders inspected | Recovery, text/PDF previews, page controls, cancellation/errors, native index queries, This PC routing and existing checks. |
| Local search baseline | Ten complete searches over 10,000 generated files | Five filename and five content searches, cancellation, allocation/retention diagnostics. |

The four new disk-full cases use an 8 MiB main stream and an 8 MiB alternate stream. The disposable
target has space for the main stream but insufficient space for both. Each Copy/Move, new/Replace
case returned **native error 112 after 8,388,608 reported copy bytes**, preserved originals/streams,
and passed recovery, retry, and Undo. No error was injected. This establishes a real failure after
main-stream writes, rather than exhaustion partway through that same main stream.

The SMB harness removes only its uniquely owned, current-account-only loopback share during a
native copy callback. All four combinations returned **native error 67 after 1,048,576 reported
bytes**. It also disconnects the source after a replacement Move commits and verifies recovery
finishes cleanup without rolling back the committed data. Move's copy/delete route is forced only
in the test build. No existing shares, SMB sessions, services, or firewall settings are changed.
All test shares were removed afterward; their backing data and evidence are preserved.

The first SMB run reproduced lost cleanup tracking: `File.Exists` returned false after disconnection,
and new-file Copy/Move erased their journals while staging files remained on the server. The
failed fixtures remain intact. Another retained stress run records the overly conservative initial
source-parent check; persisting confirmed source cleanup fixed that history-expiry regression.

The search baseline used warmed local filesystem caches. Filename searches completed in
**0.778–1.031 seconds**, with first batches in **3.8–44.4 ms**. Text-content searches completed in
**2.080–2.308 seconds**, with first batches in **9.6–13.4 ms**. Cancellation after the first batch
completed in **6.2 ms from search start**. Retained managed-byte differences after forced collection
were small in this workload; this is not evidence of day-long memory stability or UI throughput.

Local evidence:

```text
artifacts/fileops-realstorage-20261021-123301-d75f6d91/  # final passed; VHD detached
artifacts/fileops-smb-20261021-122213-2dabc5bc/          # five passed; share removed
artifacts/fileops-smb-20261021-120613-89f15883/          # reproduced cleanup failure; share removed
artifacts/fileops-stress-20261021-120926-c6d61be8/       # intermediate history-expiry failure
artifacts/fileops-stress-20261021-123128-608859e2/       # final normal-token stress passed
artifacts/desktop-candidate-final-20261021/             # desktop renders/results and final service XML
artifacts/desktop-documents-final-20261021-b/           # extended PDF/index desktop checks and current service XML
artifacts/search-performance-20261021-121745-618b2a13/  # report; generated files cleaned
artifacts/document-discovery-milestone-20261021.json   # current document/index evidence and normal-build hash
artifacts/verification-0.5.5/verification.json         # published assets/feed, delta reconstruction, fresh service results
artifacts/fileops-release-0.5.5/                       # fresh 30/0/5 normal-token stress
artifacts/desktop-release-0.5.5/                       # fresh 50-check desktop run
```

Earlier VHDs, installer packages, historical previews, and recovery evidence are preserved.
The normal x64 build passed without warnings/errors; its assembly is **0.5.5.0**, with stress-hook
and desktop-smoke markers absent. The test-project build still reports NU1900 because NuGet
vulnerability metadata was unavailable from the sandbox; restore and compilation completed.

## Remaining completion work

Real remote-server/network timeouts, physical removal, and separate-physical-device history still
require a suitable disposable external setup. The loopback SMB test uses the UAC token, one host,
and one SSD; it does not establish Wi-Fi/cable loss or server restart behavior. The VHD tests do
not simulate power loss. Multi-item history remains window-local and is not crash-atomic.

Default text content search remains a bounded filesystem scan. Explicit Windows index queries
were verified through the native provider, including a positive existing file metadata row and
accepted quoted content SQL. Windows did not index the generated fixture (zero matches); fresh
PDF/Office extraction and a positive generated document-content result remain unverified.
Office/general Shell preview handlers, PDF text selection, and password entry remain open.
See [search scope](SEARCH.md) and [preview bounds](BROWSER-WORKSPACE.md).

Full manual screen-reader, contrast, scaling, pointer/Shell/Recycle Bin, and elevation workflows
remain open. Notification registration still fails on this machine with the missing
`Microsoft.WindowsAppRuntime.Insights.Resource.dll` diagnostic. The matching
[Windows App SDK issue](https://github.com/microsoft/WindowsAppSDK/issues/6774) is marked pending
release; upgrading to an unverified package or substituting a DLL was not attempted.
Broader Shell/device/cloud browsing,
unsupported reparse types, Win+E integration, long-running UI/network endurance, and the remaining
installer lifecycle checks also remain on the [roadmap](ROADMAP.md). Passing service/smoke tests
does not establish a complete Explorer replacement.
