<#
.SYNOPSIS
    Adds "Create a desktop shortcut" and "Create a Start menu shortcut" checkboxes to a Velopack MSI.

.DESCRIPTION
    Velopack's MSI template always installs both shortcuts and offers no option to change that. This
    edits the generated MSI: the checkboxes go on the install-scope page ("Just me" / "Everyone"),
    and each shortcut component installs only when its property is set. Both are checked by default.
    Silent installs can opt out with DESKTOPSHORTCUT="" or STARTMENUSHORTCUT="".
#>
param(
    [Parameter(Mandatory)]
    [string]$MsiPath
)

$ErrorActionPreference = 'Stop'

$shortcuts = @(
    @{ Property = 'DESKTOPSHORTCUT'; Component = 'ApplicationDesktopShortcut'; Control = 'DesktopShortcutCheckBox'; Text = 'Create a &desktop shortcut'; Y = 186 },
    @{ Property = 'STARTMENUSHORTCUT'; Component = 'ApplicationStartMenuRootShortcut'; Control = 'StartMenuShortcutCheckBox'; Text = 'Create a &Start menu shortcut'; Y = 206 }
)
$dialog = 'InstallScopeDlg'
# The control the checkboxes follow in tab order, and the one they lead to.
$previousControl = 'BothScopes'

function Invoke-Member($target, [string]$name, [System.Reflection.BindingFlags]$kind, [object[]]$arguments) {
    $target.GetType().InvokeMember($name, $kind, $null, $target, $arguments)
}

function Invoke-Sql($database, [string]$sql, [object[]]$values = @()) {
    $view = Invoke-Member $database 'OpenView' 'InvokeMethod' @($sql)
    try {
        $record = $null
        if ($values.Count -gt 0) {
            $record = Invoke-Member $installer 'CreateRecord' 'InvokeMethod' @($values.Count)
            for ($index = 0; $index -lt $values.Count; $index++) {
                $property = if ($values[$index] -is [int]) { 'IntegerData' } else { 'StringData' }
                Invoke-Member $record $property 'SetProperty' @(($index + 1), $values[$index]) | Out-Null
            }
        }
        Invoke-Member $view 'Execute' 'InvokeMethod' @($record) | Out-Null
        $rows = @()
        if (-not $sql.StartsWith('SELECT')) {
            return , $rows
        }
        while ($row = Invoke-Member $view 'Fetch' 'InvokeMethod' $null) {
            $count = Invoke-Member $row 'FieldCount' 'GetProperty' $null
            $rows += , @(for ($field = 1; $field -le $count; $field++) { Invoke-Member $row 'StringData' 'GetProperty' @($field) })
        }
        return , $rows
    }
    finally {
        Invoke-Member $view 'Close' 'InvokeMethod' $null | Out-Null
    }
}

$installer = New-Object -ComObject WindowsInstaller.Installer
$database = Invoke-Member $installer 'OpenDatabase' 'InvokeMethod' @((Resolve-Path -LiteralPath $MsiPath).Path, 1)
try {
    if ((Invoke-Sql $database 'SELECT `Control` FROM `Control` WHERE `Dialog_` = ? AND `Control` = ?' @($dialog, $shortcuts[0].Control)).Count -gt 0) {
        throw "The MSI already has shortcut choices: $MsiPath"
    }

    $anchor = Invoke-Sql $database 'SELECT `Control_Next` FROM `Control` WHERE `Dialog_` = ? AND `Control` = ?' @($dialog, $previousControl)
    if ($anchor.Count -ne 1) {
        throw "The Velopack MSI template changed: $dialog has no $previousControl control."
    }
    $nextControl = $anchor[0][0]

    $secure = Invoke-Sql $database 'SELECT `Value` FROM `Property` WHERE `Property` = ''SecureCustomProperties'''
    $secureValue = if ($secure.Count -eq 1) { $secure[0][0] } else { '' }

    for ($index = 0; $index -lt $shortcuts.Count; $index++) {
        $shortcut = $shortcuts[$index]
        if ((Invoke-Sql $database 'SELECT `Component` FROM `Component` WHERE `Component` = ?' @($shortcut.Component)).Count -ne 1) {
            throw "The Velopack MSI template changed: there is no $($shortcut.Component) component."
        }

        # Installed only when chosen. Without the Transitive attribute this is evaluated once, at first
        # install, so a repair never brings back a shortcut the user declined.
        Invoke-Sql $database 'UPDATE `Component` SET `Condition` = ? WHERE `Component` = ?' @($shortcut.Property, $shortcut.Component) | Out-Null
        Invoke-Sql $database 'INSERT INTO `Property` (`Property`, `Value`) VALUES (?, ?)' @($shortcut.Property, '1') | Out-Null
        Invoke-Sql $database 'INSERT INTO `CheckBox` (`Property`, `Value`) VALUES (?, ?)' @($shortcut.Property, '1') | Out-Null

        $following = if ($index -lt $shortcuts.Count - 1) { $shortcuts[$index + 1].Control } else { $nextControl }
        Invoke-Sql $database 'INSERT INTO `Control` (`Dialog_`, `Control`, `Type`, `X`, `Y`, `Width`, `Height`, `Attributes`, `Property`, `Text`, `Control_Next`) VALUES (?, ?, ''CheckBox'', 20, ?, 330, 17, 3, ?, ?, ?)' @(
            $dialog, $shortcut.Control, [int]$shortcut.Y, $shortcut.Property, $shortcut.Text, $following) | Out-Null

        # A per-machine install runs elevated, and only secure properties reach that process.
        $secureValue = (@($secureValue -split ';' | Where-Object { $_ }) + $shortcut.Property) -join ';'
    }

    Invoke-Sql $database 'UPDATE `Control` SET `Control_Next` = ? WHERE `Dialog_` = ? AND `Control` = ?' @($shortcuts[0].Control, $dialog, $previousControl) | Out-Null
    if ($secure.Count -eq 1) {
        Invoke-Sql $database 'UPDATE `Property` SET `Value` = ? WHERE `Property` = ''SecureCustomProperties''' @($secureValue) | Out-Null
    }
    else {
        Invoke-Sql $database 'INSERT INTO `Property` (`Property`, `Value`) VALUES (''SecureCustomProperties'', ?)' @($secureValue) | Out-Null
    }

    Invoke-Member $database 'Commit' 'InvokeMethod' $null | Out-Null
}
finally {
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($database) | Out-Null
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($installer) | Out-Null
}
