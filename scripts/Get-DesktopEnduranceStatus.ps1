param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$runRoot = [IO.Path]::GetFullPath($OutputDirectory)
$path = Join-Path $runRoot 'results\endurance-progress.json'
# Atomic checkpoint replacement needs readers to allow deletion/renaming of the old file.
$stream = [IO.File]::Open($path, [IO.FileMode]::Open, [IO.FileAccess]::Read,
    [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete)
try {
    $reader = [IO.StreamReader]::new($stream)
    try { $progress = $reader.ReadToEnd() | ConvertFrom-Json }
    finally { $reader.Dispose() }
}
finally { $stream.Dispose() }
$testProcess = Get-Process -Id $progress.processId -ErrorAction SilentlyContinue
$resultPath = Join-Path $runRoot 'results\endurance-result.json'
[ordered]@{
    processId = $progress.processId
    processVisibleToCaller = $null -ne $testProcess
    requestedSeconds = $progress.requestedSeconds
    completedSearches = $progress.completedSearches
    previews = $progress.previews
    latestSample = $progress.latestSample
    resultPresent = Test-Path -LiteralPath $resultPath
    processOutcomePresent = Test-Path -LiteralPath (Join-Path $runRoot 'desktop-process.json')
} | ConvertTo-Json -Depth 5
