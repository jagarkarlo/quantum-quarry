param(
    [string]$UnityEditorPath = "$env:ProgramFiles\Unity\Hub\Editor\2022.3.12f1\Editor\Unity.exe",
    [string]$OutputDirectory,
    [switch]$SceneSurvey
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectPath = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path -LiteralPath $UnityEditorPath -PathType Leaf)) {
    throw "Unity Editor not found: $UnityEditorPath"
}
if (-not $OutputDirectory) {
    $run = 'run-' + [guid]::NewGuid().ToString('N')
    $OutputDirectory = Join-Path $projectPath "Logs\RuntimeValidation\$run"
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$reportPath = Join-Path $OutputDirectory 'report.json'
if (Test-Path -LiteralPath $reportPath) {
    throw "Choose a new output directory; a report already exists at $reportPath"
}
$buildLog = Join-Path $OutputDirectory 'build.log'
$build = Start-Process -FilePath $UnityEditorPath -ArgumentList @(
    '-batchmode', '-quit', '-projectPath', "`"$projectPath`"",
    '-executeMethod', 'QuarryValidationBuild.BuildWindows', '-logFile', "`"$buildLog`""
) -PassThru -Wait
if ($build.ExitCode -ne 0) {
    throw "Isolated validation build failed with exit code $($build.ExitCode). See $buildLog"
}
if (-not (Test-Path -LiteralPath $buildLog) -or
    -not (Select-String -LiteralPath $buildLog -SimpleMatch 'Isolated Quarry validation player built successfully.' -Quiet)) {
    throw "Validation build success message is missing. See $buildLog"
}

$playerPath = Join-Path $projectPath 'Builds\Validation\QuantumQuarryValidation.exe'
$playerLog = Join-Path $OutputDirectory 'player.log'
$playerArguments = @(
    '-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720',
    '-quarryValidationOutput', "`"$OutputDirectory`"", '-logFile', "`"$playerLog`""
)
if ($SceneSurvey) { $playerArguments += '-quarrySceneSurvey' }
$player = Start-Process -FilePath $playerPath -ArgumentList $playerArguments -PassThru
if (-not $player.WaitForExit(180000)) {
    Stop-Process -Id $player.Id
    throw "Runtime validation timed out. See $playerLog"
}
if ($player.ExitCode -ne 0) {
    throw "Runtime validation failed with exit code $($player.ExitCode). See $playerLog and $reportPath"
}
if (-not (Test-Path -LiteralPath $reportPath)) {
    throw "The validation player exited without a report. See $playerLog"
}
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
if (-not $report.passed -or $report.errors.Count -ne 0 -or $report.checks.Count -eq 0) {
    throw "Runtime validation did not pass. See $reportPath"
}
$expectedMode = if ($SceneSurvey) { 'scene-survey' } else { 'acceptance' }
if ($report.mode -ne $expectedMode) {
    throw "Expected $expectedMode, but the report contains $($report.mode)."
}
foreach ($screenshot in $report.screenshots) {
    if (-not (Test-Path -LiteralPath $screenshot -PathType Leaf)) {
        throw "Reported screenshot is missing: $screenshot"
    }
}
Write-Output "$expectedMode passed: $($report.checks.Count) checks, $($report.screenshots.Count) screenshots."
Write-Output "Report: $reportPath"
