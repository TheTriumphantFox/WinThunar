# WinThunar 0.5.15

Release candidate for Windows 11 x64. Publication and public-package verification are pending.
Project, assembly/file, manifest and release-script versions are aligned at 0.5.15
(four-part version 0.5.15.0).

## Changes

File containers reuse one accessible-name binding that follows their current content instead
of capturing each successive file entry. Names, metadata and asynchronously updated Git status
remain current across Details, Icons, Compact, search results and the split pane. Replacement
content changes the announced name and null content clears it without installing another binding.
Help > What's New summarizes the release.

The source-only desktop harness adds native content-transition checks, a stable instance key
per generated directory, recorded process outcomes and nonzero-exit rejection. Endurance
samples include GC heap/commit/fragmentation, pending-finalization and runtime information.
These test hooks are excluded from normal application packages.

## Verification

The pre-version-bump source passes **211 service tests with one symbolic-link privilege skip,
156 desktop checks and two theme-restart checks**. Its final focused fifteen-minute run passes
**1,186 cycles, 3,558 searches, 2,372 previews, 159 recorded desktop checks and two restart checks**,
with both processes exiting zero and a maximum sampled dispatcher gap of **288 ms**. The running
product/harness hashes match that source. No debugger, heap capture or forced collection is used
during its active workload; the existing diagnostic collections run only after work finishes.

After cleanup, four of 3,558 sampled entries remain alive and process handles are 3,748, versus
3,482 initially. Private memory is about 278 MiB initially, 372 MiB at workload end and 359 MiB
after cleanup. Managed memory after cleanup is about 40 MiB; the negative initial raw managed
sample is invalid and is not used for comparison. This is not proof of flat/day-long memory use.

An unchanged 0.5.14-source baseline reproduces native termination `0xC000027B`; the new focused
run does not reproduce it, but does not establish its exact root cause. A separate menu-focused
diagnostic run has different resource behavior and includes intrusive captures; it is not a
matching focused comparison. See [full evidence and limits](PRIORITY-VERIFICATION.md#unreleased-container-binding-follow-up-24-october-2026).

The versioned 0.5.15 source passes **158 desktop checks, two theme-restart checks and 211
service tests with one existing symbolic-link privilege skip**. Light/Dark What's New renders
were inspected. Tests report NU1900 when NuGet vulnerability metadata is unavailable.
Package and public-release verification are pending.

## Limits

Installed upgrade to 0.5.15, general native-runtime/retention resolution, day-long or slow-network
stability, notification delivery, manual screen-reader/contrast/scaling review and broader
document/filter coverage remain unverified. Physical hardware/network and interactive UAC
cases remain deferred at the maintainer's request.

An automatic app update does not replace an older cached MSI. Use the latest MSI for installer
safeguards. Legacy per-user migration requires actual administrator elevation in the same
Windows account; preserve an external backup of the complete user-state directory before
legacy maintenance. Legacy all-users migration remains refused. Other installer/maintenance
contexts remain outside the verified path.
