param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $projectRoot ('artifacts\released-upgrade-lab-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
}
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Choose a new lab directory; existing data is never reused.' }
New-Item -ItemType Directory -Path $output | Out-Null
for ($ancestor = Get-Item -LiteralPath $output; $ancestor; $ancestor = $ancestor.Parent) {
    if ($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Lab output cannot contain a link or volume mount ancestor.' }
}
$runId = [guid]::NewGuid().ToString('N')
$hostMachineGuid = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Cryptography' -Name MachineGuid).MachineGuid
$entries = [Collections.Generic.List[object]]::new()
foreach ($version in @('0.5.5', '0.5.6')) {
    $releasePath = Join-Path $projectRoot "artifacts\verification-$version\published-release.json"
    $release = Get-Content -LiteralPath $releasePath -Raw | ConvertFrom-Json
    if ($release.tag_name -ne "v$version" -or $release.draft -or $release.prerelease) { throw 'Expected recorded stable release metadata.' }
    $names = @('WinThunar.App-win.msi','WinThunar.App-win-Setup.exe',"WinThunar.App-$version-full.nupkg")
    if ($version -eq '0.5.6') { $names += 'WinThunar.App-0.5.6-delta.nupkg' }
    foreach ($name in $names) {
        $asset = @($release.assets | Where-Object name -eq $name)
        if ($asset.Count -ne 1 -or $asset[0].digest -notmatch '^sha256:[a-fA-F0-9]{64}$') { throw "Missing public asset digest: $version/$name" }
        $asset = $asset[0]
        $expectedHash = $asset.digest.Substring(7).ToLowerInvariant()
        $candidates = @()
        if ($version -eq '0.5.5') { $candidates += Join-Path $projectRoot "artifacts\verification-0.5.6\previous-installer-assets\$name" }
        $candidates += Join-Path $projectRoot "artifacts\installer\$name"
        $source = $null
        foreach ($candidate in $candidates) {
            if ((Test-Path -LiteralPath $candidate -PathType Leaf) -and (Get-Item -LiteralPath $candidate).Length -eq $asset.size -and (Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash.ToLowerInvariant() -eq $expectedHash) {
                $source = $candidate
                break
            }
        }
        if (-not $source) { throw "No local file matches the recorded public digest: $version/$name" }
        $relative = if ($name.EndsWith('.nupkg')) { "feed\$name" } else { "installers\$version\$name" }
        $destination = Join-Path $output $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
        Copy-Item -LiteralPath $source -Destination $destination
        $actualHash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualHash -ne $expectedHash) { throw "Copied asset hash mismatch: $relative" }
        $entries.Add([pscustomobject]@{ RelativePath = $relative; Version = $version; Bytes = $asset.size; Sha256 = $expectedHash; PublishedAssetUrl = $asset.browser_download_url })
    }
    Copy-Item -LiteralPath $releasePath -Destination (Join-Path $output "release-$version.json")
}
# Offer exactly the target full/delta entries, preserving the published asset hashes.
$releaseFeed = Get-Content (Join-Path $projectRoot 'artifacts\verification-0.5.6\published-feed.json') -Raw | ConvertFrom-Json
$assets = @($releaseFeed.Assets | Where-Object { $_.Version -eq '0.5.6' -and $_.Type -in @('Full','Delta') })
if ($assets.Count -ne 2) { throw 'Expected the published 0.5.6 full and delta feed entries.' }
@{ Assets = $assets } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'feed\releases.win.json') -Encoding UTF8
foreach ($asset in $assets) {
    if ((Get-FileHash (Join-Path $output ('feed\' + $asset.FileName)) -Algorithm SHA256).Hash -ne $asset.SHA256) { throw 'Feed and asset digests disagree.' }
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ReleasedUpgradeGuest.ps1'),(Join-Path $PSScriptRoot 'Capture-InstalledUpdaterState.ps1') -Destination $output
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\RELEASED-UPGRADE-LAB.md') -Destination (Join-Path $output 'README.md')
$manifest = [ordered]@{
    Schema = 1
    RunId = $runId
    CreatedAt = [DateTimeOffset]::Now.ToString('O')
    HostMachineGuid = $hostMachineGuid
    HostComputerName = $env:COMPUTERNAME
    BaseVersion = '0.5.5'
    TargetVersion = '0.5.6'
    Assets = @($entries.ToArray())
    Status = 'Prepared; released-app guest tests have not run.'
}
$manifest | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $output 'lab.json') -Encoding UTF8
Write-Output "Prepared released-upgrade lab: $output"
Write-Output "Verified $($entries.Count) binary assets against recorded public release digests. No installer or application was launched."
