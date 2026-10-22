# Installed updater verification

## 0.5.8 publication and host check

Published 22 October 2026 from source `b8cf52a` (PRs #21 and #22). Five public binary
sizes/SHA-256 digests, the source tag, latest stable pointer and 0.5.8 full/delta feed match
the verified build. The 202,337-byte 0.5.7-to-0.5.8 delta reconstructed all 528 package entries;
all nine historical update packages retain their hashes. Both MSI copies contain the new
maintenance policy and shortcut choices. The build passed 171 service tests with one skip
and the normal x64 build; native policy regression evidence is recorded below.

The corrected-MSI policy was tested in a separate guest using a corrected 0.5.5 MSI and
the 0.5.7 target package. This does not establish an installed 0.5.8 upgrade or migration
from an original old MSI. Existing cached MSIs retain their earlier maintenance behavior.
See [release notes](RELEASE-0.5.8.md) and `artifacts/verification-0.5.8/verification.json`.
Read-only captures immediately before/after publication found installed **0.5.7.0** and
identical hashes for three observed installation files (DLL, updater and manifest). Their
default app-data path was sandbox-redirected, so real-user settings/log hashes were not part
of that comparison. An explicit real-user capture afterward found those files last modified
at 08:49–08:50, before publication; no full immediate pre-publication state baseline is claimed.
The publication workflow did not launch or upgrade the host app.

## 0.5.7 publication and host adoption check

Published 22 October 2026 from source `24042d5` (PR #19). The public tag, latest stable release,
all five binary sizes/SHA-256 digests, and the 0.5.7 full/delta feed were verified. The
520,657-byte 0.5.6-to-0.5.7 delta reconstructed all 528 package entries. The closing-window
session fix passed 67 desktop checks and two restart checks; 171 service tests passed with one
skip, and all four isolated production-updater probe cases passed with 194 matching files each.

The read-only host captures before and after publication still find installed **0.5.3.0**,
no WinThunar process, and unchanged hashes for the five observed installation/state files.
The release workflow did not launch, close or upgrade that installation. An installed 0.5.7
guest upgrade remains unverified. The mixed-payload MSI repair and uninstall settings deletion
recorded below remain unfixed in 0.5.7. See [release notes](RELEASE-0.5.7.md) and evidence at
`artifacts/verification-0.5.7/verification.json` and `host-after-publication/` beneath that directory.

## Historical installed-upgrade inspection

Checked 21 October 2026 without launching, closing, replacing, or downgrading the installed app.
This is retrospective evidence of the existing upgrade, not a controlled repeat of the UI workflow.

## Installed 0.5.0 to 0.5.3 upgrade

The installed updater log at
`C:\Users\corte\AppData\Local\velopack\velopack_WinThunar.App.log` records:

1. PID **27420** initialized as **0.5.0** at 10:31:31.
2. The feed offered 0.5.3. No local base package was available, so Velopack downloaded the **full**
   package and verified its checksum at 10:31:55. This did not exercise delta installation.
3. The updater requested administrator permission for the Program Files installation. Its initial
   wait for PID 27420 reported access denied. The elevated updater then found no running processes
   under the installation directory before replacing files.
4. The elevated updater applied **0.5.0 to 0.5.3**, updated its MSI uninstall entry, completed the
   update hook, and reported success at 10:32:05.
5. The updater launched PID **9700**, which initialized as **0.5.3** at 10:32:06.

The log's line timestamps omit dates. The recorded update-check time in `updates.json` is
`2026-10-21T14:31:38.8498942+00:00`, corroborating 21 October at 10:31:38 Eastern.
The earlier handoff's association of PID 27420 with 0.5.3 was incorrect: reading the on-disk DLL
after an update does not establish the version loaded by the earlier process.

At the read-only capture at **10:40:41 Eastern**, no `WinThunar.exe` or `Update.exe` process was
present. The installed `sq.version` reports 0.5.3, and `current/WinThunar.dll` reports **0.5.3.0**.
All **524 deployed files** from the full package have matching SHA-256 hashes. This includes
`Squirrel.exe` deployed as root `Update.exe` and the execution stub deployed as root `WinThunar.exe`.
The locally downloaded full package and the previously verified release package both hash to
`b6acfd4f2abf21c7a4bcf57161c8f6101bf00328d367b0187f239d6964082ed2`.

The `MSI:WinThunar.App` uninstall entry reports **0.5.3**. The original Windows Installer
product-code entry still reports **0.5.0.0**. Do not infer from that older entry that the application
files failed to update; do not claim Windows Installer product metadata is fully synchronized.
No registry entries were changed during verification.

Local evidence is preserved at:

```text
artifacts/installed-upgrade-0.5.3-20261021-104038/
  verification.json
  installed-files.json
  msi-registration.json
  sq.version
  velopack_WinThunar.App.log
```

The earlier `artifacts/installed-upgrade-0.5.3-20261021-103945/` capture is also preserved.
Its file check looked for the two helper executables beneath `current/`; the final capture above
compares their actual deployment paths. The capture script is retained at
`artifacts/Capture-InstalledUpgrade-0.5.3.ps1`.

## Updater hardening prepared in 0.5.4, published in 0.5.5

These changes are included in **[0.5.5](RELEASE-0.5.5.md)** and are not part of the inspected
installed 0.5.3 binaries:

- Updater initialization errors become a failed update state. A malformed `WINTHUNAR_UPDATE_FEED`
  previously threw `UriFormatException` during `App.OnLaunched`, before a window could open.
  A regression test reproduced that failure before the fix.
- An explicit or subsequent automatic check can retry failed initialization, including rereading
  pending-package data. Failed initialization is not reported as a portable installation.
- Installer launch failures show an error in the window and keep the downloaded package ready
  for retry. Exceptions from scheduling an update on last-window close cannot escape that close.
- A small internal updater interface allows failure tests to use fakes. Tests cannot launch the
  actual installer or exit the test process. Production code still uses Velopack 1.2.161.

Nine updater regressions cover the malformed feed, initialization and pending-package retry,
portable copies, concurrent checks, download retry, failed-check scheduling, installer launch
retry, all-users exit behavior, and failure while scheduling an update on exit.

In the initial updater-only validation, the normal x64 application build passed with
**zero warnings/errors**. The complete service suite
passed with **134 passed, one skipped, zero failed** (135 total); the skip requires Windows
symbolic-link privilege. The built xUnit executable ran outside the sandbox because the sandboxed
VSTest host could not connect. Its XML report is preserved as `service-tests.xml` in the final
evidence directory above. The test-project build reported NU1900 because NuGet vulnerability
metadata was unavailable from the sandbox; package restoration and compilation completed.

The final 0.5.5 release validation passed **162 service tests, one skipped**, all **50 desktop
checks**, and **30 normal-token stress cases, five skipped**. Its 801,572-byte delta reconstructed
all 528 full-package entries from 0.5.3. All five public binary hashes and the 0.5.5 updater feed
were verified. These package checks do not establish an installed 0.5.5 upgrade; see the
[release notes](RELEASE-0.5.5.md) and `artifacts/verification-0.5.5/verification.json`.

## Read-only adoption check after 0.5.6 publication

At **20:56 Eastern on 21 October 2026**, the user's Program Files installation still reports
**0.5.3** in `sq.version` and **0.5.3.0** in `current/WinThunar.dll`. No WinThunar or Update
process was running. `updates.json` still records `2026-10-21T14:31:38.8498942+00:00`, and the
updater log does not mention 0.5.6. Its last recorded application launch initialized 0.5.3 at
14:19:32; log timestamps themselves omit dates. The evidence does **not** establish a failed
0.5.6 update: no later update check is recorded.

Use the new read-only capture tool without launching an installed executable:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Capture-InstalledUpdaterState.ps1
```

It snapshots the deployed version, hashes of the assembly/updater/manifest/log/settings,
package inventory, matching processes, and uninstall registry values. It checks that the observed
files did not change during capture. It reads registry entries directly; it never queries
`Win32_Product`, which can trigger Installer repair. `-InstallRoot`, `-UserLocalAppData`,
`-ExpectedVersion`, and a new `-OutputDirectory` support explicit capture targets. Run in the
normal Windows account context to capture its actual HKCU registry view.

Evidence is preserved in `artifacts/installed-updater-state-20261021-204733-dad02346/` and
`artifacts/installed-updater-state-20261021-205417-a3a1c231/`, with the final post-probe capture in
`artifacts/installed-updater-state-20261021-205601-14a8526b/`. The later captures used the normal
account outside the sandbox. The final `session-comparison.json` confirms identical before/after
hashes for the installed DLL, updater, manifest, user update settings, and production updater log.
No installed app, updater, or installer was started, closed, replaced, or upgraded.

## Disposable updater integration probe

Run the generated-package integration suite with the .NET 10 SDK and Velopack CLI 1.2.161:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-InstalledUpdater.ps1
```

This follows Velopack's [local-source testing approach](https://docs.velopack.io/integrating/testing).
It builds a separate console executable that compiles the production `UpdateService` and
`UpdateSettingsStore` sources. An internal settings-store constructor lets it use the **real
production backend** with isolated settings; normal application construction is unchanged.
The probe does not reference or start the WinUI application.

Every run creates a unique `WinThunar.UpdaterProbe.<GUID>` package identity and new artifact
directory. Generated versions **1.0.0 and 1.0.1** have no shortcuts. The script expands the base
package into an owned writable `current`/`Update.exe` layout, seeds a cached base only when
required, and provides a private local feed. Ownership tokens, exact executable/manifest/locator
paths, and rejection of reparse-point ancestors guard execution. Existing run directories and
the release packages in `artifacts/installer` are never reused or modified.

Four cases passed, each comparing all **194 deployed files** with target-package SHA-256 hashes:

| Case | Verified result |
| --- | --- |
| Cached base and valid delta | Native delta reconstruction completed and apply-on-exit deployed 1.0.1. |
| No cached base | The logged full-package route completed and applied. |
| Damaged delta | SHA-256 rejection triggered the logged fallback to a verified full package. |
| Damaged full package | The service reported Failed, all base installation files remained intact, and retry after restoring the fixture feed succeeded. |

Each case also verifies that download leaves the current version at 1.0.0, a fresh process
rediscovers the downloaded package as Ready, the close gate does not apply it prematurely,
`ApplyOnExit` completes, and the updated executable starts as 1.0.1 and reports UpToDate.
Velopack fast update hooks run only the guarded probe. Native logs include a process-wait
access-denied warning on this host; these cases exercise scheduling at immediate process exit,
not a long-lived process after scheduling. They do not establish general wait/termination behavior.

Velopack apply writes an HKCU uninstall entry for the probe identity. The runner waits for owned
processes to exit, saves that entry, verifies its identity and installation path, then removes only
that exact test key. All package files, installations, reports, failed runs, and native logs remain
available. No MSI/setup installation or uninstall command is invoked. Native packaging required
execution outside the sandbox. The normal x64 application build passed with zero warnings/errors;
the service suite passed **171 tests, one skipped** for symbolic-link privilege. NuGet vulnerability
metadata was unavailable during test/probe builds (NU1900), while restoration and compilation passed.

Final evidence: `artifacts/updater-probe-20261021-205448-058e9af9/report.json`, per-case
`deployed-files.json`/`native-updater.log`/startup reports, and `cleanup.json`. Cleanup confirms
zero owned processes and no remaining registration for the probe identity. Earlier development
runs and their registration backups remain preserved; their test registrations were also removed.
Service results are in `artifacts/updater-service-tests.xml`.

These generated console fixtures establish real updater/backend integration in a disposable
writable installation layout. They do **not** establish an installed released-WinUI-package delta,
per-user Setup/MSI installation, real last-window-close UI, UAC, repair, or uninstall behavior.

## Released-app guest bundle prepared

`scripts/New-ReleasedUpgradeLab.ps1` now prepares a separate 0.5.5-to-0.5.6 guest bundle with
seven binary assets verified against the recorded public release digests, a private update feed,
guest-side origin/ownership guards, deployed-file verification, and a complete UI/UAC/MSI matrix.
See [the released-upgrade lab runbook](RELEASED-UPGRADE-LAB.md).

Prepared evidence: `artifacts/released-upgrade-lab-20261021-211129-60a92d59/`. All five guest
actions reject the originating development PC before writing or launching a process. The host
is Windows 11 Home, initially without a Windows Sandbox executable or an installed VM application.
After explicit user approval, a minimal VirtualBox 7.2.22 core/driver installation and private
Windows 11 VM were configured. The firmware-only boot passed;
USB/network/Python add-ons are confirmed absent. The user-selected Microsoft evaluation requires
registration, so ISO acquisition paused for the user's details as requested. After the user completed
registration, the Microsoft ISO was downloaded and its published SHA-256 verified. **Windows Setup
has completed on build 26300 with an unelevated test user and UAC enabled.** Guest Additions works
after a restart; the evaluation activated for 90 days and a clean, powered-off snapshot was saved
with all NICs disabled and seven guest-local release assets verified. The first per-user
released-app delta case has now run; see the observations below and the lab runbook.

## Controlled released-app delta observations, 22 October 2026

Inside the disposable guest, the published **0.5.5 MSI** installed per-user with exit 0 and
DLL version **0.5.5.0**. This was a silent MSI fixture (`ALLUSERS=2`, `MSIINSTALLPERUSER=1`,
both shortcuts selected); the installer's interactive scope-selection UI remains untested.
The MSI created no package cache, so the public 0.5.5 full package was deliberately seeded and
recorded. The real app ran unelevated on session 1, with UAC enabled and all guest NICs disabled.

The actual **Help > Check for Updates** command used the private local feed. Native logs show
`Downloading delta 0.5.6`, successful patch completion, and `Delta update download complete`,
with no full fallback. While two windows displayed Ready, the DLL remained **0.5.5.0**.
Closing the secondary window left the original process (PID 3068) and 0.5.5 deployed. Closing
the last window applied **0.5.6** without UAC or an automatic relaunch. All **524 deployed files**,
including updater/stub mappings, matched the published target full package. The generated file,
bookmark and Compact List preference survived.

The session check exposed a separate failure: the original window had two tabs, but the secondary
window's single tab remained in `session.json` after both closed. The remaining window did not
save its session on close. This means the complete persistence case is **Failed**, despite the
successful updater path. A source regression with two distinct folder tabs reproduced the bug;
the fix published in 0.5.7 saves on `Window.Closed`, before App's handler schedules an update.
The regression now passes with **67 desktop checks and 2 restart checks**; the normal x64 build
has zero warnings/errors. Both failing source-test runs remain preserved.
The published 0.5.6 binaries in the guest remain unchanged by that source work.

After relaunch, the actual Help dialog reported the latest version **0.5.6**; the capture shows
one 0.5.6 process on session 1. The initial guest evidence archive was exported and its
183,484 bytes and SHA-256 checked on both machines. The host install is still 0.5.3.0, with all
five observed hashes unchanged and no WinThunar process. A named VM checkpoint preserves the
published post-upgrade state independently of the source fix.

### MSI repair produces a mixed payload

A subsequent standard **`msiexec /fomus {117822C1-8107-47FA-A8A7-D50EF2386071}`** repair of
the original product returned **0**. Both the DLL and manifest still reported **0.5.6**, but
the 524-file verification failed: **`WinThunar.deps.json` and `WinThunar.pri` reverted to the exact
public 0.5.5 contents**. This case is **Failed**; version checks and installer exit status alone
would miss the corruption. The repair was silent, so the maintenance UI is not covered.
The MSI product entry remains 0.5.5.0 under HKLM, while Velopack's `MSI:WinThunar.App` entry
is 0.5.6 under HKCU. The install log explicitly identifies the dual-mode MSI's per-user,
no-elevation installation and its LocalAppData target.

See `per-user-msi-repair-after-upgrade.txt`, `per-user-msi-repair-payload-analysis.txt`,
`per-user-repair-evidence-20261022.zip`, and its verified transfer report in the VM artifact
directory. All repair leftovers are retained. Preventing this legacy repair mismatch remains
open; the tab-persistence source fix does not address it.

### Native uninstall cleanup and settings removal

The uninstall case restored the untouched post-upgrade checkpoint, so it did not inherit the
repair's mixed files. A silent `/x` of the known product returned **0**, removed the app directory,
both uninstall registrations and both shortcuts, and preserved the generated document. Windows
Explorer opened the fixture directory and displayed both subfolders afterward (`ui-05`).
The guest's idle app process was explicitly terminated after snapshot-restored UI closing did
not take effect; that fixture setup is recorded separately from the earlier real close tests.

The uninstall also removed the separate `LocalAppData\WinThunar` state directory: `session.json`
and `updates.json` were absent. The strict lab settings-retention assertion therefore failed;
`uninstall-outcome-details.json` distinguishes successful native cleanup from that retention
result. Settings-retention policy and recovery-data handling still need review. No ambiguous
recovery data was seeded for this case, so it does **not** verify preservation of recovery journals
or Undo data. Shell verbs were never enabled, so positive Shell cleanup remains untested, as does
the Apps/maintenance UI. All pre-uninstall settings remain in the verified earlier exports and
the named checkpoint. No leftover data was manually removed to change an outcome.

Evidence is under `artifacts/windows-upgrade-vm-20261021-212229-9192ed86/`, including
`per-user-delta-ready-verified.txt`, `per-user-after-first-close.txt`,
`per-user-after-last-close.txt`, native logs, and `ui-01` through `ui-05` PNGs. Failed capture
directories and the failing source regression are preserved. The read-only capture helper now
uses shared-read streams so it can inspect an active Velopack log; a held-writer fixture and
the running guest app both verified that behavior.

## Corrected-MSI regression (22 October 2026; policy published in 0.5.8)

The [maintenance postprocessor](MSI-MAINTENANCE.md) fixes the cleanup ID in newly built MSIs
and rejects older-MSI maintenance before file changes when an updated payload is installed.
It does not retrofit the original cached MSI in an existing installation.

A separate clean per-user guest case installed a corrected copy of the 0.5.5 MSI. Both
same-version `/fomus` and `/fa` repairs returned 0 without changing payload bytes. The
native updater applied the verified 0.5.7 full package with no restart; all **524 deployed
files** matched. Older `/fomus`, `/fa`, and `/fomus` with the DLL temporarily held outside
the install root each returned 1603 with the intended diagnostic and no payload mutation.
Native AppSearch found both the newer DLL and per-user `DisplayVersion=0.5.7`.

After enabling default-folder Shell integration, native uninstall returned 0 and removed
the app directory, both product registrations, both shortcuts and owned Shell/default
integration. Five seeded settings/plugin/opaque-recovery files and the generated document
retained identical hashes. This tests byte retention, not journal replay or live Undo.
An initial runner exit-code check failed before uninstall; the corrected runner waits for
the registration process and was source-hash verified before the successful case.

The 657,834-byte guest archive was exported with matching SHA-256; see
`artifacts/windows-upgrade-vm-20261021-212229-9192ed86/maintenance-evidence-transfer.json`
and the extracted `maintenance-evidence-20261022/` case. Snapshot
`guarded-msi-maintenance-passed-20261022` preserves the passing guest. These are silent
MSI/native updater checks, not an interactive released-app upgrade or an all-users/UAC test.
The policy ships in the 0.5.8 MSI; the historical failures in older public installers remain valid.

## Still unverified

- A controlled UI repeat of installing 0.5.0, checking/downloading, choosing Install Now,
  responding to UAC, and reviewing the new app after restart.
- The remaining released-app matrix: per-user full/no-base, Restart Now, all-users UAC paths,
  interactive maintenance/Apps UI, and the public GitHub feed. The first seeded per-user delta/apply-on-close
  path passed file verification, but tab persistence failed as recorded above.
- UI outcomes after cancelling UAC or after an updater child process fails following a successful
  launch. The application can report synchronous launch errors; it cannot infer the outcome of
  a child process after it has exited.
- Remaining Windows Installer repair variants, all-users uninstall, and settings/recovery-data
  retention. The standard per-user `/fomus` repair is now a confirmed mixed-payload failure;
  native per-user uninstall cleanup and its settings deletion are recorded above.

Establish a disposable installation or isolated Windows environment before repeating upgrades.
Do not downgrade or replace the user's installation to obtain this coverage without authorization.
