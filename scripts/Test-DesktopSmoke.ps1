param(
    [string]$OutputDirectory = (Join-Path $env:TEMP ('WinThunar-desktop-smoke-' + [guid]::NewGuid().ToString('N'))),
    [ValidateRange(0,3600)][int]$EnduranceSeconds = 0,
    [ValidateRange(0,600)][int]$DiagnosticHoldSeconds = 0
)
$ErrorActionPreference = 'Stop'
if ($DiagnosticHoldSeconds -gt 0 -and $EnduranceSeconds -eq 0) { throw 'DiagnosticHoldSeconds requires an endurance run.' }
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runRoot = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $runRoot) { throw "Choose a new smoke-test output directory: $runRoot" }
New-Item -ItemType Directory -Path $runRoot | Out-Null
$publish = Join-Path $runRoot 'app'
function Save-ProcessOutcome($TestProcess, [string]$Name, [bool]$TimedOut) {
    $TestProcess.Refresh()
    $code = if ($TestProcess.HasExited) { $TestProcess.ExitCode } else { $null }
    [ordered]@{
        processId = $TestProcess.Id
        executable = Join-Path $publish 'WinThunar.exe'
        recordedUtc = [DateTime]::UtcNow.ToString('o')
        timedOut = $TimedOut
        exitCode = $code
        enduranceSeconds = $EnduranceSeconds
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runRoot ($Name + '-process.json')) -Encoding UTF8
}
& dotnet publish (Join-Path $projectRoot 'WinThunar.csproj') --configuration Release --runtime win-x64 --self-contained true -p:Platform=x64 -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true -p:PublishProfile=win-x64 -p:RoadmapSmokeTest=true --output $publish
if ($LASTEXITCODE -ne 0) { throw 'Desktop smoke build failed.' }
$previousDirectory = $env:WINTHUNAR_SMOKE_DIRECTORY
$previousRestartTheme = $env:WINTHUNAR_SMOKE_RESTART_THEME
$previousEndurance = $env:WINTHUNAR_SMOKE_ENDURANCE_SECONDS
$previousDiagnosticHold = $env:WINTHUNAR_SMOKE_DIAGNOSTIC_HOLD_SECONDS
try {
    Remove-Item Env:WINTHUNAR_SMOKE_RESTART_THEME -ErrorAction SilentlyContinue
    $env:WINTHUNAR_SMOKE_ENDURANCE_SECONDS = $EnduranceSeconds.ToString([Globalization.CultureInfo]::InvariantCulture)
    $env:WINTHUNAR_SMOKE_DIAGNOSTIC_HOLD_SECONDS = $DiagnosticHoldSeconds.ToString([Globalization.CultureInfo]::InvariantCulture)
    $env:WINTHUNAR_SMOKE_DIRECTORY = Join-Path $runRoot 'results'
    New-Item -ItemType Directory -Path $env:WINTHUNAR_SMOKE_DIRECTORY | Out-Null
    $process = Start-Process -FilePath (Join-Path $publish 'WinThunar.exe') -WindowStyle Hidden -PassThru
    $timeoutSeconds = if ($EnduranceSeconds -gt 0) { 120 + $EnduranceSeconds + $DiagnosticHoldSeconds } else { 45 }
    if (-not $process.WaitForExit($timeoutSeconds * 1000)) {
        # Stop only the test process started above, never an installed or portable user instance.
        Stop-Process -Id $process.Id -ErrorAction SilentlyContinue
        Save-ProcessOutcome $process 'desktop' $true
        throw "Desktop smoke test timed out. Outputs: $runRoot"
    }
    Save-ProcessOutcome $process 'desktop' $false
    if ($process.ExitCode -ne 0) { throw "Desktop smoke process failed (exit $($process.ExitCode)). Outputs: $runRoot" }
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
        Save-ProcessOutcome $restart 'restart' $true
        throw "Theme restart check timed out. Outputs: $runRoot"
    }
    Save-ProcessOutcome $restart 'restart' $false
    if ($restart.ExitCode -ne 0) { throw "Theme restart process failed (exit $($restart.ExitCode)). Outputs: $runRoot" }
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
    if ($null -eq $previousEndurance) { Remove-Item Env:WINTHUNAR_SMOKE_ENDURANCE_SECONDS -ErrorAction SilentlyContinue }
    else { $env:WINTHUNAR_SMOKE_ENDURANCE_SECONDS = $previousEndurance }
    if ($null -eq $previousDiagnosticHold) { Remove-Item Env:WINTHUNAR_SMOKE_DIAGNOSTIC_HOLD_SECONDS -ErrorAction SilentlyContinue }
    else { $env:WINTHUNAR_SMOKE_DIAGNOSTIC_HOLD_SECONDS = $previousDiagnosticHold }
}
