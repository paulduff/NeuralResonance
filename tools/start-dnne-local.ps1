param(
    [switch]$NoBuild = $true,
    [switch]$NoCleanStart,
    [switch]$NoEditor,
    [switch]$SkipBurnInGate = $true,
    [int]$StartupTimeoutSec = 180,
    [int]$AllowableNonOkServices = 1,
    [int]$StartupSoftNonOkAllowance = 2,
    [int]$StartupSoftMinTick = 40,
    [int]$StartupSoftMaxSnapshotAgeTicks = 20,
    [switch]$NoAutoRestart,
    [switch]$NoStartEditorOnTimeout,
    [string]$ControlBaseUrl = "http://localhost:5080",
    [switch]$WhatIf
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $PSCommandPath
$runScript = Join-Path -Path $scriptDir -ChildPath "run-dnne-stack.ps1"

if (-not (Test-Path -Path $runScript -PathType Leaf)) {
    throw "Required script not found: $runScript"
}

$invokeArgs = @{
    StartupTimeoutSec         = [Math]::Max(30, $StartupTimeoutSec)
    AllowableNonOkServices    = [Math]::Max(0, $AllowableNonOkServices)
    StartupSoftNonOkAllowance = [Math]::Max(0, $StartupSoftNonOkAllowance)
    StartupSoftMinTick        = [Math]::Max(1, $StartupSoftMinTick)
    StartupSoftMaxSnapshotAgeTicks = [Math]::Max(1, $StartupSoftMaxSnapshotAgeTicks)
    ControlBaseUrl            = $ControlBaseUrl
}

if (-not $NoCleanStart) {
    $invokeArgs.CleanStart = $true
}

if ($NoBuild) {
    $invokeArgs.NoBuild = $true
}

if ($NoEditor) {
    $invokeArgs.NoEditor = $true
}

if ($SkipBurnInGate) {
    $invokeArgs.SkipBurnInGate = $true
}

if (-not $NoAutoRestart) {
    $invokeArgs.AutoRestartNonOk = $true
}

if (-not $NoStartEditorOnTimeout) {
    $invokeArgs.StartEditorOnTimeout = $true
}

$argPreview = @()
foreach ($entry in $invokeArgs.GetEnumerator() | Sort-Object Key) {
    $argPreview += "{0}={1}" -f $entry.Key, $entry.Value
}

Write-Host "DNNE startup wrapper"
Write-Host ("  run script: {0}" -f $runScript)
Write-Host ("  args: {0}" -f ($argPreview -join ", "))

if ($WhatIf) {
    Write-Host "WhatIf set: not starting processes."
    return
}

& $runScript @invokeArgs
