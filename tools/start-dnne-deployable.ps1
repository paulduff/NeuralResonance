param(
    [string]$DeployablePath = '.',
    [string]$ControlBaseUrl = '',
    [string]$ListenHost = '',
    [ValidateSet('All', 'Left', 'Right', 'Midline')]
    [string]$Hemisphere = 'All',
    [switch]$NoApps,
    [switch]$NoStructures,
    [string]$LogRoot = '',
    [string]$SharedSecret = '',
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

function Join-Url {
    param(
        [string]$BaseUrl,
        [string]$Path
    )

    return ("{0}/{1}" -f $BaseUrl.TrimEnd('/'), $Path.TrimStart('/'))
}

function Start-DnneProcess {
    param(
        [string]$Name,
        [string]$FileName,
        [string]$Arguments,
        [string]$WorkingDirectory,
        [hashtable]$EnvironmentVariables,
        [string]$StdOutPath,
        [string]$StdErrPath
    )

    if ($WhatIf) {
        Write-Host ("WHATIF start {0}: {1} {2}" -f $Name, $FileName, $Arguments)
        return $null
    }

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $FileName
    $startInfo.Arguments = $Arguments
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true

    foreach ($entry in $EnvironmentVariables.GetEnumerator()) {
        $startInfo.Environment[$entry.Key] = [string]$entry.Value
    }

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    $stdout = [System.IO.StreamWriter]::new($StdOutPath, $true)
    $stderr = [System.IO.StreamWriter]::new($StdErrPath, $true)
    $process.add_OutputDataReceived({
        param($sender, $eventArgs)
        if ($null -ne $eventArgs.Data) {
            $stdout.WriteLine($eventArgs.Data)
            $stdout.Flush()
        }
    })
    $process.add_ErrorDataReceived({
        param($sender, $eventArgs)
        if ($null -ne $eventArgs.Data) {
            $stderr.WriteLine($eventArgs.Data)
            $stderr.Flush()
        }
    })

    if (-not $process.Start()) {
        throw "Failed to start $Name"
    }

    $process.BeginOutputReadLine()
    $process.BeginErrorReadLine()
    Write-Host ("started {0} pid={1}" -f $Name, $process.Id)
    return $process
}

function Get-EntryCommand {
    param([object]$App)

    $appPath = Resolve-FullPath (Join-Path $DeployableRoot ([string]$App.Path))
    $exe = if ($App.EntryExe) { Join-Path $appPath ([string]$App.EntryExe) } else { '' }
    if ($exe -and (Test-Path $exe -PathType Leaf)) {
        return [pscustomobject]@{
            FileName = $exe
            Arguments = ''
            WorkingDirectory = $appPath
        }
    }

    $dll = Join-Path $appPath ([string]$App.EntryDll)
    if (-not (Test-Path $dll -PathType Leaf)) {
        throw "Entry point not found for $($App.Id): $dll"
    }

    return [pscustomobject]@{
        FileName = 'dotnet'
        Arguments = ('"{0}"' -f $dll)
        WorkingDirectory = $appPath
    }
}

$DeployableRoot = Resolve-FullPath $DeployablePath
$deployableFile = Join-Path $DeployableRoot 'deployable.json'
if (-not (Test-Path $deployableFile -PathType Leaf)) {
    throw "deployable.json not found: $deployableFile"
}

$deployable = Get-Content -LiteralPath $deployableFile -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($ControlBaseUrl)) {
    $ControlBaseUrl = if ($deployable.ControlBaseUrlDefault) { [string]$deployable.ControlBaseUrlDefault } else { 'http://localhost:5080' }
}
if ([string]::IsNullOrWhiteSpace($ListenHost)) {
    $ListenHost = if ($deployable.ListenHostDefault) { [string]$deployable.ListenHostDefault } else { '0.0.0.0' }
}
if ([string]::IsNullOrWhiteSpace($LogRoot)) {
    $LogRoot = Join-Path $DeployableRoot 'logs'
}

$listenIsLoopback = $ListenHost -in @('localhost', '127.0.0.1', '::1')
if (-not $listenIsLoopback -and [string]::IsNullOrWhiteSpace($SharedSecret)) {
    throw 'A SharedSecret is required when ListenHost is not loopback.'
}

$runRoot = Join-Path $DeployableRoot 'run'
New-Item -ItemType Directory -Force -Path $LogRoot | Out-Null
New-Item -ItemType Directory -Force -Path $runRoot | Out-Null

$started = @()
$publishUrl = Join-Url -BaseUrl $ControlBaseUrl -Path '/api/v1/publish/step'
$snapshotUrl = Join-Url -BaseUrl $ControlBaseUrl -Path '/api/v1/snapshot'

