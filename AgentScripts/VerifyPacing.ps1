param([int]$TimeoutSeconds = 110)
$ErrorActionPreference = 'Stop'
$projectPath = Split-Path $PSScriptRoot -Parent
$executable = Join-Path $projectPath 'Builds\Skybreach\Skybreach.exe'
$reportPath = Join-Path $projectPath 'Logs\PacingStandalone.json'
$logPath = Join-Path $projectPath 'Logs\PacingStandalone.log'
if (!(Test-Path -LiteralPath $executable)) { throw 'Build the Windows player first.' }
New-Item -ItemType Directory -Force -Path (Join-Path $projectPath 'Logs') | Out-Null
if (Test-Path -LiteralPath $reportPath) { Remove-Item -LiteralPath $reportPath }
$playerArgs = '-batchmode -nographics -noaudio -logFile "' + $logPath + '" --pacing-probe "' + $reportPath + '"'
$process = Start-Process -FilePath $executable -ArgumentList $playerArgs -WindowStyle Hidden -PassThru
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
while (!$process.HasExited -and (Get-Date) -lt $deadline) { $process.WaitForExit(1000) | Out-Null }
if (!$process.HasExited) { Stop-Process -Id $process.Id -ErrorAction SilentlyContinue; throw 'Pacing observation timed out.' }
$process.Refresh()
$report = if (Test-Path -LiteralPath $reportPath) { Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json } else { $null }
$result = [ordered]@{ checkedAt=(Get-Date).ToString('o'); exitCode=$process.ExitCode; report=$report }
$result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $projectPath 'Logs\PacingProcess.json') -Encoding UTF8
if ($process.ExitCode -ne 0 -or $null -eq $report -or !$report.passed) { throw 'Pacing observation failed; inspect this project report.' }
[pscustomobject]@{ passed=$report.passed; checks=$report.checks.Count; scenarios=$report.scenarios.Count; secondsPerScenario=$report.simulationSecondsPerCase }
