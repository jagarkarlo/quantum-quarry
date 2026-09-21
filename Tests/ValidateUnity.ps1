param(
    [string]$UnityEditorPath = "$env:ProgramFiles\Unity\Hub\Editor\2022.3.12f1\Editor\Unity.exe",
    [switch]$GeneratePressurePrefabs,
    [switch]$BuildWindowsPlayer
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectPath = Split-Path -Parent $PSScriptRoot
$logDirectory = Join-Path $projectPath 'Logs\Validation'
if (-not (Test-Path -LiteralPath $UnityEditorPath -PathType Leaf)) {
    throw "Unity Editor not found: $UnityEditorPath"
}
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null

function Invoke-UnityCheck {
    param(
        [string]$Name,
        [string[]]$ExtraArguments,
        [string]$SuccessMarker
    )

    $logPath = Join-Path $logDirectory "$Name.log"
    $unityArguments = @(
        '-batchmode', '-quit',
        '-projectPath', "`"$projectPath`"",
        '-logFile', "`"$logPath`""
    ) + $ExtraArguments
    if (Test-Path -LiteralPath $logPath) {
        Remove-Item -LiteralPath $logPath
    }
    $process = Start-Process -FilePath $UnityEditorPath -ArgumentList $unityArguments -PassThru -Wait
    if ($process.ExitCode -ne 0) {
        throw "Unity $Name failed with exit code $($process.ExitCode). See $logPath"
    }
    if (-not (Test-Path -LiteralPath $logPath -PathType Leaf)) {
        throw "Unity $Name did not create its log: $logPath"
    }
    if (-not (Select-String -LiteralPath $logPath -SimpleMatch $SuccessMarker -Quiet)) {
        throw "Unity $Name exited without its success message. See $logPath"
    }
    Write-Output "$Name passed. Log: $logPath"
}

if ($GeneratePressurePrefabs) {
    Invoke-UnityCheck -Name 'pressure-generation' `
        -ExtraArguments @('-executeMethod', 'QuarryPressureAuthoring.CreatePrefabs') `
        -SuccessMarker 'Pressure prefabs ready.'
}

Invoke-UnityCheck -Name 'pressure-prefabs' `
    -ExtraArguments @('-executeMethod', 'QuantumQuarryProjectValidator.ValidatePressurePrefabsBatch') `
    -SuccessMarker 'Quarry Pressure custom prefab validation passed.'
Invoke-UnityCheck -Name 'project-validation' `
    -ExtraArguments @('-executeMethod', 'QuantumQuarryProjectValidator.ValidateBatch') `
    -SuccessMarker 'QuantumQuarry validation passed.'

if ($BuildWindowsPlayer) {
    $buildPath = Join-Path $projectPath 'Builds\Windows\QuantumQuarry.exe'
    Invoke-UnityCheck -Name 'windows-build' `
        -ExtraArguments @('-buildWindows64Player', "`"$buildPath`"") `
        -SuccessMarker 'Build Finished, Result: Success.'
    if (-not (Test-Path -LiteralPath $buildPath -PathType Leaf)) {
        throw "Unity reported a successful build but the executable is missing: $buildPath"
    }
    Write-Output "Windows player: $buildPath"
}
