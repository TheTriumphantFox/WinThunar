param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $projectRoot ('artifacts\updater-probe-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
}
$runRoot = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $runRoot) { throw 'Choose a new output directory; existing data is never reused.' }
New-Item -ItemType Directory -Path $runRoot | Out-Null
for ($ancestor = Get-Item -LiteralPath $runRoot; $null -ne $ancestor; $ancestor = $ancestor.Parent) {
    if ($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Probe output cannot be beneath a link or mount point.' }
}
$appId = 'WinThunar.UpdaterProbe.' + [guid]::NewGuid().ToString('N')
$token = [guid]::NewGuid().ToString('N')
$feed = Join-Path $runRoot 'packages'
$project = Join-Path $projectRoot 'tests\WinThunar.UpdaterProbe\WinThunar.UpdaterProbe.csproj'
$probeExe = 'WinThunar.UpdaterProbe.exe'
$results = [Collections.Generic.List[object]]::new()
$previousCase = $env:WINTHUNAR_UPDATER_PROBE_CASE
$previousToken = $env:WINTHUNAR_UPDATER_PROBE_TOKEN
$registrationPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\' + $appId
if (Test-Path -LiteralPath $registrationPath) { throw 'The unique probe identity is already registered.' }

function Assert-Condition($condition, [string]$message) { if (-not $condition) { throw $message } }
function Get-Sha256([string]$path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Write-Json($value, [string]$path) { $value | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $path -Encoding UTF8 }
function Expand-Installation([string]$package, [string]$destination) {
    $archive = [IO.Compression.ZipFile]::OpenRead($package)
    try {
        foreach ($entry in $archive.Entries) {
            if (-not $entry.FullName.StartsWith('lib/app/') -or $entry.FullName.EndsWith('/')) { continue }
            $relative = $entry.FullName.Substring(8)
            $target = Join-Path (Join-Path $destination 'current') $relative
            if ($relative -eq 'Squirrel.exe') { $target = Join-Path $destination 'Update.exe' }
            if ($relative -eq ($appId + '_ExecutionStub.exe')) { $target = Join-Path $destination ($appId + '.exe') }
            $target = [IO.Path]::GetFullPath($target)
            Assert-Condition ($target.StartsWith($destination + '\', [StringComparison]::OrdinalIgnoreCase)) 'Package path escaped the owned installation.'
            New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target)
        }
    }
    finally { $archive.Dispose() }
}
function Compare-Installation([string]$package, [string]$destination, [string]$reportPath) {
    $archive = [IO.Compression.ZipFile]::OpenRead($package)
    $files = @()
    try {
        foreach ($entry in $archive.Entries) {
            if (-not $entry.FullName.StartsWith('lib/app/') -or $entry.FullName.EndsWith('/')) { continue }
            $relative = $entry.FullName.Substring(8)
            $target = Join-Path (Join-Path $destination 'current') $relative
            if ($relative -eq 'Squirrel.exe') { $target = Join-Path $destination 'Update.exe' }
            if ($relative -eq ($appId + '_ExecutionStub.exe')) { $target = Join-Path $destination ($appId + '.exe') }
            $stream = $entry.Open()
            $hasher = [Security.Cryptography.SHA256]::Create()
            try { $expected = [BitConverter]::ToString($hasher.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
            finally { $hasher.Dispose(); $stream.Dispose() }
            $actual = if (Test-Path -LiteralPath $target -PathType Leaf) { Get-Sha256 $target } else { $null }
            $files += [pscustomobject]@{ Path = $relative; Expected = $expected; Actual = $actual; Matches = ($expected -eq $actual) }
        }
    }
    finally { $archive.Dispose() }
    Write-Json $files $reportPath
    Assert-Condition ($files.Count -gt 0 -and @($files | Where-Object { -not $_.Matches }).Count -eq 0) "Deployed files do not match $package"
    return $files.Count
}
function Start-Probe([string]$caseRoot, [string]$command) {
    $env:WINTHUNAR_UPDATER_PROBE_CASE = $caseRoot
    $env:WINTHUNAR_UPDATER_PROBE_TOKEN = $token
    $process = [Diagnostics.Process]::new()
    $process.StartInfo.FileName = Join-Path $caseRoot "installation\current\$probeExe"
    $process.StartInfo.Arguments = $command
    $process.StartInfo.UseShellExecute = $false
    $process.StartInfo.CreateNoWindow = $true
    $process.StartInfo.RedirectStandardOutput = $true
    $process.StartInfo.RedirectStandardError = $true
    $null = $process.Start()
    # Keep a native handle from startup so a fast child cannot lose its exit status.
    $null = $process.Handle
    $process | Add-Member -NotePropertyName ProbeOutput -NotePropertyValue (Join-Path $caseRoot "$command.stdout.log")
    $process | Add-Member -NotePropertyName ProbeError -NotePropertyValue (Join-Path $caseRoot "$command.stderr.log")
    $process | Add-Member -NotePropertyName ProbeOutputTask -NotePropertyValue $process.StandardOutput.ReadToEndAsync()
    $process | Add-Member -NotePropertyName ProbeErrorTask -NotePropertyValue $process.StandardError.ReadToEndAsync()
    return $process
}
function Wait-Probe($process, [int]$expectedExit = 0) {
    if (-not $process.WaitForExit(45000)) {
        $process.Kill()
        throw 'Owned updater probe timed out.'
    }
    [IO.File]::WriteAllText($process.ProbeOutput, $process.ProbeOutputTask.GetAwaiter().GetResult())
    [IO.File]::WriteAllText($process.ProbeError, $process.ProbeErrorTask.GetAwaiter().GetResult())
    Assert-Condition ($process.ExitCode -eq $expectedExit) "Owned probe exited $($process.ExitCode), expected $expectedExit. See $($process.ProbeError)."
}
function Read-Version([string]$install) {
    $manifest = [xml](Get-Content -LiteralPath (Join-Path $install 'current\sq.version') -Raw)
    return $manifest.package.metadata.version
}
function Damage-Package([string]$path) {
    $stream = [IO.File]::Open($path, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    try { $stream.Position = [Math]::Floor($stream.Length / 2); $value = $stream.ReadByte(); $stream.Position--; $stream.WriteByte($value -bxor 255) }
    finally { $stream.Dispose() }
}

try {
    Write-Json ([ordered]@{ AppId = $appId; Token = $token; CreatedAt = [DateTimeOffset]::Now.ToString('O'); Scope = 'Owned filesystem installation; no MSI/setup or user WinThunar executable is run.' }) (Join-Path $runRoot 'owner.json')
    foreach ($version in @('1.0.0', '1.0.1')) {
        $publish = Join-Path $runRoot "publish-$version"
        & dotnet publish $project -c Release -r win-x64 --self-contained true "-p:Version=$version" -o $publish *> (Join-Path $runRoot "build-$version.log")
        Assert-Condition ($LASTEXITCODE -eq 0) "Probe build failed: $runRoot"
        # Skip only vpk's execution check: the executable requires its ownership marker before running.
        & vpk pack --packId $appId --packVersion $version --packDir $publish --mainExe $probeExe --packTitle $appId --runtime win-x64 --shortcuts None --noInst --skipVeloAppCheck --outputDir $feed *> (Join-Path $runRoot "pack-$version.log")
        Assert-Condition ($LASTEXITCODE -eq 0) "Probe packaging failed: $runRoot"
    }
    $basePackage = Join-Path $feed "$appId-1.0.0-full.nupkg"
    $targetPackage = Join-Path $feed "$appId-1.0.1-full.nupkg"
    $deltaPackage = Join-Path $feed "$appId-1.0.1-delta.nupkg"
    Assert-Condition ((Get-Item -LiteralPath $deltaPackage).Length -lt (Get-Item -LiteralPath $targetPackage).Length) 'The fixture must offer a smaller delta.'
    foreach ($scenario in @('delta', 'full-without-base', 'damaged-delta-fallback', 'damaged-full-retry')) {
        $caseRoot = Join-Path $runRoot $scenario
        $install = Join-Path $caseRoot 'installation'
        $nativeLog = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) "velopack\velopack_$appId.log"
        $caseLogOffset = if (Test-Path -LiteralPath $nativeLog) { (Get-Content -LiteralPath $nativeLog -Raw).Length } else { 0 }
        New-Item -ItemType Directory -Path (Join-Path $install 'packages'),(Join-Path $caseRoot 'feed') -Force | Out-Null
        Write-Json @{ AppId = $appId; Token = $token } (Join-Path $caseRoot 'owner.json')
        Copy-Item -Path (Join-Path $feed '*') -Destination (Join-Path $caseRoot 'feed')
        Expand-Installation $basePackage $install
        if ($scenario -in @('delta', 'damaged-delta-fallback')) { Copy-Item -LiteralPath $basePackage -Destination (Join-Path $install 'packages') }
        if ($scenario -eq 'damaged-delta-fallback') { Damage-Package (Join-Path $caseRoot "feed\$appId-1.0.1-delta.nupkg") }
        if ($scenario -eq 'damaged-full-retry') { Damage-Package (Join-Path $caseRoot "feed\$appId-1.0.1-full.nupkg") }
        $download = Start-Probe $caseRoot 'download'
        if ($scenario -eq 'damaged-full-retry') {
            Wait-Probe $download 1
            $failure = Get-Content -LiteralPath (Join-Path $caseRoot 'download.json') -Raw | ConvertFrom-Json
            Assert-Condition ($failure.State -eq 'Failed' -and $failure.ErrorMessage) 'A corrupt full package must report failure.'
            Copy-Item -LiteralPath (Join-Path $caseRoot 'download.json') -Destination (Join-Path $caseRoot 'rejected-package.json')
            $null = Compare-Installation $basePackage $install (Join-Path $caseRoot 'unchanged-after-rejection.json')
            Copy-Item -LiteralPath $targetPackage -Destination (Join-Path $caseRoot 'feed') -Force
            $download = Start-Probe $caseRoot 'download'
        }
        Wait-Probe $download
        Assert-Condition ((Read-Version $install) -eq '1.0.0') 'Download must leave the current installation unchanged.'
        $status = Start-Probe $caseRoot 'status'
        Wait-Probe $status
        $pending = Get-Content -LiteralPath (Join-Path $caseRoot 'status.json') -Raw | ConvertFrom-Json
        Assert-Condition ($pending.InitialState -eq 'Ready' -and $pending.AvailableVersion -eq '1.0.1' -and -not $pending.RequiresAdministrator) 'A fresh process must rediscover the pending per-user update.'
        $logOffset = if (Test-Path -LiteralPath $nativeLog) { (Get-Content -LiteralPath $nativeLog -Raw).Length } else { 0 }
        $apply = Start-Probe $caseRoot 'apply-on-exit'
        $deadline = [DateTime]::UtcNow.AddSeconds(40)
        while (-not (Test-Path -LiteralPath (Join-Path $caseRoot 'ready'))) {
            Assert-Condition (-not $apply.HasExited -and [DateTime]::UtcNow -lt $deadline) 'Probe did not reach the close gate.'
            Start-Sleep -Milliseconds 100
        }
        Assert-Condition ((Read-Version $install) -eq '1.0.0' -and -not $apply.HasExited) 'The update applied before close.'
        Set-Content -LiteralPath (Join-Path $caseRoot 'close') -Value 'close owned probe'
        Wait-Probe $apply
        $deadline = [DateTime]::UtcNow.AddSeconds(45)
        do {
            Start-Sleep -Milliseconds 250
            $applied = (Test-Path -LiteralPath $nativeLog) -and (Get-Content -LiteralPath $nativeLog -Raw).Substring($logOffset).Contains('Package version 1.0.1 applied successfully.')
            $updaters = @(Get-Process Update -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($install + '\', [StringComparison]::OrdinalIgnoreCase) })
        } while ((-not $applied -or $updaters.Count -ne 0) -and [DateTime]::UtcNow -lt $deadline)
        Assert-Condition ($applied -and $updaters.Count -eq 0 -and (Read-Version $install) -eq '1.0.1') "Owned update did not complete: $caseRoot"
        $count = Compare-Installation $targetPackage $install (Join-Path $caseRoot 'deployed-files.json')
        $restarted = Start-Probe $caseRoot 'status-after-apply'
        Wait-Probe $restarted
        $finalStatus = Get-Content -LiteralPath (Join-Path $caseRoot 'status-after-apply.json') -Raw | ConvertFrom-Json
        Assert-Condition ($finalStatus.CurrentVersion -eq '1.0.1' -and $finalStatus.State -eq 'UpToDate') 'The updated probe must start and find no further update.'
        $caseLog = (Get-Content -LiteralPath $nativeLog -Raw).Substring($caseLogOffset)
        Set-Content -LiteralPath (Join-Path $caseRoot 'native-updater.log') -Value $caseLog -Encoding UTF8
        if ($scenario -eq 'delta') { Assert-Condition ($caseLog.Contains('Delta update download complete.')) 'The delta case must actually download and reconstruct a delta.' }
        if ($scenario -in @('full-without-base', 'damaged-full-retry')) { Assert-Condition ($caseLog.Contains('There is no local/base package available')) 'The no-base case must use the full-package path.' }
        if ($scenario -eq 'damaged-delta-fallback') { Assert-Condition ($caseLog.Contains('Downloading delta 1.0.1') -and $caseLog.Contains('Unable to apply delta updates, falling back to full update.') -and $caseLog.Contains('ChecksumFailedException') -and $caseLog.Contains('Full release download complete.')) 'The damaged delta must fail checksum verification and fall back to a verified full package.' }
        $results.Add([pscustomobject]@{ Scenario = $scenario; Passed = $true; FilesCompared = $count; InstalledVersion = '1.0.1'; PendingRediscovered = $true; DownloadLeftCurrentVersionUnchanged = $true; ApplyOnExitCompleted = $true; UpdatedProcessReportsUpToDate = $true })
        Write-Output "Passed: $scenario ($count deployed files)"
    }
}
catch {
    Write-Json @{ Failure = $_.ToString(); Cases = @($results.ToArray()); RunRoot = $runRoot } (Join-Path $runRoot 'report.json')
    throw
}
finally {
    $env:WINTHUNAR_UPDATER_PROBE_CASE = $previousCase
    $env:WINTHUNAR_UPDATER_PROBE_TOKEN = $previousToken
    # Real Velopack apply registers its own unique identity. Remove only that test entry,
    # after all owned executables have exited; preserve all files, packages and logs.
    $cleanupDeadline = [DateTime]::UtcNow.AddSeconds(45)
    do {
        $owned = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($runRoot + '\', [StringComparison]::OrdinalIgnoreCase) })
        if ($owned.Count -ne 0) { Start-Sleep -Milliseconds 250 }
    } while ($owned.Count -ne 0 -and [DateTime]::UtcNow -lt $cleanupDeadline)
    Assert-Condition ($owned.Count -eq 0) 'An owned process is still running; preserve the registration for inspection.'
    if (Test-Path -LiteralPath $registrationPath) {
        $entry = Get-ItemProperty -LiteralPath $registrationPath
        $entryRoot = [IO.Path]::GetFullPath($entry.InstallLocation)
        Assert-Condition ($entry.DisplayName -eq $appId -and $entryRoot.StartsWith($runRoot + '\', [StringComparison]::OrdinalIgnoreCase)) 'Refusing to remove an unowned registration.'
        Assert-Condition ((Get-Item -LiteralPath $registrationPath).SubKeyCount -eq 0) 'Unexpected registration subkeys; preserve them.'
        Write-Json ($entry | Select-Object DisplayName,DisplayVersion,InstallLocation,UninstallString,PSPath) (Join-Path $runRoot 'probe-registration.json')
        Remove-Item -LiteralPath $registrationPath -Force
    }
    Write-Json @{ AppId = $appId; RegistrationAbsent = -not (Test-Path -LiteralPath $registrationPath); OwnedProcessesRemaining = $owned.Count } (Join-Path $runRoot 'cleanup.json')
}
Write-Json @{ Cases = @($results.ToArray()); AppId = $appId; CapturedAt = [DateTimeOffset]::Now.ToString('O'); Limitations = 'Generated console packages and owned writable layout, not the released WinUI packages, Setup/MSI installation, UAC, repair/uninstall, or real window-close UI. All fixtures are preserved.' } (Join-Path $runRoot 'report.json')
Write-Output "Updater integration evidence: $runRoot"
