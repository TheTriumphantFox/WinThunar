param(
    [switch]$ElevatedWorker,
    [ValidateSet('Disconnect', 'Reconnect')][string]$ShareAction,
    [string]$RunDirectory
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactsRoot = Join-Path $projectRoot 'artifacts'

function Assert-OrdinaryDirectory([string]$Path) {
    $item = Get-Item -LiteralPath $Path -Force
    if (-not $item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Expected an ordinary directory: $Path" }
}

if (-not $ElevatedWorker -and -not $ShareAction) {
    if ($RunDirectory) { throw 'RunDirectory is reserved for the worker.' }
    & dotnet build (Join-Path $projectRoot 'tests\WinThunar.FileOps.Stress\WinThunar.FileOps.Stress.csproj') --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'SMB stress harness build failed.' }
    $RunDirectory = Join-Path $artifactsRoot ('fileops-smb-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
    $arguments = '-NoProfile -ExecutionPolicy Bypass -File "{0}" -ElevatedWorker -RunDirectory "{1}"' -f $PSCommandPath, $RunDirectory
    Write-Output "Requesting UAC for a new private SMB test share. Preserved results: $RunDirectory"
    $worker = Start-Process powershell.exe -Verb RunAs -WindowStyle Hidden -ArgumentList $arguments -PassThru -Wait
    if ($worker.ExitCode -ne 0) { throw "SMB checks failed (exit $($worker.ExitCode)). Review $RunDirectory; fixtures and journals are preserved." }
    Write-Output "SMB checks passed. Report: $RunDirectory\stress\report.json"
    exit 0
}

$principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'The private SMB share worker needs administrator permission.' }
$runRoot = [IO.Path]::GetFullPath($RunDirectory)
if ([IO.Path]::GetDirectoryName($runRoot) -ne $artifactsRoot -or [IO.Path]::GetFileName($runRoot) -notmatch '^fileops-smb-\d{8}-\d{6}-[a-f0-9]{8}$') {
    throw 'The SMB run must be directly under this checkout artifacts directory.'
}
Assert-OrdinaryDirectory $projectRoot
Assert-OrdinaryDirectory $artifactsRoot
$manifestPath = Join-Path $runRoot 'share.json'

function Read-OwnedShare {
    Assert-OrdinaryDirectory $runRoot
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.Token -notmatch '^[a-f0-9]{32}$' -or $manifest.ShareName -cne ('WinThunar-Test-' + $manifest.Token) -or
        $manifest.LocalPath -ne (Join-Path $runRoot 'share') -or $manifest.UncPath -ne ('\\localhost\' + $manifest.ShareName) -or
        $manifest.ScriptPath -ne $PSCommandPath -or $manifest.Owner -ne [Security.Principal.WindowsIdentity]::GetCurrent().Name) {
        throw 'Disposable SMB manifest ownership mismatch.'
    }
    Assert-OrdinaryDirectory $manifest.LocalPath
    if ((Get-Content -LiteralPath (Join-Path $manifest.LocalPath 'smb-owner.txt') -Raw) -cne $manifest.Token) { throw 'SMB ownership marker changed.' }
    $existing = Get-SmbShare -Name $manifest.ShareName -ErrorAction SilentlyContinue
    if ($existing -and $existing.Path -ne $manifest.LocalPath) { throw 'Share name now belongs to a different directory; refusing to change it.' }
    return $manifest
}

function Connect-OwnedShare($Manifest) {
    $existing = Get-SmbShare -Name $Manifest.ShareName -ErrorAction SilentlyContinue
    if (-not $existing) {
        New-SmbShare -Name $Manifest.ShareName -Path $Manifest.LocalPath -FullAccess $Manifest.Owner -CachingMode None -FolderEnumerationMode AccessBased | Out-Null
    }
}

if ($ShareAction) {
    $owned = Read-OwnedShare
    if ($ShareAction -eq 'Disconnect') {
        # Remove only the exact owned share; never close an SMB session, stop a service, or touch
        # unrelated open files. Windows invalidates the handles belonging to this share.
        $existing = Get-SmbShare -Name $owned.ShareName -ErrorAction Stop
        if ($existing.Path -ne $owned.LocalPath) { throw 'Share ownership changed before disconnection.' }
        Remove-SmbShare -Name $owned.ShareName -Force -Confirm:$false
        if (Get-SmbShare -Name $owned.ShareName -ErrorAction SilentlyContinue) { throw 'The owned share was not removed.' }
    }
    else { Connect-OwnedShare $owned }
    exit 0
}

if (Test-Path -LiteralPath $runRoot) { throw 'Refusing to reuse an SMB run directory.' }
foreach ($serviceName in @('LanmanServer', 'LanmanWorkstation')) {
    if ((Get-Service $serviceName).Status -ne 'Running') { throw "SMB service is not running: $serviceName. No services will be changed." }
}
New-Item -ItemType Directory -Path $runRoot | Out-Null
Start-Transcript -Path (Join-Path $runRoot 'provisioning.log') | Out-Null
$token = [guid]::NewGuid().ToString('N')
$shareName = 'WinThunar-Test-' + $token
$localPath = Join-Path $runRoot 'share'
$status = [ordered]@{ Status = 'Preparing'; ShareName = $shareName; ShareRemoved = $false; Error = $null }
$exitCode = 1
try {
    New-Item -ItemType Directory -Path $localPath | Out-Null
    Set-Content -LiteralPath (Join-Path $localPath 'smb-owner.txt') -Value $token -NoNewline -Encoding Ascii
    $manifest = [ordered]@{ Token = $token; ShareName = $shareName; LocalPath = $localPath; UncPath = ('\\localhost\' + $shareName);
        Owner = [Security.Principal.WindowsIdentity]::GetCurrent().Name; ScriptPath = $PSCommandPath }
    $manifest | ConvertTo-Json | Set-Content -LiteralPath $manifestPath -Encoding Ascii
    $owned = Read-OwnedShare
    if (Get-SmbShare -Name $shareName -ErrorAction SilentlyContinue) { throw 'The new share name already exists.' }
    Connect-OwnedShare $owned
    if (-not (Test-Path -LiteralPath ($owned.UncPath + '\smb-owner.txt'))) { throw 'The new loopback share is inaccessible. No service or firewall settings will be changed.' }
    $status.Status = 'Testing'
    $harness = Join-Path $projectRoot 'tests\WinThunar.FileOps.Stress\bin\Release\net10.0-windows\WinThunar.FileOps.Stress.exe'
    & $harness --smb-run $manifestPath (Join-Path $runRoot 'stress')
    if ($LASTEXITCODE -ne 0) { throw 'Disposable SMB transfer checks failed.' }
    $status.Status = 'Passed'
    $exitCode = 0
}
catch { $status.Status = 'Failed'; $status.Error = $_.Exception.ToString(); Write-Output $status.Error }
finally {
    try {
        $owned = Read-OwnedShare
        $existing = Get-SmbShare -Name $owned.ShareName -ErrorAction SilentlyContinue
        if ($existing) {
            if ($existing.Path -ne $owned.LocalPath) { throw 'Share ownership changed; refusing cleanup.' }
            Remove-SmbShare -Name $owned.ShareName -Force -Confirm:$false
        }
        $status.ShareRemoved = -not [bool](Get-SmbShare -Name $owned.ShareName -ErrorAction SilentlyContinue)
        if (-not $status.ShareRemoved) { throw 'Disposable share remains present.' }
    }
    catch { $status.Status = 'Failed'; $status.Error = "$($status.Error)`nShare cleanup failed: $($_.Exception.Message)"; $exitCode = 1 }
    $status | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runRoot 'provisioning.json') -Encoding Ascii
    Stop-Transcript | Out-Null
}
exit $exitCode
