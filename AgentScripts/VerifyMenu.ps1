param([int]$TimeoutSeconds = 30)
$ErrorActionPreference = 'Stop'
$projectPath = Split-Path $PSScriptRoot -Parent
$executable = Join-Path $projectPath 'Builds/Skybreach/Skybreach.exe'
$reportPath = Join-Path $projectPath 'Logs/MenuStandalone.json'
$logPath = Join-Path $projectPath 'Logs/MenuStandalone.log'
if (!(Test-Path -LiteralPath $executable)) { throw 'Build the Windows player first.' }
New-Item -ItemType Directory -Force -Path (Join-Path $projectPath 'Logs') | Out-Null
if (Test-Path -LiteralPath $reportPath) { Remove-Item -LiteralPath $reportPath }
$playerArgs = '-batchmode -nographics -noaudio -logFile "' + $logPath + '" --menu-smoke "' + $reportPath + '"'
$process = Start-Process -FilePath $executable -ArgumentList $playerArgs -WindowStyle Hidden -PassThru
if (!$process.WaitForExit($TimeoutSeconds * 1000)) {
    Stop-Process -Id $process.Id -ErrorAction SilentlyContinue
    throw 'Menu acceptance timed out.'
}
$process.Refresh()
if (!(Test-Path -LiteralPath $reportPath)) { throw "Menu acceptance produced no report (exit $($process.ExitCode))." }
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
if ($process.ExitCode -ne 0 -or !$report.passed) { throw ('Menu acceptance failed: ' + $report.error) }
[pscustomobject]@{ passed=$report.passed; checks=$report.checks.Count; runtimeErrors=$report.runtimeErrors }
