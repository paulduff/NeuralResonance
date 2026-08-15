param(
    [int]$ControlHttpPort = 5080,
    [int]$ControlHttpsPort = 5081,
    [int]$StructurePortStart = 52166,
    [int]$StructurePortEnd = 52322,
    [int]$RightHemisphereOffset = 1000,
    [string]$AppSettingsPath = "",
    [switch]$IncludeCommandLineProcesses = $true,
    [switch]$WhatIf
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-TargetPortsFromRange {
    param(
        [int]$Start,
        [int]$End,
        [int]$Offset,
        [int]$ControlHttp,
        [int]$ControlHttps
    )

    if ($Start -gt $End) {
        throw "StructurePortStart ($Start) must be <= StructurePortEnd ($End)."
    }

    $leftPorts = $Start..$End
    $rightPorts = ($Start + $Offset)..($End + $Offset)
    return @($ControlHttp, $ControlHttps) + $leftPorts + $rightPorts
}

function Resolve-AppSettingsFile {
    param(
        [string]$RequestedPath,
        [string]$WorkspaceRoot
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        if (-not (Test-Path -Path $RequestedPath -PathType Leaf)) {
            throw "AppSettingsPath not found: $RequestedPath"
        }

        return (Resolve-Path -Path $RequestedPath).Path
    }

    $defaultPath = Join-Path -Path $WorkspaceRoot -ChildPath "ControlProgram\appsettings.json"
    if (Test-Path -Path $defaultPath -PathType Leaf) {
        return $defaultPath
    }

    return $null
}

function Add-UniquePort {
    param(
        [System.Collections.Generic.HashSet[int]]$Set,
        [int]$Port
    )

    if ($Port -gt 0 -and $Port -le 65535) {
        [void]$Set.Add($Port)
    }
}

function Get-TargetPortsFromAppSettings {
    param(
        [string]$Path,
        [int]$DefaultControlHttp,
        [int]$DefaultControlHttps,
        [int]$DefaultRightOffset
    )

    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -Path $Path -PathType Leaf)) {
        return @()
    }

    try {
        $json = Get-Content -Path $Path -Raw | ConvertFrom-Json
    }
    catch {
        Write-Warning ("Failed to parse appsettings file '{0}': {1}" -f $Path, $_.Exception.Message)
        return @()
    }

    $ports = [System.Collections.Generic.HashSet[int]]::new()
    Add-UniquePort -Set $ports -Port $DefaultControlHttp
    Add-UniquePort -Set $ports -Port $DefaultControlHttps

    if ($json.SnapshotEndpoint) {
        $snapshotUri = $null
        if ([Uri]::TryCreate([string]$json.SnapshotEndpoint, [UriKind]::Absolute, [ref]$snapshotUri)) {
            Add-UniquePort -Set $ports -Port $snapshotUri.Port
        }
    }

    $rightEnabled = $true
    $rightOffset = $DefaultRightOffset
    if ($json.HemisphereHosting) {
        if ($null -ne $json.HemisphereHosting.Enabled) {
            $rightEnabled = [bool]$json.HemisphereHosting.Enabled
        }

        if ($null -ne $json.HemisphereHosting.RightPortOffset) {
            $rightOffset = [int]$json.HemisphereHosting.RightPortOffset
        }
    }

    if ($json.ServiceRegistry) {
        foreach ($entry in $json.ServiceRegistry.PSObject.Properties) {
            $serviceUri = $null
            if ([Uri]::TryCreate([string]$entry.Value, [UriKind]::Absolute, [ref]$serviceUri)) {
                Add-UniquePort -Set $ports -Port $serviceUri.Port
                if ($rightEnabled) {
                    Add-UniquePort -Set $ports -Port ($serviceUri.Port + $rightOffset)
                }
            }
        }
    }

    return @($ports)
}

function Get-DnneListeners {
    param([int[]]$Ports)

    if (-not $Ports -or $Ports.Count -eq 0) {
        return @()
    }

    return Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue |
        Where-Object { $Ports -contains $_.LocalPort } |
        Sort-Object LocalPort, OwningProcess -Unique
}

