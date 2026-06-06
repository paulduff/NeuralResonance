param(
    [string]$ControlBaseUrl = 'http://localhost:5080',
    [string[]]$Profiles = @('current'),
    [int]$DurationSec = 60,
    [int]$WarmupSec = 10,
    [int]$PollIntervalMs = 500,
    [int]$RequestTimeoutSec = 8,
    [int]$FrameMaxOutputLog = 4,
    [int]$FrameMaxSpikeLog = 4,
    [int]$FrameMaxDispatchSpikes = 96,
    [switch]$ApplyProfilesWithoutRestart = $true,
    [switch]$UseFullStateSnapshot,
    [string]$BaselinePath = '',
    [switch]$SaveBaseline,
    [double]$MinTicksPerSecond = 0.0,
    [double]$MaxStateP95Ms = 0.0,
    [double]$MaxFrameP95Ms = 0.0,
    [double]$MaxTickWallP95Ms = 0.0,
    [int]$MaxNonOkServices = -1,
    [int]$MaxSnapshotAgeTicks = -1,
    [int]$MaxDroppedSpikes = -1,
    [int]$MaxDispatchErrors = -1,
    [double]$MaxRegressionPercent = 0.0,
    [switch]$FailOnRegression,
    [string]$SummaryPath = '',
    [string]$SamplesPath = '',
    [string]$MarkdownPath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $PSCommandPath
$baseUrl = $ControlBaseUrl.TrimEnd('/')
$stateUrl = if ($UseFullStateSnapshot) { "$baseUrl/api/v1/state" } else { "$baseUrl/api/v1/performance/snapshot" }
$frameBaseUrl = "$baseUrl/api/v1/frame"
$profileUrl = "$baseUrl/api/v1/admin/perf-profile"

if ([string]::IsNullOrWhiteSpace($SummaryPath)) {
    $SummaryPath = Join-Path $scriptDir '_perf-harness-summary.json'
}
if ([string]::IsNullOrWhiteSpace($SamplesPath)) {
    $SamplesPath = Join-Path $scriptDir '_perf-harness-samples.json'
}
if ([string]::IsNullOrWhiteSpace($MarkdownPath)) {
    $MarkdownPath = Join-Path $scriptDir '_perf-harness-summary.md'
}

$DurationSec = [Math]::Max(5, $DurationSec)
$WarmupSec = [Math]::Max(0, $WarmupSec)
$PollIntervalMs = [Math]::Max(100, $PollIntervalMs)
$RequestTimeoutSec = [Math]::Max(1, $RequestTimeoutSec)
$FrameMaxOutputLog = [Math]::Max(0, $FrameMaxOutputLog)
$FrameMaxSpikeLog = [Math]::Max(0, $FrameMaxSpikeLog)
$FrameMaxDispatchSpikes = [Math]::Max(0, $FrameMaxDispatchSpikes)
$MaxRegressionPercent = [Math]::Max(0.0, $MaxRegressionPercent)
if ($null -eq $Profiles -or $Profiles.Count -eq 0) {
    $Profiles = @('current')
}

function Read-Prop {
    param(
        [object]$Object,
        [string]$Name,
        [object]$Default = $null
    )

    if ($null -eq $Object -or [string]::IsNullOrWhiteSpace($Name)) {
        return $Default
    }

    $prop = $Object.PSObject.Properties[$Name]
    if ($null -eq $prop -or $null -eq $prop.Value) {
        return $Default
    }

    return $prop.Value
}

function Read-Number {
    param(
        [object]$Object,
        [string]$Name,
        [double]$Default = 0.0
    )

    $value = Read-Prop -Object $Object -Name $Name -Default $null
    if ($null -eq $value) {
        return $Default
    }

    try {
        return [double]$value
    }
    catch {
        return $Default
    }
}

function Read-Long {
    param(
        [object]$Object,
        [string]$Name,
        [long]$Default = 0L
    )

    $value = Read-Prop -Object $Object -Name $Name -Default $null
    if ($null -eq $value) {
        return $Default
    }

    try {
        return [long]$value
    }
    catch {
        return $Default
    }
}

function Measure-Request {
    param(
        [string]$Uri,
        [string]$Method = 'GET',
        [object]$Body = $null
    )

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        if ($Method.Equals('POST', [System.StringComparison]::OrdinalIgnoreCase)) {
            $json = if ($null -eq $Body) { '{}' } else { $Body | ConvertTo-Json -Depth 12 -Compress }
            $result = Invoke-RestMethod -Uri $Uri -Method Post -ContentType 'application/json' -Body $json -TimeoutSec $RequestTimeoutSec
        }
        else {
            $result = Invoke-RestMethod -Uri $Uri -TimeoutSec $RequestTimeoutSec
        }

        $sw.Stop()
        return [pscustomobject]@{
            Ok        = $true
            LatencyMs = $sw.Elapsed.TotalMilliseconds
            Value     = $result
            Error     = ''
        }
    }
    catch {
        $sw.Stop()
        return [pscustomobject]@{
            Ok        = $false
            LatencyMs = $sw.Elapsed.TotalMilliseconds
            Value     = $null
            Error     = $_.Exception.Message
        }
    }
}

