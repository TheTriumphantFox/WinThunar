# Distribution channels

Prepared 26 October 2026 for **0.5.15**. GitHub Releases remains the source of every download;
these channels point people at the same files. Update the version, URLs and digests here with
each release, after the GitHub release is public.

| Channel | Status | What people run |
| --- | --- | --- |
| GitHub Releases | Published | MSI or portable ZIP from the README |
| Scoop (this repository's bucket) | Live on `main`; install, launch and uninstall tested 26 October 2026 | `scoop bucket add winthunar https://github.com/TheTriumphantFox/WinThunar`, then `scoop install winthunar` |
| WinGet | Manifest prepared and validated locally; not submitted | `winget install TheTriumphantFox.WinThunar` once accepted |
| Scoop Extras | Not submitted | `scoop install extras/winthunar` |
| Microsoft Store | Not started; needs a signed installer | Store listing |
| Code signing | Not set up; the release script accepts `-SignParams` | Removes the SmartScreen "unknown publisher" warning over time |

## WinGet

The manifests in `packaging/winget/` describe 0.5.15's MSI and pass `winget validate`
(WinGet 1.29). They use the versioned release URL rather than `latest/download`, because
WinGet checks the installer's SHA-256 and a `latest` URL changes with every release.

Details worth knowing before submitting:

- The MSI hides its own Apps & Features entry (`ARPSYSTEMCOMPONENT=1`). Velopack writes the
  visible **WinThunar** entry and updates its version as the app updates itself, so the
  manifest matches on display name, publisher and the MSI's stable `UpgradeCode`.
- The manifest declares the MSI's quiet default, a per-user install. Machine-wide installs
  through WinGet were not tested and are not offered.
- Installing through WinGet was not tested on this machine, which already has an all-users
  WinThunar. The winget-pkgs pull request runs its own install validation; read its result
  before relying on the manifest.

To submit, either use [wingetcreate](https://github.com/microsoft/winget-create) with a
GitHub token:

```powershell
wingetcreate submit --token <token> .\packaging\winget
```

or fork [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs), copy the three
files to `manifests/t/TheTriumphantFox/WinThunar/0.5.15/`, and open a pull request. For later
releases, `wingetcreate update TheTriumphantFox.WinThunar --version <version> --urls <msi url> --submit`
rewrites the version, URL, digest and product code; copy the result back into `packaging/winget/`.

## Scoop

`bucket/winthunar.json` makes this repository a Scoop bucket. It installs the portable ZIP,
adds a Start menu shortcut, and puts `WinThunar.exe` on the path. `checkver` and `autoupdate`
follow GitHub releases, so `scoop update winthunar` picks up new versions once the manifest's
version and hash are updated here. Portable copies do not use WinThunar's own updater.

### Scoop install test (26 October 2026)

The test ran in the disposable upgrade-lab VM (Windows 11 Enterprise Evaluation, unelevated
`WTLab` user), restored from `clean-windows-activated-before-winthunar` with temporary NAT
networking. The VM's earlier saved state was kept first as snapshot `before-scoop-test-20261026`.

| Check | Result |
| --- | --- |
| Install Scoop with its documented installer, then `scoop install git` | Passed |
| `scoop bucket add winthunar https://github.com/TheTriumphantFox/WinThunar` (from `main`) | Passed |
| `scoop install winthunar` | Installed 0.5.15; Scoop's hash check passed, and the cached ZIP's SHA-256 matches the public `WinThunar.App-win-Portable.zip` |
| Installed files | `WinThunar.dll` version 0.5.15.0, Velopack `.portable` marker present, `winthunar` shim on the path |
| Start menu shortcut **Scoop Apps > WinThunar** | Opened a responding **WinThunar 0.5.15** window on the user's desktop |
| Close the window normally | Process exited |
| `checkver.ps1` against the bucket | Reports `winthunar: 0.5.15` |
| `scoop uninstall winthunar` | Removed the app folder, shortcut and shim; kept `%LOCALAPPDATA%\WinThunar` |

The guest then shut down normally, its network adapter was set back to none, and the result is
snapshot `scoop-0515-install-launch-uninstall-passed-20261026`. Not covered: `scoop update`
across a version change (no later release exists yet) and the default-file-manager setting
from a Scoop path.

[Scoop Extras](https://github.com/ScoopInstaller/Extras) is the bucket most Scoop users already
have. It takes apps that don't fit the main bucket, which is limited to well-known command-line
tools. The in-repository install now passes, so WinThunar can be submitted there.

## Code signing

Unsigned downloads show **Windows protected your PC** with "Unknown publisher" until Windows
SmartScreen builds a reputation for the file, which takes many downloads. Signing names the
publisher and lets reputation carry across releases.

Two routes fit this project:

1. **Azure Artifact Signing** (formerly Trusted Signing). A monthly subscription, roughly
   US$10 for the Basic tier when checked; confirm on the
   [pricing page](https://azure.microsoft.com/pricing/details/artifact-signing/). Individual
   developers must be in the USA or Canada; organizations can also be in the EU or UK. Identity
   validation takes a few business days. Signing runs locally through `signtool` with
   Microsoft's signing client library, so it fits the current release process.
2. **[SignPath Foundation](https://signpath.org/)**. Free for OSI-licensed open-source
   projects, with SignPath Foundation named as the publisher. It requires an application, a
   code-signing policy on the project page, and a fully automated build whose binaries are
   verified to come from the repository, so releases would have to move to GitHub Actions.

Once a certificate or Azure account is ready, pass the `signtool sign` options to the release
script. For example, with Azure Artifact Signing:

```powershell
.\scripts\Publish-Release.ps1 -Version 0.5.16 -SignParams '/fd SHA256 /tr http://timestamp.acs.microsoft.com /td SHA256 /dlib "C:\path\to\Azure.CodeSigning.Dlib.dll" /dmd "C:\path\to\metadata.json"'
```

Velopack signs the app binaries, update stub and Setup.exe while packing. The MSI is edited
after packing, so the script signs it again and runs `signtool verify /pa` on it. This path
has not been run with a real certificate yet; check the first signed release's files with
`Get-AuthenticodeSignature` before uploading.

## Download sites and directories

These list free utilities and bring steady search traffic, but editors test what they list.
Submit after signing and the P0 stability work, so a reviewer's first run is a good one.

- **AlternativeTo:** [approved](https://alternativeto.net/software/winthunar/), confirmed by
  the maintainer on 26 October 2026. Update its screenshot to the 0.5.15
  `docs/media/winthunar-split-panes.png` if it still shows the 0.5.0 capture.
- **Softpedia:** submit through the developer/submit link on softpedia.com. Editors install
  and test the app, and may award a "100% Clean" badge.
- **MajorGeeks:** submit through the [about and contact page](https://www.majorgeeks.com/content/page/aboutcontact_us.html).
  Editors test submissions and reject most; authors cannot edit their listings.

Use the AlternativeTo field values in [LAUNCH.md](LAUNCH.md#alternativeto-submission-fields)
for all three.
