<#
.SYNOPSIS
    Patches an isolated MSI copy and verifies its guard with the native MSI condition engine.
.DESCRIPTION
    Never installs, repairs, uninstalls, or launches WinThunar. OpenPackage creates an MSI
    session only; this script evaluates conditions and reads tables without executing actions.
#>
param(
    [Parameter(Mandatory)][string]$MsiPath,
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$original = (Resolve-Path -LiteralPath $MsiPath).Path
$originalHash = (Get-FileHash -LiteralPath $original).Hash
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path (Join-Path $PSScriptRoot '..\artifacts') ('msi-policy-' + [guid]::NewGuid().ToString('N'))
}
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a new output directory; existing evidence is retained.' }
New-Item -ItemType Directory -Path $output | Out-Null
$candidate = Join-Path $output 'guarded.msi'
Copy-Item -LiteralPath $original -Destination $candidate
# Dot-source the patcher to share its MSI COM helpers, after it patches only the isolated copy.
. (Join-Path $PSScriptRoot 'Set-MsiMaintenancePolicy.ps1') -MsiPath $candidate
$patchedHash = (Get-FileHash -LiteralPath $candidate).Hash
$repeatRejected = $false
try { & (Join-Path $PSScriptRoot 'Set-MsiMaintenancePolicy.ps1') -MsiPath $candidate }
catch { $repeatRejected = $_.ToString().Contains('already has the maintenance policy') }
if (-not $repeatRejected -or (Get-FileHash -LiteralPath $candidate).Hash -ne $patchedHash) {
    throw 'Repeat patch was not rejected without mutation.'
}
$installer = New-Object -ComObject WindowsInstaller.Installer
$session = Invoke-Member $installer 'OpenPackage' 'InvokeMethod' @((Resolve-Path -LiteralPath $candidate).Path, 1)
$database = Invoke-Member $session 'Database' 'GetProperty' $null
try {
    $cleanupId = Invoke-Sql $database 'SELECT `Value` FROM `Property` WHERE `Property` = ''RustAppId'''
    if ($cleanupId[0][0] -ne 'WinThunar.App') { throw 'Cleanup still targets user settings.' }
    $rawVersion = Invoke-Member $session 'Property' 'GetProperty' @('RustAppVersion')
    $numericVersion = [version]($rawVersion -split '[-+]')[0]
    $newer = '{0}.{1}.{2}' -f $numericVersion.Major, $numericVersion.Minor, ($numericVersion.Build + 1)
    $conditionRows = Invoke-Sql $database 'SELECT `Condition`, `Sequence` FROM `InstallExecuteSequence` WHERE `Action` = ''WinThunarRejectOlderRepair'''
    $initializeRows = Invoke-Sql $database 'SELECT `Sequence` FROM `InstallExecuteSequence` WHERE `Action` = ''InstallInitialize'''
    if ($conditionRows.Count -ne 1 -or [int]$conditionRows[0][1] -ge [int]$initializeRows[0][0]) { throw 'Guard runs too late.' }
    $condition = $conditionRows[0][0]
    $migrationActions = Invoke-Sql $database 'SELECT `Action`, `Type`, `Source`, `Target` FROM `CustomAction`'
    foreach ($expected in @(@('WinThunarPrepareMigration',1,'PrepareMigration'), @('WinThunarRollbackState',1281,'RestoreState'),
        @('WinThunarProtectState',1025,'ProtectState'), @('WinThunarRestoreState',1025,'RestoreState'))) {
        $row = @($migrationActions | Where-Object { $_[0] -eq $expected[0] })
        if ($row.Count -ne 1 -or [int]$row[0][1] -ne $expected[1] -or $row[0][2] -ne 'WinThunarMigrationActions' -or $row[0][3] -ne $expected[2]) {
            throw ('Incorrect migration action type/entry point: ' + $expected[0])
        }
    }
    $migrationSequence = Invoke-Sql $database 'SELECT `Action`, `Condition`, `Sequence` FROM `InstallExecuteSequence`'
    foreach ($expected in @(@('WinThunarPrepareMigration',1490),@('InstallInitialize',1500),@('WinThunarRollbackState',1501),
        @('WinThunarProtectState',1502),@('InstallExecute',1503),@('RemoveExistingProducts',1504),@('ProcessComponents',1600),
        @('WinThunarRestoreState',6590),@('InstallFinalize',6600))) {
        $row = @($migrationSequence | Where-Object { $_[0] -eq $expected[0] })
        if ($row.Count -ne 1 -or [int]$row[0][2] -ne $expected[1]) { throw ('Unsafe migration sequence: ' + $expected[0]) }
    }
    $migrationChecks = @(foreach ($case in @(
        @{Name='Legacy upgrade'; Installed=''; Related='{117822C1-8107-47FA-A8A7-D50EF2386071}'; Expected=1},
        @{Name='Fresh installation'; Installed=''; Related=''; Expected=0},
        @{Name='Repair'; Installed='1'; Related=''; Expected=0},
        @{Name='Maintenance with related product'; Installed='1'; Related='{117822C1-8107-47FA-A8A7-D50EF2386071}'; Expected=0})) {
        Invoke-Member $session 'Property' 'SetProperty' @('Installed',$case.Installed) | Out-Null
        Invoke-Member $session 'Property' 'SetProperty' @('WIX_UPGRADE_DETECTED',$case.Related) | Out-Null
        foreach ($name in @('WinThunarPrepareMigration','InstallExecute')) {
            $row = @($migrationSequence | Where-Object { $_[0] -eq $name })
            if ((Invoke-Member $session 'EvaluateCondition' 'InvokeMethod' @($row[0][1])) -ne $case.Expected) { throw 'Migration condition failed.' }
        }
        [pscustomobject]@{Case=$case.Name; Runs=$case.Expected; Passed=$true}
    })
    $cases = @(
        @{ Name = 'Fresh install'; Installed = ''; Scope = ''; UserVersion = $newer; MachineVersion = ''; Dll = '1'; Remove = ''; Reject = 0 },
        @{ Name = 'Same version per-user'; Installed = '1'; Scope = ''; UserVersion = $rawVersion; MachineVersion = ''; Dll = ''; Remove = ''; Reject = 0 },
        @{ Name = 'Updated per-user'; Installed = '1'; Scope = ''; UserVersion = $newer; MachineVersion = ''; Dll = '1'; Remove = ''; Reject = 1 },
        @{ Name = 'Updated per-user missing DLL'; Installed = '1'; Scope = ''; UserVersion = $newer; MachineVersion = ''; Dll = ''; Remove = ''; Reject = 1 },
        @{ Name = 'Same per-user with newer machine install'; Installed = '1'; Scope = ''; UserVersion = $rawVersion; MachineVersion = $newer; Dll = ''; Remove = ''; Reject = 0 },
        @{ Name = 'Same all-users with newer user install'; Installed = '1'; Scope = '1'; UserVersion = $newer; MachineVersion = $rawVersion; Dll = ''; Remove = ''; Reject = 0 },
        @{ Name = 'Updated all-users missing DLL'; Installed = '1'; Scope = '1'; UserVersion = $rawVersion; MachineVersion = $newer; Dll = ''; Remove = ''; Reject = 1 },
        @{ Name = 'Newer DLL with old registration'; Installed = '1'; Scope = ''; UserVersion = $rawVersion; MachineVersion = ''; Dll = '1'; Remove = ''; Reject = 1 },
        @{ Name = 'Partial feature removal after update'; Installed = '1'; Scope = ''; UserVersion = $newer; MachineVersion = ''; Dll = ''; Remove = 'SomeFeature'; Reject = 1 },
        @{ Name = 'Uninstall newer per-user'; Installed = '1'; Scope = ''; UserVersion = $newer; MachineVersion = ''; Dll = '1'; Remove = 'ALL'; Reject = 0 },
        @{ Name = 'Uninstall newer all-users'; Installed = '1'; Scope = '1'; UserVersion = ''; MachineVersion = $newer; Dll = '1'; Remove = 'ALL'; Reject = 0 }
    )
    $results = @(foreach ($case in $cases) {
        foreach ($pair in @(@('Installed', $case.Installed), @('ALLUSERS', $case.Scope),
            @('WINTHUNAR_USER_VERSION', $case.UserVersion), @('WINTHUNAR_MACHINE_VERSION', $case.MachineVersion),
            @('WINTHUNAR_NEWER_PAYLOAD', $case.Dll), @('REMOVE', $case.Remove))) {
            Invoke-Member $session 'Property' 'SetProperty' @($pair[0], $pair[1]) | Out-Null
        }
        $actual = Invoke-Member $session 'EvaluateCondition' 'InvokeMethod' @($condition)
        if ($actual -ne $case.Reject) { throw "Native condition failed: $($case.Name), result $actual." }
        [pscustomobject]@{ Case = $case.Name; Reject = $actual; Passed = $true }
    })
    if ((Get-FileHash -LiteralPath $original).Hash -ne $originalHash) { throw 'Original MSI changed.' }
    $report = [ordered]@{ Original = $original; OriginalUnchanged = $true; OriginalSha256 = $originalHash;
        Candidate = $candidate; CandidateSha256 = $patchedHash; RepeatRejectedWithoutMutation = $true;
        NativeConditionChecks = $results; MigrationActionTypesAndSequenceVerified = $true; MigrationConditionChecks = $migrationChecks;
        InstallationActionsExecuted = $false; At = [DateTimeOffset]::Now.ToString('O') }
    $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'results.json') -Encoding UTF8
    "Passed $($results.Count) native condition cases; original MSI unchanged. Evidence: $output"
}
finally {
    [Runtime.InteropServices.Marshal]::ReleaseComObject($database) | Out-Null
    [Runtime.InteropServices.Marshal]::ReleaseComObject($session) | Out-Null
    [Runtime.InteropServices.Marshal]::ReleaseComObject($installer) | Out-Null
}
