param(
    # Internal continuation: the public invocation builds before requesting UAC.
    [switch]$ElevatedWorker,
    [string]$RunDirectory
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactsRoot = Join-Path $projectRoot 'artifacts'

function Assert-OrdinaryDirectory([string]$Path) {
    $item = Get-Item -LiteralPath $Path -Force
    if (-not $item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "Expected an ordinary directory: $Path"
    }
}

if (-not $ElevatedWorker) {
    if ($RunDirectory) { throw 'RunDirectory is reserved for the elevated continuation.' }
    & dotnet build (Join-Path $projectRoot 'tests\WinThunar.FileOps.Stress\WinThunar.FileOps.Stress.csproj') --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'File-operation stress harness build failed.' }
    if (-not (Test-Path -LiteralPath $artifactsRoot)) { New-Item -ItemType Directory -Path $artifactsRoot | Out-Null }
    $RunDirectory = Join-Path $artifactsRoot ('fileops-realstorage-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
    $arguments = '-NoProfile -ExecutionPolicy Bypass -File "{0}" -ElevatedWorker -RunDirectory "{1}"' -f $PSCommandPath, $RunDirectory
    Write-Output "Requesting UAC for a new 256 MiB disposable VHD. Results and image: $RunDirectory"
    # UAC is the only interactive step; all test output goes to the retained transcript.
    $worker = Start-Process powershell.exe -Verb RunAs -WindowStyle Hidden -ArgumentList $arguments -PassThru -Wait
    if ($worker.ExitCode -ne 0) { throw "Real-storage checks failed (exit $($worker.ExitCode)). Inspect $RunDirectory; failed fixtures and the VHD are preserved." }
    Write-Output "Real-storage checks passed. Report: $RunDirectory\stress\report.json"
    exit 0
}

$principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Disposable VHD provisioning requires an administrator token. Run the script without -ElevatedWorker to request UAC.'
}
Assert-OrdinaryDirectory $projectRoot
Assert-OrdinaryDirectory $artifactsRoot
$runRoot = [IO.Path]::GetFullPath($RunDirectory)
if ([IO.Path]::GetDirectoryName($runRoot) -ne $artifactsRoot -or [IO.Path]::GetFileName($runRoot) -notmatch '^fileops-realstorage-\d{8}-\d{6}-[a-f0-9]{8}$') {
    throw 'The new run directory must be directly under this checkout artifacts directory.'
}
if (Test-Path -LiteralPath $runRoot) { throw "Refusing to reuse a run directory: $runRoot" }
$hostVolume = Get-Volume -FilePath ($artifactsRoot + '\')
if ($hostVolume.FileSystemType -ne 'NTFS' -or $hostVolume.SizeRemaining -lt 2GB) {
    throw 'Provisioning requires an NTFS host with at least 2 GiB free; no existing volume will be filled.'
}
New-Item -ItemType Directory -Path $runRoot | Out-Null
Start-Transcript -Path (Join-Path $runRoot 'provisioning.log') | Out-Null
$imagePath = Join-Path $runRoot 'disposable.vhd'
$mountPath = Join-Path $runRoot 'volume'
$token = [guid]::NewGuid().ToString('N')
$label = 'WT-' + $token.Substring(0, 12)
$status = [ordered]@{ Status = 'Preparing'; ImagePath = $imagePath; MountPath = $mountPath; Token = $token; Detached = $false; Error = $null }
$exitCode = 1
try {
    New-Item -ItemType Directory -Path $mountPath | Out-Null
    Assert-OrdinaryDirectory $runRoot
    # This diskpart script can only create and attach one NEW image. Partitioning/formatting
    # below use CIM objects obtained from that exact image, never a supplied disk number.
    $diskpartScript = Join-Path $runRoot 'create-vhd.txt'
    # diskpart /s requires its native text encoding; a UTF-16 BOM is parsed as an
    # invalid first command on this Windows build. Fail rather than lose path characters.
    $commands = @("create vdisk file=`"$imagePath`" maximum=256 type=expandable", 'attach vdisk', 'exit', '') -join "`r`n"
    $encoding = [Text.Encoding]::Default
    if ($encoding.GetString($encoding.GetBytes($commands)) -cne $commands) { throw 'The image path cannot be represented in the diskpart script encoding.' }
    [IO.File]::WriteAllText($diskpartScript, $commands, $encoding)
    & diskpart.exe /s $diskpartScript | Tee-Object -FilePath (Join-Path $runRoot 'diskpart.log')
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $imagePath)) { throw 'New disposable VHD creation failed.' }

    function Get-OwnedDisk {
        Assert-OrdinaryDirectory $runRoot
        $image = Get-DiskImage -ImagePath $imagePath
        $disks = @($image | Get-Disk)
        if (-not $image.Attached -or $disks.Count -ne 1) { throw 'The new image must identify exactly one attached disk.' }
        $disk = $disks[0]
        if ($disk.IsBoot -or $disk.IsSystem -or $disk.BusType -ne 'File Backed Virtual' -or $disk.Size -ne 256MB) {
            throw 'Refusing to modify a disk that is not the new 256 MiB file-backed virtual disk.'
        }
        return $disk
    }

    $disk = Get-OwnedDisk
    if ($disk.PartitionStyle -ne 'RAW' -or @($disk | Get-Partition -ErrorAction SilentlyContinue).Count -ne 0) {
        throw 'The newly created virtual disk is not blank; preserving it without formatting.'
    }
    $disk | Initialize-Disk -PartitionStyle MBR -PassThru | Out-Null
    $disk = Get-OwnedDisk
    $partition = $disk | New-Partition -UseMaximumSize
    $currentDisk = Get-OwnedDisk
    if ($partition.DiskNumber -ne $currentDisk.Number) { throw 'New partition does not belong to the new VHD.' }
    $volume = $partition | Format-Volume -FileSystem NTFS -NewFileSystemLabel $label -Confirm:$false
    if ($volume.FileSystemType -ne 'NTFS' -or $volume.FileSystemLabel -ne $label -or $volume.Size -gt 256MB) {
        throw 'Disposable volume format verification failed.'
    }
    $partition | Add-PartitionAccessPath -AccessPath ($mountPath + '\')
    Set-Content -LiteralPath (Join-Path $mountPath 'disposable-owner.txt') -Value $token -NoNewline -Encoding Ascii
    # The script and harness are deliberately explicit about volume identity, not drive letters.
    $manifest = [ordered]@{ MountPath = $mountPath; VolumePath = $volume.Path; ImagePath = $imagePath; Token = $token; Label = $label }
    $manifestPath = Join-Path $runRoot 'volume.json'
    $manifest | ConvertTo-Json | Set-Content -LiteralPath $manifestPath -Encoding Ascii
    $status.Status = 'Testing'
    $status | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runRoot 'provisioning.json') -Encoding Ascii
    $harness = Join-Path $projectRoot 'tests\WinThunar.FileOps.Stress\bin\Release\net10.0-windows\WinThunar.FileOps.Stress.exe'
    & $harness --run (Join-Path $runRoot 'stress') --disposable-volume-manifest $manifestPath
    if ($LASTEXITCODE -ne 0) { throw 'Real-storage harness failed. Keep the image and journals for inspection.' }
    $status.Status = 'Passed'
    $exitCode = 0
}
catch {
    $status.Status = 'Failed'
    $status.Error = $_.Exception.ToString()
    Write-Output $status.Error
}
finally {
    # Preserve ALL images, including failed/ambiguous recovery data. Detach only our exact file.
    # Never recursively delete the mount directory: it can still be an attached filesystem.
    try {
        if (Test-Path -LiteralPath $imagePath) {
            $image = Get-DiskImage -ImagePath $imagePath
            if ($image.Attached) { Dismount-DiskImage -ImagePath $imagePath }
            $status.Detached = -not (Get-DiskImage -ImagePath $imagePath).Attached
            if (-not $status.Detached) { throw 'The disposable VHD is still attached.' }
        }
    }
    catch { $status.Status = 'Failed'; $status.Error = "$($status.Error)`nDetach failed: $($_.Exception.Message)"; $exitCode = 1 }
    $status | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runRoot 'provisioning.json') -Encoding Ascii
    Stop-Transcript | Out-Null
}
exit $exitCode
