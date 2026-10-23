<# Build the x64, statically linked native MSI custom actions; no target-machine .NET dependency. #>
param([Parameter(Mandatory)][string]$OutputDirectory, [switch]$Test)
$ErrorActionPreference = 'Stop'
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$installation = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $installation) { throw 'MSI migration actions require the Visual C++ x64 build tools.' }
$tools = Join-Path $installation 'Common7\Tools\VsDevCmd.bat'
$source = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\installer\MigrationState.cpp'))
$dll = Join-Path $output 'WinThunar.MigrationState.dll'
$testBuild = ''
if ($Test) {
    $testSource = Join-Path (Split-Path -Parent $source) 'MigrationStateTests.cpp'
    $testBuild = @"
if errorlevel 1 exit /b %errorlevel%
cl.exe /nologo /std:c++17 /EHsc /W4 /WX /O2 /MT /DUNICODE /D_UNICODE "$testSource" /Fo"$output\MigrationStateTests.obj" /Fe"$output\MigrationStateTests.exe" /link /INCREMENTAL:NO /DYNAMICBASE /NXCOMPAT msi.lib shell32.lib ole32.lib
"@
}
# This batch file contains only quoted, resolved build paths; it performs no file deletion.
$batch = Join-Path $output 'build.cmd'
@"
@echo off
call "$tools" -no_logo -arch=x64 -host_arch=x64
if errorlevel 1 exit /b %errorlevel%
cl.exe /nologo /std:c++17 /EHsc /W4 /WX /O2 /MT /DUNICODE /D_UNICODE /LD "$source" /Fo"$output\MigrationState.obj" /Fe"$dll" /link /INCREMENTAL:NO /DYNAMICBASE /NXCOMPAT msi.lib shell32.lib ole32.lib
$testBuild
"@ | Set-Content -LiteralPath $batch -Encoding ascii
& $env:ComSpec /d /c $batch
if ($LASTEXITCODE -ne 0) { throw "Native migration custom-action build failed: $LASTEXITCODE" }
if ($Test) {
    & (Join-Path $output 'MigrationStateTests.exe') (Join-Path $output ('fixture-' + [guid]::NewGuid().ToString('N')))
    if ($LASTEXITCODE -ne 0) { throw "Native migration state tests failed: $LASTEXITCODE" }
}
$dll
