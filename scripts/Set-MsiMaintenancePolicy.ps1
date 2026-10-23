<#
.SYNOPSIS
    Protects per-user legacy migration, preserves uninstall state, and rejects older repair.
.DESCRIPTION
    Edits a newly generated Velopack 1.2.161 MSI before distribution. This does not
    modify an installed/cached MSI, and cannot retrofit already distributed installers.
    Adds original native transaction/rollback actions compiled with Visual C++ x64.
#>
param(
    [Parameter(Mandatory)]
    [string]$MsiPath,
    [string]$MigrationActionsPath
)

$ErrorActionPreference = 'Stop'

function Invoke-Member($target, [string]$name, [System.Reflection.BindingFlags]$kind, [object[]]$arguments) {
    $target.GetType().InvokeMember($name, $kind, $null, $target, $arguments)
}

function Invoke-Sql($database, [string]$sql, [object[]]$values = @()) {
    $view = Invoke-Member $database 'OpenView' 'InvokeMethod' @($sql)
    $record = $null
    try {
        if ($values.Count -gt 0) {
            $record = Invoke-Member $installer 'CreateRecord' 'InvokeMethod' @($values.Count)
            for ($index = 0; $index -lt $values.Count; $index++) {
                $property = if ($values[$index] -is [int]) { 'IntegerData' } else { 'StringData' }
                Invoke-Member $record $property 'SetProperty' @(($index + 1), $values[$index]) | Out-Null
            }
        }
        Invoke-Member $view 'Execute' 'InvokeMethod' @($record) | Out-Null
        $rows = @()
        if ($sql.StartsWith('SELECT')) {
            while ($row = Invoke-Member $view 'Fetch' 'InvokeMethod' $null) {
                try {
                    $count = Invoke-Member $row 'FieldCount' 'GetProperty' $null
                    $rows += , @(for ($field = 1; $field -le $count; $field++) { Invoke-Member $row 'StringData' 'GetProperty' @($field) })
                }
                finally { [Runtime.InteropServices.Marshal]::ReleaseComObject($row) | Out-Null }
            }
        }
        return , $rows
    }
    finally {
        Invoke-Member $view 'Close' 'InvokeMethod' $null | Out-Null
        [Runtime.InteropServices.Marshal]::ReleaseComObject($view) | Out-Null
        if ($null -ne $record) { [Runtime.InteropServices.Marshal]::ReleaseComObject($record) | Out-Null }
    }
}