if (-not $NoStructures -and $deployable.Structures) {
    foreach ($structure in $deployable.Structures) {
        $ports = @()
        if ($Hemisphere -in @('All', 'Left') -and $structure.LeftPort) {
            $ports += [pscustomobject]@{ Hemisphere = 'L'; Port = [int]$structure.LeftPort; InstanceKey = "L_$($structure.Id)" }
        }
        if ($Hemisphere -in @('All', 'Right') -and $structure.RightPort) {
            $ports += [pscustomobject]@{ Hemisphere = 'R'; Port = [int]$structure.RightPort; InstanceKey = "R_$($structure.Id)" }
        }
        if ($Hemisphere -eq 'Midline' -or ($ports.Count -eq 0 -and $structure.LeftPort)) {
            $ports += [pscustomobject]@{ Hemisphere = 'M'; Port = [int]$structure.LeftPort; InstanceKey = "M_$($structure.Id)" }
        }

        $command = Get-EntryCommand -App $structure
        foreach ($instance in $ports) {
            $name = $instance.InstanceKey
            $envVars = @{
                PORT = $instance.Port
                ASPNETCORE_URLS = "http://${ListenHost}:$($instance.Port)"
                HEMISPHERE = $instance.Hemisphere
                SERVICE_INSTANCE = $instance.InstanceKey
                CONTROL_PUBLISH_URL = $publishUrl
            }
            if (-not [string]::IsNullOrWhiteSpace($SharedSecret)) {
                $envVars['NRE_STRUCTURE_SHARED_SECRET'] = $SharedSecret
                $envVars['NRE_CONTROL_SHARED_SECRET'] = $SharedSecret
            }
            if (-not $listenIsLoopback) {
                $envVars['NRE_STRUCTURE_LISTEN_ANY_IP'] = 'true'
            }

            $process = Start-DnneProcess `
                -Name $name `
                -FileName $command.FileName `
                -Arguments $command.Arguments `
                -WorkingDirectory $command.WorkingDirectory `
                -EnvironmentVariables $envVars `
                -StdOutPath (Join-Path $LogRoot "$name.stdout.log") `
                -StdErrPath (Join-Path $LogRoot "$name.stderr.log")

            if ($null -ne $process) {
                $started += [pscustomobject]@{
                    Name = $name
                    Kind = 'structure'
                    Pid = $process.Id
                    Port = $instance.Port
                    Hemisphere = $instance.Hemisphere
                    StartedAt = ([DateTimeOffset]$process.StartTime.ToUniversalTime()).ToString('o')
                }
            }
        }
    }
}

if (-not $NoApps -and $deployable.Apps) {
    foreach ($app in $deployable.Apps) {
        $command = Get-EntryCommand -App $app
        $envVars = @{
            NRE_CONTROL_ENDPOINTS = $ControlBaseUrl
            CONTROLPROGRAM_BASE_URL = $ControlBaseUrl
        }
        if (-not [string]::IsNullOrWhiteSpace($SharedSecret)) {
            $envVars['NRE_STRUCTURE_SHARED_SECRET'] = $SharedSecret
            $envVars['NRE_CONTROL_SHARED_SECRET'] = $SharedSecret
        }
        if ($app.Role -eq 'control') {
            $controlPort = ([Uri]$ControlBaseUrl).Port
            $envVars['PORT'] = $controlPort
            $envVars['ASPNETCORE_URLS'] = $ControlBaseUrl
            $envVars['SnapshotEndpoint'] = $snapshotUrl
            $envVars['ControlPublishUrl'] = $publishUrl
            $envVars['StructureProcessHost__AutoStartEnabled'] = 'false'
            if (-not $listenIsLoopback) {
                $envVars['NRE_CONTROL_LISTEN_ANY_IP'] = 'true'
            }
        }

        $process = Start-DnneProcess `
            -Name $app.Id `
            -FileName $command.FileName `
            -Arguments $command.Arguments `
            -WorkingDirectory $command.WorkingDirectory `
            -EnvironmentVariables $envVars `
            -StdOutPath (Join-Path $LogRoot "$($app.Id).stdout.log") `
            -StdErrPath (Join-Path $LogRoot "$($app.Id).stderr.log")

        if ($null -ne $process) {
            $started += [pscustomobject]@{
                Name = $app.Id
                Kind = 'app'
                Pid = $process.Id
                Port = $null
                Hemisphere = $null
                StartedAt = ([DateTimeOffset]$process.StartTime.ToUniversalTime()).ToString('o')
            }
        }
    }
}

$pidPath = Join-Path $runRoot 'pids.json'
$started | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $pidPath -Encoding UTF8
Write-Host ("deployable {0}: {1} process(es) recorded at {2}" -f $deployable.Name, $started.Count, $pidPath)
