param(
    [string]$InstallRoot = 'C:\Program Files\WinThunar.App',
    [string]$UserLocalAppData = [Environment]::GetFolderPath('LocalApplicationData'),
    [string]$ExpectedVersion = '0.5.6',
    [string]$OutputDirectory
)
# Read-only against the installation, user data, processes and registry. Do not use Win32_Product:
# querying it can trigger Windows Installer consistency checks/repair.
$ErrorActionPreference = 'Stop'
# A running Velopack logger holds a write handle. Permit that existing writer while
# reading; ordinary Get-FileHash/Get-Content can fail their file-sharing checks.
function Open-ObservedFile([string]$Path) {
    [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read,
        ([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
}
function Get-ObservedHash([string]$Path) {
    $stream = Open-ObservedFile $Path
    $hasher = [Security.Cryptography.SHA256]::Create()
    try { [BitConverter]::ToString($hasher.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
    finally { $hasher.Dispose(); $stream.Dispose() }
}
function Copy-ObservedFile([string]$Source, [string]$Destination) {
    $inputStream = Open-ObservedFile $Source
    try {
        $outputStream = [IO.File]::Open($Destination, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
        try { $inputStream.CopyTo($outputStream) }
        finally { $outputStream.Dispose() }
    }
    finally { $inputStream.Dispose() }
}
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $projectRoot ('artifacts\installed-updater-state-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
}
$capture = [IO.Path]::GetFullPath($OutputDirectory)
$InstallRoot = [IO.Path]::GetFullPath($InstallRoot)
$UserLocalAppData = [IO.Path]::GetFullPath($UserLocalAppData)
foreach ($protected in @($InstallRoot, $UserLocalAppData, (Join-Path $projectRoot 'artifacts\installer'))) {
    if ($capture.Equals($protected, [StringComparison]::OrdinalIgnoreCase) -or $capture.StartsWith($protected + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Evidence output must be separate from the installation, user state, and release packages.'
    }
}
if (Test-Path -LiteralPath $capture) { throw 'Choose a new evidence directory.' }
New-Item -ItemType Directory -Path $capture | Out-Null
$paths = [ordered]@{
    Assembly = Join-Path $InstallRoot 'current\WinThunar.dll'
    Manifest = Join-Path $InstallRoot 'current\sq.version'
    Updater = Join-Path $InstallRoot 'Update.exe'
    Log = Join-Path $UserLocalAppData 'velopack\velopack_WinThunar.App.log'
    Settings = Join-Path $UserLocalAppData 'WinThunar\updates.json'
}
$files = @($paths.GetEnumerator() | ForEach-Object {
    $entry = $_
    if (Test-Path -LiteralPath $entry.Value -PathType Leaf) {
        $file = Get-Item -LiteralPath $entry.Value
        [pscustomobject]@{ Kind = $entry.Key; Path = $entry.Value; Length = $file.Length; LastWriteTimeUtc = $file.LastWriteTimeUtc.ToString('O'); Sha256 = Get-ObservedHash $entry.Value }
    }
})
foreach ($kind in @('Log', 'Manifest', 'Settings')) {
    if (Test-Path -LiteralPath $paths[$kind] -PathType Leaf) { Copy-ObservedFile $paths[$kind] (Join-Path $capture ([IO.Path]::GetFileName($paths[$kind]))) }
}
$processes = @(Get-Process WinThunar,Update -ErrorAction SilentlyContinue | Select-Object Id,ProcessName,Path,StartTime)
$registration = @(foreach ($key in @(
    'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*',
    'HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*',
    'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*',
    'HKCU:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*')) {
    Get-ItemProperty -Path $key -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -like '*WinThunar*' } |
        Select-Object DisplayName,DisplayVersion,InstallLocation,PSPath,PSChildName
})
$packageFiles = @(foreach ($directory in @((Join-Path $InstallRoot 'packages'), (Join-Path $UserLocalAppData 'WinThunar.App\packages'))) {
    Get-ChildItem -LiteralPath $directory -File -ErrorAction SilentlyContinue |
        Select-Object FullName,Length,@{ n='LastWriteTimeUtc'; e={$_.LastWriteTimeUtc.ToString('O')} }
})
$manifestVersion = if (Test-Path -LiteralPath $paths.Manifest) { ([xml](Get-Content -LiteralPath $paths.Manifest -Raw)).package.metadata.version } else { $null }
$assemblyVersion = if (Test-Path -LiteralPath $paths.Assembly) { (Get-Item -LiteralPath $paths.Assembly).VersionInfo.FileVersion } else { $null }
$settings = if (Test-Path -LiteralPath $paths.Settings) { Get-Content -LiteralPath $paths.Settings -Raw | ConvertFrom-Json } else { $null }
$logSnapshot = Join-Path $capture ([IO.Path]::GetFileName($paths.Log))
$log = if (Test-Path -LiteralPath $logSnapshot) { Get-Content -LiteralPath $logSnapshot -Raw } else { '' }
$unchanged = @($files | Where-Object { (Get-ObservedHash $_.Path) -ne $_.Sha256 }).Count -eq 0
$report = [ordered]@{
    CapturedAt = [DateTimeOffset]::Now.ToString('O')
    InstallRoot = $InstallRoot
    UserLocalAppData = $UserLocalAppData
    ExpectedVersion = $ExpectedVersion
    ManifestVersion = $manifestVersion
    AssemblyFileVersion = $assemblyVersion
    ExpectedVersionDeployed = ($manifestVersion -eq $ExpectedVersion -and $assemblyVersion -eq "$ExpectedVersion.0")
    LastCheck = $settings.LastCheck
    LogMentionsExpectedVersion = $log.Contains("v$ExpectedVersion") -or $log.Contains("package $ExpectedVersion")
    Processes = $processes
    Files = $files
    ObservedFilesUnchangedDuringCapture = $unchanged
    Registration = $registration
    PackageFiles = $packageFiles
    Notes = 'Read-only snapshot. No app/updater/installer was launched, closed or changed. Log timestamps omit dates; log mentions alone do not prove adoption. Current manifest and DLL establish the deployed version, not the version loaded in an earlier process.'
}
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $capture 'verification.json') -Encoding UTF8
Write-Output "Installed updater evidence: $capture"
Write-Output "Deployed: $manifestVersion ($assemblyVersion); target adopted: $($report.ExpectedVersionDeployed); matching processes: $($processes.Count); observed files unchanged: $unchanged"
