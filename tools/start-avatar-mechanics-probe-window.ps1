param(
    [switch]$InWindow,
    [string]$LaunchTaskName = '',
    [string]$DotnetExecutable = '',
    [string]$Revision = 'unrecorded',
    [string]$BackupRoot = 'D:\DNNE-desktop-runs'
)

. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = Get-DnneRepoRoot -ScriptPath $PSCommandPath
if (-not $InWindow) {
    if ([string]::IsNullOrWhiteSpace($DotnetExecutable)) {
        $DotnetExecutable = (Get-Command dotnet -CommandType Application -ErrorAction Stop).Source
    }
    if (-not [IO.Path]::IsPathRooted($DotnetExecutable) -or
        -not (Test-Path -LiteralPath $DotnetExecutable -PathType Leaf)) {
        throw 'An existing absolute .NET executable path is required.'
    }
    $taskName = 'DNNE-Avatar-Probe-' + [Guid]::NewGuid().ToString('N').Substring(0, 12)
    $powershell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $arguments = @('-NoLogo', '-NoProfile', '-NoExit', '-ExecutionPolicy', 'Bypass', '-File',
        $PSCommandPath, '-InWindow', '-LaunchTaskName', $taskName,
        '-DotnetExecutable', $DotnetExecutable, '-BackupRoot', $BackupRoot, '-Revision', $Revision)
    $argumentText = ($arguments | ForEach-Object { ConvertTo-ProcessArgument $_ }) -join ' '
    $action = New-ScheduledTaskAction -Execute $powershell -Argument $argumentText -WorkingDirectory $repoRoot
    $principal = New-ScheduledTaskPrincipal -UserId ([Security.Principal.WindowsIdentity]::GetCurrent().Name) -LogonType Interactive -RunLevel Limited
    $settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
    Register-ScheduledTask -TaskName $taskName -Action $action -Principal $principal -Settings $settings | Out-Null
    Start-ScheduledTask -TaskName $taskName
    Write-Host "Independent visible PowerShell diagnostic requested: $taskName"
    return
}

$runName = 'avatar-mechanics-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 6)
$output = Join-Path $repoRoot ('artifacts\' + $runName)
New-Item -ItemType Directory -Path $output | Out-Null
$exitCode = 1
$transcribing = $false
try {
    Start-Transcript -Path (Join-Path $output 'powershell.log') | Out-Null
    $transcribing = $true
    $Host.UI.RawUI.WindowTitle = 'Avatar mechanics - offline floor comparison'
    Write-Host 'Offline physical comparison: no DNNE connection and no learning.' -ForegroundColor Cyan
    Write-Host "Results: $output"
    Write-Host "Source revision: $Revision"
    Write-Host 'Build uses isolated outputs; the running brain and editor are not rebuilt.'
    $buildRoot = Join-Path $output 'build'
    & $DotnetExecutable build (Join-Path $repoRoot 'tools\AvatarMechanicsProbe\AvatarMechanicsProbe.csproj') `
        --configuration Release --artifacts-path $buildRoot --verbosity minimal `
        -m:1 /p:BuildInParallel=false /p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw 'Mechanics probe build failed.' }
    $application = Join-Path $buildRoot 'bin\AvatarMechanicsProbe\release\AvatarMechanicsProbe.dll'
    & $DotnetExecutable $application (Join-Path $output 'results.json')
    if ($LASTEXITCODE -ne 0) { throw 'Mechanics comparison failed to complete.' }
    $report = Get-Content -LiteralPath (Join-Path $output 'results.json') -Raw | ConvertFrom-Json
    if (@($report.Results).Count -ne 6) { throw 'Expected six completed comparisons.' }
    Write-Host 'FINISHED means the comparisons completed. Each Qualified field separately assesses gait.' -ForegroundColor Yellow
    $report.Results | Format-Table PhysicalFloor,StepSeconds,Qualified,LeftSwing,RightSwing,DistanceMeters,FallingSamples -AutoSize
    $exitCode = 0
}
catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    $_ | Out-String | Set-Content -LiteralPath (Join-Path $output 'failure.txt') -Encoding UTF8
}
finally {
    $status = [ordered]@{ Status = $(if ($exitCode -eq 0) { 'FINISHED' } else { 'FAILED' });
        ExitCode = $exitCode; CompletedUtc = [DateTime]::UtcNow.ToString('o'); Output = $output; Revision = $Revision }
    $status | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'window-status.json') -Encoding UTF8
    if ($transcribing) { Stop-Transcript | Out-Null }
    try {
        if (-not (Test-Path -LiteralPath $BackupRoot -PathType Container)) {
            New-Item -ItemType Directory -Path $BackupRoot -Force | Out-Null
        }
        $backup = Join-Path $BackupRoot $runName
        New-Item -ItemType Directory -Path $backup | Out-Null
        foreach ($name in @('results.json', 'powershell.log', 'failure.txt', 'window-status.json')) {
            $source = Join-Path $output $name
            if (Test-Path -LiteralPath $source -PathType Leaf) {
                $target = Join-Path $backup $name
                Copy-Item -LiteralPath $source -Destination $target
                if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $target).Hash) {
                    throw "Backup hash mismatch: $name"
                }
            }
        }
        Write-Host "Verified backup: $backup"
    }
    catch {
        $exitCode = 1
        $status.Status = 'FAILED'
        $status.ExitCode = 1
        $status['BackupError'] = $_.Exception.Message
        $status | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'window-status.json') -Encoding UTF8
        Write-Host "Backup failed: $($_.Exception.Message)" -ForegroundColor Red
    }
    if ($LaunchTaskName -match '^DNNE-Avatar-Probe-[0-9a-f]{12}$') {
        Unregister-ScheduledTask -TaskName $LaunchTaskName -Confirm:$false -ErrorAction SilentlyContinue
    }
}
Write-Host "========== $($status.Status) (exit $exitCode) ==========" -ForegroundColor Cyan
Write-Host 'Tell Orion the comparison is finished; results and physical limits are assessed separately.'
