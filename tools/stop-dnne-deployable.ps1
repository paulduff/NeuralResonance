param(
    [string]$DeployablePath = '.',
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

$deployableRoot = Resolve-FullPath $DeployablePath
$pidPath = Join-Path $deployableRoot 'run\pids.json'
if (-not (Test-Path $pidPath -PathType Leaf)) {
    Write-Host ("No PID file found: {0}" -f $pidPath)
    return
}

# Windows PowerShell 5.1 already returns a JSON array as Object[]. Wrapping the
# pipeline in @() nests that array as one item, so entry.Pid becomes Object[].
$entries = Get-Content -LiteralPath $pidPath -Raw | ConvertFrom-Json
$remaining = @()
if ([string]::IsNullOrWhiteSpace($SharedSecret)) {
    $SharedSecret = [string]$env:NRE_STRUCTURE_SHARED_SECRET
}

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

    try {
        $recordedStart = [DateTimeOffset]::Parse([string]$entry.StartedAt).UtcDateTime
        $actualStart = $process.StartTime.ToUniversalTime()
    }
    catch {
        Write-Warning ("refusing unverifiable PID entry {0} pid={1}: {2}" -f $entry.Name, $pidValue, $_.Exception.Message)
        $remaining += $entry
        continue
    }

    if ([Math]::Abs(($actualStart - $recordedStart).TotalSeconds) -gt 5) {
        Write-Warning ("refusing stale PID entry {0} pid={1}; process start time does not match" -f $entry.Name, $pidValue)
        $remaining += $entry
        continue
    }

    if ($WhatIf) {
        Write-Host ("WHATIF stop {0} pid={1}" -f $entry.Name, $pidValue)
        $remaining += $entry
        continue
    }

    Write-Host ("stopping {0} pid={1}" -f $entry.Name, $pidValue)
    if ($entry.Kind -eq 'structure' -and $null -ne $entry.Port) {
        $headers = @{}
        if (-not [string]::IsNullOrWhiteSpace($SharedSecret)) {
            $headers['X-NRE-Auth'] = $SharedSecret
        }
        try {
            Invoke-WebRequest `
                -Method Post `
                -Uri ("http://localhost:{0}/api/v1/structure/shutdown" -f [int]$entry.Port) `
                -Headers $headers `
                -TimeoutSec 3 | Out-Null
        }
        catch {
            Write-Verbose ("graceful shutdown request failed for {0}: {1}" -f $entry.Name, $_.Exception.Message)
        }
    }
    elseif ($process.MainWindowHandle -ne 0) {
        $null = $process.CloseMainWindow()
    }

    try {
        Wait-Process -Id $pidValue -Timeout 8 -ErrorAction Stop
    }
    catch {
        Write-Warning ("forcing {0} after graceful shutdown timeout" -f $entry.Name)
        Stop-Process -Id $pidValue -Force -ErrorAction SilentlyContinue
    }

    if ($null -ne (Get-Process -Id $pidValue -ErrorAction SilentlyContinue)) {
        Write-Warning ("process {0} pid={1} is still running; retaining its PID entry" -f $entry.Name, $pidValue)
        $remaining += $entry
    }
}

if (-not $WhatIf) {
    if ($remaining.Count -gt 0) {
        $remaining | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $pidPath -Encoding UTF8
        Write-Warning ("{0} unresolved process entry or entries remain in {1}" -f $remaining.Count, $pidPath)
    }
    else {
        Remove-Item -LiteralPath $pidPath -Force -ErrorAction SilentlyContinue
    }
}