function Get-ExtraDnneProcesses {
    param([string]$WorkspaceRoot)

    $escapedRoot = [Regex]::Escape($WorkspaceRoot)
    $dnneHintPattern = "(NeuralResonanceEngine|NeuralResonanceEngine\.DNN|ControlProgram|Structures\\|NRE\.BlazorEditor|NRE\.WpfEditor|NRE\.WpfMazeSim|NRE\.WpfWorldSim|start-blazor-editor|start-maze-sim|start-world-sim)"

    return Get-CimInstance -ClassName Win32_Process -ErrorAction SilentlyContinue |
        Where-Object {
            $name = [string]$_.Name
            $cmd = [string]$_.CommandLine
            $exe = [string]$_.ExecutablePath

            $isDotnet = $name -ieq "dotnet.exe"
            $isDnneExe = $name -like "NeuralResonanceEngine.*" -or $name -like "NRE.BlazorEditor*" -or $name -like "NRE.WpfEditor*" -or $name -like "NRE.WpfMazeSim*" -or $name -like "NRE.WpfWorldSim*"
            if (-not $isDotnet -and -not $isDnneExe) {
                return $false
            }

            $inWorkspace = ($cmd -match $escapedRoot) -or ($exe -match $escapedRoot)
            if (-not $inWorkspace) {
                return $false
            }

            if ($isDnneExe) {
                return $true
            }

            return $cmd -match $dnneHintPattern
        } |
        Sort-Object ProcessId -Unique
}

function Add-PidReason {
    param(
        [hashtable]$Map,
        [int]$ProcessId,
        [string]$Reason
    )

    if ($ProcessId -le 0 -or $ProcessId -eq $PID) {
        return
    }

    if (-not $Map.ContainsKey($ProcessId)) {
        $Map[$ProcessId] = New-Object System.Collections.Generic.HashSet[string]
    }

    [void]$Map[$ProcessId].Add($Reason)
}

function Stop-PidWithFallback {
    param([int]$ProcessId)

    if ($null -eq (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue)) {
        Write-Host ("PID {0} already exited." -f $ProcessId)
        return
    }

    try {
        Stop-Process -Id $ProcessId -Force -ErrorAction Stop
        Write-Host ("Stopped PID {0}" -f $ProcessId)
        return
    }
    catch {
        if ($null -eq (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue)) {
            Write-Host ("PID {0} already exited." -f $ProcessId)
            return
        }

        Write-Warning ("Failed PID {0}: {1}" -f $ProcessId, $_.Exception.Message)
    }

    try {
        $taskkillOutput = & taskkill /PID $ProcessId /T /F 2>&1
        if ($LASTEXITCODE -eq 0 -or $null -eq (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue)) {
            Write-Host ("Fallback taskkill cleared PID {0}" -f $ProcessId)
            return
        }

        Write-Warning ("Fallback taskkill failed for PID {0}: {1}" -f $ProcessId, ($taskkillOutput -join ' '))
    }
    catch {
        if ($null -eq (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue)) {
            Write-Host ("PID {0} already exited." -f $ProcessId)
            return
        }

        Write-Warning ("Fallback taskkill failed for PID {0}: {1}" -f $ProcessId, $_.Exception.Message)
    }
}

$scriptDir = Split-Path -Parent $PSCommandPath
$workspaceRoot = (Resolve-Path -Path (Join-Path -Path $scriptDir -ChildPath "..")).Path
$resolvedAppSettings = Resolve-AppSettingsFile -RequestedPath $AppSettingsPath -WorkspaceRoot $workspaceRoot

