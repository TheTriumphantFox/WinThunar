# Priority verification, 23–24 October 2026

This pass verifies current public packages in the offline Windows 11 guest, fixes a reproduced
Restart Now session-loss bug and adds reusable verification tooling. That remedy and the original
tooling are published in 0.5.11; see the [release notes](RELEASE-0.5.11.md) for package status.
The later thumbnail-resource and DOCX follow-ups below are published in 0.5.12; its five public
binary digests, source tag, updater feeds and reconstructed delta are verified separately below.
The 24 October follow-up also verifies the installed public 0.5.11 Restart Now handler updating
to public 0.5.12 with both tabs retained after a secondary window overwrites the saved session.
Maintenance release 0.5.13 publishes the new source helper and updated What's New; its package
and public-feed verification is recorded below, separately from installed upgrade coverage.

## Disposition of the priority list

| Area | Verified in this pass | Remaining work |
| --- | --- | --- |
| Installer/updater | Interactive public 0.5.8 per-user installation; full update to public 0.5.10; first/last-window close behavior; seeded public 0.5.9 delta and actual Restart Now payload/relaunch; fixed public 0.5.11 handler updates to public 0.5.12 with both tabs retained; all 524 deployed hashes | Unseeded delta/cache behavior, public-network feed, Setup, all-users and UAC matrix |
| User-state protection | Public MSI refresh, both repair modes, startup recovery and uninstall retention; positive Shell cleanup | Legacy public-MSI migration under elevation, live Undo workflows and other installation contexts |
| Accessibility | Final 0.5.12 source: 85 desktop checks and two restart checks, including DOCX naming and existing focus/preview coverage | Manual screen-reader, contrast, scaling, pointer and full keyboard review |
| Hardware/network failures | Existing VHD/loopback evidence retained | Separate SMB server, physical removal and separate physical devices deferred at the maintainer's request |
| Notifications | Source desktop harness reproduces registration error `0x8007007E` for the missing Insights resource DLL | Installed/portable delivery and activation; upstream runtime issue remains pending release |
| Document discovery | Generated TXT, RTF and PDF index matches; published 0.5.12 DOCX body-text preview and fresh filesystem content search | DOCX/other Office index filters, representative editor/format coverage, layout and broader previews |
| Endurance and reparse types | Repeated UI/search/thumbnail/preview workload; cleanup/heap diagnostics; eight native unsupported-link refusal cases | Long-session memory behavior, slow network, broader browsing/cloud features and preservation of additional reparse types |

Interactive UAC cases are also deferred at the maintainer's request. These boundaries apply to
this verification pass; existing historical failures remain recorded in their original documents.

## Keyboard and endurance follow-up, 24 October 2026

