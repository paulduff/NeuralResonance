param(
    [int]$ControlHttpPort = 5080,
    [int]$ControlHttpsPort = 5081,
    [int]$RightHemisphereOffset = 1000,
    [switch]$ExcludeCommandLineProcesses,
    [switch]$WhatIf
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $PSCommandPath
$killScript = Join-Path -Path $scriptDir -ChildPath "kill-dnne-services.ps1"
$appSettingsPath = Join-Path -Path (Split-Path -Parent $scriptDir) -ChildPath "ControlProgram\appsettings.json"

if (-not (Test-Path -Path $killScript -PathType Leaf)) {
    throw "Required script not found: $killScript"
}

$invokeArgs = @{
    ControlHttpPort      = [Math]::Max(1, $ControlHttpPort)
    ControlHttpsPort     = [Math]::Max(1, $ControlHttpsPort)
    RightHemisphereOffset = [Math]::Max(1, $RightHemisphereOffset)
}

if (Test-Path -Path $appSettingsPath -PathType Leaf) {
    $invokeArgs.AppSettingsPath = $appSettingsPath
}

if (-not $ExcludeCommandLineProcesses) {
    $invokeArgs.IncludeCommandLineProcesses = $true
}

if ($WhatIf) {
    $invokeArgs.WhatIf = $true
}

$argPreview = @()
foreach ($entry in $invokeArgs.GetEnumerator() | Sort-Object Key) {
    $argPreview += "{0}={1}" -f $entry.Key, $entry.Value
}

Write-Host "DNNE shutdown wrapper"
Write-Host ("  kill script: {0}" -f $killScript)
Write-Host ("  args: {0}" -f ($argPreview -join ", "))

& $killScript @invokeArgs
