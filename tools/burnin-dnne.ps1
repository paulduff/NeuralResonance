param(
    [string]$ControlBaseUrl = 'http://localhost:5080',
    [int]$DurationSec = 1800,
    [int]$PollIntervalMs = 500,
    [int]$AllowableNonOkServices = 1,
    [int]$MaxSnapshotAgeTicks = 25,
    [int]$SnapshotAgeGraceSec = 20,
    [int]$NonOkGraceSec = 20,
    [int]$WarmupSec = 15,
    [int]$SensoryIntervalSec = 1,
    [int]$RestartCycleIntervalSec = 600,
    [int]$RestartRecoveryTimeoutSec = 90,
    [int]$MaxSensory404 = 2,
    [int]$MaxSensoryZeroDelivered = 30,
    [int]$MazeStuckFailAfterSec = 0,
    [int]$MazeStuckNoProgressWindowSec = 45,
    [float]$MazeStuckWallImpactMinRatePerSec = 0.8,
    [int]$MazeStuckMaxMotorDispatchPerPoll = 2,
    [string]$SummaryPath = '',
    [string]$SamplesPath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$baseUrl = $ControlBaseUrl.TrimEnd('/')
$stateUrl = "$baseUrl/api/v1/admin/validation?maxSnapshotAgeTicks=$MaxSnapshotAgeTicks&maxNonOkServices=$AllowableNonOkServices"
$frameUrl = "$baseUrl/api/v1/frame"
$visualUrl = "$baseUrl/api/v1/admin/input/visual"
$auditoryUrl = "$baseUrl/api/v1/admin/input/auditory"
$restartSimUrl = "$baseUrl/api/v1/admin/restart-sim"

if ([string]::IsNullOrWhiteSpace($SummaryPath)) {
    $SummaryPath = Join-Path (Split-Path -Parent $PSCommandPath) '_burnin-summary.txt'
}
if ([string]::IsNullOrWhiteSpace($SamplesPath)) {
    $SamplesPath = Join-Path (Split-Path -Parent $PSCommandPath) '_burnin-samples.json'
}

$DurationSec = [Math]::Max(10, $DurationSec)
$PollIntervalMs = [Math]::Max(100, $PollIntervalMs)
$AllowableNonOkServices = [Math]::Max(0, $AllowableNonOkServices)
$MaxSnapshotAgeTicks = [Math]::Max(2, $MaxSnapshotAgeTicks)
$SnapshotAgeGraceSec = [Math]::Max(2, $SnapshotAgeGraceSec)
$NonOkGraceSec = [Math]::Max(2, $NonOkGraceSec)
$WarmupSec = [Math]::Max(0, $WarmupSec)
$SensoryIntervalSec = [Math]::Max(1, $SensoryIntervalSec)
$RestartCycleIntervalSec = [Math]::Max(0, $RestartCycleIntervalSec)
$RestartRecoveryTimeoutSec = [Math]::Max(5, $RestartRecoveryTimeoutSec)
$MaxSensory404 = [Math]::Max(0, $MaxSensory404)
$MaxSensoryZeroDelivered = [Math]::Max(0, $MaxSensoryZeroDelivered)
$MazeStuckFailAfterSec = [Math]::Max(0, $MazeStuckFailAfterSec)
$MazeStuckNoProgressWindowSec = [Math]::Max(5, $MazeStuckNoProgressWindowSec)
$MazeStuckWallImpactMinRatePerSec = [Math]::Max(0.1, $MazeStuckWallImpactMinRatePerSec)
$MazeStuckMaxMotorDispatchPerPoll = [Math]::Max(0, $MazeStuckMaxMotorDispatchPerPoll)

function Get-HttpStatusCodeFromException {
    param([System.Exception]$Exception)
    try {
        if ($Exception.PSObject.Properties.Name -contains 'Response') {
            $resp = $Exception.Response
            if ($null -ne $resp) {
                if ($resp.PSObject.Properties.Name -contains 'StatusCode') {
                    return [int]$resp.StatusCode
                }
            }
        }
    }
    catch {
        # no-op
    }
    return 0
}

function Try-GetBoolProperty {
    param(
        [object]$Object,
        [string]$PropertyName
    )

    if ($null -eq $Object -or [string]::IsNullOrWhiteSpace($PropertyName)) {
        return $null
    }

    $property = $Object.PSObject.Properties[$PropertyName]
    if ($null -eq $property -or $null -eq $property.Value) {
        return $null
    }

    try {
        return [bool]$property.Value
    }
    catch {
        return $null
    }
}

function Get-StateSnapshot {
    param([string]$Uri)

    $state = Invoke-RestMethod -Uri $Uri -TimeoutSec 12

    if ($null -ne $state.PSObject.Properties['serviceCount']) {
        $tick = [long]$state.tick
        $lastSnapshotTick = if ($null -ne $state.PSObject.Properties['lastSnapshotTick']) {
            [long]$state.lastSnapshotTick
        }
        else {
            0L
        }
        $snapshotAgeTicks = if ($null -ne $state.PSObject.Properties['snapshotAgeTicks']) {
            $age = [long]$state.snapshotAgeTicks
            if ($age -ge 0) { $age } else { [long]::MaxValue }
        }
        elseif ($lastSnapshotTick -gt 0 -and $tick -ge $lastSnapshotTick) {
            $tick - $lastSnapshotTick
        }
        else {
            [long]::MaxValue
        }

        return [pscustomobject]@{
            Tick             = $tick
            LastSnapshotTick = $lastSnapshotTick
            SnapshotAgeTicks = $snapshotAgeTicks
            ServiceCount     = [int]$state.serviceCount
            NonOkCount       = [int]$state.nonOkCount
        }
    }

    $serviceTelemetry = @{}
    if ($null -ne $state.serviceTelemetry) {
        $serviceTelemetry = $state.serviceTelemetry
    }

    $serviceCount = 0
    $nonOkCount = 0
    foreach ($entry in $serviceTelemetry.PSObject.Properties) {
        $serviceCount++
        $statusProp = $entry.Value.PSObject.Properties['lastStatus']
        $status = if ($null -ne $statusProp -and $null -ne $statusProp.Value) { [string]$statusProp.Value } else { '' }
        if ([string]::IsNullOrWhiteSpace($status) -or -not $status.Equals('OK', [System.StringComparison]::OrdinalIgnoreCase)) {
            $nonOkCount++
        }
    }

    $tick = [long]$state.tick
    $lastSnapshotTick = [long]$state.lastSnapshotTick
    $snapshotAgeTicks = if ($lastSnapshotTick -gt 0 -and $tick -ge $lastSnapshotTick) { $tick - $lastSnapshotTick } else { [long]::MaxValue }

    return [pscustomobject]@{
        Tick            = $tick
        LastSnapshotTick = $lastSnapshotTick
        SnapshotAgeTicks = $snapshotAgeTicks
        ServiceCount    = $serviceCount
        NonOkCount      = $nonOkCount
    }
}

function Invoke-SensoryStimulus {
    param(
        [string]$Uri,
        [hashtable]$Payload
    )

    try {
        $json = $Payload | ConvertTo-Json -Compress
        $response = Invoke-RestMethod -Uri $Uri -Method Post -ContentType 'application/json' -Body $json -TimeoutSec 6
        $delivered = 0
        if ($null -ne $response.PSObject.Properties['deliveredSpikes']) {
            $delivered = [int]$response.deliveredSpikes
        }
        return [pscustomobject]@{
            Success   = $true
            Status    = 200
            Delivered = $delivered
            Error     = ''
        }
    }
    catch {
        $status = Get-HttpStatusCodeFromException -Exception $_.Exception
        return [pscustomobject]@{
            Success   = $false
            Status    = $status
            Delivered = 0
            Error     = $_.Exception.Message
        }
    }
}

function Wait-ForRecoveryAfterRestart {
    param(
        [string]$StateUri,
        [datetime]$DeadlineUtc,
        [int]$AllowableNonOk,
        [int]$MaxSnapshotAge
    )

    while ([DateTime]::UtcNow -lt $DeadlineUtc) {
        try {
            $snapshot = Get-StateSnapshot -Uri $StateUri
            if ($snapshot.Tick -gt 0 -and
                $snapshot.ServiceCount -gt 0 -and
                $snapshot.NonOkCount -le $AllowableNonOk -and
                $snapshot.SnapshotAgeTicks -le $MaxSnapshotAge) {
                return $true
            }
        }
        catch {
            # keep waiting
        }

        Start-Sleep -Milliseconds 500
    }

    return $false
}

$startedUtc = [DateTime]::UtcNow
$deadlineUtc = $startedUtc.AddSeconds($DurationSec)
$nextSensoryUtc = $startedUtc
$nextRestartUtc = if ($RestartCycleIntervalSec -gt 0) { $startedUtc.AddSeconds($RestartCycleIntervalSec) } else { [DateTime]::MaxValue }
$warmupDeadlineUtc = $startedUtc.AddSeconds($WarmupSec)
$lastProgressUtc = [DateTime]::MinValue
$lastSampleUtc = [DateTime]::MinValue

$samples = New-Object System.Collections.Generic.List[object]

$statePollErrors = 0
$snapshotStallEvents = 0
$nonOkViolationEvents = 0
$sensoryDispatchErrors = 0
$sensory404Errors = 0
$sensoryZeroDelivered = 0
$restartCycles = 0
$restartCycleFailures = 0
$framePollErrors = 0
$maxObservedNonOk = 0
$maxObservedSnapshotAge = 0L
$failReasons = New-Object System.Collections.Generic.List[string]

$snapshotStallStreak = 0
$nonOkViolationStreak = 0
$statePollErrorStreak = 0
$dispatchSinceMs = 0L
$outputSinceMs = 0L
$mazeDetected = $false
$mazeStuckEvents = 0
$mazeStuckStreakSec = 0.0
$mazeStuckCandidateActive = $false
$mazeMaxNoProgressSec = 0.0
$mazeWallImpactTotal = 0
$mazeProgressTotal = 0
$mazeMotorDispatchTotal = 0
$lastMazeProgressUtc = $startedUtc
$wallImpactWindow = New-Object System.Collections.Generic.Queue[datetime]
$motorTargets = @('M1', 'Sma', 'PremotorCortex')

$pollSeconds = $PollIntervalMs / 1000.0
$snapshotStallFailStreak = [Math]::Max(1, [int][Math]::Ceiling($SnapshotAgeGraceSec / $pollSeconds))
$nonOkFailStreak = [Math]::Max(1, [int][Math]::Ceiling($NonOkGraceSec / $pollSeconds))
$statePollFailStreak = [Math]::Max(1, [int][Math]::Ceiling($SnapshotAgeGraceSec / $pollSeconds))

Write-Host ("Starting DNNE burn-in gate for {0}s against {1}" -f $DurationSec, $baseUrl)
Write-Host ("Thresholds: nonOK<={0}, snapshotAge<={1} ticks, sensory404<={2}, sensoryZero<={3}" -f $AllowableNonOkServices, $MaxSnapshotAgeTicks, $MaxSensory404, $MaxSensoryZeroDelivered)

while ([DateTime]::UtcNow -lt $deadlineUtc) {
    $nowUtc = [DateTime]::UtcNow
    $stateSnapshot = $null
    $stateOk = $false

    try {
        $stateSnapshot = Get-StateSnapshot -Uri $stateUrl
        $stateOk = $true
        $statePollErrorStreak = 0
    }
    catch {
        $statePollErrors++
        $statePollErrorStreak++
        if ($nowUtc -gt $warmupDeadlineUtc) {
            if ($statePollErrorStreak -ge $statePollFailStreak) {
                $failReasons.Add("state endpoint polling stalled for ~$(($statePollErrorStreak * $pollSeconds).ToString('0.0'))s: $($_.Exception.Message)")
                break
            }
        }
    }

    if ($stateOk -and $null -ne $stateSnapshot) {
        if ($stateSnapshot.NonOkCount -gt $maxObservedNonOk) {
            $maxObservedNonOk = $stateSnapshot.NonOkCount
        }
        if ($stateSnapshot.SnapshotAgeTicks -lt [long]::MaxValue -and $stateSnapshot.SnapshotAgeTicks -gt $maxObservedSnapshotAge) {
            $maxObservedSnapshotAge = $stateSnapshot.SnapshotAgeTicks
        }

        if ($stateSnapshot.SnapshotAgeTicks -gt $MaxSnapshotAgeTicks) {
            $snapshotStallStreak++
            $snapshotStallEvents++
        }
        else {
            $snapshotStallStreak = 0
        }

        if ($stateSnapshot.NonOkCount -gt $AllowableNonOkServices) {
            $nonOkViolationStreak++
            $nonOkViolationEvents++
        }
        else {
            $nonOkViolationStreak = 0
        }

        if ($nowUtc -gt $warmupDeadlineUtc) {
            if ($snapshotStallStreak -ge $snapshotStallFailStreak) {
                $failReasons.Add("snapshot cadence stalled: age $($stateSnapshot.SnapshotAgeTicks) ticks for ~$(($snapshotStallStreak * $pollSeconds).ToString('0.0'))s")
                break
            }

            if ($nonOkViolationStreak -ge $nonOkFailStreak) {
                $failReasons.Add("non-OK services sustained: $($stateSnapshot.NonOkCount) for ~$(($nonOkViolationStreak * $pollSeconds).ToString('0.0'))s")
                break
            }
        }

        if (($nowUtc - $lastSampleUtc).TotalSeconds -ge 1.0) {
            $samples.Add([pscustomobject]@{
                wallClockUtc      = $nowUtc.ToString('o')
                tick              = $stateSnapshot.Tick
                lastSnapshotTick  = $stateSnapshot.LastSnapshotTick
                snapshotAgeTicks  = if ($stateSnapshot.SnapshotAgeTicks -eq [long]::MaxValue) { -1 } else { $stateSnapshot.SnapshotAgeTicks }
                serviceCount      = $stateSnapshot.ServiceCount
                nonOkCount        = $stateSnapshot.NonOkCount
                sensory404Errors  = $sensory404Errors
                sensoryZero       = $sensoryZeroDelivered
                restartFailures   = $restartCycleFailures
                mazeDetected      = $mazeDetected
                mazeWallImpactTotal = $mazeWallImpactTotal
                mazeProgressTotal = $mazeProgressTotal
                mazeMotorDispatchTotal = $mazeMotorDispatchTotal
                mazeMaxNoProgressSec = [Math]::Round($mazeMaxNoProgressSec, 1)
                mazeStuckEvents   = $mazeStuckEvents
            })
            $lastSampleUtc = $nowUtc
        }
    }

    $motorDispatchThisPoll = 0
    $isSleeping = $false
    if ($MazeStuckFailAfterSec -gt 0) {
        try {
        $frameRequestUrl = "$frameUrl?output_since_ms=$outputSinceMs&dispatch_since_ms=$dispatchSinceMs"
        $frame = Invoke-RestMethod -Uri $frameRequestUrl -TimeoutSec 6

        foreach ($entry in @($frame.OutputLog)) {
            if ($null -eq $entry) {
                continue
            }

            $wallClockMs = 0L
            try {
                if ($null -ne $entry.wallClockUnixMs) {
                    $wallClockMs = [long]$entry.wallClockUnixMs
                }
            }
            catch {
                $wallClockMs = 0L
            }
            if ($wallClockMs -gt $outputSinceMs) {
                $outputSinceMs = $wallClockMs
            }

            $message = ''
            try {
                if ($null -ne $entry.message) {
                    $message = [string]$entry.message
                }
            }
            catch {
                $message = ''
            }

            if ([string]::IsNullOrWhiteSpace($message)) {
                continue
            }

            if ($message -match '(?i)\bwall impact\b' -or $message -match '(?i)\bcollision input\b') {
                $mazeDetected = $true
                $mazeWallImpactTotal++
                $wallImpactWindow.Enqueue($nowUtc)
            }

            if ($message -match '(?i)\bcheckpoint\b' -or
                $message -match '(?i)\bgoal\b' -or
                $message -match '(?i)\bfood\b' -or
                $message -match '(?i)\breward\b' -or
                $message -match '(?i)\bscore\s*\+') {
                $mazeDetected = $true
                $mazeProgressTotal++
                $lastMazeProgressUtc = $nowUtc
            }
        }

        foreach ($entry in @($frame.DispatchSpikes)) {
            if ($null -eq $entry) {
                continue
            }

            $wallClockMs = 0L
            try {
                if ($null -ne $entry.wallClockUnixMs) {
                    $wallClockMs = [long]$entry.wallClockUnixMs
                }
            }
            catch {
                $wallClockMs = 0L
            }
            if ($wallClockMs -gt $dispatchSinceMs) {
                $dispatchSinceMs = $wallClockMs
            }

            $target = ''
            try {
                if ($null -ne $entry.targetStructure) {
                    $target = [string]$entry.targetStructure
                }
            }
            catch {
                $target = ''
            }

            if (-not [string]::IsNullOrWhiteSpace($target) -and ($motorTargets -contains $target)) {
                $mazeDetected = $true
                $mazeMotorDispatchTotal++
                $motorDispatchThisPoll++
            }
        }

        if ($null -ne $frame.State) {
            $sleepMemory = $frame.State.PSObject.Properties['SleepMemory']
            if ($null -ne $sleepMemory -and $null -ne $sleepMemory.Value) {
                $sleeping = Try-GetBoolProperty -Object $sleepMemory.Value -PropertyName 'IsSleeping'
                if ($null -ne $sleeping) {
                    $isSleeping = [bool]$sleeping
                }
            }
            else {
                $sleepMemoryRuntime = $frame.State.PSObject.Properties['SleepMemoryRuntime']
                if ($null -ne $sleepMemoryRuntime -and $null -ne $sleepMemoryRuntime.Value) {
                    $sleeping = Try-GetBoolProperty -Object $sleepMemoryRuntime.Value -PropertyName 'IsSleeping'
                    if ($null -ne $sleeping) {
                        $isSleeping = [bool]$sleeping
                    }
                }
            }
        }
        }
        catch {
            $framePollErrors++
        }

        while ($wallImpactWindow.Count -gt 0 -and ($nowUtc - $wallImpactWindow.Peek()).TotalSeconds -gt $MazeStuckNoProgressWindowSec) {
            $null = $wallImpactWindow.Dequeue()
        }

        if ($nowUtc -gt $warmupDeadlineUtc -and $mazeDetected -and -not $isSleeping) {
            $noProgressSec = ($nowUtc - $lastMazeProgressUtc).TotalSeconds
            if ($noProgressSec -gt $mazeMaxNoProgressSec) {
                $mazeMaxNoProgressSec = $noProgressSec
            }

            $wallImpactRate = 0.0
            if ($MazeStuckNoProgressWindowSec -gt 0) {
                $wallImpactRate = $wallImpactWindow.Count / [double]$MazeStuckNoProgressWindowSec
            }

            $isStuckCandidate = (
                $noProgressSec -ge $MazeStuckNoProgressWindowSec -and
                $wallImpactRate -ge $MazeStuckWallImpactMinRatePerSec -and
                $motorDispatchThisPoll -le $MazeStuckMaxMotorDispatchPerPoll
            )

            if ($isStuckCandidate) {
                if (-not $mazeStuckCandidateActive) {
                    $mazeStuckEvents++
                    $mazeStuckCandidateActive = $true
                }
                $mazeStuckStreakSec += $pollSeconds
                if ($mazeStuckStreakSec -ge $MazeStuckFailAfterSec) {
                    $failReasons.Add(
                        "maze stuck proxy triggered: noProgress=${([int]$noProgressSec)}s, wallImpactRate=$($wallImpactRate.ToString('0.00'))/s, motorDispatchPerPoll=$motorDispatchThisPoll, sustained=${([int]$mazeStuckStreakSec)}s")
                    break
                }
            }
            else {
                $mazeStuckStreakSec = 0.0
                $mazeStuckCandidateActive = $false
            }
        }
    }

    if ($nowUtc -ge $nextSensoryUtc) {
        $visual = Invoke-SensoryStimulus -Uri $visualUrl -Payload @{
            pattern        = 'BurnInVisual'
            intensity      = 0.95
            burstCount     = 12
            targetStructure = 'V1'
            sourceStructure = 'Thalamus'
        }
        $auditory = Invoke-SensoryStimulus -Uri $auditoryUrl -Payload @{
            pattern        = 'BurnInTone'
            intensity      = 0.90
            burstCount     = 10
            targetStructure = 'A1'
            sourceStructure = 'Thalamus'
        }

        foreach ($result in @($visual, $auditory)) {
            if (-not $result.Success) {
                $sensoryDispatchErrors++
                if ($result.Status -eq 404) {
                    $sensory404Errors++
                }
            }
            elseif ($result.Delivered -le 0) {
                $sensoryZeroDelivered++
            }
        }

        if ($nowUtc -gt $warmupDeadlineUtc) {
            if ($sensory404Errors -gt $MaxSensory404) {
                $failReasons.Add("sensory dispatch 404 exceeded threshold: $sensory404Errors > $MaxSensory404")
                break
            }

            if ($sensoryZeroDelivered -gt $MaxSensoryZeroDelivered) {
                $failReasons.Add("sensory zero-delivery exceeded threshold: $sensoryZeroDelivered > $MaxSensoryZeroDelivered")
                break
            }
        }

        $nextSensoryUtc = $nowUtc.AddSeconds($SensoryIntervalSec)
    }

    if ($RestartCycleIntervalSec -gt 0 -and $nowUtc -ge $nextRestartUtc) {
        $restartCycles++
        $restartOk = $true
        try {
            $null = Invoke-RestMethod -Uri $restartSimUrl -Method Post -TimeoutSec 8
        }
        catch {
            $restartOk = $false
            $restartCycleFailures++
        }

        if ($restartOk) {
            $recoveryOk = Wait-ForRecoveryAfterRestart `
                -StateUri $stateUrl `
                -DeadlineUtc ([DateTime]::UtcNow.AddSeconds($RestartRecoveryTimeoutSec)) `
                -AllowableNonOk $AllowableNonOkServices `
                -MaxSnapshotAge $MaxSnapshotAgeTicks
            if (-not $recoveryOk) {
                $restartCycleFailures++
                if ([DateTime]::UtcNow -gt $warmupDeadlineUtc) {
                    $failReasons.Add("restart cycle $restartCycles did not recover within ${RestartRecoveryTimeoutSec}s")
                    break
                }
            }
        }
        elseif ([DateTime]::UtcNow -gt $warmupDeadlineUtc) {
            $failReasons.Add("restart cycle $restartCycles failed to request /api/v1/admin/restart-sim")
            break
        }

        $nextRestartUtc = $nowUtc.AddSeconds($RestartCycleIntervalSec)
    }

    if (($nowUtc - $lastProgressUtc).TotalSeconds -ge 5.0 -and $stateOk -and $null -ne $stateSnapshot) {
        $snapshotAgeForLog = if ($stateSnapshot.SnapshotAgeTicks -eq [long]::MaxValue) { -1 } else { $stateSnapshot.SnapshotAgeTicks }
        Write-Host ("burn-in t+{0}s tick={1} nonOK={2} snapAge={3} sensory404={4} zero={5} restarts={6}/{7} mazeProgress={8} wallImpacts={9}" -f
            [int]($nowUtc - $startedUtc).TotalSeconds,
            $stateSnapshot.Tick,
            $stateSnapshot.NonOkCount,
            $snapshotAgeForLog,
            $sensory404Errors,
            $sensoryZeroDelivered,
            $restartCycleFailures,
            $restartCycles,
            $mazeProgressTotal,
            $mazeWallImpactTotal)
        $lastProgressUtc = $nowUtc
    }

    Start-Sleep -Milliseconds $PollIntervalMs
}

$finishedUtc = [DateTime]::UtcNow
$durationObservedSec = [Math]::Round(($finishedUtc - $startedUtc).TotalSeconds, 1)
$passed = $failReasons.Count -eq 0

$summaryLines = @(
    "DNNE burn-in gate",
    "controlBaseUrl: $baseUrl",
    "startedUtc: $($startedUtc.ToString('o'))",
    "finishedUtc: $($finishedUtc.ToString('o'))",
    "durationSec: $durationObservedSec",
    "result: $(if ($passed) { 'PASS' } else { 'FAIL' })",
    "maxObservedNonOk: $maxObservedNonOk",
    "maxObservedSnapshotAgeTicks: $maxObservedSnapshotAge",
    "statePollErrors: $statePollErrors",
    "snapshotStallEvents: $snapshotStallEvents",
    "nonOkViolationEvents: $nonOkViolationEvents",
    "sensoryDispatchErrors: $sensoryDispatchErrors",
    "sensory404Errors: $sensory404Errors",
    "sensoryZeroDelivered: $sensoryZeroDelivered",
    "restartCycles: $restartCycles",
    "restartCycleFailures: $restartCycleFailures",
    "framePollErrors: $framePollErrors",
    "mazeDetected: $mazeDetected",
    "mazeWallImpactTotal: $mazeWallImpactTotal",
    "mazeProgressTotal: $mazeProgressTotal",
    "mazeMotorDispatchTotal: $mazeMotorDispatchTotal",
    "mazeMaxNoProgressSec: $([Math]::Round($mazeMaxNoProgressSec, 1))",
    "mazeStuckEvents: $mazeStuckEvents",
    "mazeStuckStreakSec: $([Math]::Round($mazeStuckStreakSec, 1))",
    "mazeStuckFailAfterSec: $MazeStuckFailAfterSec",
    "mazeStuckNoProgressWindowSec: $MazeStuckNoProgressWindowSec",
    "mazeStuckWallImpactMinRatePerSec: $MazeStuckWallImpactMinRatePerSec",
    "mazeStuckMaxMotorDispatchPerPoll: $MazeStuckMaxMotorDispatchPerPoll"
)

if ($failReasons.Count -gt 0) {
    $summaryLines += "failReasons:"
    foreach ($reason in $failReasons) {
        $summaryLines += "  - $reason"
    }
}

$summaryDir = Split-Path -Parent $SummaryPath
if (-not (Test-Path $summaryDir -PathType Container)) {
    New-Item -Path $summaryDir -ItemType Directory -Force | Out-Null
}
$samplesDir = Split-Path -Parent $SamplesPath
if (-not (Test-Path $samplesDir -PathType Container)) {
    New-Item -Path $samplesDir -ItemType Directory -Force | Out-Null
}

$summaryLines | Set-Content -Path $SummaryPath -Encoding UTF8
$samples | ConvertTo-Json -Depth 6 | Set-Content -Path $SamplesPath -Encoding UTF8

Write-Host ("Burn-in summary written: {0}" -f $SummaryPath)
Write-Host ("Burn-in samples written: {0}" -f $SamplesPath)

if (-not $passed) {
    throw ("DNNE burn-in gate failed. " + ($failReasons -join ' | '))
}

Write-Host "DNNE burn-in gate passed."