The follow-up published in 0.5.14 fixes F6 region selection for search, side-pane
children, pathbar editing and split navigation. It adds preview text/PDF/scroller and disabled
command-input targets, restores file-list focus when focused auxiliary controls close, and
announces explicit split/command button actions through automation peers. Pane-local focus
notifications select the appropriate file-command pane, ignoring stale events and other windows.
See [accessibility behavior](ACCESSIBILITY.md#keyboard-follow-up-in-0514).

The original executable with new regression checks fails at **F6 leaves the search input for
the side pane**. The final keyboard/watcher follow-up passes **128 desktop checks and two
theme-restart checks**. The service suite passes **211 tests with one symbolic-link privilege
skip**. These are generated focus/automation checks, not a completed manual accessibility review.
Evidence is in `artifacts/desktop-keyboard-before-20261024/`,
`artifacts/desktop-keyboard-final-20261024/results/` and
`artifacts/keyboard-services-native-20261024/`. The first sandboxed desktop launch lacked WinRT
folder access for screenshots; the normal-desktop baseline reproduced the actual F6 failure.
The sandboxed service runner could not connect to its test host; the normal-desktop rerun passes.

The first attempted 15-minute workload **failed** with a native process termination, exit
`0xC000027B`, before a final report was written. Windows records `Microsoft.UI.Xaml.dll`
3.2.3.0 as the faulting module. The preserved minidump contains stowed HRESULTs `0x8000FFFF`
and `0x80040111`; Microsoft's public symbols resolve the latter stack through activation-factory
lookup, property-path binding, control-template creation and layout. The former stack is absent
from the minidump. This does not establish a root cause or a fixed runtime issue.

Evidence is in `artifacts/desktop-keyboard-endurance-final-20261024/`, including Windows
events, symbolized stack, source hashes and the 25,398,126-byte dump, SHA-256
`1dc7655507deda3e1f92dc251d1abdad9ce43cd78730e6218484ef804d5b4052`.
The crash precedes the separate naming-check app launch. It remains a failed endurance case.

The endurance harness now atomically saves a compact `endurance-progress.json` at each existing
sample (every ten cycles and during cleanup). A native termination can no longer erase every
completed-work/memory counter with the final in-memory report. Checkpoint file I/O is part of
the generated workload, so its timing is diagnostic rather than a controlled comparison with
older runs. The 15-minute rerun also terminates natively: its last checkpoint records **240
cycles, 720 searches and 480 previews** at 172.7 seconds. Private memory rises from 282 to
543 MiB, handles from 3,713 to 6,952, and sampled live entries from zero to 171. Its preserved
dump is **24,919,319 bytes**, SHA-256
`a68bc01ef54ff792d089caa0d68068821db75105f3768c73974bfe1388625a5b`.
See `artifacts/desktop-keyboard-soak-rerun-20261024/`. This reproduces the failure rather
than verifying 15-minute stability. These failures belong to the intermediate global-focus
observer implementation. The final implementation uses pane-local handlers and avoids repeated
border updates when the active pane has not changed. The five-minute comparisons below do not
establish the native crash's cause or a successful 15-minute retest. Day-long and slow-network
coverage remain open.

The pre-change source at `b7fc5c0`, with the file list focused and only test-harness progress
checkpoints/debounce-drain setup added, passes a five-minute comparison: **422 cycles,
1,266 searches and 844 previews**. Private memory rises from about **263 MiB** to **431 MiB**
after three diagnostic collections, handles from **3,584** to **3,666**, and **321** sampled
entries remain alive. Focused retention therefore also occurs before the keyboard changes.
This baseline does not reproduce the native crash or establish its cause. Product-source Git
blob hashes are checked against HEAD in `artifacts/desktop-baseline-focused-b-20261024/comparison-scope.json`.
The first baseline attempt hit the separate watcher race below and remains preserved.

The stale-watcher fix adds a busy guard before a debounced native event starts a refresh.
While another folder is enumerating, `CurrentPath` still names the old folder; its watcher
previously could start `NavigateAsync(CurrentPath)` and cancel the requested navigation.
The generated regression creates an actual filesystem event during the busy state, verifies
that it cannot clear that state or reload old entries, then verifies live watching resumes.
It fails against unchanged pre-change view-model code in
`artifacts/desktop-watcher-before-fix-20261024/` and passes in the final source run.

The final normal x64 publish succeeds; its DLL excludes all five checked keyboard, watcher,
smoke, endurance and restart test markers. Its verification record is in
`artifacts/keyboard-final-normal-build-20261024/verification.json`. That pre-version-bump source build predates the 0.5.14 packaging pass below; its keyboard
and watcher code is now included in published 0.5.14.

The **final source** passes the same five-minute focused local workload: **445 cycles,
1,335 searches, 890 previews, 131 recorded desktop checks and two theme-restart checks**.
Maximum observed dispatcher gap is **265 ms**. Private memory is about **281 MiB** initially,
**421 MiB** at workload end and **417 MiB** after cleanup/three diagnostic collections;
managed memory after cleanup is **38 MiB**. Handles are **3,613** initially and **3,730**
after cleanup; **291** sampled entries remain alive. The baseline and final source both retain
entries, so this does not resolve long-session retention. The native crashes above remain
preserved failures, and their absence in this shorter final run is not proof of a crash fix.

Final evidence is in `artifacts/desktop-keyboard-final-20261024/soak-results/`; source hashes
are in its parent directory. Both final short/workload runs use the same generated executable.
The keyboard render and the workload's icon/final renders were inspected. All generated data
and diagnostic collections are confined to the test build; the normal application does not
force garbage collection or write endurance checkpoints.

## Container-binding follow-up, 24 October 2026

A fresh run of the unchanged 0.5.14 source reproduces native termination `0xC000027B` in
`Microsoft.UI.Xaml.dll` 3.2.3.0. Its last checkpoint records **190 cycles, 570 searches and
380 previews at 136.9 seconds**. The Windows event records and progress are preserved in
`artifacts/priority-endurance-baseline-desktop-20261024/`. The first sandboxed attempt stopped
before the workload because WinRT screenshot-folder access was denied; that is separate from
the normal-desktop native failure. The new Windows report's temporary minidump was already
gone when inspected; no new crash dump is claimed for that run.

The source replaces per-entry accessible-name bindings with one binding to each container's
current `Content.AutomationName`. It no longer captures each successive file entry as the
binding source. A regression against unchanged product code detects that capture; the final
native checks also exercise filename/metadata changes, live Git status, replacement content,
null content and binding reuse in all three layouts and the split pane. The final short run
passes **156 desktop checks and two theme-restart checks**, with both processes exiting zero.
An intermediate test failed because a real background Git query overwrote its synthetic
status; waiting for that query removes the race. Evidence is in
`artifacts/priority-container-binding-before-20261024/`,
`artifacts/priority-container-binding-final-20261024/` and
`artifacts/priority-container-binding-final-b-20261024/`.

The smoke runner now assigns a stable instance key per generated output directory, preserving
restart behavior while preventing another test from redirecting windows into an existing run.
It records desktop/restart process outcomes and rejects nonzero exit codes even when a report
exists. Future endurance samples include pending-finalization, GC heap/commit/fragmentation
counters and runtime/architecture metadata. These additions are test-only; normal builds do
not collect garbage or write these reports.

The service suite passes **211 tests with one symbolic-link privilege skip**. Evidence is in
`artifacts/priority-endurance-services-20261024/`. The longer diagnostic run and resource
measurements are recorded separately below; neither a reduced weak-entry sample nor a single
completed workload establishes a native-crash root cause or day-long stability.

The first changed-build diagnostic completes **971 cycles, 2,913 searches and 1,942 previews**
over fifteen minutes, with **152 recorded desktop checks and two restart checks**. Its inspected
icon render shows focus on the File menu, unlike the crashing baseline's file-row focus, so it
is not a matching focused comparison. It also includes a forced diagnostic heap capture,
thread inspection, an 8.3-second dump pause and concurrent builds/short checks; its dispatcher
maximum is not an application-latency measurement. Private memory is about **274 MiB** initially,
**857 MiB** at workload end and **628 MiB** after cleanup/three diagnostic collections. Handles
are **3,541**, **18,065** and **3,696** respectively; six sampled entries survive cleanup.
The active heap is dominated by COM-wrapper tables, many reachable through pending-finalizer
containers; this does not establish the native crash's root cause. The capture and summary are
in `artifacts/priority-container-binding-fixed-20261024/`.

The final harness explicitly restores file-list focus after its container tests. A short
focused workload on that source passes **33 cycles, 99 searches, 66 previews, 159 desktop
checks and two restart checks**. The final sample records five surviving weak entries and
seven pending finalizations. This verifies the added counters, not long-session stability.
Evidence is in `artifacts/priority-container-binding-counters-20261024/`. Its icon render and
the final short-run render were inspected.

The final normal x64 Release publish succeeds and excludes the six checked smoke, endurance,
container-binding, keyboard, Git-loading and smoke-instance markers; verification is in
`artifacts/priority-container-binding-final-normal-20261024/verification.json`. These changes
are now included in published 0.5.15; its package verification is recorded below. Notification
registration still reports the existing missing Insights resource-DLL error; installed upgrades,
manual accessibility and slow-network/day-long checks
remain open.

The **final focused fifteen-minute run passes** on .NET 10.0.11 x64: **1,186 cycles,
3,558 searches, 2,372 previews, 159 desktop checks and two theme-restart checks**. Desktop and
restart processes both exit zero. The inspected initial icon render confirms file-row focus,
matching the failed baseline's condition. The six checked product/harness document hashes in
the executable's portable PDB match the current source. No debugger, heap capture or forced
collection is used during this run's active workload; its post-work cleanup retains the
existing three diagnostic collection rounds. The first minute overlaps the preceding
diagnostic run's cleanup; these are local diagnostics, not a controlled throughput benchmark.

| Final focused run | Initial | Workload end | After cleanup / third collection |
| --- | ---: | ---: | ---: |
| Private memory | 278 MiB | 372 MiB | 359 MiB |
| Process handles | 3,482 | 3,865 | 3,748 |
| Sampled entries still alive | 0 | 6 of 3,558 | 4 of 3,558 |
| Pending finalizations at the latest GC | 2,259 | 5,974 | 7 |

Maximum sampled dispatcher gap is **288 ms**. Managed memory after cleanup is about **40 MiB**.
The initial raw `GC.GetTotalMemory(false)` sample is negative and is invalid as a managed-size
measurement; it is preserved in the report and is not used for an initial/final comparison.
The positive GC heap/commit/fragmentation counters remain separate measurements.

Evidence, source verification, process outcomes and the complete sample series are in
`artifacts/priority-container-binding-focused-20261024/`. This completes the targeted focused
fifteen-minute source retest. It does not establish the earlier crash's exact root cause or
flat memory use: private and managed memory remain above startup, and the menu-focused
diagnostic behaves differently. Keep broader retention, native-runtime, day-long and
slow-network investigation open. This earlier source-verification pass did not change an
installed copy; the subsequent 0.5.15 publication is recorded below.

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
remedy is published in 0.5.11. The installed update initiated by that version passes the
[24 October retest](#installed-public-0511-restart-now-retest-24-october-2026) below.
An update initiated by an earlier version still uses its old handler.

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

## Thumbnail resource follow-up

Published in 0.5.12; the earlier source measurements below retain their original test counts.

The source image loader now starts at most six workers per batch instead of creating a task
and semaphore waiter for every directory entry. The existing six-call gate still applies across
superseded batches, and cancellation stops workers from starting the remaining entries. Workers
retain the UI context required by the shell images. A 10,000-item service regression covers
bounded concurrency and exactly-once processing; separate regressions cover cancellation and
waiting for other workers before reporting a failure.

Each entry records the size and effective content-thumbnail mode of its successful shell image.
Switching between Details and Compact, or changing text zoom in those layouts, reuses that image.
Icon-size/content-mode changes still load a new image, and changed file metadata replaces the
entry and loads afresh. This retains only the entry's existing bitmap; it adds no global image
cache. The desktop regressions use real shell images to check these paths and superseded requests.

The five-minute follow-up completed **501 cycles**, **1,503 searches** and **1,002 previews**;
its largest observed dispatcher gap was **296 ms**. It passed **78 desktop checks and two
theme-restart checks**. Private memory was about **265 MiB** initially, **347 MiB** at the end of
the workload and **339 MiB** after cleanup. Managed memory after cleanup was about **22 MiB**;
handles were 3,613 initially and 3,696 after cleanup. The first through third diagnostic collection
rounds retained five sampled weak entries and similar managed totals. Collection remains confined
to the test build.

The earlier five-minute run above completed 311 cycles and retained about 553 MiB of private
memory after its first cleanup collection. These are generated local workloads, not a controlled
benchmark or proof of flat/day-long memory use. The new measurements narrow the resource issue
and support the bounded-work/reuse improvement; slow-network endurance and longer sessions remain
open. Evidence is in `artifacts/priority-memory-workers-desktop-20261023/`.

A fresh **60-second original-build** run completed **97 cycles**, passing 73 desktop checks.
It allocated about **19.81 MiB per cycle**, versus **15.76 MiB per cycle** in the updated five-minute
run (workload allocation deltas divided by completed cycles). That is about **20% less allocation
per cycle**, with different run durations. The short original run ended near 311 MiB private memory
and did not reproduce the earlier large retained heap, so the historical memory difference cannot
be attributed solely to this change. Its unchanged original test binary excludes the new worker
pool and thumbnail regressions. Evidence is in `artifacts/priority-memory-baseline-desktop-20261023/`;
the initial restricted-shell attempts were unable to capture a render and are not counted as passes.

The service suite passes **184 tests**, with the existing symbolic-link permission skip and no
failures. The final desktop pass, with endurance disabled, passes **76 checks and two theme-restart
checks**, including the additional superseded-request regression. Its reports and inspected renders
are in `artifacts/priority-thumbnail-final-20261023/`. A normal x64 publish also passes and excludes
the smoke, endurance, thumbnail and restart test entry points; it is in
`artifacts/priority-thumbnail-release-build-20261023/`. These changes are now published in 0.5.12;
the release pass uses 211 service tests, 85 desktop checks and two theme-restart checks.

## DOCX body preview and fresh content search

Published in 0.5.12; source-stage evidence is preserved separately from release package verification.

The source now reads a DOCX's stored main-body text for the read-only preview and filesystem
`content:` search. A generated document with text split across formatting runs matches
`type:docx content:"annual report" content:José` without Windows indexing. Replacing its bytes
and querying again finds the replacement text. The preview exposes Unicode text through a
read-only control named **Selected DOCX body text preview**, and the body-text/truncation status
appears above the content. The source handle closes before parsing. Malformed input and selection
cancellation clear the old content.

The bounded reader resolves the internal main-document relationship, checks the DOCX main content
type, and handles Transitional/Strict namespaces and relocated/root-relative main parts. It caps
the package, decompressed markup, metadata, part count, nesting and extracted text. Generated tests
cover those limits, missing/duplicate/malformed parts, DTD and external-target refusal, escaping
paths, renamed macro-enabled content, cancellation, file locks, source-byte preservation, split
runs, table text and tracked-deletion/instruction exclusion. It neither launches Office nor
resolves external resources. See [DOCX support and explicit limits](DOCUMENT-TEXT.md).

The final service suite passes **211 tests**, with the existing symbolic-link permission skip
and no failures. The desktop suite passes **83 checks and two theme-restart checks**, including
seven DOCX checks; the DOCX and existing PDF renders were inspected after moving the status label.
Evidence is in `artifacts/priority-docx-desktop-20261023/` and
`artifacts/priority-docx-final-20261023/`. The search/stress project also builds successfully. A normal
x64 publish passes and includes the DOCX reader while excluding smoke/thumbnail/DOCX/restart entry
points and the generated-package fixture helper; output is in `artifacts/priority-docx-release-build-20261023/`.

This is fresh main-body text coverage, rather than Word page layout or general Office preview
support. It does not add Windows Search filters: the earlier guest's absent DOCX handler and
Office index-coverage gap remain open. Other Office formats, separate document stories,
representative files from more editors and manual screen-reader review remain open. These additions
are published in 0.5.12; the final desktop release pass also checks What's New in Light and Dark.

## 0.5.12 publication verification

Published 23 October 2026 from clean merged `main`, source `1629c72935ea1faf6075b36a36a660f834faca80`
(PR #31), using `Publish-Release.ps1 -Version 0.5.12`. Both MSI copies have matching hashes, version
0.5.12.0, shortcut/scope choices and the existing repair/migration safeguards. The embedded native
guard matches the final source build, and deliberate failure hooks are absent. Normal packages
contain the DOCX reader and exclude smoke/thumbnail/DOCX/restart entry points and test fixtures.

All five public binary sizes and SHA-256 digests match the verified build. The modern full/delta
feed, compatibility feed, latest stable release and source tag are verified. Applying the
711,758-byte 0.5.11-to-0.5.12 delta reconstructs all 528 archive entries with matching names,
lengths and SHA-256 digests. All 17 historical packages remain unchanged. The release service suite
passes 211 tests with one existing permission skip; 85 desktop and two theme-restart checks pass.
Evidence is in `artifacts/verification-0.5.12/`; see the [release notes](RELEASE-0.5.12.md).

Publication verification alone does not establish the installed Restart Now UI path; the
separate 24 October case below verifies that path. Manual accessibility, notifications, broader
Office/filter coverage and longer/slow-network endurance remain open; hardware/network and
interactive UAC cases remain deferred.

## Installed public 0.5.11 Restart Now retest, 24 October 2026

The existing offline Windows 11 guest was restored from its clean activated baseline after
preserving the previous saved state. All eight network adapters remained disabled. Five test
inputs matched their recorded public GitHub release sizes and SHA-256 digests. The public
0.5.11 MSI installed silently per-user under the unelevated test account with UAC enabled.
The base package was absent from the natural cache and was explicitly seeded; this case does
not verify naturally cached delta selection or the public-network feed.

The real **Help > Check for Updates** command downloaded the 711,758-byte public 0.5.12 delta
from the isolated local feed, with no full fallback. All **528 reconstructed archive entries**
matched the public full package by name, length and SHA-256. The app still ran 0.5.11 before
the requested restart. The original window displayed Source and Destination tabs, Destination
active, with a Source bookmark, Compact List and Light theme.

The actual **File > New Window** command opened a secondary Destination-only window. Closing
that window left the original process alive and saved only Destination in `session.json`.
The original window still visibly held both tabs. Its real **Restart Now** button exited PID
4776, applied 0.5.12 without a UAC prompt and relaunched one process, PID 7516. After native
completion, all **524 deployed files** matched the public target full package. Both tab paths,
active tab/folder, Compact view, Source bookmark, Light theme and the generated document passed
retention checks. The restarted tabs/bookmark and Compact List selection were also inspected
in the actual UI. This closes the released-handler retest; the earlier 0.5.10 loss remains a
valid historical failure.

The reusable `scripts/Test-ReleasedRestartGuest.ps1` prepares the fixture and verifies delta,
secondary-window save, relaunch, deployed files and retained state. Every action rejects the
originating host through the existing bundle preflight before any writes or launches; all five
host-refusal cases and PowerShell syntax validation pass. UI steps remain actual guest actions.
See the [lab instructions](RELEASED-UPGRADE-LAB.md#released-restart-now-regression-helper).

Evidence is under
`artifacts/windows-upgrade-vm-20261021-212229-9192ed86/priorities-restart-20261024/`.
The exported `restart-case-evidence.zip` is **177,528 bytes**, SHA-256
`70c1868ad30e812ad495b5fe18f55da583c7a187befbc4c8a1a9f4690340b17a`.
The host verified its guest digest and all **34** manifest files. Six host-side UI captures,
native logs, package comparisons, the seeded-cache record and retained state are preserved.
Snapshot `published-0511-0512-restart-session-retained-20261024` preserves the passing guest in
**Saved** state. The host's installed DLL, updater and manifest retain their pre-test hashes.

The first host-to-guest ZIP copy reported success but produced zero bytes; preparation refused
it before installation. A read-only ISO supplied the verified inputs instead. One readiness
capture ran before reconstruction finished, and one restart verification ran before native
apply/relaunch completed; both attempts are retained and are not application failures or passes.
The completed captures above establish the result. Setup, all-users/elevation, unseeded cache,
public-network discovery, live Undo and other maintenance contexts remain outside this case.

## 0.5.13 publication verification, 24 October 2026

Published from clean merged `main`, source `23d989f2ac3c7b7d89b88a3b944daf5da071c25d` (PR #33),
using `Publish-Release.ps1 -Version 0.5.13`. This maintenance release updates What's New and
ships the reusable guest helper as source tooling. App packages retain DOCX/thumbnail/session
behavior and exclude the guest helper, smoke/fault entry points and generated fixtures.

The service suite passes **211 tests**, with one existing symbolic-link permission skip;
**87 desktop checks and two theme-restart checks** pass, including the maintenance summary in
Light and Dark. Both renders were inspected. All five guest-helper actions reject the host,
and syntax validation passes. The initial restricted testhost connection timeout ran zero
tests and is retained separately from the passing service rerun.

Both MSI copies match, with version 0.5.13.0, scope/shortcut controls and existing maintenance/
migration safeguards. The embedded native guard matches its final source build, with deliberate
failure hooks absent. All five public binary sizes/SHA-256 digests, modern/compatibility feeds,
latest stable release and source tag are verified. The **345,381-byte** 0.5.12-to-0.5.13 delta
reconstructs all **528 entries** with matching names, lengths and hashes. All **19 historical
packages** retain their hashes. Evidence is in `artifacts/verification-0.5.13/`; see the
[release notes](RELEASE-0.5.13.md).

The installed test above covers public 0.5.11-to-0.5.12; package verification does not establish
an installed upgrade to 0.5.13. Other install/maintenance contexts, manual accessibility,
notifications, broader Office/filter coverage and longer/slow-network endurance remain open.
Hardware/network and interactive UAC cases remain deferred.

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

## 0.5.14 publication verification, 24 October 2026

Source `e28cde2f4af9dddd76db51c39218c51fe26e84e9` (PR #35) was packaged from clean merged `main` using
`Publish-Release.ps1 -Version 0.5.14`. The versioned suite passes **211 service tests with one
symbolic-link privilege skip, 128 desktop checks and two theme-restart checks**. Light/Dark
What's New renders were inspected. Test builds may report NU1900 when vulnerability metadata
is unavailable. The earlier source workload and native failures above remain separate evidence.

All five public binary sizes/digests, both MSI copies and their embedded native guard, the
source tag, both update feeds and latest stable are verified. The **701,691-byte** delta
reconstructs all **528 entries** and all **21 retained historical packages** keep their
hashes. Package assemblies agree and exclude smoke/fault/keyboard/watcher test entry points.
See `artifacts/verification-0.5.14/` and [release notes](RELEASE-0.5.14.md).

This establishes publication and updater-feed availability, not an installed upgrade to 0.5.14,
a native-crash fix or completed manual accessibility/longer endurance. Prior retained-entry
measurements and native failures are preserved, with hardware/network and interactive UAC deferred.

## 0.5.15 publication verification, 24 October 2026

Source `5e01f89186f3de48ccf13cdf2a9b9a80e776ee73` (PR #37) was packaged from clean merged `main` using
`Publish-Release.ps1 -Version 0.5.15`. The versioned suite passes **211 service tests with one
symbolic-link privilege skip, 158 desktop checks and two theme-restart checks**. Light/Dark
What's New renders were inspected. The earlier fifteen-minute source workload is separate
evidence and does not establish a general native-crash fix or flat memory use.

All five public binary sizes/digests, both MSI copies and embedded native guard, source tag,
both updater feeds and latest stable are verified. The **505,042-byte** delta reconstructs
all **528 entries** and all **23 retained historical packages** keep their hashes.
Application assemblies agree and checked test/fault hooks are absent. See
`artifacts/verification-0.5.15/` and [release notes](RELEASE-0.5.15.md).

Installed upgrade to 0.5.15, broader memory/runtime, manual accessibility, notifications and
day-long/slow-network coverage remain open. Hardware/network and interactive UAC remain deferred.
