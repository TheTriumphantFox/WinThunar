# Verification history

This file keeps the detailed checklist and evidence trail that used to sit under
**Remaining gaps** in the [roadmap](ROADMAP.md), as it stood when 0.5.15 was published on
24 October 2026. The roadmap now keeps a short ranked priority list; record detailed evidence
in the [priority verification](PRIORITY-VERIFICATION.md), release notes and the documents
linked below, not in the roadmap.

## Remaining-gaps checklist at 0.5.15

The [23 October priority verification](PRIORITY-VERIFICATION.md) records the current public
per-user installer lifecycle, real recovery replay, generated PDF index matches, native
unsupported-link refusal tests and sustained desktop diagnostics. Physical hardware/network
and interactive UAC cases are deferred at the maintainer's request; broader items below remain
open where their full coverage is not established.

- [x] Publish the application additions as 0.5.3 with the verified GitHub update feed.
- [x] Publish 0.5.5 with the complete updater/recovery/search/preview changes, verified assets/feed, and a reconstructed 0.5.3-to-0.5.5 delta.
- [x] Publish 0.5.6 with persistent theme selection, window/dialog propagation, verified assets/feed, and a reconstructed 0.5.5-to-0.5.6 delta.
- [x] Publish 0.5.7 with closing-window session persistence, isolated updater/guest tools, verified assets/feed, and a reconstructed 0.5.6-to-0.5.7 delta.
- [x] Publish 0.5.8 with corrected new-MSI cleanup/repair policy, native guest evidence, verified assets/feed and a reconstructed 0.5.7-to-0.5.8 delta; existing cached MSIs are not retrofitted.
- [x] Publish 0.5.9 with per-user migration state protection, interrupted-migration startup guard, verified assets/feed/tag and all 528 reconstructed 0.5.8-to-0.5.9 delta entries. Forced-rollback MSI registration failure remains disclosed.
- [x] Publish 0.5.10 with guarded same-account elevated legacy migration, verified public assets/feed/tag and embedded native policy, all 528 reconstructed 0.5.9-to-0.5.10 delta entries, and thirteen unchanged historical packages. Installed guest evidence remains distinguished from public-package verification.
- [x] Publish 0.5.12 with bounded DOCX body-text preview/search, thumbnail workers/reuse, verified public assets/feed/tag and MSI policies, all 528 reconstructed 0.5.11-to-0.5.12 delta entries and 17 unchanged historical packages.
- [x] Publish maintenance release 0.5.13 with updated What's New and reusable guest verification tooling; verify five public binary digests, source tag/feeds, both MSI policies/embedded guard, all 528 reconstructed 0.5.12-to-0.5.13 delta entries and 19 unchanged historical packages.
- [x] Publish 0.5.14 with keyboard/focus and watcher fixes; verify five public binary digests, source tag/feeds, both MSI policies/embedded guard, all 528 reconstructed 0.5.13-to-0.5.14 delta entries and 21 unchanged historical packages. Longer endurance and installed-upgrade limits remain disclosed.
- [x] Publish 0.5.15 with reusable container bindings and recorded focused endurance evidence; verify five public binary digests, source tag/feeds, both MSI policies/embedded guard, all 528 reconstructed 0.5.14-to-0.5.15 delta entries and 23 unchanged historical packages. Broader retention/crash and installed-upgrade limits remain disclosed.
- [x] Confirm the existing all-users 0.5.0 to 0.5.3 full-package upgrade from updater logs and all 524 deployed file hashes; see [updater verification](UPDATER-VERIFICATION.md).
- [ ] Repeat the installed upgrade under controlled UI conditions; verify per-user apply-on-close, installed delta updates, and Windows Installer repair/uninstall metadata. The logged all-users full-package upgrade succeeded; the original MSI product entry still reports 0.5.0.0.
  - [x] Verify the public 0.5.8 per-user MSI's interactive scope/shortcut controls and no-base full update to public 0.5.10, first/last-window close behavior, all 524 deployed hashes, tabs/bookmark/view/theme/plugin/document retention, old-repair refusal, current-MSI refresh/both repairs and uninstall with positive Shell cleanup. See the [priority evidence](PRIORITY-VERIFICATION.md).
  - [x] Verify the explicitly seeded public 0.5.9-to-0.5.10 installed delta: all 528 reconstructed entries, all 524 deployed files and actual Restart Now relaunch pass. The original two-tab session is lost after a secondary window's save; preserve this as a public-package failure.
  - [x] Fix Restart Now session loss locally by saving the invoking window before native updater exit. The regression fails before the fix and passes after it, including another window's busy-history guard; 70 desktop checks, two theme-restart checks and a normal x64 publish pass.
  - [x] Publish the Restart Now fix and Help > What's New in 0.5.11, verifying the five public assets, source tag/feeds, all 528 delta entries and 15 unchanged historical packages.
  - [x] Repeat an installed two-window Restart Now update initiated by public 0.5.11 to public 0.5.12. With an explicitly seeded base cache and offline feed, all 528 reconstructed entries and 524 deployed files match; both tabs, active folder, Compact view, bookmark, Light theme and generated document survive the secondary-window save and real Restart Now relaunch. See the [24 October evidence](PRIORITY-VERIFICATION.md#installed-public-0511-restart-now-retest-24-october-2026). Unseeded cache, public-network feed, Setup and all-users/UAC paths remain open.
  - Earlier read-only post-publication inspection found installed 0.5.3 with no later update check. The subsequent migration-lab captures found installed 0.5.8.0 before/after with unchanged observed hashes; these captures do not establish its UI upgrade path. A disposable unique-identity console probe verifies production-backend delta/full apply-on-exit, corrupt-delta fallback, corrupt-full rejection/retry, pending-package rediscovery, and updated-process startup; see [updater verification](UPDATER-VERIFICATION.md). Released-app UI, Setup/MSI, and UAC coverage remain open.
  - A [released-app guest bundle and test matrix](RELEASED-UPGRADE-LAB.md) are prepared, with seven public binary digests verified and the originating host rejected by every guest action. The user-approved Windows 11 evaluation VM is activated, with an unelevated test user, UAC enabled, and a clean snapshot with all NICs disabled. The seeded per-user released 0.5.5-to-0.5.6 delta path passed first/last-window close behavior and all 524 deployed file hashes. The complete persistence case failed because the secondary window overwrote the original two-tab session; remaining matrix cases are pending.
- [x] Save a closing window's own tabs, active folder and view before scheduling apply-on-exit. Published in 0.5.7; the source regression reproduced and corrected the guest finding, and 67 desktop checks plus 2 restart checks and a normal x64 build passed. See the [controlled upgrade observations](UPDATER-VERIFICATION.md#controlled-released-app-delta-observations-22-october-2026).
- [ ] Prevent an older MSI repair from producing a mixed payload after a Velopack update. Controlled per-user `/fomus` repair returned 0 and kept the DLL/manifest at 0.5.6, but reverted `WinThunar.deps.json` and `WinThunar.pri` to public 0.5.5 contents. The failure and all payload evidence are preserved; see [repair observations](UPDATER-VERIFICATION.md#msi-repair-produces-a-mixed-payload).
- [ ] Review uninstall settings/recovery retention. Controlled silent per-user `/x` removed the app, both registrations and both shortcuts, preserved the generated document, and left Explorer working, but also deleted the session/update settings directory. Recovery/Undo data and positive Shell cleanup were not exercised.
  - [x] Verify public 0.5.10 per-user uninstall after MSI refresh and both repairs, retaining settings, plugin data, an unresolved valid recovery journal and generated documents while removing app/registrations/shortcuts/enabled Shell integration. Starting the repaired public app also restores a safe displaced original and preserves an ambiguous case; live Undo and other scopes remain open.
  - The [MSI maintenance policy](MSI-MAINTENANCE.md), published in 0.5.8 installers, corrects the cleanup ID and adds a repair guard for an updated installation, including a missing DLL. Both base/current MSIs pass eleven native condition cases. A corrected-MSI per-user guest run passed same-version repair, native 0.5.7 apply with all 524 deployed files matching, three rejected older-repair cases without mutation, and uninstall retention plus enabled Shell cleanup. Existing cached MSIs are not changed by application updates.
- [ ] Preserve user state automatically during legacy-MSI migration. The [original 0.5.5 MSI -> native 0.5.7 update -> published 0.5.8 MSI test](MSI-MIGRATION.md) returned 0, matched all 524 target files and replaced the cached MSI/registration, but old-MSI cleanup deleted all five seeded settings/plugin/recovery files. Interactive/all-users coverage and real recovery/Undo retention remain open.
  - [x] Implement and publish automatic per-user protection in 0.5.9. Private-candidate migration retains all five seeded state files without helper restoration; all 524 deployed files match after migration and both repairs. Uninstall retains state/document and removes app/registrations/shortcuts/enabled Shell integration. Native filesystem checks cover locks, stale holds and competing-state refusal. All-users legacy migration is refused before removal.
  - [x] Guard the reproduced legacy per-user forced-rollback registration failure in 0.5.10. Same-account elevation is required before old removal; both elevated rollback cases restore state, all 524 original application files, product state/cache and uninstall metadata. The original unelevated failure remains in 0.5.9 and earlier cached installers; see [rollback evidence](MSI-MIGRATION.md#forced-rollback-finding--known-limitation). This is not a general MSI-engine fix.
    - [x] Implement and verify a [23 October source remedy](MSI-ROLLBACK.md) requiring actual elevation in the same account before legacy per-user removal. The original failure is freshly reproduced and preserved. Native/MSI policy checks, installed unelevated refusal, elevated early/late rollback, subsequent migration, both repairs and uninstall pass; all evidence archives are hash-verified. This is not included in published 0.5.9.
    - [x] Publish the guarded migration policy in 0.5.10 and verify both release MSI copies and the embedded guard. The same-account elevated path does not establish non-legacy MSI rollback, all-users migration or standard-user elevation through another account.
  - [x] Verify an explicit external backup/restore workaround from the pre-migration checkpoint. All five state files and 524 deployed files match after restoration and both repair modes. Uninstall removes the app, registrations, shortcuts and explicitly enabled Shell integration while retaining state and the generated document. This is test-helper behavior; direct MSI migration still loses state.
- [ ] Complete the manual accessibility checks documented in [ACCESSIBILITY.md](ACCESSIBILITY.md).
  - [x] Published 0.5.14 follow-up corrects F6 navigation for search, side-pane children,
    pathbar editing, split navigation, preview and command output, plus focus restoration
    when those controls close. Verify explicit split/command action names through button
    automation peers; 128 generated desktop checks and two theme-restart checks pass,
    including the separate native folder-watcher regression below.
    The full manual screen-reader, contrast, scaling and keyboard review remains open.
- [x] Verify real disk-full after main-stream writes on a disposable NTFS VHD, including Copy/Move and new/Replace recovery, retry, and Undo.
- [x] Reproduce and fix lost staging-journal tracking during owned loopback-SMB share removal; verify source loss after replacement Move commit.
- [x] Add an actionable recovery warning/dialog and safe retry while protecting live transfers/history and ambiguous copies.
- [ ] Verify remote SMB server/network interruption, physical device removal, and history across separate physical devices. VHD and loopback SMB tests use one SSD; see the [stress-harness results](FILE-OPERATIONS-STRESS.md).
- [ ] Verify notifications on installed and portable builds; registration remains unavailable on this machine.
- [x] Add bounded text-content search, filesystem drive search from This PC, and read-only text previews.
- [x] Add bounded native PDF preview with page navigation and explicit read-only Windows Search index queries.
- [ ] Verify generated PDF/Office content-indexing matches and representative document/filter coverage; index inclusion and freshness are controlled by Windows.
  - [x] Observe generated TXT, RTF and PDF content matches through the guest's read-only native Windows Search provider. DOCX did not match and its direct filter registration was absent; Office and representative-format coverage remain open.
- [ ] Broader Shell/device browsing, cloud status, Office/general document previews, and fresh document-content extraction.
  - [x] Publish bounded, read-only DOCX main-body text preview and fresh filesystem `content:` search in 0.5.12, with generated service/desktop regressions. See [DOCX support and limits](DOCUMENT-TEXT.md). Word layout, other Office formats and Windows indexing-filter coverage remain open.
- [ ] Extend local search performance measurements to sustained UI/thumbnail and slow-network endurance checks.
  - [x] Published 0.5.15: reuse each file container's accessible-name binding across content changes,
    with filename/metadata, live Git-status, replacement/null content and binding-reuse checks
    in all three layouts and the split pane. The pre-version-bump short pass completes 156 desktop checks
    and two restart checks. Per-run instance keys, recorded native exit outcomes and extra
    finalization/GC diagnostics improve repeatability; see the
    [follow-up evidence](PRIORITY-VERIFICATION.md#container-binding-follow-up-24-october-2026).
  - [x] Record the final focused fifteen-minute source retest for 0.5.15 with 1,186 cycles,
    3,558 searches, 2,372 previews, 159 desktop checks and two restart checks; both processes
    exit zero. Four sampled entries remain after cleanup and handles return to 3,748.
    Private memory remains above startup (359 versus 278 MiB); crash root cause, broader
    retention, day-long and slow-network verification remain open.
  - [x] Published 0.5.14 fix prevents an old folder's watcher from cancelling a busy
    navigation to a new folder. The native watcher regression fails against the pre-change
    source and passes with the guard; normal watching resumes afterward.
  - [x] Preserve compact endurance progress checkpoints so a native crash retains completed
    counts and memory/handle samples. Two attempted 15-minute runs terminate in native WinUI;
    their dumps and progress evidence are preserved in [priority verification](PRIORITY-VERIFICATION.md#keyboard-and-endurance-follow-up-24-october-2026).
  - [x] Verify the pre-version-bump final source with a focused five-minute workload: 445 cycles,
    1,335 searches, 890 previews, 131 recorded desktop checks and two restart checks. Both
    the pre-change focused baseline and final source retain entries after diagnostic cleanup;
    retention and the intermediate native crashes remain unresolved in that published source.
    The later fifteen-minute source retest is recorded above; day-long and slow-network
    verification remain open.
  - [x] Add and run a five-minute isolated UI/search/thumbnail/preview workload with count assertions, dispatcher/memory/handle sampling and post-work cleanup diagnostics. Memory growth during active churn remains under investigation; slow-network and day-long stability are unverified.
  - [x] Publish bounded thumbnail batch workers and reuse of unchanged images for equivalent layout/zoom requests in 0.5.12. Verify replacement on size/content-mode/metadata changes, cancellation, and a five-minute workload; see [thumbnail resource follow-up](PRIORITY-VERIFICATION.md#thumbnail-resource-follow-up). Longer and slow-network workloads remain open.
- [ ] Add and verify native handling of unsupported reparse-point types before claiming support.
  - [x] Verify safe refusal of a real synthetic third-party name-surrogate tag in eight native file/directory Copy/Move new/Replace cases. Additional types are not claimed as preservable.
