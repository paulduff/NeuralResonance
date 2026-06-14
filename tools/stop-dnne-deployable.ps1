param(
    [string]$DeployablePath = '.',
    [switch]$WhatIf
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-FullPath {
    param([string]$Path)
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path (Get-Location).Path $Path))
}

$deployableRoot = Resolve-FullPath $DeployablePath
$pidPath = Join-Path $deployableRoot 'run\pids.json'
if (-not (Test-Path $pidPath -PathType Leaf)) {
    Write-Host ("No PID file found: {0}" -f $pidPath)
    return
}

$entries = @(Get-Content -LiteralPath $pidPath -Raw | ConvertFrom-Json)
foreach ($entry in $entries) {
    if ($null -eq $entry.Pid) {
        continue
    }

    $pidValue = [int]$entry.Pid
    $process = Get-Process -Id $pidValue -ErrorAction SilentlyContinue
    if ($null -eq $process) {
        Write-Host ("already stopped {0} pid={1}" -f $entry.Name, $pidValue)
        continue
    }

    if ($WhatIf) {
        Write-Host ("WHATIF stop {0} pid={1}" -f $entry.Name, $pidValue)
        continue
    }

    Write-Host ("stopping {0} pid={1}" -f $entry.Name, $pidValue)
    Stop-Process -Id $pidValue -Force -ErrorAction SilentlyContinue
}

if (-not $WhatIf) {
    Remove-Item -LiteralPath $pidPath -Force -ErrorAction SilentlyContinue
}
