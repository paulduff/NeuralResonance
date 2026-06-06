Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-DnneRepoRoot {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ScriptPath
    )

    return (Resolve-Path (Join-Path (Split-Path -Parent $ScriptPath) '..')).Path
}

function Invoke-WithTemporaryEnvironment {
    param(
        [hashtable]$Variables,
        [scriptblock]$Action
    )

    $previous = @{}
    foreach ($entry in $Variables.GetEnumerator()) {
        $name = [string]$entry.Key
        $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        if ($null -eq $entry.Value) {
            Remove-Item -Path ("Env:{0}" -f $name) -ErrorAction SilentlyContinue
        }
        else {
            [Environment]::SetEnvironmentVariable($name, [string]$entry.Value, 'Process')
        }
    }

    try {
        & $Action
    }
    finally {
        foreach ($entry in $previous.GetEnumerator()) {
            [Environment]::SetEnvironmentVariable([string]$entry.Key, $entry.Value, 'Process')
        }
    }
}

function ConvertTo-ProcessArgument {
    param(
        [AllowNull()]
        [object]$Value
    )

    if ($null -eq $Value) {
        return '""'
    }

    $text = [string]$Value
    if ($text.Length -eq 0) {
        return '""'
    }

    if ($text -notmatch '[\s"]') {
        return $text
    }

    $escaped = $text -replace '(\\*)"', '$1$1\"'
    $escaped = $escaped -replace '(\\+)$', '$1$1'
    return '"' + $escaped + '"'
}

function Assert-DnneSimulatorExclusive {
    param(
        [Parameter(Mandatory = $true)]
        [string]$CurrentSimulator,
        [Parameter(Mandatory = $true)]
        [string[]]$BlockedSignatures,
        [switch]$WhatIf
    )

    if ($WhatIf -or $BlockedSignatures.Count -eq 0) {
        return
    }

    $pattern = ($BlockedSignatures | ForEach-Object { [regex]::Escape($_) }) -join '|'
    $running = Get-CimInstance Win32_Process |
        Where-Object {
            $_.ProcessId -ne $PID -and
            -not [string]::IsNullOrWhiteSpace($_.CommandLine) -and
            $_.CommandLine -match $pattern
        } |
        Select-Object -First 5 ProcessId, Name, CommandLine

    if ($running) {
        $details = ($running | ForEach-Object {
                "{0} (pid {1})" -f $_.Name, $_.ProcessId
            }) -join ', '

        throw ("{0} cannot start while another DNNE simulator is running: {1}. Stop the other simulator first so the brain receives one world stream." -f $CurrentSimulator, $details)
    }
}

function Start-DnneProject {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath,
        [Parameter(Mandatory = $true)]
        [string]$FriendlyName,
        [ValidateSet('Debug', 'Release')]
        [string]$Configuration = 'Debug',
        [switch]$NoBuild,
        [hashtable]$EnvironmentVariables,
        [switch]$WhatIf
    )

    if (-not (Test-Path $ProjectPath -PathType Leaf)) {
        throw "Project not found: $ProjectPath"
    }

    $resolvedProjectPath = (Resolve-Path $ProjectPath).Path
    $workingDirectory = Split-Path -Parent $resolvedProjectPath

    if ($WhatIf) {
        Write-Host ("Starting {0}" -f $FriendlyName)
        Write-Host ("  project: {0}" -f $resolvedProjectPath)
        Write-Host ("  configuration: {0}" -f $Configuration)
        Write-Host ("  no-build: {0}" -f [bool]$NoBuild)
        if ($EnvironmentVariables) {
            $envPreview = @()
            foreach ($entry in $EnvironmentVariables.GetEnumerator() | Sort-Object Key) {
                $envPreview += ("{0}={1}" -f $entry.Key, $entry.Value)
            }
            if ($envPreview.Count -gt 0) {
                Write-Host ("  env: {0}" -f ($envPreview -join ', '))
            }
        }

        Write-Host "WhatIf set: process not started."
        return $null
    }

    if (-not $NoBuild) {
        Write-Host ("Building {0} ({1})..." -f $FriendlyName, $Configuration)
        & dotnet build $resolvedProjectPath -c $Configuration --nologo --verbosity minimal
        if ($LASTEXITCODE -ne 0) {
            throw ("Build failed for {0}" -f $FriendlyName)
        }
    }

    $argumentList = @(
        'run',
        '--project',
        $resolvedProjectPath,
        '--configuration',
        $Configuration
    )
    if ($NoBuild) {
        $argumentList += '--no-build'
    }

    $envPreview = @()
    if ($EnvironmentVariables) {
        foreach ($entry in $EnvironmentVariables.GetEnumerator() | Sort-Object Key) {
            $envPreview += ("{0}={1}" -f $entry.Key, $entry.Value)
        }
    }

    Write-Host ("Starting {0}" -f $FriendlyName)
    Write-Host ("  project: {0}" -f $resolvedProjectPath)
    Write-Host ("  configuration: {0}" -f $Configuration)
    Write-Host ("  no-build: {0}" -f [bool]$NoBuild)
    if ($envPreview.Count -gt 0) {
        Write-Host ("  env: {0}" -f ($envPreview -join ', '))
    }

    $logDirectory = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)) 'NeuralResonanceEngine\logs'
    New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
    $safeName = (($FriendlyName -replace '[^A-Za-z0-9]+', '-').Trim('-')).ToLowerInvariant()
    if ([string]::IsNullOrWhiteSpace($safeName)) {
        $safeName = 'dnne-process'
    }
    $stdoutPath = Join-Path $logDirectory ("{0}-stdout.log" -f $safeName)
    $stderrPath = Join-Path $logDirectory ("{0}-stderr.log" -f $safeName)

    $quotedArgumentList = ($argumentList | ForEach-Object { ConvertTo-ProcessArgument $_ }) -join ' '
    $startAction = {
        Start-Process `
            -FilePath 'dotnet' `
            -ArgumentList $quotedArgumentList `
            -WorkingDirectory $workingDirectory `
            -RedirectStandardOutput $stdoutPath `
            -RedirectStandardError $stderrPath `
            -PassThru
    }

    $process = if ($EnvironmentVariables -and $EnvironmentVariables.Count -gt 0) {
        Invoke-WithTemporaryEnvironment -Variables $EnvironmentVariables -Action $startAction
    }
    else {
        & $startAction
    }

    Write-Host ("  pid: {0}" -f $process.Id)
    Write-Host ("  stdout: {0}" -f $stdoutPath)
    Write-Host ("  stderr: {0}" -f $stderrPath)
    return $process
}
