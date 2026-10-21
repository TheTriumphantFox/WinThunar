param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $projectRoot ('artifacts\search-performance-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8)) }
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Choose a new performance output directory.' }
& dotnet build (Join-Path $projectRoot 'tests\WinThunar.FileOps.Stress\WinThunar.FileOps.Stress.csproj') --configuration Release
if ($LASTEXITCODE -ne 0) { throw 'Search performance harness build failed.' }
& (Join-Path $projectRoot 'tests\WinThunar.FileOps.Stress\bin\Release\net10.0-windows\WinThunar.FileOps.Stress.exe') --search-performance ([IO.Path]::GetFullPath($OutputDirectory))
if ($LASTEXITCODE -ne 0) { throw "Search performance checks failed. Review $OutputDirectory\report.json; failed fixtures are preserved." }
