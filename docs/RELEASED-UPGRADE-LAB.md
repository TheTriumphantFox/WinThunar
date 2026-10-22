# Released-app upgrade lab: 0.5.5 to 0.5.6

The host preparation script copies published binaries into a new bundle and verifies every
binary against the SHA-256 digests in the preserved GitHub release metadata. It never runs an
installer, changes a release package, or starts the host's installed WinThunar.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\New-ReleasedUpgradeLab.ps1
```

## Isolation and current availability

The development PC initially checked on 21 October 2026 runs **Windows 11 Home**, with no Windows Sandbox
executable, Hyper-V management tools, VirtualBox, VMware, or QEMU installation then found. Microsoft
[does not support Windows Sandbox on Home](https://learn.microsoft.com/windows/security/application-security/application-isolation/windows-sandbox).
The CPU hypervisor flag is present and about 16 GB of physical RAM is installed, but that alone
does not provide a disposable Windows desktop. Initial bundle preparation made no host changes;
the user subsequently approved the minimal VirtualBox installation described below.

Local VM preparation is now staged in
`artifacts/windows-upgrade-vm-20261021-212229-9192ed86/`. A read-only
`WHvGetCapability(HypervisorPresent)` query succeeded (`0x00000000`, value 1), and the x64
Visual C++ runtime is already installed. Oracle VirtualBox **7.2.22** was selected for its
built-in EFI and virtual TPM support. Its **178,101,352-byte** Windows installer matches Oracle's
SHA-256 checksum and has a valid Oracle America Authenticode signature. With explicit user
approval, **VBoxApplication** was installed to `C:\Program Files\Oracle\VirtualBox`; the USB,
network, and Python features are confirmed absent (MSI feature state 2). Desktop/Quick Launch
shortcuts and file associations were disabled. The support driver is running. Installation and
the core-driver repair both returned 0, with no restart required or performed. No Windows
optional feature or security setting was changed. See Oracle's
[Windows installer options](https://docs.oracle.com/en/virtualization/virtualbox/7.2/user/installation.html).

The first MSI invocation unnecessarily supplied `REMOVE` for optional features. Oracle's
`ca_VBoxSupDrvInst`/start conditions are `NOT REMOVE`, so the MSI reported success while leaving
the support driver unregistered. A first firmware start failed with `VBoxDrvStub` missing.
Repairing only `VBoxApplication`, without the `REMOVE` property, installed/started the driver
and preserved all optional features as absent. Both logs and the original hardening failure are
preserved; do not reuse the initial command containing `REMOVE`.

`vm-created.json` identifies **WinThunar-UpgradeLab-5188e3ef**, UUID
`1690f743-510f-4228-a885-299c021c6ee6`, with 4 GiB RAM (initially 6 GiB), a dynamic 80 GiB VDI, EFI,
Secure Boot, and virtual TPM 2.0. Its VirtualBox configuration registry and all VM files are
inside the owned artifact directory. Networking, USB, audio, clipboard sharing, drag-and-drop,
and shared folders are disabled. A **firmware-only boot passed using the native Windows
hypervisor**, and the empty guest was powered off again. This verifies the VM platform, not
Windows installation or the released-app upgrade workflow. Evidence includes
`host-final-installed-features.json`, `host-core-repair-result.json`, `vm-final-info.txt`,
`firmware-check-result.json`, and the guest's `Logs/VBox.log`.

The initial two-vCPU configuration stalled in EFI after Windows Setup's first restart, with a
black display and the firmware log ending near `PciHostBridgeDxe.efi`. This matches a
[reported VirtualBox NEM/two-vCPU issue](https://github.com/VirtualBox/virtualbox/issues/799).
The unfinished setup state was preserved in snapshot `unfinished-setup-before-cpu-change`
(**not** the clean baseline), and only the guest CPU count changed to **three**. The same VM
then passed EFI and reached the Windows boot loader. Host hypervisor/security settings were
unchanged. `efi-cpu-workaround.json` and `pre-cpu-workaround-VBox.log` preserve this evidence;
the current plan records three CPUs and the original two-CPU setting.

That preparation directory also contains `WinThunar-UpgradeLab-Input.iso`, a **650,260,480-byte**
read-only input image containing the seven release binaries, private feed, guest scripts, and
runbook. It excludes the host's installed-state evidence. Extraction verified all seven binary
hashes again, and the extracted guest script rejects the originating host. This is **test-input
media**, not a Windows installer ISO. `input-iso.json` and `input-iso-verification.json` preserve
the image digest and content checks. It is attached to the VM as read-only optical media.

The user selected Microsoft's 90-day Enterprise evaluation and instructed a pause if registration
requires personal details. Microsoft's [evaluation download route](https://info.microsoft.com/ww-landing-windows-11-enterprise.html)
requires a registration form, so the media step was initially stopped without entering or
submitting any details. After the user reported registration complete, Microsoft's English x64
26H2 evaluation ISO was downloaded to the owned `media/` directory. Its **8,225,329,152 bytes**
match Microsoft's published SHA-256
`bc3f24086ebadc94489066b5ad78089e2cf5c3491e90e790bb81a2b199c10e38`;
see `windows-media-verification.json` and Microsoft's
[authenticity reference](https://support.microsoft.com/en-us/servicing/os/windows/docs/2026/09/verify-the-authenticity-of-a-windows-11-enterprise-evaluation-iso-file).
VirtualBox detects Windows 11 Enterprise Evaluation **10.0.26300.9457 x64 en-US**, image index 1,
with unattended installation supported. Installation has started only in the private VM, with
a generated local test account and bundled Guest Additions. Test credentials are stored in an
access-restricted local file and are never printed or passed as command-line password text.
Setup subsequently completed on build **26300**, with `IMAGE_STATE_COMPLETE`, both setup flags
zero, an unelevated local test user, and UAC enabled (`EnableLUA=1`, secure desktop enabled).
Guest Additions' execution service became available after a guest restart. The original 6 GiB
allocation left only about 350 MiB free on the host, so the guest was normally shut down and
reduced to 4 GiB. The offline evaluation initially reported notification mode with zero grace
time. Temporary NAT networking, without port forwarding or additional host components, allowed
normal Microsoft activation: license status 1 and 129,600 evaluation minutes (90 days). The guest
was then disconnected, normally shut down, and all NICs set to `none`. Snapshot
**`clean-windows-activated-before-winthunar`**, UUID `b9033125-7a7d-43c2-b916-924357bd81ba`,
preserves the powered-off, activated baseline with all seven guest-local release assets verified
and no WinThunar installation. See `clean-baseline-result.json`,
`guest-baseline-prerequisites.txt`, `guest-license-after-network.json`,
`guest-info-first-desktop.json`, `guest-license-first-check.json`, and
`evaluation-activation-preparation.json`. The first per-user delta case has run; the remaining
cases are pending. Its updater path passed, but tab persistence failed in the published app,
as described below.
The host WinThunar DLL, updater, manifest, update settings, and updater log
retained identical hashes after VM setup; see `host-winthunar-after-vm-setup/session-comparison.json`.

Use a **separate disposable Windows 11 x64 guest**, with a normal interactive user and UAC
enabled. Preserve a clean snapshot before each independent scenario. Installations must occur
inside that guest: another folder or another launch of the released app on the development PC
would share application settings, single-instance activation, Shell registration, and MSI identity.

Copy the complete bundle to a guest-local directory, such as `C:\WinThunarUpgradeLabInput`.
Do not run from a host-writable shared folder. Keep a guest-local output directory and export it
after the scenario. Disconnect external networking for deterministic local-feed checks; the
local feed still contains the public package hashes. A subsequent public-GitHub-feed test needs
networking and is a separate scenario.

`ReleasedUpgradeGuest.ps1` rejects the originating host using both MachineGuid and computer
name **before any writes or process launches**. It verifies all seven binary assets on every
invocation. The bundle is intended for this specific originating host and guest; these guards
are accident prevention, not a security boundary against an altered bundle.

## Follow-on MSI maintenance case (22 October 2026)

The later [unpublished maintenance-policy regression](MSI-MAINTENANCE.md) uses a corrected
MSI copy and the verified 0.5.7 full package in a separate clean per-user case. Same-version
repairs, three rejected older-repair cases, all 524 deployed target hashes, and uninstall
retention/positive Shell cleanup passed. Its archive was exported with matching length and
SHA-256. Snapshot `guarded-msi-maintenance-passed-20261022` is the current **Saved** state;
the earlier clean and failure snapshots are retained. This run reduced guest RAM to 3 GiB
after startup/resource delays; the original clean baseline still records 4 GiB. No host
security settings changed. This follow-on case does not complete the interactive released-app,
all-users/UAC or legacy-MSI migration matrix below.

## Bundle contents

- `installers/0.5.5/`: published generic MSI and Setup executable.
- `installers/0.5.6/`: published generic MSI and Setup executable for comparison/reinstallation.
- `feed/`: unmodified 0.5.5 full, 0.5.6 full, and 0.5.6 delta packages, plus a feed containing
  only the target 0.5.6 full/delta entries. The base package is available for deliberate cache seeding.
- `lab.json`: origin identity, versions, asset sizes/hashes, and original published asset URLs.
- `release-0.5.5.json` and `release-0.5.6.json`: recorded public release metadata.
- `ReleasedUpgradeGuest.ps1`, `Capture-InstalledUpdaterState.ps1`, and this runbook.

The generic release MSIs differ from the separately named local versioned MSI artifacts.
The bundle deliberately uses the **actual published generic assets**, identified by their public
digests, rather than substituting a similarly named local build.

## Preflight and per-user delta scenario

Run from an **unelevated** Windows PowerShell window in the guest bundle directory:

```powershell
.\ReleasedUpgradeGuest.ps1 -Action Preflight
.\ReleasedUpgradeGuest.ps1 -Action Initialize -Scope PerUser
```

Initialize requires no existing WinThunar installation, registration, settings, or process.
It creates a case ownership marker and disables automatic checks in the otherwise fresh guest
settings. Install the published **0.5.5 MSI** interactively, choose **Just me**, and record both
shortcut choices. Confirm the DLL is 0.5.5.0 and capture the installed baseline:

```powershell
.\ReleasedUpgradeGuest.ps1 -Action Capture -Scope PerUser -Label installed-055
```

Check `%LOCALAPPDATA%\WinThunar.App\packages` for the 0.5.5 full package. If the installer
did not cache it, seed only that guest-owned cache by copying
`feed\WinThunar.App-0.5.5-full.nupkg` into it, and record that fact. This is an explicitly seeded
delta scenario; it does not establish natural cache retention by the installer.

Launch through the guest helper so the real app inherits its private local feed:

```powershell
.\ReleasedUpgradeGuest.ps1 -Action Launch -Scope PerUser
```

Use generated guest folders to establish a bookmark, two tabs, and a saved view preference.
Version 0.5.5 predates the explicit theme selector; test its initial System fallback after upgrade,
then select Dark in 0.5.6 and verify persistence on a later restart. Do not claim theme migration
from a selector that did not exist in 0.5.5.

Use the actual **Help > Check for Updates** command. Confirm Ready/Restart Now, capture the
download state, and check the log for **Downloading delta**, **Delta update download complete**,
and native patch completion, with no full fallback. Verify the installed DLL remains 0.5.5.0
while the windows are still open. Capture before closing:

```powershell
.\ReleasedUpgradeGuest.ps1 -Action Capture -Scope PerUser -Label delta-ready
```

Close one of two windows: the process and 0.5.5 files should remain. Close the last window:
the updater should apply without UAC or an automatic relaunch. Wait for the guest updater to
finish, then verify the target and relaunch it:

```powershell
.\ReleasedUpgradeGuest.ps1 -Action VerifyTarget -Scope PerUser -Label after-close
.\ReleasedUpgradeGuest.ps1 -Action Launch -Scope PerUser
```

Verify all expected settings, normal folder browsing, file create/copy/rename/Undo on generated
data, and an explicit update check reporting UpToDate. Capture the post-restart state and UI.
The file verifier compares all deployed package files, including root updater/stub mappings;
its report explicitly leaves UI workflow status unverified until observations are recorded.

## Independent scenarios (restore the clean snapshot each time)

The first case used a **silent published MSI fixture**, with both shortcuts selected, and an
explicitly seeded base cache; this does not verify the installer's interactive scope controls
or natural base-cache retention. The real Help command downloaded and reconstructed the delta
without a full fallback. First-window close retained PID 3068 and 0.5.5; last-window close
applied 0.5.6 without UAC or an automatic relaunch. All **524 deployed target files** matched.
The bookmark, Compact List preference and generated file survived. However, the secondary
window's single-tab session overwrote the original window's two tabs, which were not saved
again at close. The full persistence case is therefore **Failed**. The published guest binaries
retain that finding; a separate source fix now saves on `Window.Closed` before update scheduling.
Its regression passed with **67 desktop checks and 2 restart checks**, and the normal x64 build
completed with zero warnings/errors. No commit or release was made.

See `released-upgrade-results.json`, `source-session-close-verification.json`, native logs and
the `ui-01` through `ui-03` images in the owned VM artifact directory. Failed capture attempts
and both failing source-test runs are retained. The active-log capture helper was corrected to
permit a writer while reading. One VirtualBox `copyto` operation returned success with a zero-byte
helper; the retry used the guest execution channel and checked the helper's SHA-256 before use.
Verify transferred evidence hashes before treating a transfer result as complete.

The subsequent standard per-user **`/fomus` MSI repair failed payload verification** despite
exit 0 and a 0.5.6 DLL/manifest. `WinThunar.deps.json` and `WinThunar.pri` reverted to the exact
public 0.5.5 hashes. Repair logs, all 524 comparisons, the two mismatches and their base-package
hashes are exported in `per-user-repair-evidence-20261022.zip` with a verified transfer hash.
Snapshot **`released-056-after-msi-repair-mixed-payload`** preserves the failed installation;
the untouched **`released-056-delta-session-loss-observed`** checkpoint is used for independent
uninstall testing. No leftover file or registry entry was removed to make a check pass.

The independent silent `/x` uninstall returned 0 and removed the app directory, both registrations
and both shortcuts. The generated document remained, and Explorer opened its parent fixture
directory afterward (`ui-05-explorer-after-uninstall.png`). However, the separate
`LocalAppData\WinThunar` directory and its session/update settings were also removed. The strict
settings-retention assertion failed; the recorded outcome separates cleanup from retention.
Recovery/Undo data was not seeded, Shell verbs were not enabled, and the Apps uninstall UI was
not exercised. The idle guest process was terminated after a restored UI close failed to land;
this fixture setup is recorded and does not replace the earlier successful actual close tests.

All three guest evidence archives were exported with matching byte counts and SHA-256 hashes.
The final normal Windows shutdown stopped guest services and blanked the display, but did not
reach VirtualBox's powered-off state during observation. Snapshot
**`post-uninstall-settings-removed-shutdown-pending`** preserves that state; the VM is left
**Saved**, not running, and successful shutdown is not claimed. The clean powered-off baseline,
post-upgrade checkpoint and separate repair-failure checkpoint remain available. See
`session-result-20261022.json` and `final-snapshot-list.txt`. A final read-only host comparison
still found installed 0.5.3.0, zero WinThunar processes and identical hashes for all five observed
files (`host-final-after-msi-tests-20261022/session-comparison.json`).

| Scenario | Required observations |
| --- | --- |
| Per-user delta, last-window close | Seeded base recorded; actual delta route; first-window close does not apply; last-window close applies; no UAC/relaunch; settings preserved. |
| Per-user full, no base | No cached base before check; logged full route; same close and persistence assertions. |
| Per-user Restart Now | Click the real button; only the guest app exits; update applies and one updated app restarts. |
| All-users UAC cancel | Initialize with `-Scope AllUsers`; install 0.5.5 for Everyone. Launch unelevated. Close with a ready update: no surprise UAC. Relaunch, choose Install Now, cancel UAC manually; record whether the app stays open/exits, deployed version, pending package, and retry path. |
| All-users UAC approve | Repeat from a clean snapshot; approve UAC manually; verify update and restart, file hashes, scope and both uninstall registrations. |
| MSI repair after updater upgrade | Capture both the MSI product-code entry and `MSI:WinThunar.App` before/after. Run Repair for the original MSI inside the guest. Capture any version regression, changed file hashes, shortcut behavior and pending packages. Do not assume a successful repair exit preserves 0.5.6. |
| MSI uninstall after updater upgrade | Use the guest's installed-app uninstall flow; record exit status and remaining files, registrations, shortcuts, Shell entries and retained settings. Confirm Explorer's folder handling still works. Never manually delete remaining data to make the test pass. |
| Public GitHub feed | Separate network-enabled guest run without the helper's local feed override; capture discovery, downloaded package hashes, delta/full choice and outcome. |

For each scenario keep: Windows build, user/elevation status, installer scope/shortcut choices,
MSI install/repair/uninstall verbose logs, app/updater process IDs, native updater logs, package
inventory, registry snapshots, settings copies, screenshots, expected versus observed behavior,
and a result of **Passed**, **Failed**, or **Not run**. UAC approval/cancellation is an explicit
human interaction; do not disable UAC to automate it. Log timestamps omit dates, so preserve
capture timestamps and correlate process IDs and settings check times.

All guest outputs live beneath `%USERPROFILE%\WinThunarUpgradeLab\<run-id>-<scope>`. Preserve
failures and ambiguous data. Export results before restoring/destroying the guest. Preparation,
asset hashes, or the separate generated console probe do not count as passing this UI matrix.
