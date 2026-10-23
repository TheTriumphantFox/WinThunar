# Priority verification, 23 October 2026

This pass verifies current public packages in the offline Windows 11 guest, fixes a reproduced
Restart Now session-loss bug and adds reusable verification tooling. These changes are published
in 0.5.11; see the [release notes](RELEASE-0.5.11.md) for publication and package-verification status.

## Disposition of the priority list

| Area | Verified in this pass | Remaining work |
| --- | --- | --- |
| Installer/updater | Interactive public 0.5.8 per-user installation; full update to public 0.5.10; first/last-window close behavior; seeded public 0.5.9 delta and actual Restart Now payload/relaunch; all 524 deployed hashes; session fix published in 0.5.11 with verified packages/feed | Installed update initiated by the fixed public handler; unseeded delta/cache behavior, Setup, all-users and UAC matrix |
| User-state protection | Public MSI refresh, both repair modes, startup recovery and uninstall retention; positive Shell cleanup | Legacy public-MSI migration under elevation, live Undo workflows and other installation contexts |
| Accessibility | 70 desktop checks and two restart checks, including existing names/focus/preview coverage | Manual screen-reader, contrast, scaling, pointer and full keyboard review |
| Hardware/network failures | Existing VHD/loopback evidence retained | Separate SMB server, physical removal and separate physical devices deferred at the maintainer's request |
| Notifications | Source desktop harness reproduces registration error `0x8007007E` for the missing Insights resource DLL | Installed/portable delivery and activation; upstream runtime issue remains pending release |
| Document discovery | Generated TXT, RTF and PDF content matches from the guest's native Windows Search provider | DOCX/other Office filters, representative format coverage, fresh extraction and broader previews |
| Endurance and reparse types | Repeated UI/search/thumbnail/preview workload; cleanup/heap diagnostics; eight native unsupported-link refusal cases | Long-session memory behavior, slow network, broader browsing/cloud features and preservation of additional reparse types |

Interactive UAC cases are also deferred at the maintainer's request. These boundaries apply to
this verification pass; existing historical failures remain recorded in their original documents.

## Public per-user installer lifecycle

The base MSI was selected by its recorded public SHA-256 digest. Its actual scope and shortcut
pages were exercised, choosing **Install just for you** and both shortcuts. Installation completed
without UAC. No full package was naturally present in the cache, and no base was seeded for this case.
The real **Help > Check for Updates** command offered 0.5.10 and downloaded its full package.

Closing the secondary single-tab window left the original PID and 0.5.8 payload intact. Closing
the original two-tab window applied 0.5.10 without UAC or an automatic relaunch. All **524 deployed
files** matched the public full package. Both tab paths, active tab, bookmark, Compact view, Dark
theme, plugin fixture and generated document survived.

The cached public 0.5.8 MSI refused both `/fomus` and `/fa` repair with the expected guard message
and exit 1603. Each refusal left all 524 files and retained state unchanged. Installing the public
0.5.10 MSI then replaced the MSI registration and cache. Its standard and forced repairs passed,
with all 524 files and six seeded state files unchanged after each operation.

Two valid recovery journals were retained through MSI refresh and repair. Starting the repaired
public application restored a displaced original when its destination was absent and removed
its incomplete staging data. The ambiguous case preserved the edited destination, source,
incoming data, displaced original and journal. The actual warning and **Interrupted Transfers**
review dialog were observed. These are generated journal fixtures, rather than a new forced-crash
or live-Undo migration experiment.

Uninstall with explicitly enabled default-folder integration retained all five remaining state
files, the unresolved recovery journal and generated documents. It removed the app, both uninstall
entries, both shortcuts and all three owned Shell verbs, and restored default folder handling.
The MSI's Windows Installer entry was under HKLM even though the install scope and payload were
per-user; the harness checks both registry hives.

Evidence is under
`artifacts/windows-upgrade-vm-20261021-212229-9192ed86/priorities-20261023/`.
The exported `full-case-evidence.zip` is **952,867 bytes**, SHA-256
`69a92f479b074fa83962985b2590ebc86d1365f40040e5c1746fba71b541649a`.
The host verified its guest digest and all **87** files in the embedded manifest. Snapshot
`published-0510-maintenance-recovery-uninstall-passed-20261023` preserves the completed guest case.

An initial file comparison ran while the updater was still applying. Its failure was retained,
and verification was repeated after waiting for native completion. An initial maintenance lookup
stopped before MSI execution because it inspected HKCU only; that attempt is preserved separately.
Neither attempt is counted as an application failure or a passing check.

## Installed delta and Restart Now session fix

A separate clean guest case installed the public 0.5.9 per-user MSI and explicitly seeded its
public full package because no base package was naturally cached. The real Help command downloaded
the public 0.5.10 delta without a full fallback. All **528 reconstructed package entries** matched
the public full package by name, length and SHA-256. Its ZIP container digest differed after
reconstruction; content equivalence was checked entry by entry.

