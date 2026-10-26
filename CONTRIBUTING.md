# Contributing to WinThunar

Bug reports and feature requests go in [GitHub issues](https://github.com/TheTriumphantFox/WinThunar/issues);
first-time testers can use the [tester-feedback form](https://github.com/TheTriumphantFox/WinThunar/issues/new?template=tester-feedback.yml).
WinThunar is a clean-room implementation: do not copy code or artwork from Thunar or other
Xfce projects, which are GPL-licensed.

## Build

Use Windows 11 with the .NET 10 SDK and Windows SDK build tools required by
`WinThunar.csproj` (the target framework is `net10.0-windows10.0.26100.0`). The commands
below target x64. Project platform entries for x86 and ARM64 do not imply downloadable
releases for those architectures.

```powershell
dotnet restore .\WinThunar.csproj -r win-x64
dotnet build .\WinThunar.csproj --configuration Debug -p:Platform=x64 --no-restore
dotnet run --project .\WinThunar.csproj --configuration Debug -p:Platform=x64
```

Run the isolated file-operation and browser-state tests:

```powershell
dotnet test .\tests\WinThunar.FileOps.Tests\WinThunar.FileOps.Tests.csproj --configuration Release
```

The source includes an isolated storage-failure and process-termination harness:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-FileOperationsStress.ps1
```

It injects native copy errors and terminates its own test workers. The JSON report separates those
checks from optional real cross-volume tests and skipped physical-storage tests. See the
[stress-harness scope and results](docs/FILE-OPERATIONS-STRESS.md).

The source also includes an isolated desktop smoke build. Its harness is excluded from normal
application packages; it does not restore or save your normal session:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-DesktopSmoke.ps1
```

The script writes a JSON report and rendered PNGs to a new temporary folder. The test-only
code is excluded from normal builds. The relative-symbolic-link unit test explicitly skips
when the Windows account lacks permission to create symbolic links.

Developer Mode must be enabled in Windows Settings to register and launch the development package.

## Windows release

Install the Velopack packaging tool once:

```powershell
dotnet tool install -g vpk --version 1.2.161
```

Build a release from the main checkout; this example uses 0.5.15:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Publish-Release.ps1 -Version 0.5.15
```

For a new release, choose an unused version and update all four version locations before
building. The script refuses existing output paths. It writes a self-contained x64 folder,
a versioned ZIP and MSI, SHA-256 checksum files, and Velopack distribution packages beneath
`artifacts/`. **Never delete `artifacts/installer`: later versions need its earlier packages
to build delta updates.**

A build reaches installed users only after its Velopack packages are published to GitHub
Releases. See [building and publishing releases](docs/RELEASES.md) for the version checklist,
output filenames, upload command, and update-feed testing.

Bundled plugin manifests live beside the executable in `Plugins`. Personal manifests and
custom actions live in `%LOCALAPPDATA%\WinThunar\Plugins`, so reinstalling or replacing the
app does not overwrite them.

## Icon

WinThunar's icon is an original design (a folder with a lightning bolt) and is covered by the
project's MIT License. It is drawn by `scripts\New-AppIcons.ps1`, which writes `Assets\AppIcon.ico`
and the MSIX logo images; change the shapes there and re-run it rather than editing the images.

