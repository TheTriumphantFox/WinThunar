param(
    [Parameter()]
    [ValidatePattern('^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$')]
    [string]$Version = '0.1.12',

    [Parameter()]
    [switch]$PackageExistingPublish
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts'))
$releaseName = "WinThunar-$Version-win-x64"
$publishDirectory = [System.IO.Path]::GetFullPath((Join-Path $artifactsRoot $releaseName))
$zipPath = [System.IO.Path]::GetFullPath((Join-Path $artifactsRoot "$releaseName.zip"))
$checksumPath = "$zipPath.sha256"

if (-not $publishDirectory.StartsWith($artifactsRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'The publish directory escaped the workspace artifacts directory.'
}

foreach ($outputPath in @($zipPath, $checksumPath)) {
    if (Test-Path -LiteralPath $outputPath) {
        throw "Release output already exists: $outputPath. Move or remove that exact prior output before publishing again."
    }
}

if ((Test-Path -LiteralPath $publishDirectory) -and -not $PackageExistingPublish) {
    throw "Release output already exists: $publishDirectory. Move or remove that exact prior output before publishing again."
}

if ($PackageExistingPublish -and -not (Test-Path -LiteralPath $publishDirectory -PathType Container)) {
    throw "The existing publish directory was not found: $publishDirectory"
}

New-Item -ItemType Directory -Path $artifactsRoot -Force | Out-Null

if (-not $PackageExistingPublish) {
    & dotnet publish (Join-Path $projectRoot 'WinThunar.csproj') `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        -p:Platform=x64 `
        -p:Version=$Version `
        -p:WindowsPackageType=None `
        -p:WindowsAppSDKSelfContained=true `
        -p:PublishProfile=win-x64 `
        --output $publishDirectory

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }
}

$executable = Join-Path $publishDirectory 'WinThunar.exe'
if (-not (Test-Path -LiteralPath $executable)) {
    throw "The published release does not contain WinThunar.exe."
}

$pluginDirectory = Join-Path $publishDirectory 'Plugins'
$pluginCount = @(Get-ChildItem -LiteralPath $pluginDirectory -Filter '*.json' -File).Count
if ($pluginCount -lt 4) {
    throw "Expected at least four bundled plugin manifests; found $pluginCount."
}

$archiveCreated = $false
for ($attempt = 1; $attempt -le 5; $attempt++) {
    try {
        [System.IO.Compression.ZipFile]::CreateFromDirectory(
            $publishDirectory,
            $zipPath,
            [System.IO.Compression.CompressionLevel]::Optimal,
            $false)
        $archiveCreated = $true
        break
    }
    catch {
        if (Test-Path -LiteralPath $zipPath) {
            Remove-Item -LiteralPath $zipPath -Force
        }
        if ($attempt -eq 5) {
            throw
        }
        Start-Sleep -Seconds $attempt
    }
}

if (-not $archiveCreated) {
    throw 'The release archive could not be created.'
}
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath $checksumPath -Value "$hash  $releaseName.zip" -Encoding ascii

[pscustomobject]@{
    Executable = $executable
    Archive = $zipPath
    Checksum = $checksumPath
    Sha256 = $hash
    PluginCount = $pluginCount
}