function Get-Percentile {
    param(
        [double[]]$Values,
        [double]$Percentile
    )

    if ($null -eq $Values -or $Values.Count -eq 0) {
        return 0.0
    }

    $sorted = @($Values | Sort-Object)
    if ($sorted.Count -eq 1) {
        return [double]$sorted[0]
    }

    $rank = ($Percentile / 100.0) * ($sorted.Count - 1)
    $lower = [Math]::Floor($rank)
    $upper = [Math]::Ceiling($rank)
    if ($lower -eq $upper) {
        return [double]$sorted[$lower]
    }

    $fraction = $rank - $lower
    return ([double]$sorted[$lower] * (1.0 - $fraction)) + ([double]$sorted[$upper] * $fraction)
}

function Get-Stats {
    param([double[]]$Values)

    if ($null -eq $Values -or $Values.Count -eq 0) {
        return [pscustomobject]@{
            Count = 0
            Min   = 0.0
            P50   = 0.0
            P95   = 0.0
            P99   = 0.0
            Max   = 0.0
            Mean  = 0.0
        }
    }

    $sum = 0.0
    $min = [double]::PositiveInfinity
    $max = [double]::NegativeInfinity
    foreach ($value in $Values) {
        $sum += $value
        if ($value -lt $min) { $min = $value }
        if ($value -gt $max) { $max = $value }
    }

    return [pscustomobject]@{
        Count = $Values.Count
        Min   = [Math]::Round($min, 3)
        P50   = [Math]::Round((Get-Percentile -Values $Values -Percentile 50), 3)
        P95   = [Math]::Round((Get-Percentile -Values $Values -Percentile 95), 3)
        P99   = [Math]::Round((Get-Percentile -Values $Values -Percentile 99), 3)
        Max   = [Math]::Round($max, 3)
        Mean  = [Math]::Round(($sum / $Values.Count), 3)
    }
}

function Get-Delta {
    param(
        [object]$First,
        [object]$Last,
        [string]$Name
    )

    if ($null -eq $First -or $null -eq $Last) {
        return 0
    }

    return [long]([Math]::Max(0, (Read-Long -Object $Last -Name $Name) - (Read-Long -Object $First -Name $Name)))
}

function Add-Check {
    param(
        [System.Collections.Generic.List[object]]$Checks,
        [string]$Profile,
        [string]$Metric,
        [double]$Value,
        [string]$Rule,
        [bool]$Passed,
        [string]$Severity = 'fail',
        [string]$Detail = ''
    )

    $Checks.Add([pscustomobject]@{
        Profile  = $Profile
        Metric   = $Metric
        Value    = [Math]::Round($Value, 3)
        Rule     = $Rule
        Passed   = $Passed
        Severity = $Severity
        Detail   = $Detail
    })
}

function Get-BaselineRunMap {
    param([string]$Path)

    $map = @{}
    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path $Path -PathType Leaf)) {
        return $map
    }

    try {
        $baseline = Get-Content -Path $Path -Raw | ConvertFrom-Json
        foreach ($run in @($baseline.Runs)) {
            $profile = [string](Read-Prop -Object $run -Name 'Profile' -Default '')
            if (-not [string]::IsNullOrWhiteSpace($profile)) {
                $map[$profile.ToLowerInvariant()] = $run
            }
        }
    }
    catch {
        throw "Unable to read baseline '$Path': $($_.Exception.Message)"
    }

    return $map
}

