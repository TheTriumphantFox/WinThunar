# Installed updater verification

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

## Updater hardening in the 0.5.4 source candidate

These changes are **unreleased** and are not part of the installed 0.5.3 binaries:

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

The normal x64 application build passed with **zero warnings/errors**. The complete service suite
passed with **134 passed, one skipped, zero failed** (135 total); the skip requires Windows
symbolic-link privilege. The built xUnit executable ran outside the sandbox because the sandboxed
VSTest host could not connect. Its XML report is preserved as `service-tests.xml` in the final
evidence directory above. The test-project build reported NU1900 because NuGet vulnerability
metadata was unavailable from the sandbox; package restoration and compilation completed.

## Still unverified

- A controlled UI repeat of installing 0.5.0, checking/downloading, choosing Install Now,
  responding to UAC, and reviewing the new app after restart.
- Per-user apply-on-close and a real installed delta update. Package reconstruction alone does
  not establish either workflow.
- UI outcomes after cancelling UAC or after an updater child process fails following a successful
  launch. The application can report synchronous launch errors; it cannot infer the outcome of
  a child process after it has exited.
- Windows Installer repair/uninstall behavior with the two registry versions described above.

Establish a disposable installation or isolated Windows environment before repeating upgrades.
Do not downgrade or replace the user's installation to obtain this coverage without authorization.
