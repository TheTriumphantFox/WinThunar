<#
.SYNOPSIS
    Verify an original per-user MSI -> native app update -> corrected MSI migration in a disposable guest.
.DESCRIPTION
    Requires migration-input.json with OriginMachineGuid, GuestMachineGuid, RunId, BaseProductCode,
    TargetProductCode, BaseVersion, UpdatedVersion, TargetVersion and Assets (Role, Name, Bytes, Sha256).
    Roles: BaseMsi, UpdatedPackage, TargetMsi, TargetPackage. Use a clean guest snapshot per case.
    Rollback uses a private TargetMsi with a deliberate type-19 failure; never publish that MSI.
    Silent native checks only. Evidence, including failed stages, is retained. No recovery replay or live Undo.
    ElevatedMigration explicitly tests migration/maintenance under the same elevated guest account.
    RejectMigration verifies unelevated refusal before removal and compares the original cache/state.
    PreserveUserState explicitly tests an external backup/restore workaround; it is not installer behavior.
#>
param(
    [Parameter(Mandatory)][string]$InputDirectory,
    [Parameter(Mandatory)][ValidateSet('Install','Update','VerifyUpdate','Migrate','RejectMigration','Rollback','Repair','Uninstall','Export')][string]$Stage,
    [switch]$PreserveUserState,
    [switch]$ElevatedMigration
)
$ErrorActionPreference = 'Stop'
$inputRoot = [IO.Path]::GetFullPath($InputDirectory)
$manifest = Get-Content -LiteralPath (Join-Path $inputRoot 'migration-input.json') -Raw | ConvertFrom-Json
$machine = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Cryptography').MachineGuid
if (-not $machine -or $machine -eq $manifest.OriginMachineGuid -or $machine -ne $manifest.GuestMachineGuid) {
    throw 'Refusing to run outside the declared disposable guest, before writes or launches.'
}
function Assert-Condition($value, [string]$message) { if (-not $value) { throw $message } }
Assert-Condition ($manifest.RunId -match '^[a-f0-9]{32}$') 'Invalid case identity.'
Assert-Condition (-not $PreserveUserState -or $Stage -eq 'Migrate') 'PreserveUserState applies only to Migrate.'
foreach ($code in @($manifest.BaseProductCode, $manifest.TargetProductCode)) {
    Assert-Condition ($code -match '^\{[A-Fa-f0-9-]{36}\}$') 'Invalid MSI identity.'
}
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$elevated = ([Security.Principal.WindowsPrincipal]$identity).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
Assert-Condition ($elevated -eq [bool]$ElevatedMigration) 'Elevation must match the explicitly requested test context.'
Assert-Condition (-not $ElevatedMigration -or $Stage -in @('Migrate','Rollback','Repair','Uninstall','Export')) 'Only migration/maintenance and evidence export may run elevated.'
$assets = @{}
foreach ($role in @('BaseMsi','UpdatedPackage','TargetMsi','TargetPackage')) {
    $entry = @($manifest.Assets | Where-Object Role -eq $role)
    Assert-Condition ($entry.Count -eq 1) ('Missing or duplicate role: ' + $role)
    $path = [IO.Path]::GetFullPath((Join-Path $inputRoot $entry[0].Name))
    Assert-Condition ($path.StartsWith($inputRoot + '\', [StringComparison]::OrdinalIgnoreCase)) 'Asset escaped input root.'
    Assert-Condition ((Get-Item -LiteralPath $path).Length -eq $entry[0].Bytes -and (Get-FileHash -LiteralPath $path).Hash -eq $entry[0].Sha256) ('Asset hash/size mismatch: ' + $role)
    $assets[$role] = $path
}
$case = Join-Path $env:USERPROFILE ('WinThunarMsiMigration\' + $manifest.RunId)
$root = Join-Path $env:LOCALAPPDATA 'WinThunar.App'
$state = Join-Path $env:LOCALAPPDATA 'WinThunar'
$dll = Join-Path $root 'current\WinThunar.dll'
function Write-Json($value, [string]$name) {
    $path = Join-Path $case $name
    Assert-Condition (-not (Test-Path -LiteralPath $path)) ('Evidence already exists: ' + $name)
    $value | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $path -Encoding UTF8
}
function Invoke-CheckedProcess([string]$exe, [string]$arguments, [string]$label) {
    $p = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $null = $p.Handle
    Assert-Condition ($p.WaitForExit(120000)) ($label + ' timed out; preserve guest/process/logs.')
    $p.Refresh()
    Write-Json @{ExitCode=$p.ExitCode; Arguments=$arguments; At=[DateTimeOffset]::Now.ToString('O')} ($label + '.json')
    Assert-Condition ($p.ExitCode -eq 0) ($label + ' failed: ' + $p.ExitCode)
}
function Invoke-Msi([string]$operation, [string]$label) {
    $log = Join-Path $case ($label + '.log')
    Assert-Condition (-not (Test-Path -LiteralPath $log)) 'Prior MSI evidence exists.'
    Invoke-CheckedProcess "$env:SystemRoot\System32\msiexec.exe" ($operation + ' /qn /norestart /l*v "' + $log + '"') $label
}
function Get-StateFiles {
    @(Get-ChildItem -LiteralPath $state -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object {
        [pscustomobject]@{Path=$_.FullName.Substring($state.Length + 1); Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}
    } | Sort-Object Path)
}
function Compare-State([string]$label) {
    $before = Get-Content -LiteralPath (Join-Path $case 'seeded-state.json') -Raw | ConvertFrom-Json
    $checks = @(foreach ($file in $before) {
        $path = Join-Path $state $file.Path
        $actual = if (Test-Path -LiteralPath $path) { (Get-FileHash -LiteralPath $path).Hash } else { $null }
        [pscustomobject]@{Path=$file.Path; Expected=$file.Sha256; Actual=$actual; Matches=$actual -eq $file.Sha256}
    })
    Write-Json $checks ($label + '-state.json')
    return @($checks | Where-Object { -not $_.Matches }).Count -eq 0
}
function Compare-Package([string]$role, [string]$label) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($assets[$role])
    try {
        $checks = @(foreach ($entry in $archive.Entries) {
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
            [pscustomobject]@{Path=$relative; Expected=$expected; Actual=$actual; Matches=$actual -eq $expected}
        })
        Write-Json $checks ($label + '-files.json')
        return ($checks.Count -gt 500 -and @($checks | Where-Object { -not $_.Matches }).Count -eq 0)
    }
    finally { $archive.Dispose() }
}
function Get-Registration {
    @(foreach ($hive in @('HKCU','HKLM')) {
        Get-ItemProperty ($hive + ':\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*') -ErrorAction SilentlyContinue |
            Where-Object { $_.DisplayName -like '*WinThunar*' } |
            Select-Object PSPath,PSChildName,DisplayVersion,InstallLocation,UninstallString,WindowsInstaller
    })
}
function Get-ProductHealth([string]$code) {
    $installer = New-Object -ComObject WindowsInstaller.Installer
    try {
        $health = [ordered]@{ProductCode=$code; State=$installer.GetType().InvokeMember('ProductState',[Reflection.BindingFlags]::GetProperty,$null,$installer,@($code))}
        try {
            $cache = $installer.GetType().InvokeMember('ProductInfo',[Reflection.BindingFlags]::GetProperty,$null,$installer,@($code,'LocalPackage'))
            $health.LocalPackage = $cache
            $health.CacheSha256 = (Get-FileHash -LiteralPath $cache).Hash
        } catch { $health.CacheError = $_.ToString() }
        [pscustomobject]$health
    }
    finally { [Runtime.InteropServices.Marshal]::ReleaseComObject($installer) | Out-Null }
}
function Get-CachedPolicy {
    function Invoke-Member($target, [string]$name, [Reflection.BindingFlags]$kind, [object[]]$arguments) {
        $target.GetType().InvokeMember($name, $kind, $null, $target, $arguments)
    }
    $installer = New-Object -ComObject WindowsInstaller.Installer
    try {
        $cache = Invoke-Member $installer 'ProductInfo' 'GetProperty' @($manifest.TargetProductCode, 'LocalPackage')
        $db = Invoke-Member $installer 'OpenDatabase' 'InvokeMethod' @($cache, 0)
        try {
            $rows = @(foreach ($sql in @('SELECT `Value` FROM `Property` WHERE `Property` = ''RustAppId''','SELECT `Target` FROM `CustomAction` WHERE `Action` = ''WinThunarRejectOlderRepair''')) {
                $view = Invoke-Member $db 'OpenView' 'InvokeMethod' @($sql)
                try {
                    Invoke-Member $view 'Execute' 'InvokeMethod' @($null) | Out-Null
                    $row = Invoke-Member $view 'Fetch' 'InvokeMethod' $null
                    if ($row) {
                        try { Invoke-Member $row 'StringData' 'GetProperty' @(1) }
                        finally { [Runtime.InteropServices.Marshal]::ReleaseComObject($row) | Out-Null }
                    }
                }
                finally { Invoke-Member $view 'Close' 'InvokeMethod' $null | Out-Null; [Runtime.InteropServices.Marshal]::ReleaseComObject($view) | Out-Null }
            })
            [pscustomobject]@{LocalPackage=$cache; Sha256=(Get-FileHash -LiteralPath $cache).Hash; CleanupId=$rows[0]; RepairGuard=($rows.Count -eq 2 -and $rows[1].Contains('This older installer cannot safely repair it.'))}
        }
        finally { [Runtime.InteropServices.Marshal]::ReleaseComObject($db) | Out-Null }
    }
    finally { [Runtime.InteropServices.Marshal]::ReleaseComObject($installer) | Out-Null }
}
if ($Stage -eq 'Install') {
    foreach ($path in @($case,$root,$state,(Join-Path $env:ProgramFiles 'WinThunar.App'))) {
        Assert-Condition (-not (Test-Path -LiteralPath $path)) 'Requires a clean guest.'
    }
    Assert-Condition (@(Get-Registration).Count -eq 0) 'Existing WinThunar registration.'
    Assert-Condition (-not (Get-Process WinThunar -ErrorAction SilentlyContinue)) 'Guest app running.'
    New-Item -ItemType Directory -Path $case | Out-Null
    Write-Json @{RunId=$manifest.RunId; MachineGuid=$machine; User=$identity.Name} 'owner.json'
} else {
    $owner = Get-Content -LiteralPath (Join-Path $case 'owner.json') -Raw | ConvertFrom-Json
    Assert-Condition ($owner.RunId -eq $manifest.RunId -and $owner.MachineGuid -eq $machine) 'Case ownership mismatch.'
}
if ($Stage -eq 'Export') {
    $zip = $case + '-evidence.zip'
    Assert-Condition (-not (Test-Path -LiteralPath $zip)) 'Export already exists.'
    Compress-Archive -LiteralPath $case -DestinationPath $zip
    @{Archive=$zip; Bytes=(Get-Item -LiteralPath $zip).Length; Sha256=(Get-FileHash -LiteralPath $zip).Hash} | ConvertTo-Json
    return
}
$stageMarker = Join-Path $case ($Stage.ToLowerInvariant() + '-started.json')
Assert-Condition (-not (Test-Path -LiteralPath $stageMarker)) 'Stage already attempted; preserve evidence and restore a snapshot for a new case.'
Write-Json @{Stage=$Stage; At=[DateTimeOffset]::Now.ToString('O')} ($Stage.ToLowerInvariant() + '-started.json')
try {
    Assert-Condition (-not (Get-Process WinThunar -ErrorAction SilentlyContinue)) 'Guest app running.'
    if ($Stage -eq 'Install') {
        Invoke-Msi ('/i "' + $assets.BaseMsi + '" ALLUSERS=2 MSIINSTALLPERUSER=1 DESKTOPSHORTCUT=1 STARTMENUSHORTCUT=1') 'base-install'
        Assert-Condition ((Get-Item -LiteralPath $dll).VersionInfo.FileVersion -eq $manifest.BaseVersion) 'Wrong base payload.'
        Invoke-CheckedProcess (Join-Path $root 'current\WinThunar.exe') '--register-default' 'register-shell'
        Assert-Condition ((Get-ItemProperty 'HKCU:\Software\Classes\Directory\shell').'(default)' -eq 'WinThunar') 'Shell fixture not enabled.'
        New-Item -ItemType Directory -Path (Join-Path $state 'TransferRecovery'),(Join-Path $state 'Plugins') -Force | Out-Null
        '{"RememberSession":true,"MigrationFixture":true}' | Set-Content (Join-Path $state 'session.json')
        '{"AutomaticallyCheck":false}' | Set-Content (Join-Path $state 'updates.json')
        'migration plugin fixture' | Set-Content (Join-Path $state 'Plugins\keep.txt')
        '{"OpaqueUnresolvedFixture":true}' | Set-Content (Join-Path $state 'TransferRecovery\unresolved.json')
        'retained original bytes' | Set-Content (Join-Path $state 'TransferRecovery\retained-original.bin')
        Write-Json @(Get-StateFiles) 'seeded-state.json'
        'user document fixture' | Set-Content (Join-Path $case 'keep-document.txt')
        Write-Json @{Sha256=(Get-FileHash (Join-Path $case 'keep-document.txt')).Hash} 'document.json'
        Write-Json @(Get-Registration) 'base-registration.json'
    }
    if ($Stage -in @('Update','VerifyUpdate')) {
        Assert-Condition ((Get-Content (Join-Path $case 'install-result.json') -Raw | ConvertFrom-Json).Result -eq 'Passed') 'Install stage must pass first.'
        if ($Stage -eq 'Update') {
            Invoke-CheckedProcess (Join-Path $root 'Update.exe') ('apply --norestart --silent --package "' + $assets.UpdatedPackage + '" --log "' + (Join-Path $case 'native-update.log') + '"') 'native-update'
        } else {
            Assert-Condition ((Get-Content (Join-Path $case 'native-update.json') -Raw | ConvertFrom-Json).ExitCode -eq 0) 'Requires a completed native update.'
        }
        $label = $Stage.ToLowerInvariant()
        $files = Compare-Package 'UpdatedPackage' $label
        $retained = Compare-State $label
        Write-Json @(Get-Registration) ($label + '-registration.json')
        Assert-Condition ($files -and $retained -and (Get-Item -LiteralPath $dll).VersionInfo.FileVersion -eq $manifest.UpdatedVersion) 'Native update payload/state verification failed.'
    }
    if ($Stage -eq 'Migrate') {
        $verified = @('update-result.json','verifyupdate-result.json' | ForEach-Object {
            $path = Join-Path $case $_
            if (Test-Path -LiteralPath $path) { (Get-Content -LiteralPath $path -Raw | ConvertFrom-Json).Result }
        })
        Assert-Condition ($verified -contains 'Passed') 'Update stage must pass first.'
        $backup = Join-Path $case 'state-backup'
        if ($PreserveUserState) {
            Assert-Condition (@(Get-ChildItem -LiteralPath $state -Recurse -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count -eq 0) 'Backup fixture must not contain reparse points.'
            Assert-Condition (-not (Test-Path -LiteralPath $backup)) 'Backup already exists.'
            Copy-Item -LiteralPath $state -Destination $backup -Recurse -Force
            $backupChecks = @(foreach ($file in (Get-StateFiles)) {
                [pscustomobject]@{Path=$file.Path; Sha256=$file.Sha256; Matches=((Get-FileHash -LiteralPath (Join-Path $backup $file.Path)).Hash -eq $file.Sha256)}
            })
            Write-Json $backupChecks 'backup-verification.json'
            Assert-Condition ($backupChecks.Count -gt 0 -and @($backupChecks | Where-Object { -not $_.Matches }).Count -eq 0) 'Backup verification failed; installer not started.'
        }
        try {
            Invoke-Msi ('/i "' + $assets.TargetMsi + '" ALLUSERS=2 MSIINSTALLPERUSER=1 DESKTOPSHORTCUT=1 STARTMENUSHORTCUT=1') 'target-install'
        }
        finally {
            if ($PreserveUserState) {
                # Record the native outcome first. Restore only the independently hashed backup;
                # never start the app/recovery worker while opaque recovery fixtures are present.
                $nativeRetained = Compare-State 'native-before-restore'
                Assert-Condition (-not (Get-Process WinThunar -ErrorAction SilentlyContinue)) 'App started unexpectedly; backup retained for manual recovery.'
                New-Item -ItemType Directory -Path $state -Force | Out-Null
                foreach ($file in $backupChecks) {
                    $source = Join-Path $backup $file.Path
                    Assert-Condition ((Get-FileHash -LiteralPath $source).Hash -eq $file.Sha256) 'Backup changed; restore stopped.'
                    $destination = Join-Path $state $file.Path
                    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
                    Copy-Item -LiteralPath $source -Destination $destination -Force
                }
                Write-Json @{ExternalBackupRestoreUsed=$true; NativeStateRetained=$nativeRetained; Backup=$backup} 'external-restore.json'
            }
        }
        $files = Compare-Package 'TargetPackage' 'migrated'
        $retained = Compare-State 'migrated'
        $registration = @(Get-Registration)
        Write-Json $registration 'migrated-registration.json'
        $policy = Get-CachedPolicy
        Write-Json $policy 'cached-policy.json'
        $registered = @($registration | Where-Object PSChildName -eq $manifest.BaseProductCode).Count -eq 0 -and
            @($registration | Where-Object PSChildName -eq $manifest.TargetProductCode).Count -eq 1
        Write-Json @{PayloadMatches=$files; StateRetained=$retained; ExternalBackupRestoreUsed=[bool]$PreserveUserState; OldProductRemoved=$registered; CachedPolicyCorrect=($policy.CleanupId -eq 'WinThunar.App' -and $policy.RepairGuard)} 'migration-observations.json'
        Assert-Condition ($files -and $retained -and $registered -and $policy.CleanupId -eq 'WinThunar.App' -and $policy.RepairGuard) 'Migration failed payload/state/registration/cache verification; preserve all evidence.'
        Assert-Condition (-not (Test-Path -LiteralPath (Join-Path $env:LOCALAPPDATA 'WinThunar.MsiMigrationState'))) 'Migration left state in holding.'
    }
    if ($Stage -in @('Rollback','RejectMigration')) {
        # Rollback requires an isolated candidate with a deliberate type-19 failure after
        # old-product removal, or after InstallExecuteAgain flushes the restored state.
        # RejectMigration instead uses an ordinary candidate without injected failure.
        $label = $Stage.ToLowerInvariant()
        $beforeHealth = Get-ProductHealth $manifest.BaseProductCode
        Write-Json $beforeHealth ($label + '-before-product.json')
        Assert-Condition ($beforeHealth.State -eq 5 -and $beforeHealth.CacheSha256) 'Original MSI must be fully registered with a readable cache before the test.'
        $beforeRegistration = @(Get-Registration)
        Write-Json $beforeRegistration ($label + '-before-registration.json')
        $log = Join-Path $case ($label + '-install.log')
        $arguments = '/i "' + $assets.TargetMsi + '" ALLUSERS=2 MSIINSTALLPERUSER=1 /qn /norestart /l*v "' + $log + '"'
        $process = Start-Process "$env:SystemRoot\System32\msiexec.exe" -ArgumentList $arguments -WindowStyle Hidden -PassThru
        $null = $process.Handle
        Assert-Condition ($process.WaitForExit(120000)) 'Rollback installer timed out.'
        $process.Refresh()
        Write-Json @{ExitCode=$process.ExitCode; Arguments=$arguments; Elevated=$elevated} ($label + '-install.json')
        Assert-Condition ($process.ExitCode -eq 1603) 'Expected MSI failure.'
        $text = Get-Content -LiteralPath $log -Raw
        if ($Stage -eq 'Rollback') {
            Assert-Condition ($text.Contains('WinThunar deliberate migration rollback test')) 'Expected injected failure diagnostic missing.'
            Assert-Condition ($text.Contains('State directory held outside legacy cleanup.') -and $text.Contains('State directory restored (success or rollback).')) 'Protection/rollback did not execute.'
        } else {
            Assert-Condition ($text.Contains('requires administrator approval for safe installer rollback')) 'Expected unelevated migration refusal missing.'
            Assert-Condition (-not ($text -match '(?m)(Doing action: RemoveExistingProducts|^Action start [^\r\n]*: RemoveExistingProducts\.)')) 'Rejected migration began old-product removal.'
            Assert-Condition (-not $text.Contains('State directory held outside legacy cleanup.')) 'Rejected migration moved user state.'
        }
        $files = Compare-Package 'UpdatedPackage' 'rolled-back'
        $retained = Compare-State 'rolled-back'
        $registration = @(Get-Registration)
        Write-Json $registration 'rolled-back-registration.json'
        $afterHealth = Get-ProductHealth $manifest.BaseProductCode
        Write-Json $afterHealth ($label + '-after-product.json')
        Assert-Condition ($files -and $retained) 'Rollback changed original updated payload or state.'
        Assert-Condition (@($registration | Where-Object PSChildName -eq $manifest.BaseProductCode).Count -eq 1 -and
            @($registration | Where-Object PSChildName -eq $manifest.TargetProductCode).Count -eq 0) 'Rollback product registration mismatch.'
        Assert-Condition (($registration | ConvertTo-Json -Depth 5 -Compress) -eq
            ($beforeRegistration | ConvertTo-Json -Depth 5 -Compress)) 'Original uninstall metadata changed.'
        Assert-Condition ($afterHealth.State -eq 5 -and $afterHealth.LocalPackage -eq $beforeHealth.LocalPackage -and
            $afterHealth.CacheSha256 -eq $beforeHealth.CacheSha256) 'Rollback did not restore installed product state and the original MSI cache.'
        Assert-Condition (-not (Test-Path -LiteralPath (Join-Path $env:LOCALAPPDATA 'WinThunar.MsiMigrationState'))) 'Rollback left state in holding.'
    }
    if ($Stage -eq 'Repair') {
        Assert-Condition ((Get-Content (Join-Path $case 'migrate-result.json') -Raw | ConvertFrom-Json).Result -eq 'Passed') 'Migration must pass first.'
        foreach ($mode in @(@{Switch='/fomus';Label='standard'},@{Switch='/fa';Label='forced'})) {
            Invoke-Msi ($mode.Switch + ' ' + $manifest.TargetProductCode) ($mode.Label + '-repair')
            $files = Compare-Package 'TargetPackage' ($mode.Label + '-repair')
            $retained = Compare-State ($mode.Label + '-repair')
            Assert-Condition ($files -and $retained) 'Repair changed payload/state.'
        }
    }
    if ($Stage -eq 'Uninstall') {
        Assert-Condition ((Get-Content (Join-Path $case 'repair-result.json') -Raw | ConvertFrom-Json).Result -eq 'Passed') 'Repair must pass first.'
        Invoke-CheckedProcess (Join-Path $root 'current\WinThunar.exe') '--register-default' 'pre-uninstall-register-shell'
        Assert-Condition ((Get-ItemProperty 'HKCU:\Software\Classes\Directory\shell').'(default)' -eq 'WinThunar') 'Positive Shell cleanup requires an enabled default handler.'
        foreach ($class in @('Directory','Drive','Directory\Background')) {
            Assert-Condition (Test-Path ('HKCU:\Software\Classes\' + $class + '\shell\WinThunar')) 'Positive Shell cleanup requires enabled verbs.'
        }
        Assert-Condition (Compare-State 'before-uninstall') 'Shell fixture setup changed retained state.'
        Invoke-Msi ('/x ' + $manifest.TargetProductCode) 'target-uninstall'
        $retained = Compare-State 'uninstalled'
        $document = Get-Content (Join-Path $case 'document.json') -Raw | ConvertFrom-Json
        $checks = [ordered]@{StateRetained=$retained; AppRemoved=(-not (Test-Path -LiteralPath $root)); RegistrationsRemoved=(@(Get-Registration).Count -eq 0); DocumentRetained=((Get-FileHash (Join-Path $case 'keep-document.txt')).Hash -eq $document.Sha256)}
        foreach ($dir in @([Environment]::GetFolderPath('Desktop'),[Environment]::GetFolderPath('Programs'))) {
            $checks['ShortcutRemoved-' + $dir] = -not (Test-Path -LiteralPath (Join-Path $dir 'WinThunar.lnk'))
        }
        foreach ($class in @('Directory','Drive','Directory\Background')) {
            $checks['ShellRemoved-' + $class] = -not (Test-Path ('HKCU:\Software\Classes\' + $class + '\shell\WinThunar'))
        }
        $checks.DefaultRestored = (Get-ItemProperty 'HKCU:\Software\Classes\Directory\shell' -ErrorAction SilentlyContinue).'(default)' -ne 'WinThunar'
        Write-Json $checks 'uninstall-observations.json'
        Assert-Condition (@($checks.Values | Where-Object { -not $_ }).Count -eq 0) 'Uninstall verification failed.'
    }
    Write-Json @{Result='Passed'; Stage=$Stage; Elevated=$elevated; InteractiveUiTested=$false; RecoveryReplayTested=$false; LiveUndoTested=$false; At=[DateTimeOffset]::Now.ToString('O')} ($Stage.ToLowerInvariant() + '-result.json')
    'Passed: ' + $Stage
}
catch {
    Write-Json @{Result='Failed'; Stage=$Stage; Elevated=$elevated; Error=$_.ToString(); At=[DateTimeOffset]::Now.ToString('O')} ($Stage.ToLowerInvariant() + '-result.json')
    throw
}