function Get-RegressionPercent {
    param(
        [double]$Current,
        [double]$Baseline,
        [switch]$HigherIsBetter
    )

    if ($Baseline -le 0.0) {
        return 0.0
    }

    if ($HigherIsBetter) {
        return (($Baseline - $Current) / $Baseline) * 100.0
    }

    return (($Current - $Baseline) / $Baseline) * 100.0
}

function Invoke-Profile {
    param([string]$Profile)

    if ([string]::IsNullOrWhiteSpace($Profile) -or $Profile.Equals('current', [System.StringComparison]::OrdinalIgnoreCase)) {
        return
    }

    $body = @{
        profile = $Profile
        restartSimulation = -not $ApplyProfilesWithoutRestart
    }
    $result = Measure-Request -Uri $profileUrl -Method 'POST' -Body $body
    if (-not $result.Ok) {
        throw "Unable to apply performance profile '$Profile': $($result.Error)"
    }
}

function Run-ProfileSample {
    param([string]$Profile)

    Invoke-Profile -Profile $Profile

    $startedUtc = [DateTime]::UtcNow
    $warmupDeadlineUtc = $startedUtc.AddSeconds($WarmupSec)
    $deadlineUtc = $startedUtc.AddSeconds($WarmupSec + $DurationSec)
    $samples = New-Object System.Collections.Generic.List[object]
    $stateErrors = 0
    $frameErrors = 0
    $transportErrors = 0
    $firstTransport = $null
    $lastTransport = $null
    $firstMeasuredTick = 0L
    $lastMeasuredTick = 0L
    $firstMeasuredUtc = [DateTime]::MinValue
    $lastMeasuredUtc = [DateTime]::MinValue
    $maxNonOk = 0
    $maxSnapshotAgeTicks = 0L
    $frameUri = "${frameBaseUrl}?include_connectome=0&max_output_log=$FrameMaxOutputLog&max_spike_log=$FrameMaxSpikeLog&max_dispatch_spikes=$FrameMaxDispatchSpikes"

    Write-Host ("Sampling profile '{0}' for {1}s after {2}s warmup..." -f $Profile, $DurationSec, $WarmupSec)

    while ([DateTime]::UtcNow -lt $deadlineUtc) {
        $nowUtc = [DateTime]::UtcNow
        $inWarmup = $nowUtc -lt $warmupDeadlineUtc
        $stateResult = Measure-Request -Uri $stateUrl
        $frameResult = Measure-Request -Uri $frameUri

        if (-not $stateResult.Ok) {
            $stateErrors++
        }
        if (-not $frameResult.Ok) {
            $frameErrors++
        }

        if ($inWarmup) {
            Start-Sleep -Milliseconds $PollIntervalMs
            continue
        }

        $state = $stateResult.Value
        $frame = $frameResult.Value
        $transport = Read-Prop -Object $state -Name 'transportStats' -Default $null
        if ($null -eq $transport) {
            $transport = Read-Prop -Object $state -Name 'transport' -Default $null
        }
        if ($null -eq $transport) {
            $transportErrors++
        }
        else {
            if ($null -eq $firstTransport) {
                $firstTransport = $transport
            }
            $lastTransport = $transport
        }

        $tick = Read-Long -Object $state -Name 'tick'
        if ($firstMeasuredTick -eq 0L -and $tick -gt 0L) {
            $firstMeasuredTick = $tick
            $firstMeasuredUtc = $nowUtc
        }
        if ($tick -gt 0L) {
            $lastMeasuredTick = $tick
            $lastMeasuredUtc = $nowUtc
        }

        $serviceCount = [int](Read-Long -Object $state -Name 'serviceCount' -Default 0)
        $nonOk = [int](Read-Long -Object $state -Name 'nonOkCount' -Default 0)
        $serviceTelemetry = Read-Prop -Object $state -Name 'serviceTelemetry' -Default $null
        if ($serviceCount -le 0 -and $null -ne $serviceTelemetry) {
            foreach ($entry in $serviceTelemetry.PSObject.Properties) {
                $serviceCount++
                $status = [string](Read-Prop -Object $entry.Value -Name 'lastStatus' -Default '')
                if (-not $status.Equals('OK', [System.StringComparison]::OrdinalIgnoreCase)) {
                    $nonOk++
                }
            }
        }

        $lastSnapshotTick = Read-Long -Object $state -Name 'lastSnapshotTick'
        $snapshotAgeTicks = Read-Long -Object $state -Name 'snapshotAgeTicks' -Default ([long]::MinValue)
        if ($snapshotAgeTicks -eq [long]::MinValue) {
            $snapshotAgeTicks = if ($lastSnapshotTick -gt 0L -and $tick -ge $lastSnapshotTick) { $tick - $lastSnapshotTick } else { -1L }
        }
        if ($nonOk -gt $maxNonOk) {
            $maxNonOk = $nonOk
        }
        if ($snapshotAgeTicks -gt $maxSnapshotAgeTicks) {
            $maxSnapshotAgeTicks = $snapshotAgeTicks
        }

        $samples.Add([pscustomobject]@{
            profile             = $Profile
            wallClockUtc        = $nowUtc.ToString('o')
            tick                = $tick
            simulationClockMs   = Read-Number -Object $state -Name 'simulationClockMs'
            stateLatencyMs      = [Math]::Round($stateResult.LatencyMs, 3)
            frameLatencyMs      = [Math]::Round($frameResult.LatencyMs, 3)
            frameBytesApprox    = if ($null -eq $frame) { 0 } else { (($frame | ConvertTo-Json -Depth 16 -Compress).Length) }
            serviceCount        = $serviceCount
            nonOkCount          = $nonOk
            snapshotAgeTicks    = $snapshotAgeTicks
            tickWallMs          = Read-Number -Object $transport -Name 'tickWallMs'
            tickWallP95Ms       = Read-Number -Object $transport -Name 'tickWallP95Ms'
            ackLatencyEwmaMs    = Read-Number -Object $transport -Name 'ackLatencyEwmaMs'
            adaptivePressure    = Read-Number -Object $transport -Name 'adaptivePressure'
            adaptiveScale       = Read-Number -Object $transport -Name 'adaptiveScale'
            generatedSpikes     = Read-Long -Object $transport -Name 'generatedSpikes'
            routedSpikes        = Read-Long -Object $transport -Name 'routedSpikes'
            deliveredSpikes     = Read-Long -Object $transport -Name 'deliveredSpikes'
            droppedSpikes       = Read-Long -Object $transport -Name 'dispatchQueueDroppedSpikes'
            dispatchErrors      = Read-Long -Object $transport -Name 'dispatchQueueDispatchErrors'
            stateOk             = $stateResult.Ok
            frameOk             = $frameResult.Ok
        })

        Start-Sleep -Milliseconds $PollIntervalMs
    }

    $finishedUtc = [DateTime]::UtcNow
    $measuredSeconds = if ($firstMeasuredUtc -ne [DateTime]::MinValue -and $lastMeasuredUtc -gt $firstMeasuredUtc) {
        ($lastMeasuredUtc - $firstMeasuredUtc).TotalSeconds
    }
    else {
        0.0
    }
    $tickDelta = [Math]::Max(0L, $lastMeasuredTick - $firstMeasuredTick)
    $ticksPerSec = if ($measuredSeconds -gt 0.0) { $tickDelta / $measuredSeconds } else { 0.0 }

    $stateLatencies = @($samples | ForEach-Object { [double]$_.stateLatencyMs })
    $frameLatencies = @($samples | ForEach-Object { [double]$_.frameLatencyMs })
    $tickWallValues = @($samples | Where-Object { $_.tickWallMs -gt 0 } | ForEach-Object { [double]$_.tickWallMs })
    $tickWallP95Values = @($samples | Where-Object { $_.tickWallP95Ms -gt 0 } | ForEach-Object { [double]$_.tickWallP95Ms })
    $ackValues = @($samples | Where-Object { $_.ackLatencyEwmaMs -gt 0 } | ForEach-Object { [double]$_.ackLatencyEwmaMs })
    $frameBytes = @($samples | Where-Object { $_.frameBytesApprox -gt 0 } | ForEach-Object { [double]$_.frameBytesApprox })

    return [pscustomobject]@{
        Profile             = $Profile
        StartedUtc          = $startedUtc.ToString('o')
        FinishedUtc         = $finishedUtc.ToString('o')
        WarmupSec           = $WarmupSec
        DurationSec         = $DurationSec
        SampleCount         = $samples.Count
        StateErrors         = $stateErrors
        FrameErrors         = $frameErrors
        TransportErrors     = $transportErrors
        FirstTick           = $firstMeasuredTick
        LastTick            = $lastMeasuredTick
        TickDelta           = $tickDelta
        MeasuredSeconds     = [Math]::Round($measuredSeconds, 3)
        TicksPerSecond      = [Math]::Round($ticksPerSec, 3)
        MaxNonOk            = $maxNonOk
        MaxSnapshotAgeTicks = $maxSnapshotAgeTicks
        StateLatencyMs      = Get-Stats -Values $stateLatencies
        FrameLatencyMs      = Get-Stats -Values $frameLatencies
        TickWallMs          = Get-Stats -Values $tickWallValues
        TickWallP95Ms       = Get-Stats -Values $tickWallP95Values
        AckLatencyEwmaMs    = Get-Stats -Values $ackValues
        FrameBytesApprox    = Get-Stats -Values $frameBytes
        TransportDelta      = [pscustomobject]@{
            GeneratedSpikes       = Get-Delta -First $firstTransport -Last $lastTransport -Name 'generatedSpikes'
            RoutedSpikes          = Get-Delta -First $firstTransport -Last $lastTransport -Name 'routedSpikes'
            DeliveredSpikes       = Get-Delta -First $firstTransport -Last $lastTransport -Name 'deliveredSpikes'
            DroppedBatches        = Get-Delta -First $firstTransport -Last $lastTransport -Name 'dispatchQueueDroppedBatches'
            DroppedSpikes         = Get-Delta -First $firstTransport -Last $lastTransport -Name 'dispatchQueueDroppedSpikes'
            DispatchErrors        = Get-Delta -First $firstTransport -Last $lastTransport -Name 'dispatchQueueDispatchErrors'
            SpontaneousGenerated  = Get-Delta -First $firstTransport -Last $lastTransport -Name 'totalSpontaneousGenerated'
            SpontaneousDelivered  = Get-Delta -First $firstTransport -Last $lastTransport -Name 'totalSpontaneousDelivered'
        }
        Samples             = $samples
    }
}

