# Dot-sourced by Set-MsiMaintenancePolicy.ps1 within its uncommitted MSI transaction.
function Add-MsiMigrationPolicy($database, $installer, [string]$actionsPath) {
    $sequence = Invoke-Sql $database 'SELECT `Action`, `Sequence` FROM `InstallExecuteSequence`'
    $required = @{InstallInitialize=1500; RemoveExistingProducts=1401; ProcessComponents=1600; InstallFinalize=6600}
    foreach ($name in $required.Keys) {
        $row = @($sequence | Where-Object { $_[0] -eq $name })
        if ($row.Count -ne 1 -or [int]$row[0][1] -ne $required[$name]) {
            throw "Unexpected MSI $name sequence; migration policy not committed."
        }
    }
    if (@($sequence | Where-Object { $_[0] -in @('InstallExecute','InstallExecuteAgain') -or
        ([int]$_[1] -ge 1490 -and [int]$_[1] -le 1510 -and $_[0] -ne 'InstallInitialize') -or [int]$_[1] -eq 6590 }).Count) {
        throw 'Unexpected MSI transaction actions; migration policy not committed.'
    }
    $upgrade = Invoke-Sql $database 'SELECT `UpgradeCode`, `ActionProperty` FROM `Upgrade`'
    if (-not @($upgrade | Where-Object { $_[0] -eq '{31804CE9-95B4-5685-89F0-20B2157CAD52}' -and $_[1] -eq 'WIX_UPGRADE_DETECTED' }).Count) {
        throw 'Unexpected MSI upgrade identity; migration policy not committed.'
    }
    $view = Invoke-Member $database 'OpenView' 'InvokeMethod' @('INSERT INTO `Binary` (`Name`, `Data`) VALUES (?, ?)')
    $record = Invoke-Member $installer 'CreateRecord' 'InvokeMethod' @(2)
    try {
        Invoke-Member $record 'StringData' 'SetProperty' @(1, 'WinThunarMigrationActions') | Out-Null
        Invoke-Member $record 'SetStream' 'InvokeMethod' @(2, (Resolve-Path -LiteralPath $actionsPath).Path) | Out-Null
        Invoke-Member $view 'Execute' 'InvokeMethod' @($record) | Out-Null
    } finally {
        Invoke-Member $view 'Close' 'InvokeMethod' $null | Out-Null
        [Runtime.InteropServices.Marshal]::ReleaseComObject($view) | Out-Null
        [Runtime.InteropServices.Marshal]::ReleaseComObject($record) | Out-Null
    }
    # Deferred actions intentionally impersonate the installing user: they touch only that
    # user's known LocalAppData. No caller-supplied path is used with SYSTEM privileges.
    $actions = @(
        @('WinThunarPrepareMigration', 1, 'PrepareMigration', 'NOT Installed AND WIX_UPGRADE_DETECTED', 1490),
        @('WinThunarRollbackState', 1281, 'RestoreState', 'WinThunarMigrationNeeded', 1501),
        @('WinThunarProtectState', 1025, 'ProtectState', 'WinThunarMigrationNeeded', 1502),
        @('WinThunarRestoreState', 1025, 'RestoreState', 'WinThunarMigrationNeeded', 6590)
    )
    foreach ($action in $actions) {
        Invoke-Sql $database 'INSERT INTO `CustomAction` (`Action`, `Type`, `Source`, `Target`) VALUES (?, ?, ?, ?)' @(
            $action[0], [int]$action[1], 'WinThunarMigrationActions', $action[2]) | Out-Null
        Invoke-Sql $database 'INSERT INTO `InstallExecuteSequence` (`Action`, `Condition`, `Sequence`) VALUES (?, ?, ?)' @(
            $action[0], $action[3], [int]$action[4]) | Out-Null
    }
    # Flush only state protection, leaving the MSI transaction open. Remove the old app
    # before any new payload is installed (legacy cleanup deletes the install directory).
    # MSI supports RemoveExistingProducts between InstallExecute and InstallFinalize.
    Invoke-Sql $database 'INSERT INTO `InstallExecuteSequence` (`Action`, `Condition`, `Sequence`) VALUES (?, ?, ?)' @(
        'InstallExecute', 'NOT Installed AND WIX_UPGRADE_DETECTED', 1503) | Out-Null
    Invoke-Sql $database 'UPDATE `InstallExecuteSequence` SET `Sequence` = ? WHERE `Action` = ?' @(1504, 'RemoveExistingProducts') | Out-Null
}
