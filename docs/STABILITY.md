# Stability investigation, 24–26 October 2026

**Current status:** the typed-tracker candidate completes a second one-hour workload and
restart with clean exits, but again fails the resource-growth gate: 312 additional median
handles and 11.52 MiB of median private memory. Its tracker median remains 170.
The conditional 24-hour retest is not started; handle-type diagnostics continue. The earlier
86,400-second request fails at approximately 21 hours with native `0xC000027B`; its post-work
heap/restart stages are not reached. The retained dumps and sample histories are preserved.
Passing checks below establish their stated scopes, not closure of P0 stability.

This is source work following published 0.5.15. It has not been published or applied to
installed copies. P0 stability remains open until the longer workload and native-crash
investigation below have adequate evidence.

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

Before closing this priority:

- Preserve the verified native regression and check final build/source identity.
- Complete and inspect an extended workload on the changed source, including late memory,
  handle and weak-cohort trends, result/preview counts, focus coverage, clean process exit
  and restart. A launched workload is not a completed soak.
- Obtain adequate evidence for the historical native failure, retaining unsuccessful
  reproduction attempts and distinguishing a mitigation from a proven root cause.
- Record remaining limits explicitly. Separate-server/physical-device and interactive UAC
  work remains deferred; this local stability investigation does not verify those paths.
