param(
    [int]$ControlHttpPort = 5080,
    [int]$ControlHttpsPort = 5081,
    [int]$EditorHttpPort = 5090,
    [int]$RightHemisphereOffset = 1000,
    [int]$GracefulTimeoutSec = 180,
    [string]$CheckpointPath = '',
    [switch]$ExcludeCommandLineProcesses,
    [switch]$WhatIf
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-LocalDnneGuiProcesses {
    param([string]$WorkspaceRoot)

    $escapedRoot = [Regex]::Escape($WorkspaceRoot)
    return Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
        Where-Object {
            $name = [string]$_.Name
            $commandLine = [string]$_.CommandLine
            (($name -like 'NRE.WpfWorldSim*') -or
             ($name -like 'NRE.WpfMazeSim*') -or
             ($name -like 'NRE.WpfEditor*')) -and
            $commandLine -match $escapedRoot
        } |
        Sort-Object ProcessId -Unique
}

function Get-LocalDnneRuntimeProcesses {
    param([string]$WorkspaceRoot)

    $escapedRoot = [Regex]::Escape($WorkspaceRoot)
    return Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
        Where-Object {
            $name = [string]$_.Name
            $commandLine = [string]$_.CommandLine
            ($name -eq 'dotnet.exe' -or
             $name -like 'NeuralResonanceEngine.*' -or
             $name -like 'NRE.Blazor*' -or
             $name -like 'NRE.Wpf*') -and
            $commandLine -match $escapedRoot
        } |
        Sort-Object ProcessId -Unique
}

function Request-GuiClose {
    param(
        [object[]]$Processes,
        [int]$TimeoutSec
    )

    foreach ($entry in $Processes) {
        $process = Get-Process -Id ([int]$entry.ProcessId) -ErrorAction SilentlyContinue
        if ($null -eq $process) {
            continue
        }

        Write-Host ("Closing {0} pid={1}..." -f $entry.Name, $entry.ProcessId)
        [void]$process.CloseMainWindow()
    }

    $deadline = [DateTime]::UtcNow.AddSeconds([Math]::Max(1, $TimeoutSec))
    while ([DateTime]::UtcNow -lt $deadline) {
        $remaining = @($Processes | Where-Object {
            $null -ne (Get-Process -Id ([int]$_.ProcessId) -ErrorAction SilentlyContinue)
        })
        if ($remaining.Count -eq 0) {
            return
        }

        Start-Sleep -Milliseconds 250
    }
}

function New-ControlHeaders {
    $headers = @{}
    $secret = [string]$env:NRE_CONTROL_SHARED_SECRET
    if (-not [string]::IsNullOrWhiteSpace($secret)) {
        $headers['X-NRE-Auth'] = $secret
    }

    return $headers
}

$scriptDir = Split-Path -Parent $PSCommandPath
$workspaceRoot = (Resolve-Path (Join-Path $scriptDir '..')).Path
$killScript = Join-Path $scriptDir 'kill-dnne-services.ps1'
$appSettingsPath = Join-Path $workspaceRoot 'ControlProgram\appsettings.json'
$controlBaseUrl = "http://localhost:$([Math]::Max(1, $ControlHttpPort))"
$editorBaseUrl = "http://localhost:$([Math]::Max(1, $EditorHttpPort))"

if (-not (Test-Path -LiteralPath $killScript -PathType Leaf)) {
    throw "Required script not found: $killScript"
}

if ([string]::IsNullOrWhiteSpace($CheckpointPath)) {
    $CheckpointPath = Join-Path `
        ([Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)) `
        'NeuralResonanceEngine\checkpoints\last-graceful-network-state.json'
}
$CheckpointPath = [System.IO.Path]::GetFullPath($CheckpointPath)

Write-Host 'DNNE graceful shutdown'
Write-Host ("  control: {0}" -f $controlBaseUrl)
Write-Host ("  checkpoint: {0}" -f $CheckpointPath)

if (-not $WhatIf) {
    $guiProcesses = @(Get-LocalDnneGuiProcesses -WorkspaceRoot $workspaceRoot)
    Request-GuiClose -Processes $guiProcesses -TimeoutSec 8

    $headers = New-ControlHeaders
    try {
        $worldStop = Invoke-RestMethod `
            -Method Post `
            -Uri "$editorBaseUrl/editor/api/admin/shutdown" `
            -TimeoutSec 30
        if ($worldStop.reportPath) {
            Write-Host ("World report persisted: {0}" -f $worldStop.reportPath)
        }
        Write-Host 'Blazor world accepted the graceful shutdown request.'
    }
    catch {
        Write-Warning ("Blazor world graceful shutdown request failed: {0}" -f $_.Exception.Message)
    }

    try {
        Invoke-RestMethod `
            -Method Post `
            -Uri "$controlBaseUrl/api/v1/admin/quiesce" `
            -Headers $headers `
            -TimeoutSec 120 | Out-Null
        Write-Host 'Brain tick coordinator quiesced.'
    }
    catch {
        Write-Warning ("Brain quiescence request failed: {0}" -f $_.Exception.Message)
    }

    try {
        $checkpointDirectory = Split-Path -Parent $CheckpointPath
        New-Item -ItemType Directory -Force -Path $checkpointDirectory | Out-Null
        $temporaryCheckpoint = "$CheckpointPath.$PID.tmp"
        Invoke-WebRequest `
            -Method Get `
            -Uri "$controlBaseUrl/api/v1/admin/network/export" `
            -Headers $headers `
            -OutFile $temporaryCheckpoint `
            -TimeoutSec 120 | Out-Null

        if (-not (Test-Path -LiteralPath $temporaryCheckpoint -PathType Leaf) -or
            (Get-Item -LiteralPath $temporaryCheckpoint).Length -lt 64) {
            throw 'Control returned an empty network checkpoint.'
        }

        Move-Item -LiteralPath $temporaryCheckpoint -Destination $CheckpointPath -Force
        Write-Host ("Live network checkpoint exported: {0}" -f $CheckpointPath)
    }
    catch {
        Write-Warning ("Live network checkpoint export failed: {0}" -f $_.Exception.Message)
    }

    try {
        Invoke-RestMethod `
            -Method Post `
            -Uri "$controlBaseUrl/api/v1/admin/shutdown" `
            -Headers $headers `
            -TimeoutSec 10 | Out-Null
        Write-Host 'Control accepted the graceful shutdown request.'
    }
    catch {
        Write-Warning ("Graceful Control shutdown request failed: {0}" -f $_.Exception.Message)
    }

    $deadline = [DateTime]::UtcNow.AddSeconds([Math]::Max(10, $GracefulTimeoutSec))
    do {
        $runtimeProcesses = @(Get-LocalDnneRuntimeProcesses -WorkspaceRoot $workspaceRoot)
        if ($runtimeProcesses.Count -eq 0) {
            break
        }

        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)

    if ($runtimeProcesses.Count -gt 0) {
        Write-Warning ("{0} DNNE process(es) exceeded the graceful shutdown timeout; force fallback will inspect them." -f $runtimeProcesses.Count)
    }
}
else {
    Write-Host 'WhatIf: would stop the Blazor world, quiesce the brain, export one coherent checkpoint, and request graceful Control shutdown.'
}

$invokeArgs = @{
    ControlHttpPort = [Math]::Max(1, $ControlHttpPort)
    ControlHttpsPort = [Math]::Max(1, $ControlHttpsPort)
    RightHemisphereOffset = [Math]::Max(1, $RightHemisphereOffset)
}
if (Test-Path -LiteralPath $appSettingsPath -PathType Leaf) {
    $invokeArgs.AppSettingsPath = $appSettingsPath
}
if (-not $ExcludeCommandLineProcesses) {
    $invokeArgs.IncludeCommandLineProcesses = $true
}
if ($WhatIf) {
    $invokeArgs.WhatIf = $true
}

Write-Host 'Checking for remaining DNNE processes...'
& $killScript @invokeArgs
