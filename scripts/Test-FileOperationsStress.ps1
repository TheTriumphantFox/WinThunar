param(
    [string]$OutputDirectory,
    [string]$SecondVolumeRoot,
    [switch]$BasicSecondVolume,
    [switch]$KeepFixtures
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $projectRoot ('artifacts\fileops-stress-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
}
$runRoot = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $runRoot) { throw "Choose a new stress-test output directory: $runRoot" }
$project = Join-Path $projectRoot 'tests\WinThunar.FileOps.Stress\WinThunar.FileOps.Stress.csproj'
& dotnet build $project --configuration Release
if ($LASTEXITCODE -ne 0) { throw 'File-operation stress harness build failed.' }
$harness = Join-Path $projectRoot 'tests\WinThunar.FileOps.Stress\bin\Release\net10.0-windows\WinThunar.FileOps.Stress.exe'
$harnessArguments = @('--run', $runRoot)
if ($SecondVolumeRoot) { $harnessArguments += @('--second-volume', [IO.Path]::GetFullPath($SecondVolumeRoot)) }
if ($BasicSecondVolume) { $harnessArguments += '--basic-second-volume' }
if ($KeepFixtures) { $harnessArguments += '--keep-fixtures' }
& $harness @harnessArguments
if ($LASTEXITCODE -ne 0) { throw "File-operation stress checks failed. Review $runRoot\report.json and retained failing fixtures." }
Write-Output "File-operation stress checks passed. Report: $runRoot\report.json"
