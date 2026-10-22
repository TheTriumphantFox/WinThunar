<#
.SYNOPSIS
    Native per-user MSI regression checks, restricted to a declared disposable guest.
.DESCRIPTION
    Requires maintenance-input.json containing origin/guest MachineGuids, a unique RunId,
    ProductCode, BaseVersion, TargetVersion, and SHA-256 hashes for Msi and Package assets.
    Run each stage in order in a clean guest. Evidence and failures are never deleted.
    This exercises silent MSI/native updater commands, not the app's interactive update UI.
#>
param(
    [Parameter(Mandatory)][string]$InputDirectory,
    [Parameter(Mandatory)][ValidateSet('Install','Repair','Uninstall','Export')][string]$Stage
)
$ErrorActionPreference = 'Stop'
$inputRoot = [IO.Path]::GetFullPath($InputDirectory)
$manifest = Get-Content -LiteralPath (Join-Path $inputRoot 'maintenance-input.json') -Raw | ConvertFrom-Json
$machine = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Cryptography').MachineGuid
if (-not $machine -or $machine -eq $manifest.OriginMachineGuid -or $machine -ne $manifest.GuestMachineGuid) {
    throw 'Refusing to run outside the declared disposable guest, before writes or launches.'
}
if ($manifest.RunId -notmatch '^[a-f0-9]{32}$' -or $manifest.ProductCode -notmatch '^\{[A-Fa-f0-9-]{36}\}$') {
    throw 'Invalid fixture identity.'
}
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
if (([Security.Principal.WindowsPrincipal]$identity).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'These per-user cases require an unelevated guest account.'
}
foreach ($asset in @($manifest.Msi, $manifest.Package)) {
    $path = [IO.Path]::GetFullPath((Join-Path $inputRoot $asset.Name))
    if (-not $path.StartsWith($inputRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or
        (Get-FileHash -LiteralPath $path).Hash -ne $asset.Sha256) { throw 'Input asset hash/path mismatch.' }
}
$case = Join-Path $env:USERPROFILE ('WinThunarMsiMaintenance\' + $manifest.RunId)
$root = Join-Path $env:LOCALAPPDATA 'WinThunar.App'
$state = Join-Path $env:LOCALAPPDATA 'WinThunar'
$dll = Join-Path $root 'current\WinThunar.dll'
function Assert-Condition($value, [string]$message) { if (-not $value) { throw $message } }
function Write-Json($value, [string]$name) {
    $value | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $case $name) -Encoding UTF8
}
function Invoke-Msi([string]$operation, [string]$label, [int]$expected = 0) {
    $log = Join-Path $case ($label + '.log')
    Assert-Condition (-not (Test-Path -LiteralPath $log)) 'This stage has prior evidence; preserve it and use a new clean case.'
    $process = Start-Process "$env:SystemRoot\System32\msiexec.exe" -ArgumentList (
        $operation + ' /qn /norestart /l*v "' + $log + '"') -WindowStyle Hidden -PassThru
    $null = $process.Handle
    Assert-Condition ($process.WaitForExit(120000)) 'MSI timed out; preserve the running guest and logs.'
    $process.Refresh()
    Write-Json @{ ExitCode = $process.ExitCode; Expected = $expected; Operation = $operation; At = [DateTimeOffset]::Now.ToString('O') } ($label + '.json')
    Assert-Condition ($process.ExitCode -eq $expected) "MSI $label exited $($process.ExitCode); expected $expected."
}
function Get-Payload {
    @(Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object { $_.FullName -notlike ($root + '\packages\*') } |
        ForEach-Object { [pscustomobject]@{ Path = $_.FullName.Substring($root.Length); Sha256 = (Get-FileHash -LiteralPath $_.FullName).Hash } } |
        Sort-Object Path)
}
function Compare-Package {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead((Join-Path $inputRoot $manifest.Package.Name))
    try {
        $files = @(foreach ($entry in $archive.Entries) {
            if (-not $entry.FullName.StartsWith('lib/app/') -or $entry.FullName.EndsWith('/')) { continue }
            $relative = $entry.FullName.Substring(8)
            $target = Join-Path (Join-Path $root 'current') $relative
            if ($relative -eq 'Squirrel.exe') { $target = Join-Path $root 'Update.exe' }
            if ($relative -eq 'WinThunar_ExecutionStub.exe') { $target = Join-Path $root 'WinThunar.exe' }
            $target = [IO.Path]::GetFullPath($target)
            Assert-Condition ($target.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase)) 'Package escaped install root.'
            $stream = $entry.Open(); $sha = [Security.Cryptography.SHA256]::Create()
            try { $expected = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
            finally { $sha.Dispose(); $stream.Dispose() }
            $actual = if (Test-Path -LiteralPath $target) { (Get-FileHash -LiteralPath $target).Hash } else { $null }
            [pscustomobject]@{ Path = $relative; Expected = $expected; Actual = $actual; Matches = $expected -eq $actual }
        })
        Write-Json $files 'target-files.json'
        Assert-Condition ($files.Count -gt 500 -and @($files | Where-Object { -not $_.Matches }).Count -eq 0) 'Target package file verification failed.'
        return $files.Count
    }
    finally { $archive.Dispose() }
}
if ($Stage -eq 'Install') {
    foreach ($path in @($case, $root, $state, (Join-Path $env:ProgramFiles 'WinThunar.App'))) {
        Assert-Condition (-not (Test-Path -LiteralPath $path)) 'Requires a clean guest without WinThunar or previous case state.'
    }
    foreach ($hive in @('HKCU','HKLM')) {
        Assert-Condition (-not @(Get-ChildItem ($hive + ':\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall') |
            Where-Object { $_.PSChildName -eq $manifest.ProductCode -or $_.PSChildName -eq 'MSI:WinThunar.App' }).Count) 'Existing product registration.'
    }
    Assert-Condition (-not (Get-Process WinThunar -ErrorAction SilentlyContinue)) 'Guest app already running.'
    New-Item -ItemType Directory -Path $case | Out-Null
    Write-Json @{ RunId = $manifest.RunId; MachineGuid = $machine; User = $identity.Name } 'owner.json'
    Invoke-Msi ('/i "' + (Join-Path $inputRoot $manifest.Msi.Name) + '" ALLUSERS=2 MSIINSTALLPERUSER=1 DESKTOPSHORTCUT=1 STARTMENUSHORTCUT=1') 'install'
    Assert-Condition ((Get-Item -LiteralPath $dll).VersionInfo.FileVersion -eq $manifest.BaseVersion) 'Base version mismatch.'
    $before = Get-Payload
    Invoke-Msi ('/fomus ' + $manifest.ProductCode) 'same-version-repair'
    Invoke-Msi ('/fa ' + $manifest.ProductCode) 'same-version-force-repair'
    Assert-Condition (($before | ConvertTo-Json -Compress) -eq ((Get-Payload) | ConvertTo-Json -Compress)) 'Same-version repairs changed payload bytes.'
    & (Join-Path $root 'Update.exe') apply --help 2>&1 | Out-String | Set-Content (Join-Path $case 'updater-apply-help.txt')
    'Installed corrected MSI; both same-version repairs passed.'
    return
}
$owner = Get-Content -LiteralPath (Join-Path $case 'owner.json') -Raw | ConvertFrom-Json
Assert-Condition ($owner.RunId -eq $manifest.RunId -and $owner.MachineGuid -eq $machine) 'Case ownership mismatch.'
if ($Stage -eq 'Repair') {
    Assert-Condition (-not (Get-Process WinThunar -ErrorAction SilentlyContinue)) 'Close guest app before repair.'
    Assert-Condition ((Get-Item -LiteralPath $dll).VersionInfo.FileVersion -eq $manifest.TargetVersion) 'Apply the newer package in this guest before this stage.'
    $count = Compare-Package
    $before = Get-Payload
    Write-Json $before 'before-blocked-repairs.json'
    foreach ($repair in @(@{ Switch = '/fomus'; Label = 'older-standard-repair' }, @{ Switch = '/fa'; Label = 'older-force-repair' })) {
        Invoke-Msi ($repair.Switch + ' ' + $manifest.ProductCode) $repair.Label 1603
        Assert-Condition ((Get-Content (Join-Path $case ($repair.Label + '.log')) -Raw).Contains('This older installer cannot safely repair it.')) 'Expected repair guard diagnostic missing.'
        Assert-Condition (($before | ConvertTo-Json -Compress) -eq ((Get-Payload) | ConvertTo-Json -Compress)) 'Rejected repair changed payload bytes.'
    }
    # Repair is often invoked because a file is missing. Preserve the DLL outside the install
    # root and prove the scope-specific updated-version marker still prevents a mixed payload.
    $heldDll = Join-Path $case 'held-WinThunar.dll'
    Assert-Condition (-not (Test-Path -LiteralPath $heldDll)) 'Held DLL evidence already exists.'
    Move-Item -LiteralPath $dll -Destination $heldDll
    $missingBefore = Get-Payload
    Invoke-Msi ('/fomus ' + $manifest.ProductCode) 'older-repair-missing-dll' 1603
    Assert-Condition ((Get-Content (Join-Path $case 'older-repair-missing-dll.log') -Raw).Contains('This older installer cannot safely repair it.')) 'Missing-DLL repair guard diagnostic missing.'
    Assert-Condition (($missingBefore | ConvertTo-Json -Compress) -eq ((Get-Payload) | ConvertTo-Json -Compress)) 'Missing-DLL repair changed payload bytes.'
    Move-Item -LiteralPath $heldDll -Destination $dll
    Assert-Condition (($before | ConvertTo-Json -Compress) -eq ((Get-Payload) | ConvertTo-Json -Compress)) 'DLL restoration changed fixture bytes.'
    Write-Json @{ Result = 'Passed'; TargetFiles = $count; RejectedRepairModes = @('/fomus','/fa','/fomus with missing DLL'); PayloadUnchanged = $true } 'repair-result.json'
    'Both older repair modes rejected before mutation; all package files match.'
}
if ($Stage -eq 'Uninstall') {
    Assert-Condition ((Get-Content (Join-Path $case 'repair-result.json') -Raw | ConvertFrom-Json).Result -eq 'Passed') 'Repair stage must pass first.'
    $registration = Start-Process -FilePath (Join-Path $root 'current\WinThunar.exe') -ArgumentList '--register-default' -WindowStyle Hidden -PassThru
    $null = $registration.Handle
    Assert-Condition ($registration.WaitForExit(45000)) 'Shell registration timed out.'
    $registration.Refresh()
    Assert-Condition ($registration.ExitCode -eq 0) 'Shell registration failed.'
    Assert-Condition ((Get-ItemProperty 'HKCU:\Software\Classes\Directory\shell').'(default)' -eq 'WinThunar') 'Default folder handler not registered.'
    # Unresolved, opaque recovery input must survive byte-for-byte. Do not start the UI/recovery
    # worker after seeding. This checks installer retention, not journal replay or live Undo.
    New-Item -ItemType Directory -Path (Join-Path $state 'TransferRecovery'),(Join-Path $state 'Plugins') -Force | Out-Null
    '{"UnresolvedMaintenanceFixture":true}' | Set-Content (Join-Path $state 'TransferRecovery\unresolved.json')
    'retained original bytes' | Set-Content (Join-Path $state 'TransferRecovery\retained-original.bin')
    '{"RememberSession":true}' | Set-Content (Join-Path $state 'session.json')
    '{"AutomaticallyCheck":false}' | Set-Content (Join-Path $state 'updates.json')
    'user plugin preservation fixture' | Set-Content (Join-Path $state 'Plugins\keep.txt')
    $stateBefore = @(Get-ChildItem -LiteralPath $state -Recurse -File | ForEach-Object { [pscustomobject]@{ Path = $_.FullName; Sha256 = (Get-FileHash -LiteralPath $_.FullName).Hash } })
    Write-Json $stateBefore 'state-before-uninstall.json'
    $document = Join-Path $case 'keep-document.txt'
    'user document preservation fixture' | Set-Content -LiteralPath $document
    $documentHash = (Get-FileHash -LiteralPath $document).Hash
    Invoke-Msi ('/x ' + $manifest.ProductCode) 'uninstall'
    Assert-Condition (-not (Test-Path -LiteralPath $root)) 'Application install directory remains.'
    foreach ($file in $stateBefore) {
        Assert-Condition ((Get-FileHash -LiteralPath $file.Path).Hash -eq $file.Sha256) ('User state changed: ' + $file.Path)
    }
    Assert-Condition ((Get-FileHash -LiteralPath $document).Hash -eq $documentHash) 'User document changed.'
    foreach ($hive in @('HKCU','HKLM')) {
        foreach ($key in @($manifest.ProductCode,'MSI:WinThunar.App')) {
            Assert-Condition (-not (Test-Path ($hive + ':\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\' + $key))) 'Uninstall registration remains.'
        }
    }
    foreach ($directory in @([Environment]::GetFolderPath('Desktop'), [Environment]::GetFolderPath('Programs'))) {
        Assert-Condition (-not (Test-Path -LiteralPath (Join-Path $directory 'WinThunar.lnk'))) 'Shortcut remains.'
    }
    foreach ($class in @('Directory','Drive','Directory\Background')) {
        Assert-Condition (-not (Test-Path ('HKCU:\Software\Classes\' + $class + '\shell\WinThunar'))) 'Owned Shell verb remains.'
    }
    Assert-Condition ((Get-ItemProperty 'HKCU:\Software\Classes\Directory\shell' -ErrorAction SilentlyContinue).'(default)' -ne 'WinThunar') 'Default folder handler not restored.'
    Write-Json @{ Result = 'Passed'; PreservedStateFiles = $stateBefore.Count; DocumentPreserved = $true; AppRemoved = $true; RegistrationsRemoved = $true; ShortcutsRemoved = $true; ShellRegistrationRemoved = $true; RecoveryReplayTested = $false; LiveUndoTested = $false } 'uninstall-result.json'
    'Uninstall removed app/registrations/shortcuts/Shell integration and retained all seeded state bytes.'
}
if ($Stage -eq 'Export') {
    $zip = $case + '-evidence.zip'
    Assert-Condition (-not (Test-Path -LiteralPath $zip)) 'Evidence archive already exists.'
    Compress-Archive -LiteralPath $case -DestinationPath $zip
    @{ Archive = $zip; Bytes = (Get-Item -LiteralPath $zip).Length; Sha256 = (Get-FileHash -LiteralPath $zip).Hash } | ConvertTo-Json
}
