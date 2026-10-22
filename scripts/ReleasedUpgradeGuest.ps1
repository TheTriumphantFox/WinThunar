param(
    [Parameter(Mandatory)][ValidateSet('Preflight','Initialize','Launch','Capture','VerifyTarget')][string]$Action,
    [ValidateSet('PerUser','AllUsers')][string]$Scope = 'PerUser',
    [ValidatePattern('^[a-zA-Z0-9-]+$')][string]$Label = 'capture'
)
$ErrorActionPreference = 'Stop'
$bundle = [IO.Path]::GetFullPath($PSScriptRoot)
$lab = Get-Content -LiteralPath (Join-Path $bundle 'lab.json') -Raw | ConvertFrom-Json
$machineGuid = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Cryptography' -Name MachineGuid).MachineGuid
# This guard precedes all writes and all process launches. A separate directory on the host
# cannot isolate WinThunar's single-instance key, settings, notifications, Shell or MSI identity.
if ([string]::IsNullOrWhiteSpace($machineGuid) -or $machineGuid -eq $lab.HostMachineGuid -or $env:COMPUTERNAME -eq $lab.HostComputerName) {
    throw 'This bundle must run in a separate disposable Windows guest. Refusing the originating host before any writes or launches.'
}
if ($lab.Schema -ne 1 -or $lab.RunId -notmatch '^[a-f0-9]{32}$' -or $lab.BaseVersion -ne '0.5.5' -or $lab.TargetVersion -ne '0.5.6') { throw 'Unexpected lab identity or versions.' }
foreach ($asset in $lab.Assets) {
    $assetPath = [IO.Path]::GetFullPath((Join-Path $bundle $asset.RelativePath))
    if (-not $assetPath.StartsWith($bundle + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Asset escaped the lab bundle.' }
    if ((Get-Item -LiteralPath $assetPath).Length -ne $asset.Bytes -or (Get-FileHash -LiteralPath $assetPath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $asset.Sha256) { throw "Asset failed verification: $($asset.RelativePath)" }
}
if ($Action -eq 'Preflight') {
    Write-Output 'Separate Windows machine and all seven release assets verified. No app or installer was launched.'
    return
}
$guestRoot = Join-Path ([Environment]::GetFolderPath('UserProfile')) ('WinThunarUpgradeLab\' + $lab.RunId + '-' + $Scope)
$markerPath = Join-Path $guestRoot 'owner.json'
$installRoot = if ($Scope -eq 'PerUser') { Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'WinThunar.App' } else { Join-Path $env:ProgramFiles 'WinThunar.App' }
$userState = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'WinThunar'
function Write-Json($value, [string]$path) { $value | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $path -Encoding UTF8 }
if ($Action -eq 'Initialize') {
    $existing = @(Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*','HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*','HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*' -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -eq 'WinThunar' })
    if ($existing.Count -or (Test-Path -LiteralPath $installRoot) -or (Test-Path -LiteralPath $userState) -or (Get-Process WinThunar -ErrorAction SilentlyContinue)) { throw 'Use a clean guest snapshot; existing WinThunar state is never overwritten.' }
    if (Test-Path -LiteralPath $guestRoot) { throw 'This guest case already exists. Restore the clean snapshot for another case.' }
    New-Item -ItemType Directory -Path $guestRoot | Out-Null
    Write-Json @{ RunId = $lab.RunId; MachineGuid = $machineGuid; Scope = $Scope; InstallRoot = $installRoot } $markerPath
    New-Item -ItemType Directory -Path $userState | Out-Null
    # Prevent an uncontrolled startup check; the test uses the real explicit Check for Updates UI.
    Write-Json @{ CheckAutomatically = $false; LastCheck = $null } (Join-Path $userState 'updates.json')
    Write-Output "Initialized isolated guest case: $guestRoot"
    Write-Output "Install the published 0.5.5 MSI manually with scope $Scope; then run Launch."
    return
}
$owner = Get-Content -LiteralPath $markerPath -Raw | ConvertFrom-Json
if ($owner.RunId -ne $lab.RunId -or $owner.MachineGuid -ne $machineGuid -or $owner.Scope -ne $Scope -or $owner.InstallRoot -ne $installRoot) { throw 'Guest ownership does not match.' }
if ($Action -eq 'Launch') {
    $isElevated = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if ($isElevated) { throw 'Launch the app from a normal, unelevated PowerShell session to test per-user behavior and UAC.' }
    if (Get-Process WinThunar -ErrorAction SilentlyContinue) { throw 'A WinThunar process is already running in the guest.' }
    $manifest = [xml](Get-Content -LiteralPath (Join-Path $installRoot 'current\sq.version') -Raw)
    if ($manifest.package.metadata.id -ne 'WinThunar.App' -or $manifest.package.metadata.version -notin @('0.5.5','0.5.6')) { throw 'Expected the released WinThunar installation.' }
    $previousFeed = $env:WINTHUNAR_UPDATE_FEED
    try {
        $env:WINTHUNAR_UPDATE_FEED = Join-Path $bundle 'feed'
        # Visible: the actual guest app is the explicit subject of the interactive test.
        $process = Start-Process -FilePath (Join-Path $installRoot 'current\WinThunar.exe') -PassThru
        Write-Json @{ ProcessId = $process.Id; Version = $manifest.package.metadata.version; LaunchedAt = [DateTimeOffset]::Now.ToString('O'); Feed = $env:WINTHUNAR_UPDATE_FEED; Scope = $Scope } (Join-Path $guestRoot ('launch-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.json'))
    }
    finally { $env:WINTHUNAR_UPDATE_FEED = $previousFeed }
    return
}
$captureRoot = Join-Path $guestRoot ($Label + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 6))
& (Join-Path $bundle 'Capture-InstalledUpdaterState.ps1') -InstallRoot $installRoot -ExpectedVersion '0.5.6' -OutputDirectory $captureRoot
foreach ($stateFile in @('session.json','appearance.json')) {
    $statePath = Join-Path $userState $stateFile
    if (Test-Path -LiteralPath $statePath) { Copy-Item -LiteralPath $statePath -Destination $captureRoot }
}
if ($Action -eq 'Capture') { return }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$targetPackage = Join-Path $bundle 'feed\WinThunar.App-0.5.6-full.nupkg'
$archive = [IO.Compression.ZipFile]::OpenRead($targetPackage)
$files = @()
try {
    foreach ($entry in $archive.Entries) {
        if (-not $entry.FullName.StartsWith('lib/app/') -or $entry.FullName.EndsWith('/')) { continue }
        $relative = $entry.FullName.Substring(8)
        $target = Join-Path (Join-Path $installRoot 'current') $relative
        if ($relative -eq 'Squirrel.exe') { $target = Join-Path $installRoot 'Update.exe' }
        if ($relative -eq 'WinThunar_ExecutionStub.exe') { $target = Join-Path $installRoot 'WinThunar.exe' }
        $stream = $entry.Open()
        $hasher = [Security.Cryptography.SHA256]::Create()
        try { $expected = [BitConverter]::ToString($hasher.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
        finally { $hasher.Dispose(); $stream.Dispose() }
        $actual = if (Test-Path -LiteralPath $target -PathType Leaf) { (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() } else { $null }
        $files += [pscustomobject]@{ RelativePath = $relative; Expected = $expected; Actual = $actual; Matches = ($expected -eq $actual) }
    }
}
finally { $archive.Dispose() }
Write-Json $files (Join-Path $captureRoot 'deployed-files.json')
$version = (Get-Item -LiteralPath (Join-Path $installRoot 'current\WinThunar.dll')).VersionInfo.FileVersion
$valid = $version -eq '0.5.6.0' -and $files.Count -gt 500 -and @($files | Where-Object { -not $_.Matches }).Count -eq 0
Write-Json @{ Passed = $valid; AssemblyFileVersion = $version; ComparedFiles = $files.Count; Scope = $Scope; UiWorkflowVerified = $false; Notes = 'File verification only. Record UI, UAC, close behavior, settings, repair and uninstall observations separately.' } (Join-Path $captureRoot 'target-verification.json')
if (-not $valid) { throw "Target deployment mismatch; preserve all guest evidence: $captureRoot" }
Write-Output "All $($files.Count) deployed target files match the published full package."
