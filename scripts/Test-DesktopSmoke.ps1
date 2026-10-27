param(
    [string]$OutputDirectory = (Join-Path $env:TEMP ('WinThunar-desktop-smoke-' + [guid]::NewGuid().ToString('N'))),
    [ValidateRange(0,86400)][int]$EnduranceSeconds = 0,
    [ValidateRange(0,600)][int]$DiagnosticHoldSeconds = 0,
    [ValidateSet('Existing','Files','Menu','Alternate')][string]$EnduranceFocus = 'Existing',
    [switch]$EnduranceOnly,
    [switch]$KeepAwake
)
$ErrorActionPreference = 'Stop'
if ($DiagnosticHoldSeconds -gt 0 -and $EnduranceSeconds -eq 0) { throw 'DiagnosticHoldSeconds requires an endurance run.' }
if ($EnduranceOnly -and $EnduranceSeconds -eq 0) { throw 'EnduranceOnly requires an endurance run.' }
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runRoot = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $runRoot) { throw "Choose a new smoke-test output directory: $runRoot" }
New-Item -ItemType Directory -Path $runRoot | Out-Null
$publish = Join-Path $runRoot 'app'
if (-not ('WinThunarTestPowerRequest2' -as [type])) {
    Add-Type -TypeDefinition @'
using System.Runtime.InteropServices;
public static class WinThunarTestPowerRequest2 {
    [DllImport("kernel32.dll")]
    public static extern uint SetThreadExecutionState(uint flags);
    [DllImport("kernel32.dll")]
    private static extern bool QueryUnbiasedInterruptTime(out ulong time);
    public static ulong AwakeTime() {
        ulong time;
        if (!QueryUnbiasedInterruptTime(out time)) throw new System.InvalidOperationException("Could not read Windows awake time.");
        return time;
    }
}
'@
}
function Wait-ForTestExit($TestProcess, [int]$TimeoutSeconds) {
    $started = [WinThunarTestPowerRequest2]::AwakeTime()
    while (-not $TestProcess.HasExited) {
        if ($TestProcess.WaitForExit(1000)) { return $true }
        $now = [WinThunarTestPowerRequest2]::AwakeTime()
        if (($now - $started) / 10000000.0 -ge $TimeoutSeconds) { return $false }
    }
    return $true
}
# Record the inputs before building; later edits cannot silently change a running test's provenance.
$sourceFiles = @('WinThunar.csproj', 'MainPage.xaml', 'MainPage.xaml.cs', 'MainWindow.xaml.cs',
    'ViewModels/MainPageViewModel.cs', 'Models/FileSystemEntry.cs', 'Services/ShellImageService.cs',
    'Services/FileContainerNameTracker.cs', 'Services/FileContainerNameRegistry.cs', 'Services/ShellThumbnailReader.cs', 'Services/FilePreviewLoader.cs',
    'tests/WinThunar.DesktopSmoke/EnduranceChecks.cs', 'tests/WinThunar.DesktopSmoke/SmokeChecks.cs',
    'tests/WinThunar.DesktopSmoke/ContainerBindingChecks.cs', 'scripts/Test-DesktopSmoke.ps1')
[ordered]@{
    recordedUtc = [DateTime]::UtcNow.ToString('o')
    requestedSeconds = $EnduranceSeconds
    focus = $EnduranceFocus
    enduranceOnly = [bool]$EnduranceOnly
    keepAwake = [bool]$KeepAwake
    sourceCommit = (& git -C $projectRoot rev-parse HEAD)
    sources = @($sourceFiles | ForEach-Object {
        [ordered]@{ path = $_; sha256 = (Get-FileHash -LiteralPath (Join-Path $projectRoot $_) -Algorithm SHA256).Hash }
    })
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $runRoot 'source-inputs.json') -Encoding UTF8
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
        enduranceFocus = $EnduranceFocus
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runRoot ($Name + '-process.json')) -Encoding UTF8
}
& dotnet publish (Join-Path $projectRoot 'WinThunar.csproj') --configuration Release --runtime win-x64 --self-contained true -p:Platform=x64 -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true -p:PublishProfile=win-x64 -p:RoadmapSmokeTest=true --output $publish
if ($LASTEXITCODE -ne 0) { throw 'Desktop smoke build failed.' }
$verifier = Join-Path $runRoot 'source-verifier'
& dotnet build (Join-Path $projectRoot 'tests\WinThunar.RuntimeProbe\WinThunar.RuntimeProbe.csproj') --configuration Release --no-incremental --output $verifier
if ($LASTEXITCODE -ne 0) { throw 'Source verification tool build failed.' }
& dotnet (Join-Path $verifier 'WinThunar.RuntimeProbe.dll') --verify-sources (Join-Path $publish 'WinThunar.pdb') (Join-Path $runRoot 'source-inputs.json') $projectRoot |
    Set-Content -LiteralPath (Join-Path $runRoot 'compiled-source-verification.json') -Encoding UTF8
