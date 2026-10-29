param(
    [Parameter()]
    [ValidatePattern('^\d+\.\d+(?:\.\d+)?(?:[-+][0-9A-Za-z.-]+)?$')]
    [string]$Version = '0.6.1',

    [Parameter()]
    [switch]$PackageExistingPublish,

    [Parameter()]
    [switch]$SkipInstaller,

    # signtool.exe "sign" options, for example a certificate thumbprint or Azure Artifact
    # Signing's /dlib and /dmd options, with /fd SHA256 and a /tr timestamp server.
    # Velopack signs the binaries and Setup.exe; this script re-signs the MSI after editing it.
    [Parameter()]
    [string]$SignParams
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts'))
$releaseName = "WinThunar-$Version-win-x64"
$publishDirectory = [System.IO.Path]::GetFullPath((Join-Path $artifactsRoot $releaseName))
$zipPath = [System.IO.Path]::GetFullPath((Join-Path $artifactsRoot "$releaseName.zip"))
$checksumPath = "$zipPath.sha256"

# The installer's package id is deliberately not "WinThunar": a per-user install goes to
# %LOCALAPPDATA%\<packId> and uninstall deletes that folder, which would take the user's
# session, plugins, and transfer-recovery records in %LOCALAPPDATA%\WinThunar with it.
$installerPackId = 'WinThunar.App'
# Every release's packages stay here so later versions can be built as delta updates.
$installerReleasesDirectory = Join-Path $artifactsRoot 'installer'
# The MSI is the installer people run: it asks whether to install for the current user or for
# everyone, and (after Set-MsiShortcutChoices.ps1) which shortcuts to create. Velopack's Setup.exe
# can do neither; it stays in the releases directory for scripted per-user installs.
$msiPath = Join-Path $artifactsRoot "$releaseName.msi"
$msiChecksumPath = "$msiPath.sha256"

if (-not $publishDirectory.StartsWith($artifactsRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'The publish directory escaped the workspace artifacts directory.'
}

if (-not $SkipInstaller) {
    if ($Version -notmatch '^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$') {
        throw "The installer needs a three-part version such as 1.2.3; '$Version' is not one. Pass -SkipInstaller for a ZIP-only build."
    }
    if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) {
        throw 'The Velopack CLI (vpk) is not installed. Run: dotnet tool install -g vpk --version 1.2.161, or pass -SkipInstaller.'
    }
}

$signTool = $null
if ($SignParams) {
    if ($SkipInstaller) {
        throw 'Signing applies to the installer packages; -SignParams cannot be combined with -SkipInstaller.'
    }
    $signTool = Get-ChildItem -Path "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue |
        Sort-Object { [version]$_.Directory.Parent.Name } -Descending |
        Select-Object -First 1 -ExpandProperty FullName
    if (-not $signTool) {
        throw 'signtool.exe was not found in the Windows SDK. Install the Windows SDK signing tools or omit -SignParams.'
    }
}

$plannedOutputs = @($zipPath, $checksumPath)
if (-not $SkipInstaller) {
    $plannedOutputs += @($msiPath, $msiChecksumPath)
}

foreach ($outputPath in $plannedOutputs) {
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

$msiHash = $null
if (-not $SkipInstaller) {
    $signArguments = if ($SignParams) { @('--signParams', $SignParams) } else { @() }
    & vpk pack `
        --packId $installerPackId `
        --runtime win-x64 `
        --msi `
        --instLocation Either `
        --packVersion $Version `
        --packDir $publishDirectory `
        --mainExe 'WinThunar.exe' `
        --packTitle 'WinThunar' `
        --packAuthors 'TheTriumphantFox' `
        --icon (Join-Path $projectRoot 'Assets\AppIcon.ico') `
        --outputDir $installerReleasesDirectory `
        @signArguments

    if ($LASTEXITCODE -ne 0) {
        throw "vpk pack failed with exit code $LASTEXITCODE."
    }

    $builtMsi = Join-Path $installerReleasesDirectory "$installerPackId-win.msi"
    if (-not (Test-Path -LiteralPath $builtMsi)) {
        throw "vpk pack did not produce $builtMsi."
    }

    # Edited in place so the MSI that `vpk upload` publishes offers the same choices.
    & (Join-Path $PSScriptRoot 'Set-MsiShortcutChoices.ps1') -MsiPath $builtMsi
    & (Join-Path $PSScriptRoot 'Set-MsiMaintenancePolicy.ps1') -MsiPath $builtMsi
    if ($signTool) {
        # The edits above invalidate any signature applied during packing.
        $signing = Start-Process -FilePath $signTool -ArgumentList "sign $SignParams `"$builtMsi`"" -NoNewWindow -Wait -PassThru
        if ($signing.ExitCode -ne 0) {
            throw "signtool could not sign $builtMsi (exit code $($signing.ExitCode))."
        }
        & $signTool verify /pa $builtMsi
        if ($LASTEXITCODE -ne 0) {
            throw "The signed MSI did not verify: $builtMsi"
        }
    }
    Copy-Item -LiteralPath $builtMsi -Destination $msiPath
    $msiHash = (Get-FileHash -LiteralPath $msiPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -LiteralPath $msiChecksumPath -Value "$msiHash  $releaseName.msi" -Encoding ascii
}

[pscustomobject]@{
    Executable = $executable
    Archive = $zipPath
    Checksum = $checksumPath
    Sha256 = $hash
    Installer = if ($SkipInstaller) { $null } else { $msiPath }
    InstallerSha256 = $msiHash
    Signed = [bool]$signTool
    PluginCount = $pluginCount
}