$allRuns = New-Object System.Collections.Generic.List[object]
$allSamples = New-Object System.Collections.Generic.List[object]
$overallStartedUtc = [DateTime]::UtcNow

foreach ($profile in $Profiles) {
    $run = Run-ProfileSample -Profile $profile
    $allRuns.Add($run)
    foreach ($sample in $run.Samples) {
        $allSamples.Add($sample)
    }
}

$summaryRuns = @($allRuns | ForEach-Object {
    [pscustomobject]@{
        Profile             = $_.Profile
        StartedUtc          = $_.StartedUtc
        FinishedUtc         = $_.FinishedUtc
        WarmupSec           = $_.WarmupSec
        DurationSec         = $_.DurationSec
        SampleCount         = $_.SampleCount
        StateErrors         = $_.StateErrors
        FrameErrors         = $_.FrameErrors
        TransportErrors     = $_.TransportErrors
        FirstTick           = $_.FirstTick
        LastTick            = $_.LastTick
        TickDelta           = $_.TickDelta
        MeasuredSeconds     = $_.MeasuredSeconds
        TicksPerSecond      = $_.TicksPerSecond
        MaxNonOk            = $_.MaxNonOk
        MaxSnapshotAgeTicks = $_.MaxSnapshotAgeTicks
        StateLatencyMs      = $_.StateLatencyMs
        FrameLatencyMs      = $_.FrameLatencyMs
        TickWallMs          = $_.TickWallMs
        TickWallP95Ms       = $_.TickWallP95Ms
        AckLatencyEwmaMs    = $_.AckLatencyEwmaMs
        FrameBytesApprox    = $_.FrameBytesApprox
        TransportDelta      = $_.TransportDelta
    }
})

