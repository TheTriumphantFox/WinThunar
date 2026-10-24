param(
    [Parameter(Mandatory)][ValidateSet('Prepare','Ready','BeforeRestart','VerifyRestart','Export')][string]$Action,
    [Parameter(Mandatory)][string]$Bundle
)
$ErrorActionPreference = 'Stop'
$Bundle = [IO.Path]::GetFullPath($Bundle)
# The bundle helper rejects the originating host before any writes or launches.
& (Join-Path $Bundle 'ReleasedUpgradeGuest.ps1') -Action Preflight
$lab = Get-Content (Join-Path $Bundle 'lab.json') -Raw | ConvertFrom-Json
$case = Join-Path $env:USERPROFILE ('WinThunarUpgradeLab\' + $lab.RunId + '-PerUser')
$root = Join-Path $env:LOCALAPPDATA 'WinThunar.App'
$state = Join-Path $env:LOCALAPPDATA 'WinThunar'
function Write-Json($value, [string]$path) {
    $value | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $path -Encoding UTF8
}
if ($Action -eq 'Prepare') {
    & (Join-Path $Bundle 'ReleasedUpgradeGuest.ps1') -Action Initialize
    $msi = Join-Path $Bundle ('installers\' + $lab.BaseVersion + '\WinThunar.App-win.msi')
    $arguments = '/i "' + $msi + '" /qn /norestart ALLUSERS=2 MSIINSTALLPERUSER=1 DESKTOPSHORTCUT=0 STARTMENUSHORTCUT=1 /l*v "' + (Join-Path $case 'base-install.log') + '"'
    $process = Start-Process "$env:SystemRoot\System32\msiexec.exe" -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    Write-Json @{ ExitCode = $process.ExitCode; Mode = 'Silent public per-user MSI fixture' } (Join-Path $case 'base-install.json')
    if ($process.ExitCode -ne 0) { throw 'Base installation failed.' }
    if ((Get-Item (Join-Path $root 'current\WinThunar.dll')).VersionInfo.FileVersion -ne ($lab.BaseVersion + '.0')) { throw 'Wrong installed version.' }
    $packages = Join-Path $root 'packages'
    $cache = @(Get-ChildItem $packages -File -ErrorAction SilentlyContinue | Select-Object Name,Length)
    $relative = 'feed\WinThunar.App-' + $lab.BaseVersion + '-full.nupkg'
    $base = Join-Path $packages ([IO.Path]::GetFileName($relative))
    $seeded = -not (Test-Path $base)
    if ($seeded) {
        New-Item -ItemType Directory -Path $packages -Force | Out-Null
        Copy-Item (Join-Path $Bundle $relative) $base
    }
    $asset = $lab.Assets | Where-Object RelativePath -eq $relative
    if ((Get-FileHash $base).Hash -ne $asset.Sha256) { throw 'Base cache digest mismatch.' }
    Write-Json @{ Seeded = $seeded; NaturalCache = $cache; Sha256 = (Get-FileHash $base).Hash } (Join-Path $case 'base-cache.json')
    $fixture = Join-Path $case 'Documents'
    New-Item -ItemType Directory -Path (Join-Path $fixture 'Source'),(Join-Path $fixture 'Destination') | Out-Null
    'Generated Restart Now retention document.' | Set-Content (Join-Path $fixture 'Source\keep.txt')
    Write-Json @{ LastPath = (Join-Path $fixture 'Destination'); Tabs = @((Join-Path $fixture 'Source'),(Join-Path $fixture 'Destination')); ActiveTabIndex = 1; RestoreTabs = $true; ViewMode = 2; Bookmarks = @((Join-Path $fixture 'Source')) } (Join-Path $state 'session.json')
    Write-Json @{ Theme = 'Light' } (Join-Path $state 'appearance.json')
    Copy-Item (Join-Path $state 'session.json') (Join-Path $case 'expected-session.json')
    $taskAction = New-ScheduledTaskAction -Execute "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -Argument ('-NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File "' + (Join-Path $Bundle 'ReleasedUpgradeGuest.ps1') + '" -Action Launch')
    $principal = New-ScheduledTaskPrincipal -UserId ([Security.Principal.WindowsIdentity]::GetCurrent().Name) -LogonType Interactive -RunLevel Limited
    $settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Minutes 5) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
    $taskName = 'WinThunar-Restart-' + $lab.RunId
    Register-ScheduledTask -TaskName $taskName -Action $taskAction -Principal $principal -Settings $settings | Out-Null
    Start-ScheduledTask -TaskName $taskName
    Write-Output "Prepared $($lab.BaseVersion) to $($lab.TargetVersion); seeded base cache: $seeded; case: $case"
    return
}
if ($Action -eq 'Ready') {
    & (Join-Path $Bundle 'ReleasedUpgradeGuest.ps1') -Action Capture -Label delta-ready
    $log = Join-Path $env:LOCALAPPDATA 'velopack\velopack_WinThunar.App.log'
    $stream = [IO.File]::Open($log,[IO.FileMode]::Open,[IO.FileAccess]::Read,([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
    $reader = New-Object IO.StreamReader($stream)
    try { $text = $reader.ReadToEnd() } finally { $reader.Dispose(); $stream.Dispose() }
    if ($text -notmatch 'Downloading delta' -or $text -notmatch 'Delta update download complete' -or $text -match '(?i)falling back|fallback to full') { throw 'Expected successful delta download without full fallback.' }
    $relative = 'feed\WinThunar.App-' + $lab.TargetVersion + '-full.nupkg'
    $package = Join-Path $root ('packages\' + [IO.Path]::GetFileName($relative))
    Add-Type -AssemblyName System.IO.Compression,System.IO.Compression.FileSystem
    $expectedZip = [IO.Compression.ZipFile]::OpenRead((Join-Path $Bundle $relative))
    $actualZip = [IO.Compression.ZipFile]::OpenRead($package)
    try {
        if ($expectedZip.Entries.Count -ne $actualZip.Entries.Count) { throw 'Reconstructed entry count differs.' }
        $checks = @(foreach ($entry in $expectedZip.Entries) {
            $actual = $actualZip.GetEntry($entry.FullName)
            if (-not $actual) { throw "Missing reconstructed entry: $($entry.FullName)" }
            $expectedStream = $entry.Open(); $actualStream = $actual.Open(); $sha = [Security.Cryptography.SHA256]::Create()
            try { $expectedHash = [BitConverter]::ToString($sha.ComputeHash($expectedStream)); $actualHash = [BitConverter]::ToString($sha.ComputeHash($actualStream)) }
            finally { $expectedStream.Dispose(); $actualStream.Dispose(); $sha.Dispose() }
            [pscustomobject]@{ Path = $entry.FullName; Matches = ($entry.Length -eq $actual.Length -and $expectedHash -eq $actualHash) }
        })
    } finally { $expectedZip.Dispose(); $actualZip.Dispose() }
    Write-Json $checks (Join-Path $case 'reconstructed-entries.json')
    if (@($checks | Where-Object { -not $_.Matches }).Count) { throw 'Reconstructed content mismatch.' }
    if ((Get-Item (Join-Path $root 'current\WinThunar.dll')).VersionInfo.FileVersion -ne ($lab.BaseVersion + '.0')) { throw 'Update applied before requested restart.' }
    $processes = @(Get-Process WinThunar)
    if ($processes.Count -ne 1) { throw 'Expected one app process.' }
    $processInfo = Get-CimInstance Win32_Process -Filter "ProcessId=$($processes[0].Id)"
    $elevated = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if ($elevated) { throw 'Guest verification must run unelevated.' }
    Write-Json @{ Passed = $true; DeltaDownloaded = $true; FullFallback = $false; ReconstructedEntries = $checks.Count; OldPid = $processes[0].Id; ProcessPath = $processInfo.ExecutablePath; ExpectedSession = (Get-Content (Join-Path $case 'expected-session.json') -Raw | ConvertFrom-Json) } (Join-Path $case 'delta-ready.json')
    Get-Content (Join-Path $case 'delta-ready.json') -Raw
    return
}
if ($Action -eq 'BeforeRestart') {
    $ready = Get-Content (Join-Path $case 'delta-ready.json') -Raw | ConvertFrom-Json
    $session = Get-Content (Join-Path $state 'session.json') -Raw | ConvertFrom-Json
    $processes = @(Get-Process WinThunar)
    if ($processes.Count -ne 1 -or $processes[0].Id -ne $ready.OldPid -or $session.Tabs.Count -ne 1 -or $session.Tabs[0] -ne $ready.ExpectedSession.Tabs[1]) { throw 'Expected original process and overwritten single-tab session.' }
    Write-Json @{ OriginalPid = $processes[0].Id; Session = $session } (Join-Path $case 'before-restart.json')
    Write-Output 'Original process survives; secondary window saved its single-tab session.'
    return
}
if ($Action -eq 'VerifyRestart') {
    $ready = Get-Content (Join-Path $case 'delta-ready.json') -Raw | ConvertFrom-Json
    if (-not (Test-Path (Join-Path $case 'before-restart.json'))) { throw 'Missing two-window precondition evidence.' }
    $processes = @(Get-Process WinThunar -ErrorAction SilentlyContinue)
    $updaters = @(Get-Process Update -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($root + '\',[StringComparison]::OrdinalIgnoreCase) })
    $version = (Get-Item (Join-Path $root 'current\WinThunar.dll')).VersionInfo.FileVersion
    if ($version -ne ($lab.TargetVersion + '.0') -or $processes.Count -ne 1 -or $processes[0].Id -eq $ready.OldPid -or $updaters.Count) { throw 'Target has not finished restarting; preserve evidence and retry after native completion.' }
    & (Join-Path $Bundle 'ReleasedUpgradeGuest.ps1') -Action VerifyTarget -Label after-restart-now
    $session = Get-Content (Join-Path $state 'session.json') -Raw | ConvertFrom-Json
    $appearance = Get-Content (Join-Path $state 'appearance.json') -Raw | ConvertFrom-Json
    $expected = $ready.ExpectedSession
    $tabsRetained = ($session.Tabs.Count -eq 2 -and $session.Tabs[0] -eq $expected.Tabs[0] -and $session.Tabs[1] -eq $expected.Tabs[1] -and $session.ActiveTabIndex -eq $expected.ActiveTabIndex -and $session.LastPath -eq $expected.LastPath)
    $viewRetained = $session.ViewMode -eq $expected.ViewMode
    $bookmarkRetained = @($session.Bookmarks).Count -eq 1 -and $session.Bookmarks[0] -eq $expected.Bookmarks[0]
    $documentRetained = (Get-Content (Join-Path $expected.Tabs[0] 'keep.txt') -Raw).Trim() -eq 'Generated Restart Now retention document.'
    $passed = $tabsRetained -and $viewRetained -and $bookmarkRetained -and $documentRetained -and $appearance.Theme -eq 'Light'
    Write-Json @{ Passed = $passed; DeltaPayloadPassed = $true; OldPid = $ready.OldPid; NewPid = $processes[0].Id; TargetVersion = $version; TabsRetained = $tabsRetained; ViewRetained = $viewRetained; BookmarkRetained = $bookmarkRetained; DocumentRetained = $documentRetained; Session = $session; Theme = $appearance.Theme; At = [DateTimeOffset]::Now.ToString('O') } (Join-Path $case 'restart-now-result.json')
    Get-Content (Join-Path $case 'restart-now-result.json') -Raw
    if (-not $passed) { throw 'Restart retention failed.' }
    return
}
if ($Action -eq 'Export') {
    # Export failures too; evidence must not depend on a passing result.
    $output = Join-Path $env:USERPROFILE ('WinThunarRestartEvidence-' + $lab.RunId)
    if (Test-Path $output) { throw 'Evidence export already exists.' }
    New-Item -ItemType Directory -Path $output | Out-Null
    Copy-Item -LiteralPath $case -Destination (Join-Path $output 'Case') -Recurse
    Copy-Item -LiteralPath $state -Destination (Join-Path $output 'UserState') -Recurse
    Write-Json @(Get-ChildItem $output -File -Recurse | ForEach-Object { [pscustomobject]@{ Path = $_.FullName.Substring($output.Length + 1); Bytes = $_.Length; Sha256 = (Get-FileHash $_.FullName).Hash } }) (Join-Path $output 'files.json')
    Add-Type -AssemblyName System.IO.Compression,System.IO.Compression.FileSystem
    $zip = $output + '.zip'
    [IO.Compression.ZipFile]::CreateFromDirectory($output,$zip)
    Write-Json @{ Archive = $zip; Bytes = (Get-Item $zip).Length; Sha256 = (Get-FileHash $zip).Hash } ($output + '-export.json')
    Get-Content ($output + '-export.json') -Raw
}
