param(
    [string]$DeployablePath = '.',
    [string]$ControlBaseUrl = '',
    [string]$SharedSecret = '',
    [double]$MinimumFreeMemoryGB = 1.0,
    [double]$MinimumFreeDiskGB = 5.0,
    [int]$MinimumDotnetMajor = 8,
    [int]$MaximumClockSkewSeconds = 5,
    [switch]$RequireControl,
    [string]$ReportPath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Add-Check {
    param(
        [System.Collections.Generic.List[object]]$Checks,
        [string]$Name,
        [bool]$Passed,
        [string]$Detail,
        [bool]$Required = $true
    )
    $Checks.Add([pscustomobject]@{
        Name = $Name
        Passed = $Passed
        Required = $Required
        Detail = $Detail
    })
}

function Get-FreeMemoryGB {
    if ([System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Win32NT) {
        $os = Get-CimInstance Win32_OperatingSystem
        return [Math]::Round(([double]$os.FreePhysicalMemory * 1KB) / 1GB, 2)
    }

    if (Test-Path -LiteralPath '/proc/meminfo') {
        $line = Get-Content -LiteralPath '/proc/meminfo' | Where-Object { $_ -match '^MemAvailable:' } | Select-Object -First 1
        if ($line -match '(\d+)') {
            return [Math]::Round(([double]$matches[1] * 1KB) / 1GB, 2)
        }
    }

    return [Math]::Round([GC]::GetGCMemoryInfo().TotalAvailableMemoryBytes / 1GB, 2)
}

function Test-PortAvailable {
    param([int]$Port)
    $listener = $null
    try {
        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Any, $Port)
        $listener.Start()
        return $true
    }
    catch {
        return $false
    }
    finally {
        if ($null -ne $listener) { $listener.Stop() }
    }
}

$root = [System.IO.Path]::GetFullPath($DeployablePath)
$documentPath = Join-Path $root 'deployable.json'
if (-not (Test-Path -LiteralPath $documentPath -PathType Leaf)) {
    throw "deployable.json not found: $documentPath"
}
$deployable = Get-Content -LiteralPath $documentPath -Raw | ConvertFrom-Json
$checks = [System.Collections.Generic.List[object]]::new()
$isWindows = [System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Win32NT
$platform = if ($isWindows) { 'windows' } else { 'linux' }

$supportedPlatforms = @($deployable.Platforms)
Add-Check $checks 'platform' ($platform -in $supportedPlatforms) "$platform; supported=$($supportedPlatforms -join ',')"
Add-Check $checks 'powershell-core' ($PSVersionTable.PSEdition -eq 'Core' -or $isWindows) "$($PSVersionTable.PSEdition) $($PSVersionTable.PSVersion)" (-not $isWindows)

$dotnetVersionText = (& dotnet --version 2>$null | Select-Object -First 1)
$dotnetMajor = 0
if ($dotnetVersionText -match '^(\d+)\.') { $dotnetMajor = [int]$matches[1] }
Add-Check $checks 'dotnet-runtime' ($dotnetMajor -ge $MinimumDotnetMajor) "found=$dotnetVersionText required-major=$MinimumDotnetMajor"

$freeMemoryGB = Get-FreeMemoryGB
Add-Check $checks 'free-memory' ($freeMemoryGB -ge $MinimumFreeMemoryGB) "free=${freeMemoryGB}GB required=${MinimumFreeMemoryGB}GB"
$driveRoot = [System.IO.Path]::GetPathRoot($root)
$drive = [System.IO.DriveInfo]::new($driveRoot)
$freeDiskGB = [Math]::Round($drive.AvailableFreeSpace / 1GB, 2)
Add-Check $checks 'free-disk' ($freeDiskGB -ge $MinimumFreeDiskGB) "free=${freeDiskGB}GB required=${MinimumFreeDiskGB}GB"

foreach ($entry in @($deployable.Apps) + @($deployable.Structures)) {
    $entryRoot = Join-Path $root ([string]$entry.Path)
    $exePath = if ($entry.EntryExe) { Join-Path $entryRoot ([string]$entry.EntryExe) } else { '' }
    $dllPath = Join-Path $entryRoot ([string]$entry.EntryDll)
    Add-Check $checks "entry-$($entry.Id)" ((Test-Path -LiteralPath $exePath -PathType Leaf) -or (Test-Path -LiteralPath $dllPath -PathType Leaf)) "exe=$exePath dll=$dllPath"
}

$claimedPorts = [System.Collections.Generic.HashSet[int]]::new()
foreach ($structure in @($deployable.Structures)) {
    foreach ($port in @([int]$structure.LeftPort, [int]$structure.RightPort)) {
        if (-not $claimedPorts.Add($port)) {
            Add-Check $checks "port-$port" $false 'port is duplicated inside deployable'
        }
        else {
            Add-Check $checks "port-$port" (Test-PortAvailable -Port $port) 'must be available before startup'
        }
    }
}

if ([string]::IsNullOrWhiteSpace($ControlBaseUrl)) {
    $ControlBaseUrl = [string]$deployable.ControlBaseUrlDefault
}
$controlUri = [Uri]$ControlBaseUrl
try {
    $addresses = [System.Net.Dns]::GetHostAddresses($controlUri.DnsSafeHost)
    Add-Check $checks 'control-dns' ($addresses.Count -gt 0) "$($controlUri.DnsSafeHost) -> $($addresses -join ', ')"
}
catch {
    Add-Check $checks 'control-dns' $false $_.Exception.Message $RequireControl
}

$clockDetail = 'control not queried'
$clockPassed = $false
try {
    $request = [System.Net.HttpWebRequest]::Create($ControlBaseUrl)
    $request.Method = 'HEAD'
    $request.Timeout = 3000
    $response = $request.GetResponse()
    $dateHeader = $response.Headers['Date']
    $response.Close()
    if (-not [string]::IsNullOrWhiteSpace($dateHeader)) {
        $remote = [DateTimeOffset]::Parse($dateHeader).UtcDateTime
        $skew = [Math]::Abs(([DateTime]::UtcNow - $remote).TotalSeconds)
        $clockPassed = $skew -le $MaximumClockSkewSeconds
        $clockDetail = "skew=$([Math]::Round($skew, 2))s maximum=${MaximumClockSkewSeconds}s"
    }
}
catch {
    $clockDetail = $_.Exception.Message
}
Add-Check $checks 'control-clock' $clockPassed $clockDetail $RequireControl

if ([string]::IsNullOrWhiteSpace($SharedSecret)) {
    $SharedSecret = [string]$env:NRE_STRUCTURE_SHARED_SECRET
}
$listenHost = [string]$deployable.ListenHostDefault
$nonLoopback = $listenHost -notin @('localhost', '127.0.0.1', '::1')
Add-Check $checks 'shared-secret' (-not $nonLoopback -or -not [string]::IsNullOrWhiteSpace($SharedSecret)) 'required for non-loopback listeners' $nonLoopback

$requiredFailures = @($checks | Where-Object { $_.Required -and -not $_.Passed })
$report = [pscustomobject]@{
    Schema = 'dnne.node-preflight.v1'
    GeneratedAt = [DateTimeOffset]::UtcNow.ToString('o')
    Node = [System.Net.Dns]::GetHostName()
    Deployable = [string]$deployable.Name
    Platform = $platform
    Passed = $requiredFailures.Count -eq 0
    Checks = @($checks)
}

foreach ($check in $checks) {
    $marker = if ($check.Passed) { 'PASS' } elseif ($check.Required) { 'FAIL' } else { 'WARN' }
    Write-Host ("[{0}] {1}: {2}" -f $marker, $check.Name, $check.Detail)
}
if (-not [string]::IsNullOrWhiteSpace($ReportPath)) {
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath ([System.IO.Path]::GetFullPath($ReportPath)) -Encoding UTF8
}
if (-not $report.Passed) {
    throw "Node preflight failed with $($requiredFailures.Count) required check(s)."
}
return $report