$installer = New-Object -ComObject WindowsInstaller.Installer
$database = Invoke-Member $installer 'OpenDatabase' 'InvokeMethod' @((Resolve-Path -LiteralPath $MsiPath).Path, 1)
try {
    if ((Invoke-Sql $database 'SELECT `Action` FROM `CustomAction` WHERE `Action` = ''WinThunarRejectOlderRepair''').Count) {
        throw 'The MSI already has the maintenance policy.'
    }
    $appId = Invoke-Sql $database 'SELECT `Value` FROM `Property` WHERE `Property` = ''ApplicationFolderName'''
    $cleanupId = Invoke-Sql $database 'SELECT `Value` FROM `Property` WHERE `Property` = ''RustAppId'''
    $version = Invoke-Sql $database 'SELECT `Value` FROM `Property` WHERE `Property` = ''ProductVersion'''
    $appVersion = Invoke-Sql $database 'SELECT `Value` FROM `Property` WHERE `Property` = ''RustAppVersion'''
    $cleanup = Invoke-Sql $database 'SELECT `Target` FROM `CustomAction` WHERE `Action` = ''SetRustCleanupData'''
    $files = Invoke-Sql $database 'SELECT `File`, `Component_`, `FileName`, `Version` FROM `File`'
    $mainFile = @($files | Where-Object { ($_[2] -split '\|')[-1] -eq 'WinThunar.dll' })
    if ($appId.Count -ne 1 -or $appId[0][0] -ne 'WinThunar.App' -or
        $cleanupId.Count -ne 1 -or $cleanupId[0][0] -ne 'WinThunar' -or
        $cleanup.Count -ne 1 -or $cleanup[0][0] -cne '[INSTALLFOLDER]"[RustAppId]"[TempFolder]' -or
        $version.Count -ne 1 -or $appVersion.Count -ne 1 -or -not $appVersion[0][0] -or $mainFile.Count -ne 1) {
        throw 'Unexpected Velopack MSI identity, cleanup action, or application payload; no changes committed.'
    }
    $component = Invoke-Sql $database 'SELECT `ComponentId`, `KeyPath` FROM `Component` WHERE `Component` = ?' @($mainFile[0][1])
    if ($component.Count -ne 1 -or $component[0][1] -ne $mainFile[0][0] -or -not $component[0][0]) {
        throw 'WinThunar.dll must be a registered component key file.'
    }
    if ([version]$mainFile[0][3] -ne [version]$version[0][0]) {
        throw 'MSI ProductVersion must match the WinThunar.dll file version.'
    }
    $actions = Invoke-Sql $database 'SELECT `Action`, `Sequence` FROM `InstallExecuteSequence`'
    $search = @($actions | Where-Object { $_[0] -eq 'AppSearch' })
    $cost = @($actions | Where-Object { $_[0] -eq 'CostFinalize' })
    if ($cost.Count -ne 1 -or $search.Count -gt 1 -or
        ($search.Count -eq 1 -and [int]$search[0][1] -ge [int]$cost[0][1]) -or
        ($search.Count -eq 0 -and @($actions | Where-Object { [int]$_[1] -eq 50 }).Count) -or
        @($actions | Where-Object { [int]$_[1] -eq ([int]$cost[0][1] + 1) }).Count) {
        throw 'Unexpected execute sequence; cannot place the repair guard before the transaction.'
    }

    # The native cleanup action removes LocalAppData\<RustAppId> as well as INSTALLFOLDER.
    # The upstream template incorrectly uses the display title here, deleting our user state.
    Invoke-Sql $database 'UPDATE `Property` SET `Value` = ? WHERE `Property` = ''RustAppId''' @($appId[0][0]) | Out-Null

    # Locate the actual installed DLL through MSI's registered component key path. This works
    # for both scopes and custom install locations, without trusting a caller-supplied path.
    # A minimum version one revision higher excludes this MSI's own payload; subsequent normal
    # releases have a higher build/patch number, so language matching at equality is irrelevant.
    $fileVersion = [version]$mainFile[0][3]
    if ($fileVersion.Revision -lt 0 -or $fileVersion.Revision -ge 65535) {
        throw 'Expected a four-part file version with room for the next revision.'
    }
    $newerVersion = '{0}.{1}.{2}.{3}' -f $fileVersion.Major, $fileVersion.Minor, $fileVersion.Build, ($fileVersion.Revision + 1)
    foreach ($table in @(
        @{ Name = 'Signature'; Sql = 'CREATE TABLE `Signature` (`Signature` CHAR(72) NOT NULL, `FileName` CHAR(255) NOT NULL, `MinVersion` CHAR(20), `MaxVersion` CHAR(20), `MinSize` LONG, `MaxSize` LONG, `MinDate` LONG, `MaxDate` LONG, `Languages` CHAR(255) PRIMARY KEY `Signature`)' },
        @{ Name = 'CompLocator'; Sql = 'CREATE TABLE `CompLocator` (`Signature_` CHAR(72) NOT NULL, `ComponentId` CHAR(38) NOT NULL, `Type` SHORT PRIMARY KEY `Signature_`)' },
        @{ Name = 'RegLocator'; Sql = 'CREATE TABLE `RegLocator` (`Signature_` CHAR(72) NOT NULL, `Root` SHORT NOT NULL, `Key` CHAR(255) NOT NULL, `Name` CHAR(255), `Type` SHORT PRIMARY KEY `Signature_`)' },
        @{ Name = 'AppSearch'; Sql = 'CREATE TABLE `AppSearch` (`Property` CHAR(72) NOT NULL, `Signature_` CHAR(72) NOT NULL PRIMARY KEY `Property`, `Signature_`)' }
    )) {
        if (-not (Invoke-Sql $database 'SELECT `Name` FROM `_Tables` WHERE `Name` = ?' @($table.Name)).Count) {
            Invoke-Sql $database $table.Sql | Out-Null
        }
    }
    Invoke-Sql $database 'INSERT INTO `Signature` (`Signature`, `FileName`, `MinVersion`) VALUES (?, ?, ?)' @('WinThunarNewerPayload', 'WinThunar.dll', $newerVersion) | Out-Null
    Invoke-Sql $database 'INSERT INTO `CompLocator` (`Signature_`, `ComponentId`, `Type`) VALUES (?, ?, ?)' @('WinThunarNewerPayload', $component[0][0], 1) | Out-Null
    Invoke-Sql $database 'INSERT INTO `AppSearch` (`Property`, `Signature_`) VALUES (?, ?)' @('WINTHUNAR_NEWER_PAYLOAD', 'WinThunarNewerPayload') | Out-Null
    # Velopack updates its ARP version even though the original MSI ProductVersion stays put.
    # Check the matching scope as well, so a damaged/missing DLL cannot bypass the guard.
    foreach ($scope in @(@{ Name = 'User'; Root = 1 }, @{ Name = 'Machine'; Root = 2 })) {
        $signature = 'WinThunar' + $scope.Name + 'Version'
        Invoke-Sql $database 'INSERT INTO `RegLocator` (`Signature_`, `Root`, `Key`, `Name`, `Type`) VALUES (?, ?, ?, ?, ?)' @(
            $signature, $scope.Root, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\MSI:WinThunar.App', 'DisplayVersion', 18) | Out-Null
        Invoke-Sql $database 'INSERT INTO `AppSearch` (`Property`, `Signature_`) VALUES (?, ?)' @(
            ('WINTHUNAR_' + $scope.Name.ToUpperInvariant() + '_VERSION'), $signature) | Out-Null
    }
    if ($search.Count -eq 0) {
        Invoke-Sql $database 'INSERT INTO `InstallExecuteSequence` (`Action`, `Sequence`) VALUES (?, ?)' @('AppSearch', 50) | Out-Null
    }
    # A type-19 error stops silent repair as well as maintenance UI repair, before file actions.
    # Keep it out of the UI sequence so maintenance can still offer Remove. Uninstall is allowed.
    Invoke-Sql $database 'INSERT INTO `CustomAction` (`Action`, `Type`, `Target`) VALUES (?, ?, ?)' @(
        'WinThunarRejectOlderRepair', 19,
        'WinThunar has been updated to a newer version. This older installer cannot safely repair it. Download and install the latest WinThunar MSI, or uninstall before reinstalling. Your settings and recovery data are retained by this installer.') | Out-Null
    Invoke-Sql $database 'INSERT INTO `InstallExecuteSequence` (`Action`, `Condition`, `Sequence`) VALUES (?, ?, ?)' @(
        'WinThunarRejectOlderRepair',
        'Installed AND NOT (REMOVE~="ALL") AND (WINTHUNAR_NEWER_PAYLOAD OR ((NOT ALLUSERS=1) AND WINTHUNAR_USER_VERSION AND WINTHUNAR_USER_VERSION <> RustAppVersion) OR (ALLUSERS=1 AND WINTHUNAR_MACHINE_VERSION AND WINTHUNAR_MACHINE_VERSION <> RustAppVersion))',
        ([int]$cost[0][1] + 1)) | Out-Null
    if (-not $MigrationActionsPath) {
        $nativeOutput = Join-Path (Join-Path $PSScriptRoot '..\artifacts\msi-native') ([guid]::NewGuid().ToString('N'))
        & (Join-Path $PSScriptRoot 'Build-MsiMigrationActions.ps1') -OutputDirectory $nativeOutput | Out-Host
        $MigrationActionsPath = Join-Path $nativeOutput 'WinThunar.MigrationState.dll'
    }
    . (Join-Path $PSScriptRoot 'Add-MsiMigrationPolicy.ps1')
    Add-MsiMigrationPolicy $database $installer $MigrationActionsPath
    Invoke-Member $database 'Commit' 'InvokeMethod' $null | Out-Null
}
finally {
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($database) | Out-Null
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($installer) | Out-Null
}
