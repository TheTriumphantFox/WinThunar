param(
    [string]$OutputDirectory,
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$BaseVersion = '0.5.5',
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$TargetVersion = '0.5.6',
    [string[]]$AssetDirectories = @(),
    [switch]$MsiOnly
)
$ErrorActionPreference = 'Stop'
if ([version]$BaseVersion -ge [version]$TargetVersion) { throw 'TargetVersion must be newer than BaseVersion.' }
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
function Get-PublicEvidencePath([string]$version, [string]$name) {
    foreach ($relative in @("artifacts\verification-$version\$name", "artifacts\verification-$version\ship\$name")) {
        $path = Join-Path $projectRoot $relative
        if (Test-Path -LiteralPath $path -PathType Leaf) { return $path }
    }
    throw "Missing recorded public release evidence: $version/$name"
}
$preservedAssetDirectories = @(Get-ChildItem (Join-Path $projectRoot 'artifacts') -Directory -Filter 'verification-*' | ForEach-Object {
    $path = Join-Path $_.FullName 'previous-installer-assets'
    if (Test-Path -LiteralPath $path -PathType Container) { $path }
})
$additionalCandidates = @(foreach ($directory in @($AssetDirectories) + $preservedAssetDirectories) {
    Get-ChildItem -LiteralPath $directory -File -Recurse | Where-Object Extension -in @('.msi','.exe','.nupkg')
})
$releaseFeed = Get-Content (Get-PublicEvidencePath $TargetVersion 'published-feed.json') -Raw | ConvertFrom-Json
$assets = @($releaseFeed.Assets | Where-Object {
    ($_.Version -eq $TargetVersion -and $_.Type -eq 'Full') -or
    ($_.Type -eq 'Delta' -and [version]$_.Version -gt [version]$BaseVersion -and [version]$_.Version -le [version]$TargetVersion)
})
if (@($assets | Where-Object { $_.Version -eq $TargetVersion -and $_.Type -eq 'Delta' }).Count -ne 1 -or
    @($assets | Where-Object Type -eq 'Full').Count -ne 1) { throw 'Expected the published target full and delta feed entries.' }
$versions = @(@($BaseVersion, $TargetVersion) + @($assets.Version) | Select-Object -Unique)
foreach ($version in $versions) {
    $releasePath = Get-PublicEvidencePath $version 'published-release.json'
    $release = Get-Content -LiteralPath $releasePath -Raw | ConvertFrom-Json
    if ($release.tag_name -ne "v$version" -or $release.draft -or $release.prerelease) { throw 'Expected recorded stable release metadata.' }
    $names = @($assets | Where-Object Version -eq $version | ForEach-Object FileName)
    if ($version -in @($BaseVersion, $TargetVersion)) {
        $names += 'WinThunar.App-win.msi'
        if (-not $MsiOnly) { $names += 'WinThunar.App-win-Setup.exe' }
    }
    if ($version -eq $BaseVersion) { $names += "WinThunar.App-$version-full.nupkg" }
    foreach ($name in $names) {
        $asset = @($release.assets | Where-Object name -eq $name)
        if ($asset.Count -ne 1 -or $asset[0].digest -notmatch '^sha256:[a-fA-F0-9]{64}$') { throw "Missing public asset digest: $version/$name" }
        $asset = $asset[0]
        $expectedHash = $asset.digest.Substring(7).ToLowerInvariant()
        $candidates = @()
        $candidates += Join-Path $projectRoot "artifacts\installer\$name"
        if ($name.EndsWith('.msi')) { $candidates += Join-Path $projectRoot "artifacts\WinThunar-$version-win-x64.msi" }
        $candidates += @($additionalCandidates | Where-Object Length -eq $asset.size | ForEach-Object FullName)
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
# Retain intermediate deltas when the base and target are more than one release apart.
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
    BaseVersion = $BaseVersion
    TargetVersion = $TargetVersion
    IncludesSetup = -not $MsiOnly
    Assets = @($entries.ToArray())
    Status = 'Prepared; released-app guest tests have not run.'
}
$manifest | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $output 'lab.json') -Encoding UTF8
Write-Output "Prepared released-upgrade lab: $output"
Write-Output "Verified $($entries.Count) binary assets against recorded public release digests. No installer or application was launched."