$checks = New-Object System.Collections.Generic.List[object]
$baselineRuns = Get-BaselineRunMap -Path $BaselinePath

foreach ($run in $summaryRuns) {
    if ($MinTicksPerSecond -gt 0.0) {
        Add-Check -Checks $checks -Profile $run.Profile -Metric 'ticksPerSecond' -Value ([double]$run.TicksPerSecond) -Rule (">= {0:0.###}" -f $MinTicksPerSecond) -Passed ([double]$run.TicksPerSecond -ge $MinTicksPerSecond)
    }
    if ($MaxStateP95Ms -gt 0.0) {
        Add-Check -Checks $checks -Profile $run.Profile -Metric 'stateLatencyMs.p95' -Value ([double]$run.StateLatencyMs.P95) -Rule ("<= {0:0.###} ms" -f $MaxStateP95Ms) -Passed ([double]$run.StateLatencyMs.P95 -le $MaxStateP95Ms)
    }
    if ($MaxFrameP95Ms -gt 0.0) {
        Add-Check -Checks $checks -Profile $run.Profile -Metric 'frameLatencyMs.p95' -Value ([double]$run.FrameLatencyMs.P95) -Rule ("<= {0:0.###} ms" -f $MaxFrameP95Ms) -Passed ([double]$run.FrameLatencyMs.P95 -le $MaxFrameP95Ms)
    }
    if ($MaxTickWallP95Ms -gt 0.0) {
        Add-Check -Checks $checks -Profile $run.Profile -Metric 'tickWallMs.p95' -Value ([double]$run.TickWallMs.P95) -Rule ("<= {0:0.###} ms" -f $MaxTickWallP95Ms) -Passed ([double]$run.TickWallMs.P95 -le $MaxTickWallP95Ms)
    }
    if ($MaxNonOkServices -ge 0) {
        Add-Check -Checks $checks -Profile $run.Profile -Metric 'maxNonOk' -Value ([double]$run.MaxNonOk) -Rule ("<= {0}" -f $MaxNonOkServices) -Passed ([int]$run.MaxNonOk -le $MaxNonOkServices)
    }
    if ($MaxSnapshotAgeTicks -ge 0) {
        Add-Check -Checks $checks -Profile $run.Profile -Metric 'maxSnapshotAgeTicks' -Value ([double]$run.MaxSnapshotAgeTicks) -Rule ("<= {0}" -f $MaxSnapshotAgeTicks) -Passed ([long]$run.MaxSnapshotAgeTicks -le $MaxSnapshotAgeTicks)
    }
    if ($MaxDroppedSpikes -ge 0) {
        Add-Check -Checks $checks -Profile $run.Profile -Metric 'droppedSpikes' -Value ([double]$run.TransportDelta.DroppedSpikes) -Rule ("<= {0}" -f $MaxDroppedSpikes) -Passed ([long]$run.TransportDelta.DroppedSpikes -le $MaxDroppedSpikes)
    }
    if ($MaxDispatchErrors -ge 0) {
        Add-Check -Checks $checks -Profile $run.Profile -Metric 'dispatchErrors' -Value ([double]$run.TransportDelta.DispatchErrors) -Rule ("<= {0}" -f $MaxDispatchErrors) -Passed ([long]$run.TransportDelta.DispatchErrors -le $MaxDispatchErrors)
    }

    if ($MaxRegressionPercent -gt 0.0 -and -not [string]::IsNullOrWhiteSpace($BaselinePath)) {
        $key = ([string]$run.Profile).ToLowerInvariant()
        if ($baselineRuns.ContainsKey($key)) {
            $baseline = $baselineRuns[$key]
            $comparisons = @(
                [pscustomobject]@{
                    Metric = 'ticksPerSecond'
                    Current = [double]$run.TicksPerSecond
                    Baseline = [double](Read-Number -Object $baseline -Name 'TicksPerSecond')
                    HigherIsBetter = $true
                    Unit = '% slower'
                },
                [pscustomobject]@{
                    Metric = 'stateLatencyMs.p95'
                    Current = [double]$run.StateLatencyMs.P95
                    Baseline = [double](Read-Number -Object (Read-Prop -Object $baseline -Name 'StateLatencyMs') -Name 'P95')
                    HigherIsBetter = $false
                    Unit = '% slower'
                },
                [pscustomobject]@{
                    Metric = 'frameLatencyMs.p95'
                    Current = [double]$run.FrameLatencyMs.P95
                    Baseline = [double](Read-Number -Object (Read-Prop -Object $baseline -Name 'FrameLatencyMs') -Name 'P95')
                    HigherIsBetter = $false
                    Unit = '% slower'
                },
                [pscustomobject]@{
                    Metric = 'tickWallMs.p95'
                    Current = [double]$run.TickWallMs.P95
                    Baseline = [double](Read-Number -Object (Read-Prop -Object $baseline -Name 'TickWallMs') -Name 'P95')
                    HigherIsBetter = $false
                    Unit = '% slower'
                }
            )

            foreach ($comparison in $comparisons) {
                if ($comparison.Baseline -le 0.0) {
                    continue
                }

                $regression = Get-RegressionPercent -Current $comparison.Current -Baseline $comparison.Baseline -HigherIsBetter:([bool]$comparison.HigherIsBetter)
                $passed = $regression -le $MaxRegressionPercent
                Add-Check `
                    -Checks $checks `
                    -Profile $run.Profile `
                    -Metric ("baseline.{0}" -f $comparison.Metric) `
                    -Value $regression `
                    -Rule ("<= {0:0.###}{1}" -f $MaxRegressionPercent, $comparison.Unit) `
                    -Passed $passed `
                    -Severity ($(if ($FailOnRegression) { 'fail' } else { 'warn' })) `
                    -Detail ("current {0:0.###}, baseline {1:0.###}" -f $comparison.Current, $comparison.Baseline)
            }
        }
        else {
            Add-Check -Checks $checks -Profile $run.Profile -Metric 'baseline.profile' -Value 0 -Rule 'profile exists in baseline' -Passed $false -Severity ($(if ($FailOnRegression) { 'fail' } else { 'warn' })) -Detail ("No baseline run found for profile '{0}'." -f $run.Profile)
        }
    }
}

