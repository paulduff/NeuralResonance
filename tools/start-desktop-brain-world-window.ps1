param(
    [ValidateRange(60, 86400)][int]$DurationSec = 1800,
    [string]$TrainingStatusPath = '',
    [string]$BackupRoot = 'D:\DNNE-desktop-runs',
    [switch]$WithEntity,
    [switch]$WhatIf,
    [switch]$InWindow,
    [string]$LaunchTaskName = ''
)

. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
if ($WhatIf) {
    Write-Host 'Plan only: would open an independent visible PowerShell window for the guarded desktop run.'
    return
}

if (-not $InWindow) {
    # ShellExecute inherits Codex's process job. The Windows task service instead
    # creates an interactive process that can outlive the originating tool call.
    $taskName = 'DNNE-Desktop-' + [Guid]::NewGuid().ToString('N').Substring(0, 12)
    $powershell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $arguments = @('-NoLogo', '-NoProfile', '-NoExit', '-ExecutionPolicy', 'Bypass', '-File',
        $PSCommandPath, '-InWindow', '-LaunchTaskName', $taskName,
        '-DurationSec', $DurationSec, '-BackupRoot', $BackupRoot)
    if (-not [string]::IsNullOrWhiteSpace($TrainingStatusPath)) { $arguments += @('-TrainingStatusPath', $TrainingStatusPath) }
    if ($WithEntity) { $arguments += '-WithEntity' }
    $argumentText = ($arguments | ForEach-Object { ConvertTo-ProcessArgument $_ }) -join ' '
    $action = New-ScheduledTaskAction -Execute $powershell -Argument $argumentText -WorkingDirectory (Get-DnneRepoRoot -ScriptPath $PSCommandPath)
    $principal = New-ScheduledTaskPrincipal -UserId ([Security.Principal.WindowsIdentity]::GetCurrent().Name) -LogonType Interactive -RunLevel Limited
    $settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
    Register-ScheduledTask -TaskName $taskName -Action $action -Principal $principal -Settings $settings | Out-Null
    Start-ScheduledTask -TaskName $taskName
    Write-Host "Independent visible PowerShell window requested: $taskName"
    return
}

try {
    $invokeArguments = @{ DurationSec = $DurationSec; BackupRoot = $BackupRoot; NoPause = $true }
    if (-not [string]::IsNullOrWhiteSpace($TrainingStatusPath)) { $invokeArguments.TrainingStatusPath = $TrainingStatusPath }
    if ($WithEntity) { $invokeArguments.WithEntity = $true }
    & (Join-Path $PSScriptRoot 'run-desktop-brain-world.ps1') @invokeArguments
}
finally {
    if ($LaunchTaskName -match '^DNNE-Desktop-[0-9a-f]{12}$') {
        Unregister-ScheduledTask -TaskName $LaunchTaskName -Confirm:$false -ErrorAction SilentlyContinue
    }
}
