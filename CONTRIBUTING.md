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

## Windows App SDK upgrades and the System colors theme

The **System colors** theme (`Services/SystemColorsTheme.cs`) recolors brushes in place rather than
asking WinUI to reload resources, so it carries its own copies of some WinUI default colors. Those
copies are also what every other theme shows, so they must match the WinUI version WinThunar ships.
They were last checked against **Microsoft.WindowsAppSDK 2.4.0**, which resolves to
**Microsoft.WindowsAppSDK.WinUI 2.3.6**.

| Copied value | Light | Dark | Lives in |
| --- | --- | --- | --- |
| `SystemAltHighColor` (window surfaces) | `#FFFFFFFF` | `#FF000000` | `App.xaml` |
| `SubtleFillColorSecondary` (selected rows and tree items) | `#09000000` | `#0FFFFFFF` | `SystemColorsTheme.cs` |
| `SubtleFillColorTertiary` (selected, pointer over; selected icons) | `#06000000` | `#0AFFFFFF` | `SystemColorsTheme.cs` |
| `SolidBackgroundFillColorTertiary` (selected tab) | `#FFF9F9F9` | `#FF282828` | `SystemColorsTheme.cs` |
| `LayerFillColorDefault` (System colors panes) | `#80FFFFFF` | `#4C3A3A3A` | `SystemColorsTheme.cs` |

`SystemColorsTheme.SelectionKeys` also records which of the two subtle fills each selection key
uses. After changing the `Microsoft.WindowsAppSDK` version, restore, find the WinUI version in
`obj\project.assets.json` (`Microsoft.WindowsAppSDK.WinUI/…`), and compare the table with that
version's `generic.xaml`:

```powershell
$generic = Get-ChildItem "$env:USERPROFILE\.nuget\packages\microsoft.windowsappsdk.winui\*\lib\native\Microsoft.UI\Themes\generic.xaml"
Select-String -Path $generic -Pattern 'x:Key="(SubtleFillColorSecondary|SubtleFillColorTertiary|SolidBackgroundFillColorTertiary|LayerFillColorDefault|SystemAltHighColor)"|x:Key="((ListView|GridView|TreeView)ItemBackgroundSelected(PointerOver|Pressed)?|TabViewItemHeaderBackgroundSelected)" ResourceKey'
```

Each value appears once per theme dictionary: `Default` (dark) first, then `HighContrast`, then
`Light`. High Contrast is never copied. Update the table and the code together, then run the
desktop smoke script; its theme checks confirm every theme draws file-row selection with
WinThunar's shared brush.

Three WinUI behaviours shaped this design; keep them in mind when changing it:

- **Control templates ignore app-level overrides.** Selection and tab keys such as
  `ListViewItemBackgroundSelected` put in `App.xaml`'s theme dictionaries never reach the list,
  tree or tab templates. `SystemColorsTheme.Attach` puts them in each control's own resources
  instead, from the `MainPage` constructor. A new list, grid, tree or tab view that should follow
  System colors needs adding to that list.
- **A `StaticResource` alias in `App.xaml`'s theme dictionaries resolves once**, against the
  Windows theme, so Light can end up with the Dark color. Write literal colors there.
- **WinUI keys its dark dictionary `Default`.** A `Dark` key is skipped by the control templates.

The theme also recolors selections when the Windows accent changes (`UISettings.ColorValuesChanged`);
that path has not been exercised by an automated check.

## Windows release

Install the Velopack packaging tool once:

```powershell
dotnet tool install -g vpk --version 1.2.161
```

Build a release from the main checkout; this example uses 0.6.1:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Publish-Release.ps1 -Version 0.6.1
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

