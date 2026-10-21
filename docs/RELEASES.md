# Building and publishing releases

Checked 21 October 2026 against the release script, project/manifest versions, and GitHub
Releases. The latest published application is **0.5.0**, released 14 October 2026.
See the [release/source status](ROADMAP.md#release-and-source-status) for unreleased work.
The prepared local candidate is **0.5.2**; it has not been published. Its version locations
are aligned and its [candidate notes](RELEASE-0.5.2.md) describe verification limits.

## Prerequisites and version checklist

Use the main checkout on Windows with the [build prerequisites](../README.md#build).
Public release packaging targets `win-x64` and uses Velopack 1.2.161:

```powershell
dotnet tool install -g vpk --version 1.2.161
```

Before a new release, select an unused three-part version and update all of these together:

1. `Version`, `AssemblyVersion`, and `FileVersion` in `WinThunar.csproj`.
2. `Identity Version` in `Package.appxmanifest` (four numeric components).
3. The default `$Version` in `scripts/Publish-Release.ps1`.
4. The README release example and the current release/status references in these docs.

For example, a release `X.Y.Z` uses assembly/file/manifest version `X.Y.Z.0`.
Documentation-only changes do not require a new application version.

## Build and local outputs

Run from the main checkout. This example uses the prepared candidate; existing output paths
are deliberately not overwritten:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Publish-Release.ps1 -Version 0.5.2
```

For version `X.Y.Z`, the script writes:

| Output | Purpose |
| --- | --- |
| `artifacts/WinThunar-X.Y.Z-win-x64/` | Self-contained executable, runtime, and bundled plugin manifests. |
| `artifacts/WinThunar-X.Y.Z-win-x64.zip` and `.zip.sha256` | Local portable archive and SHA-256 checksum. |
| `artifacts/WinThunar-X.Y.Z-win-x64.msi` and `.msi.sha256` | Local MSI copy, with install-scope and shortcut choices, and its checksum. |
| `artifacts/installer/` | Velopack MSI, Setup EXE, portable ZIP, versioned packages, and update-feed files. |

**Never delete `artifacts/installer`.** Keep earlier packages between releases; later
versions need them to build delta updates. Rebuilding the same version requires resolving
the exact conflicting outputs deliberately. Do not remove the entire artifacts tree.

`-SkipInstaller` builds only the self-contained folder, versioned ZIP, and ZIP checksum.
`-PackageExistingPublish` packages an already-existing versioned publish folder without
running `dotnet publish`; it still refuses pre-existing final ZIP/MSI/checksum outputs.
Use it only when that folder already contains the intended application version and files.

The script validates `WinThunar.exe` and at least four bundled JSON plugin manifests. It
also adds shortcut choices to the Velopack MSI before copying it to the versioned local name.

## GitHub filenames and installation

`vpk upload` publishes files from `artifacts/installer`, whose user-facing names differ
from the versioned copies above:

| GitHub asset | Use |
| --- | --- |
| `WinThunar.App-win.msi` | Recommended installer; current-user/all-users choice and optional shortcuts. |
| `WinThunar.App-win-Portable.zip` | Extract and run without installation; no automatic updates. |
| `WinThunar.App-win-Setup.exe` | Velopack's per-user setup path; it does not offer the MSI's scope/shortcut choices. |
| `WinThunar.App-X.Y.Z-full.nupkg`, delta packages when available, `RELEASES`, and `releases.win.json` | Application update packages/feed; not files users manually install. |

The upload does not automatically include the separate versioned ZIP/MSI checksum files
from the parent `artifacts` folder. Do not point users to those files on GitHub unless they
were separately uploaded. Windows 11 x64 is the supported download target; a separate .NET
installation is not required. x86/ARM64 entries in the project are not release downloads.

## Publish to GitHub Releases

A build only reaches installed users after publishing its Velopack packages to GitHub
Releases. A Git commit or merged documentation PR is not an application release.

Provide a GitHub token securely through `GITHUB_TOKEN`; it must have permission to create
releases and upload assets for this repository. Keep it out of source, command history,
and logs. With the token already supplied, run:

```powershell
$releaseVersion = '0.5.2' # Replace with the version just built.
try {
    vpk upload github --outputDir .\artifacts\installer --repoUrl https://github.com/TheTriumphantFox/WinThunar --token $env:GITHUB_TOKEN --publish --releaseName "WinThunar $releaseVersion" --tag "v$releaseVersion"
    if ($LASTEXITCODE -ne 0) { throw 'GitHub release upload failed.' }
}
finally {
    Remove-Item Env:GITHUB_TOKEN -ErrorAction SilentlyContinue
}
```

Check the published tag/version, MSI and portable links, and feed assets afterward. A 401
or 403 can indicate an expired token or insufficient repository permission; refresh the
credential securely before retrying.

## Verify automatic updating

Install the previous release, then launch it from a PowerShell session that sets
`WINTHUNAR_UPDATE_FEED` to the new local `artifacts/installer` directory or to a URL serving
that feed. Restore/remove the override after the test. Select **Help > Check for Updates...**
and verify the expected version is downloaded. Portable copies do not support this updater.

Installed copies first evaluate automatic checking about 15 seconds after launch, then
reevaluate hourly while open. The normal interval between successful feed checks is one
day; a failed feed check can be retried on the next evaluation. Per-user writable installations apply a
downloaded update after the final window closes, or through **Restart Now**. An all-users
installation in Program Files needs elevation; **Install Now** requests it and closing the
app does not trigger that prompt. Restart/install actions wait for running transfers to
finish or be cancelled. Automatic checks can be disabled in Preferences' Updates section.
