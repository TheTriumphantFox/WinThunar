# Distribution channels

Prepared 26 October 2026; manifests updated 29 October 2026 for **0.6.0**. GitHub Releases remains the source of every download;
these channels point people at the same files. Update the version, URLs and digests here with
each release, after the GitHub release is public.

| Channel | Status | What people run |
| --- | --- | --- |
| GitHub Releases | Published | MSI or portable ZIP from the README |
| Scoop (this repository's bucket) | Live once `bucket/winthunar.json` is on `main` | `scoop bucket add winthunar https://github.com/TheTriumphantFox/WinThunar`, then `scoop install winthunar` |
| WinGet | Manifest prepared and validated locally; not submitted | `winget install TheTriumphantFox.WinThunar` once accepted |
| Scoop Extras | Not submitted | `scoop install extras/winthunar` |
| Microsoft Store | Not started; needs a signed installer | Store listing |
| Code signing | Not set up; the release script accepts `-SignParams` | Removes the SmartScreen "unknown publisher" warning over time |

## WinGet

The manifests in `packaging/winget/` describe 0.6.0's MSI and pass `winget validate`
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
files to `manifests/t/TheTriumphantFox/WinThunar/0.6.0/`, and open a pull request. For later
releases, `wingetcreate update TheTriumphantFox.WinThunar --version <version> --urls <msi url> --submit`
rewrites the version, URL, digest and product code; copy the result back into `packaging/winget/`.

## Scoop

`bucket/winthunar.json` makes this repository a Scoop bucket. It installs the portable ZIP,
adds a Start menu shortcut, and puts `WinThunar.exe` on the path. `checkver` and `autoupdate`
follow GitHub releases, so `scoop update winthunar` picks up new versions once the manifest's
version and hash are updated here. Portable copies do not use WinThunar's own updater.

The manifest is valid JSON with the published ZIP's SHA-256, but Scoop was not available to
test an install. Check `scoop install winthunar` once after the bucket reaches `main`.

[Scoop Extras](https://github.com/ScoopInstaller/Extras) is the bucket most Scoop users already
have. It takes apps that don't fit the main bucket, which is limited to well-known command-line
tools. Submit there once an install from this repository's bucket has been checked.

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

- **AlternativeTo:** [submitted 19 October](https://alternativeto.net/software/winthunar/)
  and still awaiting review. Check its state; if it is still pending, update the screenshot
  to `docs/media/winthunar-split-panes.png` and the description from [PROMOTION.md](PROMOTION.md).
- **Softpedia:** submit through the developer/submit link on softpedia.com. Editors install
  and test the app, and may award a "100% Clean" badge.
- **MajorGeeks:** submit through the [about and contact page](https://www.majorgeeks.com/content/page/aboutcontact_us.html).
  Editors test submissions and reject most; authors cannot edit their listings.

Use the AlternativeTo field values in [LAUNCH.md](LAUNCH.md#alternativeto-submission-fields)
for all three.
