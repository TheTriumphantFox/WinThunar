# Stability investigation, 24–27 October 2026

**Current status:** the remaining handle growth was Windows.Storage's brokered `StorageFile`
calls retaining impersonation Token handles. With thumbnails and image previews moved off
that path, **the one-hour Files and one-hour Menu workloads both pass the resource gate** with
no crash. On the final source, on mains power with no standby, the medians change by −10 and
−2 handles (limit 128) and −0.45 and +4.34 MiB of private memory (limit 8 MiB), with at most
one Token handle. See
[Token-handle cause and storage-free thumbnails](#token-handle-cause-and-storage-free-thumbnails-26-october)
and [Visible-only binding and final-source gates](#visible-only-binding-and-final-source-gates-27-october).
One launch in about 100 of the earlier source hit a WinUI virtualization access violation
(`0xC0000005`). Each hidden file view is now unbound from the entries collection. The trigger
is unproven, but 100 launches of the changed source are clean. **A 24-hour soak of the final
source completes with no crash**, past the point where the historical `0xC000027B` failure
happened. Handles stay flat, and private memory grows 12.3 MiB over the day. Three UI-thread stalls of 24–53 seconds during the soak came from WinThunar starting `git.exe`
on the UI thread while Windows held the file lookup that process creation does. Git status now
runs entirely on the thread pool, refreshes once a minute instead of every 10 seconds, and can
be turned off in Preferences. See [UI freezes from Git process
starts](#ui-freezes-from-git-process-starts-28-october).

This is source work following published 0.5.15. It has not been published or applied to
installed copies.

## Confirmed retained-memory cause

The bundled .NET 10.0.11 runtime has the COM-wrapper diagnostic-registry regression in
[dotnet/runtime#129191](https://github.com/dotnet/runtime/issues/129191). On each native
crossing it appends the cached wrapper to a list associated with the managed object,
including when that wrapper is already recorded. The long-lived object keeps the duplicate
list alive. The [10.0.11 source](https://github.com/dotnet/runtime/blob/v10.0.11/src/libraries/System.Private.CoreLib/src/System/Runtime/InteropServices/ComWrappers.cs)
still contains the unconditional append. The upstream
[deduplication fix](https://github.com/dotnet/runtime/pull/133292) is merged into the servicing
branch; its milestone is 10.0.13. A merged fix does not establish its presence in our runtime.

This mechanism is directly observed in WinThunar's post-cleanup full heap, not inferred just
from a private-memory graph. After a five-minute UI workload and three diagnostic collection
rounds, the file-entry collection has **778,836 registry records for one distinct wrapper**,
with **1,048,576 array slots**. Another string has 135,828 records for one wrapper. The
diagnostic wrapper arrays occupy **10,688,520 bytes**. Excluding free heap space, enumerated
managed objects occupy **15,751,570 bytes**. Filesystem entries and recycled XAML templates
also remain, but are a separate, bounded-cache/retention question.

Evidence: `artifacts/stability-bounded-files-20261025/after-cleanup.dmp`,
`ccw-lists.json`, `heap-statistics.json`, and `after-cleanup.gcdump`.
The full dump is the authoritative source for the counts: gcdump warns that .NET 10 lacks
its newer non-lossy event buffering. The capture occurs during the explicit diagnostic hold,
after active work and cleanup. Its dispatcher pause is not an application-latency measurement.

## Source workaround and verification

Release builds set `System.Diagnostics.Debugger.IsSupported=false` through
`RuntimeHostConfigurationOption`, the documented runtime workaround. Debug builds retain
the existing policy. This disables managed-debugger support in Release, including the
diagnostic registry; it does not add forced GC, reflection into runtime internals, or cache
trimming to application code. Reassess the switch when the bundled runtime contains the
upstream deduplication fix. Native crash dumps and offline managed-heap enumeration remain
available, although the disabled registry no longer supplies its managed-to-COM associations.

`tests/WinThunar.RuntimeProbe` isolates the cause using one long-lived object and two million
balanced COM calls. Every returned pointer is released. Diagnostic collections occur only
in the probe. With the normal 10.0.11 policy it retains **2,000,000 records** and about
**16 MiB** of managed memory. Using the generated normal application's actual runtimeconfig,
deps file and bundled runtime, the same probe retains **zero records** and about **0.3 MiB**.
Both explicit expectation checks pass. The probe uses reflection solely to inspect the known
.NET 10 registry and is excluded from the application project.

Evidence: `artifacts/runtime-registry-probe-final-20261025/`. To repeat:

```powershell
dotnet build .\tests\WinThunar.RuntimeProbe\WinThunar.RuntimeProbe.csproj -c Release -o .\artifacts\runtime-probe
dotnet .\artifacts\runtime-probe\WinThunar.RuntimeProbe.dll --expect-duplicates
# After publishing a normal Release build to artifacts\stability-normal:
Copy-Item .\artifacts\runtime-probe\WinThunar.RuntimeProbe.dll .\artifacts\stability-normal\
dotnet exec --runtimeconfig .\artifacts\stability-normal\WinThunar.runtimeconfig.json --depsfile .\artifacts\stability-normal\WinThunar.deps.json .\artifacts\stability-normal\WinThunar.RuntimeProbe.dll --expect-disabled
```

A five-minute changed-source UI run passes **161 desktop checks and two restart checks**;
both processes exit zero. The post-cleanup full dump has **no diagnostic wrapper lists or
their arrays**, and **4,209,052 bytes** of enumerated non-free managed objects. This run uses
explicit file focus for each search, while the earlier pilot set it only in the icon phase;
the heap confirms removal of the specific registry, but these runs are not a controlled
throughput/private-memory comparison. Remaining recycled template/entry roots still require
long-session assessment. Private memory is not the same quantity as live managed objects.

Evidence: `artifacts/stability-registry-fixed-files-20261025/`.
The service suite passes **211 tests, one existing symbolic-link privilege skip**. Its first
sandboxed attempt cannot connect to the test host; the normal-desktop run passes. A normal
x64 Release publish succeeds, contains the runtime switch, and excludes the five checked
smoke/endurance/container/probe markers from `WinThunar.dll`.
Evidence: `artifacts/stability-services-20261025/stability-services-desktop.trx` and
`artifacts/stability-normal-20261025/verification.json`. MSBuild's evaluated Debug host options
do not contain the disabling switch; that is configuration evidence, not a Debug UI run.

## Longer-workload instrumentation

The test harness now supports up to 86,400 seconds and explicit Existing/Files/Menu/Alternate
focus modes. Each query checks its generated result count and records actual focus. Full
query and resource histories are streamed to NDJSON. In-memory histories retain at most 600
records; weak observations preserve the first 100 entries throughout the run plus the latest
500. A rolling window alone could forget an old leak. Reports distinguish cumulative counts
from the bounded observation window. Source inputs, build-output digests, live process identity
and terminal outcomes are recorded separately.

No forced collections or debugger captures are added during the active normal workload.
Three explicit diagnostic collections remain after work and idle. `-EnduranceOnly` supplies a
minimal, matching debugger workload; it does not establish the full desktop regression suite.

The initial one-hour request on unchanged source **fails**, rather than completing the hour:
its last checkpoint records 1,630 cycles, 4,890 searches and 3,260 previews at 1,334.8 seconds.
The final report records `UnauthorizedAccessException` replacing `endurance-progress.json`.
The process exits zero but the runner correctly rejects its failed report. This is a harness
failure, not a native-termination result. A reader that omits `FileShare.Delete` can block
atomic checkpoint replacement. The source status reader now allows replacement, and the
publisher tolerates sharing/access-denied replacement failures while retaining the already
flushed NDJSON sample and counting skipped checkpoint writes. Other I/O failures propagate.
Evidence: `artifacts/stability-baseline-desktop-20261025/`.

The corrected minimal 40-second Alternate-focus run passes four workload checks and two
restart checks. A separate injected collision holds its checkpoint open without delete
sharing for 15 seconds. The workload skips two checkpoint replacements, completes 49 cycles,
147 searches and 98 previews, writes its final report, and exits zero. This verifies the
reader collision rather than merely assuming that a retry will help.
Evidence: `artifacts/stability-checkpoint-regression-20261025/` and
`artifacts/stability-checkpoint-collision-20261025/verification.json`.

Live status should be read with:

```powershell
.\scripts\Get-DesktopEnduranceStatus.ps1 -OutputDirectory .\artifacts\RUN-DIRECTORY
```

`processVisibleToCaller=false` may mean sandbox isolation; it is not proof that the test
stopped. Check the runner's live session or inspect the exact process on the normal desktop.

## Native crash and completion gates

The preserved historical minidump lacks the primary `0x8000FFFF` stowed-exception stack.
Its secondary `0x80040111` stack runs through activation lookup, binding and template layout.
That minidump alone cannot establish the primary crash cause.

Microsoft-signed ProcDump is stored only under `artifacts/diagnostics-tools/procdump/`.
Initial launch-under-debugger attempts stop at a theme-dialog timing assertion, before the
workload, and are not native-crash reproductions. A later run attaches the native monitor
after the preserved 0.5.14 executable starts its original workload. **That run reproduces
native `0xC000027B`** and captures a **1,151,098,630-byte full dump**. Its last checkpoint is
260 cycles, 780 searches and 520 previews at 326.0 seconds. The primary `0x8000FFFF` stack
is now present: `CXcpDispatcher::OnReentrancyProtectedWindowMessage`, nested dispatcher
messages, COM's STA wait loop, and CLR wait/monitor machinery. The UI thread's managed stack
is `Monitor.Enter_Slowpath` → `ComWrappersSupport.CreateCCWForObjectForABI` →
`ManagedCustomPropertyProviderVftbl.GetCustomPropertyForJit` → native `SetBinding` →
`MainPage.FileContainer_Changing`. Stack roots identify the waited-on object and its
`ConditionalWeakTable<object, ManagedObjectWrapperHolder>` owner. The dump does not supply
a reliable owning-thread identity, so no specific lock owner/deadlock is claimed.

This establishes the immediate failure mechanism: an internal managed COM-wrapper-table
wait pumps messages during a protected XAML container callback, re-entering the XAML
dispatcher. It matches the independently reported
[WinUI lock/reentrancy problem](https://github.com/microsoft/microsoft-ui-xaml/issues/11099).
It is distinct from the unbounded duplicate diagnostic list; the Release switch addresses
that list and does not remove every runtime table lock.

The new source gives fresh containers an immediate string name, then queues the reusable
`Content.AutomationName` binding on the dispatcher after the native callback returns. The
queued work follows current content, handles a replaced/recycled item, and marks installation
before making the native call so a nested callback cannot replace an in-progress binding.
A native preparation edge case is covered explicitly: the queued callback can run before
the container receives its content. Install the binding even then so it observes that later
assignment; skipping null content silently leaves the container with only a static name.
A weak per-container state map avoids both repeated native binding-expression queries in
the callback and retaining dead containers. File-template thumbnail/visibility/Git properties
now use compiled one-way bindings, avoiding their reflection-based custom-property setup
on the rendering path. Side-pane templates with a different model remain unchanged.

Evidence: `artifacts/stability-original-0514-late-monitor-20261025/`, including the full dump,
`stowed-exception.json`, `symbolized-stack.txt`, `managed-threads.txt` and `ui-stack-roots.txt`.
The old minidumps retain their original limitations; the new full dump provides this mechanism.

A separate comparison
restores only the old per-entry binding method into the 0.5.15 source, with identical bounded
focused-workload code in the comparison snapshots. Its minimal mode omits the general smoke
suite. It completes fifteen minutes without reproducing the crash; this does not rule out
the context-dependent mechanism observed in the original executable. Source/binary
identities and failure evidence are retained under
`artifacts/stability-binding-comparison-20261025/` and
`artifacts/stability-original-0514-late-monitor-20261025/`.

## Final native checks and day-long run, 25 October

The corrected source passes **185 desktop checks and two restart checks**, including the
native callback deferral, replacement before queued initialization, live accessible names,
Git badge rendering and thumbnail clear/restore in Details, Icons, Compact and the split pane.
Its 60-second Alternate-focus workload completes **95 cycles, 285 searches and 190 previews**.
Both processes exit zero. After three diagnostic collections the first weak cohort retains
one of 100 sampled entries, total observed survivors are ten, handles are 3,722 and managed
memory is 3,415,968 bytes. Private memory remains higher than startup, so the short result is
not a long-session memory conclusion. Evidence: `artifacts/stability-native-deferral-final-20261025/`.

The final native deferral regression also runs against unchanged 0.5.15 product code and
fails at the intended assertion: a fresh container already has a binding inside the protected
callback. Its test source SHA-256 matches the current test byte-for-byte. Evidence:
`artifacts/stability-deferral-before-final-20261025/regression-outcome.json`.

An **86,400-second Alternate-focus run was launched** at 09:47:16 EDT on
25 October. It uses the full smoke suite before the workload, a 180-second diagnostic hold
after cleanup, and restart checks after normal process exit. The generated executable is
`artifacts/stability-day-soak-20261025/app/WinThunar.exe`; the recorded PID is **13432**.
PID alone is not durable identity: revalidate it against the recorded executable and start
time before attaching a diagnostic tool. Source inputs, output digests and eight compiled
C# source hashes are verified in that directory. Initial status is a live workload, not a pass.

The runner temporarily holds `ES_SYSTEM_REQUIRED` on its own thread, allows display sleep,
and restores the previous thread request in `finally`; it does not modify power settings.
Explicit user-initiated suspend can still interrupt coverage. Inspect full sample gaps and
per-hour search activity before claiming a day of active work. The three forced collections
and any heap capture belong only to post-work diagnostics; the active run has no debugger
or forced collections. The expected terminal result is on the morning of 26 October,
depending on workload completion, cleanup, the diagnostic hold and restart checks.

A passive post-work helper waited on this exact process and start time. It was to capture
`after-day-cleanup.dmp` only when the app publishes its post-cleanup diagnostic-hold signal,
then writes offline heap statistics, diagnostic-registry counts and a digest/outcome record.
The helper does not inspect or pause the active workload. Its script and outcomes are in the
run directory. A completed dump capture still does not substitute for checking the runner's
final desktop/restart outcomes. Initial minutes overlap the final normal publish and the
isolated before-change regression; throughput and dispatcher samples are not a benchmark.

## Failed extended run, 26 October

The process terminates at 06:46:33 EDT on 26 October; the runner records exit
`-1073741189` (`0xC000027B`) and rejects the run. The last checkpoint contains **92,010
cycles, 276,030 searches and 184,020 previews** at **75,540 seconds**. No final endurance
report, restart result or post-work heap is claimed. The waiting capture helper exits with
`captured=false` because the diagnostic hold is never reached.

Windows identifies `Microsoft.UI.Xaml.dll` 3.2.3.0. The temporary WER dump is gone, but the
retained local `WinThunar.exe.13432.dmp` is copied into the run directory as `day-crash.dmp`
(24,811,657 bytes) with a SHA-256 record. The stowed HRESULTs are `0x8000FFFF` and
`0x80040111`; this minidump lacks their native stack memory. Its UI managed stack again
waits in `Monitor.Enter_Slowpath` → `CreateCCWForObjectForABI` →
`ManagedCustomPropertyProviderVftbl.GetCustomPropertyForJit`. It does not establish an exact
native callsite or lock owner. Deferred initial binding creation therefore did not remove
the entire reflection/property-reconnection path.

The streamed samples distinguish a stable small managed heap from sustained native resource
growth. Representative hourly medians are:

| Elapsed hour | Private MiB | Process handles | Managed MiB |
| --- | ---: | ---: | ---: |
| 0 | 353.0 | 4,228 | 6.6 |
| 5 | 513.1 | 9,143 | 7.9 |
| 10 | 694.6 | 14,613 | 7.1 |
| 15 | 826.8 | 18,656 | 6.9 |
| 20 | 910.7 | 21,275 | 7.8 |

The final sample has 923.5 MiB private memory and 21,599 handles. The initial weak-entry
cohort has zero survivors; twenty recent sampled entries remain. There is also a **2,300.8
second gap between samples** around hours 19–20 and a later 71-second gap. Kernel-Power events
record Modern Standby following a lid transition from about 05:10 to 05:48 on 26 October,
matching the longer gap. The later gap remains unexplained. Do not count the entire elapsed
duration as uninterrupted active coverage. Evidence: `power-window-events.json`,
`trend-summary.json`, the complete NDJSON histories, Windows events,
WER report, `stowed-exception.json` and `managed-threads.txt` in the original run directory.

The next source candidate removes the container's reflection binding entirely. Typed
content-change and `AutomationName` notifications preserve names, Git updates, replacement
and null transitions. The first weak-RCW-keyed candidate passes its functional tests but its
post-cleanup heap retains **38,414 trackers and native callback delegates**; it is rejected
as a retention remedy. The weak map has very few live keys: native containers can outlive
their managed wrappers, so managed-wrapper identity is insufficient ownership for a native
callback registration. The next candidate anchors tracker identity to the native container's
internal `Tag`, with a weak back-reference to the container. A weak page-local registry
supports teardown without retaining dead trackers. Native content-callback registration runs
once per native container outside the protected callback; subsequent updates use typed
properties and strings. The proposed device-template cleanup was not applied.

Its five-minute full-context run passes **199 desktop checks and two restart checks**,
including old-source notifications after replacement and empty-content updates without any
reflection binding. It completes 105 cycles, 315 searches and 210 previews. The post-cleanup
heap contains **170 trackers and 170 native content-change delegates**, compared with
38,414 of each in the rejected weak-key-only candidate. Managed memory after cleanup is
3,423,024 bytes; the original weak-entry cohort has no survivors and seven recent samples
survive. Post-cleanup handle profiling records 3,562 handles. The runs overlap diagnostic
builds/baselines and have different cycle counts; this is not a throughput comparison.
Evidence: `artifacts/stability-typed-names-owned-20261026/` and its full heap/statistics.

A one-hour run with the same product source is launched, adding bounded tracker-count
sampling and a post-cleanup registration-count assertion to the test build. Its source record
now includes the new tracker service. Native/private-memory trends and longer native-runtime
stability still require verification; the five-minute result does not close priority 1.

The hour completes **3,117 cycles, 9,351 searches and 6,234 previews**, passing **200 desktop
checks and two restart checks**. Both processes exit zero. It nevertheless fails the resource
gate: median handles increase by **281** and median private memory by **12,568,576 bytes**
(about 12 MiB) between the 20–30 minute and final ten-minute windows. Tracker median growth
is zero. The continuation worker correctly leaves `dayStarted=false`; no new day-long pass
is established. The captured post-work heap contains **170 trackers, 170 native content-change
delegates and 186 file entries**, with no diagnostic-wrapper lists. This bounds the demonstrated
tracker and registry leaks while leaving native/private-resource growth unresolved. Evidence:
`artifacts/stability-typed-names-hour-20261026/continuation-outcome.json`, process outcomes,
full sample history and `after-hour-heap-statistics.json`.

A matching one-hour diagnostic in `artifacts/stability-native-handles-20261026/` finishes
naturally at 09:52 EDT on 26 October, without a rebuild, branch change or pull during the run.
It completes **3,051 cycles, 9,153 searches and 6,102 previews** over **3,600.684 active
seconds**, passing **200 desktop checks and two restart checks**. Both processes exit zero
without timing out. The complete search log records 4,578 file-focused and 4,575 menu-focused
searches. Its 307 workload samples have a maximum awake-time gap of 28.446 seconds; the
maximum wall-time gap is also 28.445 seconds. Compiled-source verification passes and all
recorded source hashes still match at completion.

The resource gate compares the 20–30 minute and final ten-minute workload windows using
awake time, excluding post-work collection samples:

| Metric | 20–30 minutes (33 samples) | Final ten minutes (79 samples) | Growth | Allowed growth |
| --- | ---: | ---: | ---: | ---: |
| Median handles | 3,779 | 4,091 | **312 — fail** | 128 |
| Median private bytes | 350,048,256 | 362,127,360 | **12,079,104 (11.52 MiB) — fail** | 8,388,608 (8 MiB) |
| Median live name trackers | 170 | 170 | 0 — pass | 32 |

This independently repeats the resource failure; **no conditional day-long run is started**.
After the three post-work diagnostic collections, the final sample has 170 trackers,
3,560,888 managed bytes, zero survivors from the initial 100-entry cohort and seven recent
survivors. It still has 4,117 handles and 421,928,960 private bytes. Those cleanup samples
are distinct from the settled workload medians. This run has no diagnostic hold or new full
heap dump; the earlier hour's full heap remains separate evidence on identical product source.

The passive observer preserves 61 timestamped handle summaries and process measurements in
`artifacts/stability-native-handle-observer-20261026/`. Its app-finished record agrees with
the desktop outcome, but its `runnerExitCode` is null; the runner's positive terminal message,
two process-outcome files and both result reports establish completion. Many intermediate
handle summaries contain unnamed types, limiting attribution. The final summary names 2,483
Event handles and 467 Token handles among 4,115 total handles, with 314 unnamed handles.
These are diagnostic observations, not proof of the allocating component. Preserve the raw
summaries and use a separately validated type reader before attributing growth.

Evidence: `resource-gate.json`, `Evaluate-ResourceGate.ps1`, both `*-process.json` files,
the complete NDJSON histories and terminal reports in the run directory, plus
`observer-outcome.json`, `snapshots.ndjson` and `handles-*.txt` in the observer directory.

The continuation worker starts a new 86,400-second run only after the hour's full regression,
restart, duration, resource and post-work heap checks pass. It compares settled resource
windows, rejecting more than 128 additional median handles, 8 MiB of median private memory,
or 32 additional median trackers. It also checks the full heap for bounded tracker/callback
counts and requires unchanged product sources. These are gates for starting a longer test,
not proof of day-long stability. Evidence: `artifacts/stability-typed-names-hour-20261026/`.

The next harness measures workload and timeout duration using Windows
`QueryUnbiasedInterruptTime`, excluding system sleep. Samples retain both wall and awake
times; dispatcher timing distinguishes the two. Its temporary system-awake request leaves
display sleep available and restores the previous request on exit. A full short regression
passes **200 desktop checks and two restart checks**, with 31.2 seconds of active work.
Evidence: `artifacts/stability-awake-clock-final-20261026/harness-verification.json`.

A copied-source comparison initially used a stale incremental build and is invalidated.
The preserved PDB checksum differs from its recorded C# source. A forced rebuild reproduces
the expected old-source failure. The harness now verifies compiled C# document checksums in
the portable PDB against its source manifest before launching the app; its verifier rejects
the stale output and accepts the rebuilt output. The continuation worker permits only the
two exact, separately verified test-harness changes above while requiring identical product
source. Evidence: `artifacts/stability-typed-name-regression-before-forced-20261026/`
and the short regression's `compiled-source-verification.json`.

Resource isolation preserves a minimal-workload baseline and a full-context baseline. Two
full-context attempts fail in pre-work checks (a Git-drain timing limit and a native replacement
probe); they are not workload failures or passes. The Git-drain test timeout is aligned above
the service's eight-second limit. A separate diagnostic baseline omits only the fresh-container
probe to reach the original sustained workload; its reduced setup is recorded and cannot
substitute for the complete regression suite.

The earlier deferred-binding normal x64 Release build also succeeds and excludes the five checked test/probe
markers. Running the balanced-call probe with this build's actual runtimeconfig, dependency
map and bundled runtime again retains zero diagnostic-registry records after two million
calls. Evidence: `artifacts/stability-normal-final-20261025/verification.json` and
`runtime-probe-result.json`.

The current native-owned tracker candidate also passes a normal x64 Release publish on
26 October. Its PDB document checksums match the recorded product inputs, the checked
test/probe markers are absent, and its actual runtime configuration disables the diagnostic
registry. The balanced-call probe again retains **zero records after two million calls**.
The probe DLL is copied into this diagnostic artifact directory only after checking the
normal build; it is not added to application packaging. Evidence:
`artifacts/stability-normal-typed-names-20261026/verification.json`,
`compiled-source-verification.json` and `runtime-probe-result.json`.

### Handle-type follow-up, 26 October

The failed hour is followed by a separate **300.233-active-second** Alternate diagnostic,
with **292 cycles, 876 searches and 584 previews**, **200 desktop checks and two restart
checks**. Both processes exit zero without timing out. All recorded source hashes and the
compiled C# checksums match. This is an instrumented five-minute diagnostic, not another
one-hour gate or a day-long pass.

The reader uses the documented Windows
[Process Snapshotting handle API](https://learn.microsoft.com/windows/win32/api/processsnapshot/ns-processsnapshot-pss_handle_entry),
without a debugger, VA clone, thread capture or forced collection during work. It validates
all 32 known Event handle values/types in its own process before application use. Unnamed
types remain explicit. Thirteen snapshots take 6–18 ms each and preserve type counts and
handle values, without object names. During the populated workload, Event counts range from
2,472 to 2,501 and Token counts from 15 to 136. After cleanup, Token counts fall to 38; the
last held snapshot has 2,435 Events and 3,477 total handles, including 309 unnamed handles.
This narrows the categories to measure but does not establish a monotonic Token leak or
identify the allocating component. Reused handle values alone do not establish object identity.

The passive helper captures a **371,926,123-byte** full dump only during the post-work hold.
Its offline heap has **170 trackers, 170 native content-change delegates and 211 file entries**,
with no diagnostic-wrapper lists. Observer process exit-code fields are again null; the two
runner-owned process reports, successful terminal message and result reports establish the
clean desktop/restart outcomes. The initial follow-up launch fails before building or starting
the app because an inherited PowerShell module path hides `Get-FileHash`; a fresh-directory
retry with native Windows PowerShell modules produces the verified result above.

Evidence: `artifacts/stability-pss-handles-final-20261026/diagnostic-verification.json`,
both process reports, source records, `post-work-capture-outcome.json` and the full heap;
the adjacent `-observer/` directory holds all typed snapshots. The reader, observer script,
known-handle self-test and initial failed-launch logs are retained in
`artifacts/stability-native-handle-observer-20261026/`. The next targeted step is an hour of
typed snapshots aligned with the settled resource windows, followed by held-cleanup counts;
allocation tracing is needed before assigning a growing category to application or runtime
code. The failed resource gate continues to block the conditional day-long retest.

### Token-handle cause and storage-free thumbnails, 26 October

The typed-snapshot hour (`artifacts/stability-pss-hour-20261026/`) completes **3,032 cycles,
9,096 searches and 6,064 previews** with 200 desktop and two restart checks, clean exits and
no crash. It fails the resource gate for the third time: median handles grow **227** and median
private memory **14,589,952 bytes**. Its typed snapshots attribute the handle growth almost
entirely to one category: median **Token** handles rise from **176 to 420** between the
settled windows, while Event, Thread and every other type stay within ±3. After cleanup the
process still holds **405 Token handles**. The five-minute diagnostic fell back to 38.

That hour's full dump has no handle stream, and `!htrace -diff` records handle values without
stacks on this machine. Breakpoints on `NtOpenProcessToken[Ex]`, `NtOpenThreadToken[Ex]`,
`NtDuplicateToken` and `NtFilterToken` give attribution instead. Each breakpoint records the
call stack plus a one-shot return breakpoint that records the returned handle value. After
the workload, a `DebugBreakProcess` break during the diagnostic hold lists the live Token
handles. The method is first validated on the known-token fixture: all 32 deliberately
retained tokens match their `OpenProcessToken` caller.

On the application, the debugger observes **about 4,000 token opens per workload cycle**,
nearly all inside `windows.storage.dll`. The tokens still open at the first break come from
Windows.Storage's brokered WinRT calls, which duplicate the caller's token
(`combase!CServerSecurity::ImpersonateClient` → `DuplicateToken`), plus the shell thumbnail
cache (`thumbcache` → `OpenProcessToken`). These calls are made by `StorageFile.GetThumbnailAsync`,
`StorageFolder.GetThumbnailAsync` and `StorageFile.OpenReadAsync`. Breakpoint overhead slows the
workload to five cycles in ten minutes, so this trace attributes the category; it does not
measure the leak rate. Evidence: `artifacts/stability-token-stacks-20261026/` (early break,
`token-attribution-early-break.txt`) and `artifacts/stability-token-stacks-20261026-run2/`.

The source no longer uses `StorageFile` or `StorageFolder` for thumbnails or image previews:

- `ShellThumbnailReader` asks the shell's in-process `IShellItemImageFactory` for the item
  image off the UI thread. It copies the returned `HBITMAP` into premultiplied BGRA, fixing
  opaque images that report a zero alpha channel, and deletes the bitmap. `ShellImageService`
  then creates a `WriteableBitmap` on the UI thread. Small list sizes request type icons only,
  as before.
- Image previews decode from a `FileStream` through `AsRandomAccessStream`. A codec failure
  still falls back to the shell image.
- Preview selection (PDF, DOCX, text, image, unsupported) moves out of `MainPage.xaml.cs` into
  `FilePreviewLoader`, which returns a typed result. `MainPage` keeps only the UI updates.
- `RecycleBinService` now disposes its `WindowsIdentity`, which holds a Token handle until it
  is finalized.

A console check of the reader (`artifacts/stability-p0-tooling-20261026/readertest/`) returns a
96×60 opaque JPEG thumbnail, a transparent 96×96 icon, the folder image and the workload PNG's
exact pixels.

**Twenty-minute check.** An Alternate run on the changed source
(`artifacts/stability-storage-fix-20min-20261026/`) passes 200 desktop and two restart checks
and completes **1,745 cycles in 1,200 active seconds**, about 70% more cycles per second than
the old source. Its 21 typed snapshots show **zero Token handles throughout**. Workload handles
stay between 3,357 and 3,520, and private memory between 311 and 316 MiB after warm-up. Its idle
sample briefly reports 505 MiB, settling to 374 MiB after collection. The harness now samples
directly after its final window capture (`after-capture`) to separate capture surfaces from
cleanup. A two-minute repeat shows the capture adding 11 MiB and cleanup ending below the
workload level. The hour runs below do not repeat the spike.

**One-hour Files workload: passes.** `artifacts/stability-files-hour-2-20261026/` completes
**4,909 cycles, 14,727 searches and 9,818 previews** in 3,600.5 active seconds. All searches
keep focus in the file list. It passes 200 desktop and two restart checks with both exits zero,
compiled-source verification and unchanged recorded sources. The maximum active sample gap is
9.7 seconds.

| Metric | 20–30 minutes (84 samples) | Final ten minutes (79 samples) | Growth | Allowed growth |
| --- | ---: | ---: | ---: | ---: |
| Median handles | 3,379 | 3,388 | **9 — pass** | 128 |
| Median private bytes | 317,569,024 | 318,963,712 | **1,394,688 (1.33 MiB) — pass** | 8 MiB |
| Median live name trackers | 160 | 160 | 0 — pass | 32 |

Private memory stays between 300 and 310 MiB for the whole workload and ends at 291 MiB after
cleanup. The 61 typed snapshots record at most **one** Token handle and none at the end. No
entry from the initial 100-entry cohort survives. Evidence: `resource-gate.json` in the run
directory and the adjacent `-observer/snapshots.ndjson`.

**One-hour Menu workload: passes, with two standby interruptions.**
`artifacts/stability-menu-hour-20261026/` completes **4,751 cycles, 14,253 searches and 9,502
previews** in 3,600.3 active seconds. All searches keep focus in the File menu. It passes 200
desktop and two restart checks with both exits zero, compiled-source verification and
unchanged recorded sources. The laptop was on battery. It entered Modern Standby twice during
the run: from 14:14 to 15:07 (lid, then battery-austerity) and from 15:33 to 17:41 (idle timeout,
then battery-austerity). That stretched wall time to 14,077.6 seconds; the largest wall gap
between samples is 7,333 seconds. The harness counts awake time, and the largest *active*
sample gap is 13.3 seconds. The process survives both suspend/resume cycles. A temporary
display-awake request covered the last 20 active minutes; it changed no power settings.

| Metric | 20–30 minutes (86 samples) | Final ten minutes (76 samples) | Growth | Allowed growth |
| --- | ---: | ---: | ---: | ---: |
| Median handles | 3,366 | 3,409.5 | **43.5 — pass** | 128 |
| Median private bytes | 316,536,832 | 318,844,928 | **2,308,096 (2.20 MiB) — pass** | 8 MiB |
| Median live name trackers | 169 | 169 | 0 — pass | 32 |

The handle growth is a single step at the second resume, not a slope. Ten-minute medians are
3,380, 3,366 and 3,358 before it, then 3,408 and 3,409.5 after it. Private memory stays between
301 and 305 MiB for the whole workload and ends at 300 MiB after cleanup. All 61 typed snapshots
record **zero** Token handles. Evidence: `resource-gate.json`, the NDJSON histories and the
`-observer/` snapshots. An uninterrupted Menu hour on mains power, which removes the standby
caveat, passes on 27 October; see
[Visible-only binding and final-source gates](#visible-only-binding-and-final-source-gates-27-october).

The menu-focused diagnostic that preceded this work reached 857 MiB and 18,065 handles.
Both workloads now stay near 300 MiB and 3,400 handles.

**Native access violation on one launch.** The first Files attempt
(`artifacts/stability-files-hour-20261026/`) exits with `0xC0000005` 23 seconds after launch, at
the first endurance cycle and after the pre-work checks. The fault is a null read in
`Microsoft.UI.Xaml!ModernCollectionBasePanel::CacheManager::InitCollectionsCache`, reached from
`GenerateContainerAtIndex` during an ordinary render-tick `UpdateLayout`. No application frame
is on the stack. The run starts right after the view switches from Icons to Details while the
search replaces 60 image results with 300 text results. The retained dump is `crash-28772.dmp`
with `crash-stack-full.txt`. The Application log for the preceding five days records no earlier
WinThunar crash with this signature; the earlier crashes are `0xC000027B` and one combase
`0xC0000602`. On the changed source this access violation occurs **once in thirteen launches**:
the 20-minute run, a two-minute run, eight 45-second full-suite runs
(`artifacts/stability-av-rate-new-20261026/`) and both one-hour workloads do not repeat it.
That rate cannot show whether faster thumbnail completion makes this WinUI path more likely.
It is recorded as an open native failure, not as a pass.

**Access-violation follow-up, 26–27 October.** WinUI's open source identifies the null pointer.
`CacheManager::InitCollectionsCache` dereferences `m_strongHost`, which matches `[this+0x60]` in
the disassembly. The panel sets `m_strongHost` from a weak reference to its host list in
`GuaranteeCache`, at the start of each virtualization pass (`CacheStrongRefs`). It clears it in
`UnguaranteeCache` when that scope ends. The scope is not reference-counted, and collection-change
handlers (`OnItemAdded`, `OnItemRemoved`, `OnCollectionReset`, `OnDataElementReplaced`) and
`ScrollItemIntoView` open their own scope. So either of two things can null the host
mid-generation: a nested call into the same panel during a virtualization pass, or a host weak
reference that no longer resolves. The WER minidump lacks heap memory and cannot tell which.

Attempts to reproduce or narrow it, all on the changed source:

- 30 alternating launches each of the old and new builds (full suite plus 20 seconds of work,
  `artifacts/stability-av-compare-20261026/`): no crash in either. One old-build launch fails a
  timing-sensitive theme-selector check without crashing.
- A test-only probe counts container-callback depth and records any `Entries` change made while
  a container callback is on the stack. In 1,694 cycles it records **none**
  (`artifacts/stability-reentrancy-probe-20261026/`).
- Debugger breakpoints on `GuaranteeCache` for nested entry and a dead host see neither in
  18 cycles. Overhead makes that sample too small (`artifacts/stability-guarantee-trace-20261026/`).
- 50 launches with cdb attached, armed to write a full dump on a second-chance access violation:
  no crash (`artifacts/stability-av-catch-20261026/`).
- A 30-minute stress run repeating the container-binding checks before every cycle (177
  sequences): no crash and no reentrant change (`artifacts/stability-container-stress-20261026/`).

In total the access violation appears once in about 100 launches, and never in about 16,000
steady-state cycles or the stress sequences. Its trigger is not established.

## Visible-only binding and final-source gates, 27 October

**Defensive change.** Details, Icons and Compact used to keep their `ItemsSource` bound to
`Entries` all the time, so one search reset ran collection-change handlers on all three
virtualizing panels, including two hidden ones. The crash started right after a view switch
during a reset. Each view now binds to `DetailsEntries`, `IconEntries` or `CompactEntries`,
which return `Entries` only while that view is active. `SetViewMode` carries the selection and
keyboard focus to the newly bound view. A hidden panel then has no items and gets no
collection-change calls. This removes the collection-change and view-switch overlap. It is a
mitigation, not a proven root cause.

The touched code also moves out of `MainPage.xaml.cs`. Container-name tracker ownership is in
`FileContainerNameRegistry`, preview loading in `FilePreviewLoader`, and shell thumbnail
reading in `ShellThumbnailReader`.

**Launch batch.** 100 consecutive launches of the changed source each run the full suite and
about 20 seconds of work. All 100 exit zero with no crash
(`artifacts/stability-visible-binding-launches-20261027/launches.ndjson`). A later full-suite
run passes 200 desktop checks and 2 restart checks. Two earlier restart-check timeouts that
morning also happen on the unchanged baseline (`c4a655d`). The laptop had just resumed with a
4% battery, and the changed source passes once it is charging.

**One-hour gates on the final source** (`aa44e38`), on mains power with a display-awake hold.
In each run the largest gap between samples is the same in wall and awake time, so no
standby occurs:

| Run | Cycles / searches / previews | Checks | Handle growth | Private-memory growth | Tracker growth | Token handles |
| --- | ---: | --- | ---: | ---: | ---: | ---: |
| Files (`stability-files-hour-final-20261027`) | 4,947 / 14,841 / 9,894 | 200 + 2 restart, exits 0 | −10 — pass | −0.45 MiB — pass | +4 — pass | 0 |
| Menu (`stability-menu-hour-final-20261027`) | 5,394 / 16,182 / 10,788 | 200 + 2 restart, exits 0 | −2 — pass | +4.34 MiB — pass | +14.5 — pass | ≤ 1 |

Median handles stay near 3,350, and median private memory near 322 MiB (Files) and
327–331 MiB (Menu). Both compiled-source checks match the committed files.

**Day-long retest.** An 86,400-second Alternate-focus soak of the same source runs from
14:32 UTC on 27 October to 19:51 UTC on 28 October (`artifacts/stability-day-soak-final-20261027/`),
with no debugger attached. That is the workload that ended with `0xC000027B` after 75,540 seconds
on 25–26 October. **It completes with no crash.** It runs 101,401 cycles, 304,203 searches and
202,802 previews across 86,400 awake seconds. It passes 200 desktop checks and 2 restart checks,
both processes exit zero, and compiled-source verification passes. The laptop was in Modern
Standby three times (18:01–22:08, 09:16–09:53 and 18:14–18:49 UTC). The workload counts only
awake time, so 105,505 wall seconds pass in total.

| Measure | 20–30 min | Last 10 min | Change |
| --- | ---: | ---: | ---: |
| Median handles | 3,369 | 3,368 | −1 |
| Median private memory | 333.0 MiB | 345.2 MiB | +12.3 MiB |
| Median live name trackers | 132 | 127 | −5 |

Handles and trackers stay flat for the whole day. Token handles are at most one in all 288
snapshots, and the most trackers alive at once is 252. Private memory rises 5.5 MiB in the
first four hours, stays between 338 and 341 MiB from hour 4 to hour 21, then ends at 345 MiB.
That is about 0.5 MiB an hour overall, and about 0.3 MiB an hour from hour 4 on. The
8 MiB limit applies to the one-hour gate; no day-long limit is defined. The hour-gate evaluator
rejects this run only for hour-specific reasons: the requested length must be 3,600 seconds,
and one sample gap exceeds 60 seconds (see the stalls below). `day-gate.json` applies the same
windows to the full day.

**UI stalls.** The app's UI thread stalls three times: 23.6 s two minutes after a standby
resume, 41.6 s nineteen minutes after another resume, and 53.4 s at 17:37 UTC on 28 October.
The worst stall in either one-hour run is 0.3 s. The Application log records no hang or error
for WinThunar. These are not crashes. The cause was found the next day; see [UI freezes from
Git process starts](#ui-freezes-from-git-process-starts-28-october).

This does not prove a root cause for `0xC000027B`. The crashing stacks ran through
custom-property lookups for a reflection binding set in `FileContainer_Changing`. Typed
container-name tracking removes that binding. The crash does not recur in a full day of the
workload that produced it.

Before closing this priority:

- Preserve the verified native regression and check final build/source identity.
- Complete and inspect an extended workload on the changed source, including late memory,
  handle and weak-cohort trends, result/preview counts, focus coverage, clean process exit
  and restart. A launched workload is not a completed soak.
- Obtain adequate evidence for the historical native failure, retaining unsuccessful
  reproduction attempts and distinguishing a mitigation from a proven root cause.
- Record remaining limits explicitly. Separate-server/physical-device and interactive UAC
  work remains deferred; this local stability investigation does not verify those paths.

## UI freezes from Git process starts, 28 October

**Reproduction.** The day-soak workload runs again on the same source
(`artifacts/stability-hang-repro-20261028/`), with the laptop put into standby and woken
several times. A watchdog sends the main window `WM_NULL` through `SendMessageTimeout` every
second. When the window has not answered for 5 seconds it writes a full dump with `cdb`, and a
second one at 20 seconds. Two freezes occur: 62 seconds at 23:00 UTC on 28 October and 53
seconds at 00:08 UTC on 29 October. The second overlaps a test run that also starts Git.

**Cause.** In all four dumps the UI thread is inside `Process.Start`, called from
`GitStatusService.RunAsync` for `MainPageViewModel.RefreshGitStatusAsync`. `CreateProcessW`
searches `PATH` for `git` (`SearchPathW`), and its `ZwQueryAttributesFile` call on `C:\Program
Files\Git\cmd\git.exe` has not returned. The badge timer ran this every 10 seconds, and each
refresh starts up to three `git.exe` processes. A timeout also killed the Git process tree on
the UI thread, which opens every process on the system.

**The block is in Windows, not in WinThunar.** A separate logger process starts the same
`git.exe` throughout the run (`stability-hang-repro-20261028-startlog.ndjson`). Two of its
starts block for 58.8 and 41.9 seconds, at 23:05 and 23:19 UTC, with WinThunar not involved.
Normally a Git start takes 82 ms (median; at most 159 ms) and a tree kill 26 ms. The component
that holds the file query is not identified. Each of the soak's three stalls starts within
minutes of a Windows app-package installation or cleanup in the event logs. The soak kept no
dumps, so its stalls are matched to this cause by their signature, not by stacks.

**Fix.** `GitStatusService.ReadAsync` now runs the whole query on the thread pool, including
the process start, output reads and the tree kill on timeout. The view model still applies the
result on the UI thread. A regression test (`SlowProcessStartDoesNotBlockTheCaller`) injects a
process start that blocks for 1.5 seconds and requires `ReadAsync` to return within 0.5 seconds
on another thread. It fails on the previous code.

**Fewer Git starts.** The badge timer now runs once a minute instead of every 10 seconds.
Navigation, F5 and changes in the open folder still refresh badges at once. A new **Show Git
status badges** preference (on by default) stops all Git starts when turned off. Desktop smoke
checks cover turning it off and back on.

**Verification.** The same three-hour Alternate workload runs on the fix (`d0712d2`, still on
the 10-second timer) with the watchdog and the logger
(`artifacts/stability-hang-fixed-20261029/`), on mains power with no standby. The logger
catches Windows holding a Git start for 68.4 seconds at 01:54 UTC on 29 October. WinThunar
keeps sampling every 7–8 seconds through it, the watchdog never sees the window stop answering,
and the longest gap between UI-thread ticks over the whole run is 0.61 seconds. The run
completes 15,526 cycles, 46,578 searches and 31,052 previews. It passes 200 desktop checks and
2 restart checks, both processes exit zero, and compiled-source verification passes. Between
minutes 20–30 and the last 10 minutes, median handles change by −8.5 and private memory by +4.1
MiB. The later commits change only the timer and add the preference; on `9c73f20` the FileOps
suite passes 213 tests with 1 skipped, and the full desktop suite passes 198 checks and 2
restart checks with the display held on
(`artifacts/smoke-git-preference-20261029c/`). Two earlier attempts ran just after the long run
released its display hold. Their 198 checks pass, but the theme restart times out. Launched by
hand, that restart passes both visible and hidden.

## Launch hang after an instance exits, 29 October

The theme-restart check, which relaunches the app a second after the smoke run exits, timed out
in four of eight scripted runs on the 0.6.0 source; earlier timeouts also occurred on `c4a655d`.
A watcher dumped one hung launch (`artifacts/smoke-0.6.0-restartwatch-20261029-3/`). Its main
thread waits without a bound in `Program.TryRedirectToRunningInstance`, and a thread-pool
thread waits in `RedirectActivationToAsync` for the instance registered under the app's key,
which had already exited. Users could hit the same wait after Restart Now or by reopening
WinThunar right after closing it: a background process with no window.

The redirect now opens the registered process first and runs standalone if it is gone.
Otherwise the wait also ends when that process exits, or after 10 seconds. A launch that does
not redirect takes over the key when it is free. In five consecutive full runs afterwards
(`smoke-0.6.0-redirectfix-20261029-2` to `-6`), every check and restart passes, and each
restart takes about 2 seconds. Six manual relaunches before the fix all passed, so the race is
timing-dependent; these runs show the hang no longer occurs in the harness, not which exit each
launch took.
