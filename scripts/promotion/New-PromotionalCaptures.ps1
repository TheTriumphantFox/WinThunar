param(
    [string]$RepositoryRoot = (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent),
    [string]$SourceRef = 'v0.5.15',
    [string]$OutputDirectory,
    # The location bar shows this path, so keep it outside the capturing account's profile.
    [string]$FixtureDirectory = 'C:\Users\Public\Documents\WinThunar Demo'
)
$ErrorActionPreference = 'Stop'
$runRoot = Join-Path $env:TEMP ('WinThunarPromo-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
$source = Join-Path $runRoot 'source'
$publish = Join-Path $runRoot 'app'
$fixture = [IO.Path]::GetFullPath($FixtureDirectory)
if (Test-Path -LiteralPath $fixture) { throw "Choose a new fixture directory: $fixture" }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $runRoot 'captures' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) { throw "Choose a new capture output directory: $OutputDirectory" }
New-Item -ItemType Directory -Path $source, $OutputDirectory, (Join-Path $fixture 'Incoming'), (Join-Path $fixture 'Organized') -Force | Out-Null
$sourceArchive = Join-Path $runRoot 'source.zip'
& git -C $RepositoryRoot archive --format=zip "--output=$sourceArchive" $SourceRef
if ($LASTEXITCODE -ne 0) { throw 'Could not archive the release source.' }
Expand-Archive -LiteralPath (Join-Path $runRoot 'source.zip') -DestinationPath $source

# Instrument only the disposable source snapshot. Real settings, transfer journals,
# shell registration, installed builds, and release/update packages are not used.
$profile = Join-Path $runRoot 'profile'
foreach ($file in Get-ChildItem -LiteralPath $source -Filter '*.cs' -Recurse) {
    $text = [IO.File]::ReadAllText($file.FullName)
    $text = $text.Replace('Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)', '@"' + $profile + '"')
    [IO.File]::WriteAllText($file.FullName, $text)
}
$programPath = Join-Path $source 'Program.cs'
$program = [IO.File]::ReadAllText($programPath)
$program = $program.Replace('"WinThunar.Main"', '"WinThunar.Promo.' + [guid]::NewGuid().ToString('N') + '"')
$program = $program.Replace('if (ShellRegistrationService.IsSupported && Environment.ProcessPath is { } executable)', 'if (false && ShellRegistrationService.IsSupported && Environment.ProcessPath is { } executable)')
[IO.File]::WriteAllText($programPath, $program)
$appPath = Join-Path $source 'App.xaml.cs'
[IO.File]::WriteAllText($appPath, [IO.File]::ReadAllText($appPath).Replace('Updates.StartAutomaticChecks();', '// Disabled in promotional capture build.'))
$pagePath = Join-Path $source 'MainPage.xaml.cs'
$page = [IO.File]::ReadAllText($pagePath)
$marker = 'private async void Page_Loaded(object sender, RoutedEventArgs e)'
$start = $page.IndexOf($marker, [StringComparison]::Ordinal)
if ($start -lt 0) { throw 'Release Page_Loaded hook not found.' }
$brace = $page.IndexOf('{', $start)
$page = $page.Insert($brace + 1, "`r`n        await CapturePromoAsync();`r`n        return;`r`n")
$bulkStart = $page.IndexOf('private async Task BulkRenameAsync(', [StringComparison]::Ordinal)
$show = $page.IndexOf('await dialog.ShowAsync()', $bulkStart, [StringComparison]::Ordinal)
if ($bulkStart -lt 0 -or $show -lt 0) { throw 'Bulk-rename capture hook not found.' }
$page = $page.Remove($show, 'await dialog.ShowAsync()'.Length).Insert($show, 'await CapturePromoRenameAsync(dialog)')
[IO.File]::WriteAllText($pagePath, $page)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'CapturePromo.cs.txt') -Destination (Join-Path $source 'CapturePromo.cs')

foreach ($name in @('draft-budget.txt', 'draft-checklist.txt', 'draft-notes.txt')) {
    [IO.File]::WriteAllText((Join-Path $fixture "Incoming\$name"), "Synthetic WinThunar demonstration file: $name`r`n")
}
foreach ($name in @('Reference', 'Resources')) { New-Item -ItemType Directory -Path (Join-Path $fixture "Incoming\$name") | Out-Null }
[IO.File]::WriteAllText((Join-Path $fixture 'Incoming\Read me.txt'), 'Only sample files are used in this demo.')
[IO.File]::WriteAllText((Join-Path $fixture 'Organized\About this folder.txt'), 'Renamed project documents will appear here.')
foreach ($file in Get-ChildItem -LiteralPath $fixture -Recurse) { $file.LastWriteTime = [datetime]'2026-10-14T12:00:00' }

& dotnet publish (Join-Path $source 'WinThunar.csproj') --configuration Release --runtime win-x64 --self-contained true -p:Platform=x64 -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true -p:PublishProfile=win-x64 -p:PublishTrimmed=false -p:PublishReadyToRun=false --output $publish
if ($LASTEXITCODE -ne 0) { throw "Promotional capture build failed. Scratch folder: $runRoot" }
$priorOutput = $env:WINTHUNAR_PROMO_OUTPUT
$priorFixture = $env:WINTHUNAR_PROMO_FIXTURE
$priorSource = $env:WINTHUNAR_PROMO_SOURCE
try {
    $env:WINTHUNAR_PROMO_OUTPUT = $OutputDirectory
    $env:WINTHUNAR_PROMO_FIXTURE = $fixture
    $env:WINTHUNAR_PROMO_SOURCE = $SourceRef
    $captureProcess = Start-Process -FilePath (Join-Path $publish 'WinThunar.exe') -WindowStyle Hidden -PassThru
    if (-not $captureProcess.WaitForExit(60000)) {
        Stop-Process -Id $captureProcess.Id -ErrorAction SilentlyContinue
        throw "Promotional capture timed out. Scratch folder: $runRoot"
    }
    $reportPath = Join-Path $OutputDirectory 'capture-result.json'
    if (-not (Test-Path -LiteralPath $reportPath)) { throw "No capture report. Exit: $($captureProcess.ExitCode). Scratch folder: $runRoot" }
    $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    if ($report.failure) { throw $report.failure }
    $report.checks | Write-Output
    Write-Output "Promotional screenshots: $OutputDirectory"
}
finally {
    if ($null -eq $priorOutput) { Remove-Item Env:WINTHUNAR_PROMO_OUTPUT -ErrorAction SilentlyContinue } else { $env:WINTHUNAR_PROMO_OUTPUT = $priorOutput }
    if ($null -eq $priorFixture) { Remove-Item Env:WINTHUNAR_PROMO_FIXTURE -ErrorAction SilentlyContinue } else { $env:WINTHUNAR_PROMO_FIXTURE = $priorFixture }
    if ($null -eq $priorSource) { Remove-Item Env:WINTHUNAR_PROMO_SOURCE -ErrorAction SilentlyContinue } else { $env:WINTHUNAR_PROMO_SOURCE = $priorSource }
    # The fixture holds only the synthetic files created above.
    Remove-Item -LiteralPath $fixture -Recurse -Force -ErrorAction SilentlyContinue
}