$failedChecks = @($checks | Where-Object { -not $_.Passed -and $_.Severity -eq 'fail' })
$warningChecks = @($checks | Where-Object { -not $_.Passed -and $_.Severity -ne 'fail' })
$result = if ($failedChecks.Count -eq 0) { 'PASS' } else { 'FAIL' }

$summary = [pscustomobject]@{
    Tool                = 'DNNE performance harness'
    ControlBaseUrl      = $baseUrl
    StartedUtc          = $overallStartedUtc.ToString('o')
    FinishedUtc         = ([DateTime]::UtcNow).ToString('o')
    Result              = $result
    Profiles            = @($Profiles)
    DurationSec         = $DurationSec
    WarmupSec           = $WarmupSec
    PollIntervalMs      = $PollIntervalMs
    StateSource         = $stateUrl
    UseFullStateSnapshot = $UseFullStateSnapshot.IsPresent
    BaselinePath        = $BaselinePath
    SaveBaseline        = $SaveBaseline.IsPresent
    Thresholds          = [pscustomobject]@{
        MinTicksPerSecond  = $MinTicksPerSecond
        MaxStateP95Ms      = $MaxStateP95Ms
        MaxFrameP95Ms      = $MaxFrameP95Ms
        MaxTickWallP95Ms   = $MaxTickWallP95Ms
        MaxNonOkServices   = $MaxNonOkServices
        MaxSnapshotAgeTicks = $MaxSnapshotAgeTicks
        MaxDroppedSpikes   = $MaxDroppedSpikes
        MaxDispatchErrors  = $MaxDispatchErrors
        MaxRegressionPercent = $MaxRegressionPercent
        FailOnRegression   = $FailOnRegression.IsPresent
    }
    FrameQuery          = [pscustomobject]@{
        IncludeConnectome      = $false
        MaxOutputLog           = $FrameMaxOutputLog
        MaxSpikeLog            = $FrameMaxSpikeLog
        MaxDispatchSpikes      = $FrameMaxDispatchSpikes
    }
    Checks              = @($checks.ToArray())
    FailedCheckCount    = $failedChecks.Count
    WarningCheckCount   = $warningChecks.Count
    Runs                = @($summaryRuns)
}

