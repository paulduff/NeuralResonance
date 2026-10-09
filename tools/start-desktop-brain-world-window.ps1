param(
    [ValidateRange(60, 86400)][int]$DurationSec = 1800,
    [string]$TrainingStatusPath = '',
    [string]$BackupRoot = 'D:\DNNE-desktop-runs',
    [switch]$WithEntity,
    [switch]$UseRunningStack,
    [switch]$WhatIf,
    [switch]$InWindow,
    [string]$LaunchTaskName = '',
    [string]$GitExecutable = '',
    [string]$DotnetExecutable = ''
)

. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
if ($WhatIf -and -not $InWindow) {
    Write-Host 'Plan only: would open an independent visible PowerShell window for the guarded desktop run.'
    return
}

if (-not $InWindow) {
    if ([string]::IsNullOrWhiteSpace($GitExecutable)) { $GitExecutable = (Get-Command git -CommandType Application -ErrorAction Stop).Source }
    if ([string]::IsNullOrWhiteSpace($DotnetExecutable)) { $DotnetExecutable = (Get-Command dotnet -CommandType Application -ErrorAction Stop).Source }
    # ShellExecute inherits Codex's process job. The Windows task service instead
    # creates an interactive process that can outlive the originating tool call.
    $taskName = 'DNNE-Desktop-' + [Guid]::NewGuid().ToString('N').Substring(0, 12)
    $powershell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $arguments = @('-NoLogo', '-NoProfile', '-NoExit', '-ExecutionPolicy', 'Bypass', '-File',
        $PSCommandPath, '-InWindow', '-LaunchTaskName', $taskName,
        '-DurationSec', $DurationSec, '-BackupRoot', $BackupRoot,
        '-GitExecutable', $GitExecutable, '-DotnetExecutable', $DotnetExecutable)
    if (-not [string]::IsNullOrWhiteSpace($TrainingStatusPath)) { $arguments += @('-TrainingStatusPath', $TrainingStatusPath) }
    if ($WithEntity) { $arguments += '-WithEntity' }
    if ($UseRunningStack) { $arguments += '-UseRunningStack' }
    $argumentText = ($arguments | ForEach-Object { ConvertTo-ProcessArgument $_ }) -join ' '
    $action = New-ScheduledTaskAction -Execute $powershell -Argument $argumentText -WorkingDirectory (Get-DnneRepoRoot -ScriptPath $PSCommandPath)
    $principal = New-ScheduledTaskPrincipal -UserId ([Security.Principal.WindowsIdentity]::GetCurrent().Name) -LogonType Interactive -RunLevel Limited
    $settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
    Register-ScheduledTask -TaskName $taskName -Action $action -Principal $principal -Settings $settings | Out-Null
    Start-ScheduledTask -TaskName $taskName
    Write-Host "Independent visible PowerShell window requested: $taskName"
    return
}

$previousToolPath = $env:PATH
try {
    $toolDirectories = @()
    foreach ($tool in @($GitExecutable, $DotnetExecutable)) {
        if ([string]::IsNullOrWhiteSpace($tool) -or -not [IO.Path]::IsPathRooted($tool) -or
            -not (Test-Path -LiteralPath $tool -PathType Leaf)) {
            throw 'The desktop window requires existing absolute Git and .NET executable paths from its launcher.'
        }
        $toolDirectories += Split-Path -Parent $tool
    }
    # Task Scheduler does not inherit Codex's bundled Git PATH. This affects
    # only this child window and its DNNE processes, never the machine PATH.
    $env:PATH = ($toolDirectories + @($previousToolPath)) -join ';'
    if ($WhatIf) {
        Get-Command git,dotnet -CommandType Application | Select-Object Name,Source
        Write-Host 'Window tool paths checked. No builds or services started.'
        return
    }
    $invokeArguments = @{ DurationSec = $DurationSec; BackupRoot = $BackupRoot; NoPause = $true }
    if (-not [string]::IsNullOrWhiteSpace($TrainingStatusPath)) { $invokeArguments.TrainingStatusPath = $TrainingStatusPath }
    if ($WithEntity) { $invokeArguments.WithEntity = $true }
    if ($UseRunningStack) { $invokeArguments.UseRunningStack = $true }
    & (Join-Path $PSScriptRoot 'run-desktop-brain-world.ps1') @invokeArguments
}
finally {
    $env:PATH = $previousToolPath
    if ($LaunchTaskName -match '^DNNE-Desktop-[0-9a-f]{12}$') {
        Unregister-ScheduledTask -TaskName $LaunchTaskName -Confirm:$false -ErrorAction SilentlyContinue
    }
}
