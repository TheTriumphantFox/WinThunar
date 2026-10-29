# WinThunar 0.6.0

Prepared 29 October 2026 for Windows 11 x64 from `main` after PR #41. Project, assembly/file,
manifest and release-script versions are aligned at 0.6.0 (four-part version 0.6.0.0).
Publication details are added once the GitHub release is live.

## Changes

This release closes the P0 stability work in the [roadmap](ROADMAP.md). Details and evidence
are in [STABILITY.md](STABILITY.md).

**No native crash over a full day.** File containers track their accessible names with a typed
tracker owned by the native container, replacing the reflection binding that ran in the stacks
of the earlier `0xC000027B` termination. Each hidden Details, Icons or Compact view is unbound
from the entry list, so only the visible view gets collection changes; this mitigates a rare
WinUI virtualization access violation. Release builds keep the workaround for the .NET 10.0.11
COM-wrapper leak ([dotnet/runtime#129191](https://github.com/dotnet/runtime/issues/129191)).

**Memory and handles level off.** Thumbnails come from the shell's in-process image factory and
image previews decode from a file stream, instead of brokered Windows.Storage calls that kept
impersonation Token handles open (176 → 420 in an hour before the fix).

**No freezes from Git.** Git status queries run entirely in the background. Windows can hold a
`git.exe` start for up to a minute; the window used to freeze for that long. Badges refresh on
navigation, on changes in the open folder and once a minute (was every 10 seconds). A new
**Show Git status badges** preference, on by default, stops WinThunar starting Git when turned
off.

**No stuck launch after closing.** A new launch hands its request to the running instance. If
that instance was closing, as when Restart Now or a quick relaunch follows the last window
closing, the handoff never completed and the new launch waited forever without a window. The
wait now ends when the old instance exits, or after 10 seconds, and the launch opens its own
window.

Help > What's New summarizes the release. Preview loading, shell thumbnails and container-name
tracking move out of `MainPage.xaml.cs` into services.

## Verification

On the source before the Git changes (`aa44e38`), on mains power with no standby, the one-hour
Files and Menu workloads pass the resource gate: handles −10 and −2 (limit 128), private memory
−0.45 and +4.34 MiB (limit 8 MiB). 100 consecutive launches exit cleanly. A 24-hour soak of the
workload that crashed after about 21 hours completes 101,401 cycles with no crash: handles −1
and private memory +12.3 MiB.

A three-hour run of the background Git fix stays responsive through a 68-second system block of
a Git start: no watchdog hang, and the longest UI-thread gap is 0.61 seconds. With the Git
preference added, the source passes 213 service tests with one symbolic-link privilege skip,
198 desktop checks and two theme-restart checks.

The versioned 0.6.0 source passes **214 service tests** and, in five consecutive full runs,
**200 desktop checks and two theme-restart checks** each, with both processes exiting zero and
compiled-source verification passing. Before the startup fix, the theme restart hung in four
of eight scripted runs; a dump of one shows the launch waiting on the handoff to an instance that
had already closed. The five passing restarts take about 2 seconds each, so these runs do not
show which exit of the bounded wait they took.

## Limits

Neither native root cause is proven; `0xC000027B` does not recur in a full day after the
binding on its stack was removed, and the access violation is mitigated. The Windows component
that holds `git.exe` lookups is not identified. Installed upgrade to 0.6.0, slow-network
stability, notification delivery and manual screen-reader, contrast and scaling review remain
unverified. Physical hardware/network and interactive UAC cases remain deferred at the
maintainer's request.

An automatic app update does not replace an older cached MSI. Use the latest MSI for installer
safeguards. Legacy all-users migration remains refused.