$summary | ConvertTo-Json -Depth 20 | Set-Content -Path $SummaryPath -Encoding UTF8
$allSamples | ConvertTo-Json -Depth 20 | Set-Content -Path $SamplesPath -Encoding UTF8
if ($SaveBaseline -and -not [string]::IsNullOrWhiteSpace($BaselinePath)) {
    $summary | ConvertTo-Json -Depth 20 | Set-Content -Path $BaselinePath -Encoding UTF8
}

$md = New-Object System.Collections.Generic.List[string]
$md.Add('# DNNE Performance Harness')
$md.Add('')
$md.Add(('Control: `{0}`' -f $baseUrl))
$md.Add(('State source: `{0}`' -f $stateUrl))
$md.Add(('Started: `{0}`' -f $summary.StartedUtc))
$md.Add(('Finished: `{0}`' -f $summary.FinishedUtc))
$md.Add(('Result: **{0}**' -f $summary.Result))
$md.Add('')
$md.Add('| Profile | Ticks/sec | Tick wall p95 ms | State p95 ms | Frame p95 ms | Max non-OK | Max snapshot age | Drops | Dispatch errors |')
$md.Add('|---|---:|---:|---:|---:|---:|---:|---:|---:|')
foreach ($run in $summaryRuns) {
    $md.Add(('| {0} | {1:0.000} | {2:0.000} | {3:0.000} | {4:0.000} | {5} | {6} | {7} | {8} |' -f
        $run.Profile,
        [double]$run.TicksPerSecond,
        [double]$run.TickWallP95Ms.P95,
        [double]$run.StateLatencyMs.P95,
        [double]$run.FrameLatencyMs.P95,
        [int]$run.MaxNonOk,
        [long]$run.MaxSnapshotAgeTicks,
        [long]$run.TransportDelta.DroppedSpikes,
        [long]$run.TransportDelta.DispatchErrors))
}
$md.Add('')
$md.Add('## Checks')
$md.Add('')
if ($checks.Count -eq 0) {
    $md.Add('No thresholds or baseline comparisons were requested.')
}
else {
    $md.Add('| Status | Severity | Profile | Metric | Value | Rule | Detail |')
    $md.Add('|---|---|---|---|---:|---|---|')
    foreach ($check in $checks) {
        $status = if ($check.Passed) { 'PASS' } else { 'FAIL' }
        $md.Add(('| {0} | {1} | {2} | {3} | {4:0.###} | {5} | {6} |' -f
            $status,
            $check.Severity,
            $check.Profile,
            $check.Metric,
            [double]$check.Value,
            $check.Rule,
            $check.Detail))
    }
}
$md.Add('')
$md.Add('Artifacts:')
$md.Add(('- Summary JSON: `{0}`' -f $SummaryPath))
$md.Add(('- Samples JSON: `{0}`' -f $SamplesPath))
if ($SaveBaseline -and -not [string]::IsNullOrWhiteSpace($BaselinePath)) {
    $md.Add(('- Saved baseline: `{0}`' -f $BaselinePath))
}
$md | Set-Content -Path $MarkdownPath -Encoding UTF8

Write-Host ''
Write-Host 'Performance harness complete.'
Write-Host ("Result: {0}" -f $result)
Write-Host ("Summary: {0}" -f $SummaryPath)
Write-Host ("Samples: {0}" -f $SamplesPath)
Write-Host ("Markdown: {0}" -f $MarkdownPath)
if ($SaveBaseline -and -not [string]::IsNullOrWhiteSpace($BaselinePath)) {
    Write-Host ("Saved baseline: {0}" -f $BaselinePath)
}
foreach ($run in $summaryRuns) {
    Write-Host ("{0}: {1:0.000} ticks/sec, tick-wall p95={2:0.000}ms, state p95={3:0.000}ms, frame p95={4:0.000}ms" -f
        $run.Profile,
        [double]$run.TicksPerSecond,
        [double]$run.TickWallP95Ms.P95,
        [double]$run.StateLatencyMs.P95,
        [double]$run.FrameLatencyMs.P95)
}
if ($failedChecks.Count -gt 0) {
    Write-Host ("Failed checks: {0}" -f $failedChecks.Count)
    exit 2
}
