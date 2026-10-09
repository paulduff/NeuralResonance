param(
    [ValidateRange(60, 86400)][int]$DurationSec = 1800,
    [string]$TrainingStatusPath = '',
    [string]$BackupRoot = 'D:\DNNE-desktop-runs',
    [switch]$WithEntity,
    [switch]$WhatIf
)

. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
$arguments = @('-NoProfile', '-NoExit', '-ExecutionPolicy', 'Bypass', '-File',
    (Join-Path $PSScriptRoot 'run-desktop-brain-world.ps1'), '-DurationSec', $DurationSec,
    '-BackupRoot', $BackupRoot)
if (-not [string]::IsNullOrWhiteSpace($TrainingStatusPath)) { $arguments += @('-TrainingStatusPath', $TrainingStatusPath) }
if ($WithEntity) { $arguments += '-WithEntity' }
if ($WhatIf) {
    Write-Host 'Plan only: would open an independent visible PowerShell window for the guarded desktop run.'
    return
}
$argumentText = ($arguments | ForEach-Object { ConvertTo-ProcessArgument $_ }) -join ' '
Start-Process -FilePath 'powershell.exe' -ArgumentList $argumentText -WindowStyle Normal | Out-Null
