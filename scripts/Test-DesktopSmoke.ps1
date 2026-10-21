param([string]$OutputDirectory = (Join-Path $env:TEMP ('WinThunar-desktop-smoke-' + [guid]::NewGuid().ToString('N'))))
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runRoot = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $runRoot) { throw "Choose a new smoke-test output directory: $runRoot" }
New-Item -ItemType Directory -Path $runRoot | Out-Null
$publish = Join-Path $runRoot 'app'
& dotnet publish (Join-Path $projectRoot 'WinThunar.csproj') --configuration Release --runtime win-x64 --self-contained true -p:Platform=x64 -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true -p:PublishProfile=win-x64 -p:RoadmapSmokeTest=true --output $publish
if ($LASTEXITCODE -ne 0) { throw 'Desktop smoke build failed.' }
$previousDirectory = $env:WINTHUNAR_SMOKE_DIRECTORY
$previousRestartTheme = $env:WINTHUNAR_SMOKE_RESTART_THEME
try {
    Remove-Item Env:WINTHUNAR_SMOKE_RESTART_THEME -ErrorAction SilentlyContinue
    $env:WINTHUNAR_SMOKE_DIRECTORY = Join-Path $runRoot 'results'
    New-Item -ItemType Directory -Path $env:WINTHUNAR_SMOKE_DIRECTORY | Out-Null
    $process = Start-Process -FilePath (Join-Path $publish 'WinThunar.exe') -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(45000)) {
        # Stop only the test process started above, never an installed or portable user instance.
        Stop-Process -Id $process.Id -ErrorAction SilentlyContinue
        throw "Desktop smoke test timed out. Outputs: $runRoot"
    }
    $report = Join-Path $env:WINTHUNAR_SMOKE_DIRECTORY 'result.json'
    if (-not (Test-Path -LiteralPath $report)) { throw "No desktop smoke report (exit $($process.ExitCode)). Outputs: $runRoot" }
    $result = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    $result.checks | Write-Output
    if ($result.failure) { throw $result.failure }
    # Restart only the generated smoke app, using its isolated appearance settings.
    $env:WINTHUNAR_SMOKE_RESTART_THEME = 'Dark'
    $restart = Start-Process -FilePath (Join-Path $publish 'WinThunar.exe') -WindowStyle Hidden -PassThru
    if (-not $restart.WaitForExit(45000)) {
        Stop-Process -Id $restart.Id -ErrorAction SilentlyContinue
        throw "Theme restart check timed out. Outputs: $runRoot"
    }
    $restartReport = Join-Path $env:WINTHUNAR_SMOKE_DIRECTORY 'theme-restart-result.json'
    if (-not (Test-Path -LiteralPath $restartReport)) { throw "No theme restart report (exit $($restart.ExitCode)). Outputs: $runRoot" }
    $restartResult = Get-Content -LiteralPath $restartReport -Raw | ConvertFrom-Json
    $restartResult.checks | Write-Output
    if ($restartResult.failure) { throw $restartResult.failure }
    Write-Output "Desktop smoke checks passed. Report and rendered views: $report"
}
finally {
    if ($null -eq $previousDirectory) { Remove-Item Env:WINTHUNAR_SMOKE_DIRECTORY -ErrorAction SilentlyContinue }
    else { $env:WINTHUNAR_SMOKE_DIRECTORY = $previousDirectory }
    if ($null -eq $previousRestartTheme) { Remove-Item Env:WINTHUNAR_SMOKE_RESTART_THEME -ErrorAction SilentlyContinue }
    else { $env:WINTHUNAR_SMOKE_RESTART_THEME = $previousRestartTheme }
}