The original window had Source and Destination tabs, with Destination active. Opening and closing
a secondary single-tab window replaced the shared saved session while leaving the original
process and its two tabs open. Clicking the original window's actual **Restart Now** button
updated and relaunched one 0.5.10 process without UAC. All **524 deployed files** matched. The
restarted application visibly had only Destination: the published Restart Now path lost the
original tab session. Compact view, the Source bookmark and Light theme remained. This is a
payload/relaunch pass and a session-retention failure, not a complete persistence pass.

The local source remedy calls `SaveSession()` after the global busy-operation guard and before
`ApplyAndRestart()`, because the native updater exits without the normal window-close save.
A regression invokes the real button with a test-only updater backend. It first reproduces the
secondary-window overwrite, checks that history work in another window blocks restart without
changing the session, and then verifies the invoking window's two tabs, active folder and view
at the moment the updater would exit. It failed before the fix and passes after it.

Evidence is in `artifacts/priority-restart-before-fix-20261023/`,
`artifacts/priority-restart-fixed-20261023/` and the guest's
`priorities-delta-20261023/` directory. The fixed source suite passes **70 desktop checks and two
theme-restart checks**. A normal x64 publish also passes and excludes the updater replacement,
restart regression and endurance entry points. The public 0.5.10 packages are unchanged; the
remedy is published in 0.5.11 and requires an installed update initiated by that version for
the remaining public-handler retest. An update initiated by an earlier version still uses
its old handler.

The exported `priorities-delta-20261023/delta-case-evidence.zip` is **172,195 bytes**, SHA-256
`0c29bdafaecd6e8c27789e59def5d492029e8975a34588fc8ac9ca56b6b7f823`.
The host verified the guest digest and all **27** manifest files. Snapshot
`published-059-0510-delta-restart-session-loss-20261023` preserves the failed persistence case.
The VM was saved with all eight network adapters disabled.

## Desktop endurance and cleanup diagnostics

Run the isolated workload with:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-DesktopSmoke.ps1 -EnduranceSeconds 300
```

The workload uses 300 generated text files and 60 generated PNGs. It repeats image/name/content
queries, all three layouts, scrolling, zoom and image/text previews. It records result counts,
thumbnail observations, process memory/handles, GC counts and a 100 ms dispatcher heartbeat.
After the workload it clears the listing, lets pending work drain and collects diagnostic samples.
Forced collection exists only in the test build. Optional `-DiagnosticHoldSeconds` writes a PID/path
marker and holds that test process for an external heap diagnostic.

The final five-minute run completed **311 cycles**, **933 searches** and **622 previews**, with
70 desktop checks and two restart checks passing. Its largest observed dispatcher gap was
**361 ms**. Handles grew from 3,646 to 7,728 during work, then fell to 3,783 after idle and 3,697
after collection. Private memory remained about **553 MiB**, versus **250 MiB** initially; managed
memory also remained elevated after that first collection. A separate shorter diagnostic released
more managed objects during heap collection. Those results support further investigation of
cleanup/cache behavior; they do not establish flat memory use or day-long stability.

Reports and inspected renders are in `artifacts/priority-desktop-long-drain-20261023/`, with the
earlier workload, shorter drain run and owned-process heap graph in the adjacent
`priority-desktop-endurance-20261023/`, `priority-desktop-drain-20261023/` and
`priority-desktop-heap-20261023/` directories. These are local workloads with warmed caches.

## Document filters and unsupported links

The guest's default Documents location indexed the generated token in TXT, RTF and PDF fixtures
without an indexing-configuration change. DOCX did not match, and no `.docx` `PersistentHandler`
registration was found in that guest. The exported native-query report and all four fixtures are
included in the full-case evidence. This verifies a positive generated PDF result from the native
provider, rather than every document filter or a fresh extraction from current document bytes.

`UnsupportedReparsePointTests` creates a real NTFS third-party name-surrogate tag with its own GUID.
All eight file/directory, Copy/Move, new/Replace combinations refuse unsupported preservation,
offer the Windows-assist path and retain the source and destination. The fixtures do not install
a provider or enable symbolic-link privilege. Additional reparse types remain outside native
preservation support. The buffer format follows Microsoft's
[reparse-tag documentation](https://learn.microsoft.com/windows/win32/fileio/reparse-point-tags)
and [GUID buffer definition](https://learn.microsoft.com/windows/win32/api/winnt/ns-winnt-reparse_guid_data_buffer).

The final service suite passed **181 tests**, with one symbolic-link privilege skip and no
failures. The normal x64 publish build after the restart fix passed, and its DLL excludes
smoke/endurance/restart test entry points. Builds report NU1900 because vulnerability metadata was unavailable from NuGet.
The [notification runtime issue](https://github.com/microsoft/WindowsAppSDK/issues/6774) remains
marked pending release; no runtime substitution was made.