if ($LASTEXITCODE -ne 0) { throw 'Published PDB does not match the recorded source inputs.' }
[ordered]@{
    recordedUtc = [DateTime]::UtcNow.ToString('o')
    files = @('WinThunar.exe', 'WinThunar.dll', 'WinThunar.pdb', 'WinThunar.runtimeconfig.json',
        'System.Private.CoreLib.dll', 'WinRT.Runtime.dll', 'Microsoft.UI.Xaml.dll') | ForEach-Object {
        [ordered]@{ path = $_; sha256 = (Get-FileHash -LiteralPath (Join-Path $publish $_) -Algorithm SHA256).Hash }
    }
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $runRoot 'build-outputs.json') -Encoding UTF8
$previousDirectory = $env:WINTHUNAR_SMOKE_DIRECTORY
$previousRestartTheme = $env:WINTHUNAR_SMOKE_RESTART_THEME
$previousEndurance = $env:WINTHUNAR_SMOKE_ENDURANCE_SECONDS
$previousDiagnosticHold = $env:WINTHUNAR_SMOKE_DIAGNOSTIC_HOLD_SECONDS
$previousEnduranceFocus = $env:WINTHUNAR_SMOKE_ENDURANCE_FOCUS
$previousEnduranceOnly = $env:WINTHUNAR_SMOKE_ENDURANCE_ONLY
$previousPowerRequest = [uint32]0
try {
    if ($KeepAwake) {
        # ES_CONTINUOUS | ES_SYSTEM_REQUIRED. Allow display sleep and preserve power settings.
        $previousPowerRequest = [WinThunarTestPowerRequest2]::SetThreadExecutionState([uint32]2147483649)
        if ($previousPowerRequest -eq 0) { throw 'Could not hold a temporary system-awake request.' }
    }
    Remove-Item Env:WINTHUNAR_SMOKE_RESTART_THEME -ErrorAction SilentlyContinue
    $env:WINTHUNAR_SMOKE_ENDURANCE_SECONDS = $EnduranceSeconds.ToString([Globalization.CultureInfo]::InvariantCulture)
    $env:WINTHUNAR_SMOKE_DIAGNOSTIC_HOLD_SECONDS = $DiagnosticHoldSeconds.ToString([Globalization.CultureInfo]::InvariantCulture)
    $env:WINTHUNAR_SMOKE_ENDURANCE_FOCUS = $EnduranceFocus
    $env:WINTHUNAR_SMOKE_ENDURANCE_ONLY = if ($EnduranceOnly) { '1' } else { '0' }
    $env:WINTHUNAR_SMOKE_DIRECTORY = Join-Path $runRoot 'results'
    New-Item -ItemType Directory -Path $env:WINTHUNAR_SMOKE_DIRECTORY | Out-Null
    $process = Start-Process -FilePath (Join-Path $publish 'WinThunar.exe') -WindowStyle Hidden -PassThru
    [ordered]@{
        processId = $process.Id
        executable = Join-Path $publish 'WinThunar.exe'
        startedUtc = [DateTime]::UtcNow.ToString('o')
        enduranceSeconds = $EnduranceSeconds
        enduranceFocus = $EnduranceFocus
        enduranceOnly = [bool]$EnduranceOnly
        keepAwake = [bool]$KeepAwake
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runRoot 'active-run.json') -Encoding UTF8
    $timeoutSeconds = if ($EnduranceSeconds -gt 0) { 120 + $EnduranceSeconds + $DiagnosticHoldSeconds } else { 45 }
    if (-not (Wait-ForTestExit $process $timeoutSeconds)) {
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
    if (-not (Wait-ForTestExit $restart 45)) {
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
    if ($previousPowerRequest -ne 0) {
        [void][WinThunarTestPowerRequest2]::SetThreadExecutionState($previousPowerRequest)
    }
    if ($null -eq $previousDirectory) { Remove-Item Env:WINTHUNAR_SMOKE_DIRECTORY -ErrorAction SilentlyContinue }
    else { $env:WINTHUNAR_SMOKE_DIRECTORY = $previousDirectory }
    if ($null -eq $previousRestartTheme) { Remove-Item Env:WINTHUNAR_SMOKE_RESTART_THEME -ErrorAction SilentlyContinue }
    else { $env:WINTHUNAR_SMOKE_RESTART_THEME = $previousRestartTheme }
    if ($null -eq $previousEndurance) { Remove-Item Env:WINTHUNAR_SMOKE_ENDURANCE_SECONDS -ErrorAction SilentlyContinue }
    else { $env:WINTHUNAR_SMOKE_ENDURANCE_SECONDS = $previousEndurance }
    if ($null -eq $previousDiagnosticHold) { Remove-Item Env:WINTHUNAR_SMOKE_DIAGNOSTIC_HOLD_SECONDS -ErrorAction SilentlyContinue }
    else { $env:WINTHUNAR_SMOKE_DIAGNOSTIC_HOLD_SECONDS = $previousDiagnosticHold }
    if ($null -eq $previousEnduranceFocus) { Remove-Item Env:WINTHUNAR_SMOKE_ENDURANCE_FOCUS -ErrorAction SilentlyContinue }
    else { $env:WINTHUNAR_SMOKE_ENDURANCE_FOCUS = $previousEnduranceFocus }
    if ($null -eq $previousEnduranceOnly) { Remove-Item Env:WINTHUNAR_SMOKE_ENDURANCE_ONLY -ErrorAction SilentlyContinue }
    else { $env:WINTHUNAR_SMOKE_ENDURANCE_ONLY = $previousEnduranceOnly }
}