$rangePorts = Get-TargetPortsFromRange `
    -Start $StructurePortStart `
    -End $StructurePortEnd `
    -Offset $RightHemisphereOffset `
    -ControlHttp $ControlHttpPort `
    -ControlHttps $ControlHttpsPort

$configuredPorts = Get-TargetPortsFromAppSettings `
    -Path $resolvedAppSettings `
    -DefaultControlHttp $ControlHttpPort `
    -DefaultControlHttps $ControlHttpsPort `
    -DefaultRightOffset $RightHemisphereOffset

$targetPorts = @($rangePorts + $configuredPorts | Sort-Object -Unique)
$pidReasons = @{}

$listeners = @(Get-DnneListeners -Ports $targetPorts)
foreach ($listener in $listeners) {
    Add-PidReason -Map $pidReasons -ProcessId $listener.OwningProcess -Reason ("LISTEN:{0}" -f $listener.LocalPort)
}

$extraProcesses = @()
if ($IncludeCommandLineProcesses) {
    $extraProcesses = @(Get-ExtraDnneProcesses -WorkspaceRoot $workspaceRoot)
    foreach ($proc in $extraProcesses) {
        Add-PidReason -Map $pidReasons -ProcessId ([int]$proc.ProcessId) -Reason "CMDLINE_MATCH"
    }
}

if ($pidReasons.Count -eq 0) {
    Write-Host "No DNNE listeners or extra DNNE processes found."
    return
}

Write-Host "Found DNNE listeners:"
if (@($listeners).Count -gt 0) {
    $listeners |
        Select-Object LocalAddress, LocalPort, OwningProcess |
        Format-Table -AutoSize
}
else {
    Write-Host "  (none)"
}

Write-Host ""
Write-Host "Targeting DNNE processes:"
$pidReasons.GetEnumerator() |
    Sort-Object Name |
    ForEach-Object {
        $proc = Get-Process -Id $_.Key -ErrorAction SilentlyContinue
        $procName = if ($null -ne $proc) { $proc.ProcessName } else { "unknown/exited" }
        Write-Host ("  PID {0} [{1}] <- {2}" -f $_.Key, $procName, ([string]::Join(", ", $_.Value)))
    }

$processIds = @($pidReasons.Keys | Sort-Object)
if ($WhatIf) {
    Write-Host ""
    Write-Host ("WhatIf: would stop PIDs: {0}" -f ($processIds -join ", "))
    return
}

Write-Host ""
Write-Host ("Stopping PIDs: {0}" -f ($processIds -join ", "))
foreach ($procId in $processIds) {
    Stop-PidWithFallback -ProcessId $procId
}

$remaining = @()
$remainingExtra = @()
for ($pass = 1; $pass -le 4; $pass++) {
    Start-Sleep -Milliseconds 700
    $remaining = @(Get-DnneListeners -Ports $targetPorts)
    $remainingExtra = @()
    if ($IncludeCommandLineProcesses) {
        $remainingExtra = @(Get-ExtraDnneProcesses -WorkspaceRoot $workspaceRoot | Where-Object { $_.ProcessId -ne $PID })
    }

    if (@($remaining).Count -eq 0 -and @($remainingExtra).Count -eq 0) {
        break
    }

    $leftoverPids = [System.Collections.Generic.HashSet[int]]::new()
    foreach ($listener in $remaining) {
        if ($listener.OwningProcess -gt 0 -and $listener.OwningProcess -ne $PID) {
            [void]$leftoverPids.Add([int]$listener.OwningProcess)
        }
    }
    foreach ($proc in $remainingExtra) {
        if ($proc.ProcessId -gt 0 -and $proc.ProcessId -ne $PID) {
            [void]$leftoverPids.Add([int]$proc.ProcessId)
        }
    }

    if ($leftoverPids.Count -gt 0) {
        Write-Host ("Shutdown pass {0}: retrying {1} leftover PID(s)." -f $pass, $leftoverPids.Count)
        foreach ($leftoverPid in $leftoverPids) {
            Stop-PidWithFallback -ProcessId $leftoverPid
        }
    }
}

if (@($remaining).Count -gt 0) {
    Write-Warning "Some listeners are still active:"
    $remaining |
        Select-Object LocalAddress, LocalPort, OwningProcess |
        Format-Table -AutoSize
}
else {
    Write-Host "All DNNE control and structure listeners are stopped."
}

if ($IncludeCommandLineProcesses) {
    if (@($remainingExtra).Count -gt 0) {
        Write-Warning "Some extra DNNE processes are still alive:"
        $remainingExtra |
            Select-Object ProcessId, Name, CommandLine |
            Format-Table -AutoSize
    }
    else {
        Write-Host "No extra DNNE command-line processes remain."
    }
}

