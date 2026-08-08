param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$ControlBaseUrl = 'http://localhost:5080',
    [int]$DurationSec = 300,
    [int]$WarmupSec = 20,
    [int]$PollIntervalMs = 1000,
    [double]$MinimumDistance = 1.0,
    [int]$MinimumVisitedCells = 2,
    [string]$StatePath = '',
    [string]$SummaryPath = '',
    [string]$SamplesPath = '',
    [switch]$NoStart,
    [switch]$AllowNoInteraction,
    [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $PSCommandPath
$repoRoot = (Resolve-Path (Join-Path $scriptDir '..')).Path
$logRoot = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)) 'NRE.WpfWorldSim'
if ([string]::IsNullOrWhiteSpace($StatePath)) {
    $StatePath = Join-Path $logRoot 'worldsim-state.json'
}
if ([string]::IsNullOrWhiteSpace($SummaryPath)) {
    $SummaryPath = Join-Path $repoRoot 'artifacts\worldsim-qualification-summary.txt'
}
if ([string]::IsNullOrWhiteSpace($SamplesPath)) {
    $SamplesPath = Join-Path $repoRoot 'artifacts\worldsim-qualification-samples.json'
}

$DurationSec = [Math]::Max(30, $DurationSec)
$WarmupSec = [Math]::Min([Math]::Max(0, $DurationSec - 5), [Math]::Max(0, $WarmupSec))
$PollIntervalMs = [Math]::Min(5000, [Math]::Max(250, $PollIntervalMs))
$StatePath = [System.IO.Path]::GetFullPath($StatePath)

function Get-WorldSimProcess {
    $candidates = @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
        Where-Object {
            $_.ProcessId -ne $PID -and
            $_.Name -notin @('powershell.exe', 'pwsh.exe') -and
            -not [string]::IsNullOrWhiteSpace($_.CommandLine) -and
            $_.CommandLine -match 'NRE\.WpfWorldSim'
        })

    $application = $candidates |
        Where-Object { $_.Name -eq 'NRE.WpfWorldSim.exe' } |
        Select-Object -First 1
    if ($null -ne $application) {
        return $application
    }

    return $candidates | Select-Object -First 1
}

function Read-WorldState {
    if (-not (Test-Path -LiteralPath $StatePath -PathType Leaf)) {
        return $null
    }

    try {
        return Get-Content -LiteralPath $StatePath -Raw | ConvertFrom-Json
    }
    catch {
        return $null
    }
}

function Get-LongValue {
    param([object]$Object, [string]$Name)
    if ($null -eq $Object) { return 0L }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) { return 0L }
    return [long]$property.Value
}

function Get-DoubleValue {
    param([object]$Object, [string]$Name)
    if ($null -eq $Object) { return 0.0 }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) { return 0.0 }
    return [double]$property.Value
}

$startedByHarness = $false
$worldProcess = Get-WorldSimProcess
if ($null -eq $worldProcess) {
    if ($NoStart) {
        throw 'WorldSim is not running and -NoStart was specified.'
    }

    Write-Host 'Starting visible DNNE WorldSim for embodied qualification...'
    $arguments = @{
        Configuration = $Configuration
        ControlBaseUrl = $ControlBaseUrl
        StatePath = $StatePath
    }
    if ($NoBuild) {
        $arguments.NoBuild = $true
    }
    & (Join-Path $scriptDir 'start-world-sim.ps1') @arguments
    $startedByHarness = $true
}

$readyDeadline = [DateTimeOffset]::UtcNow.AddSeconds([Math]::Max(30, $WarmupSec + 15))
$initial = $null
while ([DateTimeOffset]::UtcNow -lt $readyDeadline) {
    $worldProcess = Get-WorldSimProcess
    $candidate = Read-WorldState
    if ($null -ne $worldProcess -and
        $null -ne $candidate -and
        [string]$candidate.protocolVersion -eq 'dnne.worldsim.state.v1' -and
        [int]$candidate.processId -eq [int]$worldProcess.ProcessId -and
        [bool]$candidate.running -and
        [bool]$candidate.worldReady) {
        $initial = $candidate
        break
    }
    Start-Sleep -Milliseconds 500
}
if ($null -eq $initial) {
    throw "WorldSim did not publish a ready state at $StatePath"
}

$startedUtc = [DateTimeOffset]::UtcNow
$deadlineUtc = $startedUtc.AddSeconds($DurationSec)
$warmupDeadlineUtc = $startedUtc.AddSeconds($WarmupSec)
$sessionId = [string]$initial.sessionId
$samples = New-Object System.Collections.Generic.List[object]
$failReasons = New-Object System.Collections.Generic.List[string]
$staleStreak = 0
$disconnectedStreak = 0
$last = $initial

Write-Host ("WorldSim qualification for {0}s; visible process pid {1}" -f $DurationSec, $initial.processId)
while ([DateTimeOffset]::UtcNow -lt $deadlineUtc) {
    Start-Sleep -Milliseconds $PollIntervalMs
    $now = [DateTimeOffset]::UtcNow
    $worldProcess = Get-WorldSimProcess
    if ($null -eq $worldProcess) {
        $failReasons.Add('WorldSim process exited during qualification.')
        break
    }

    $state = Read-WorldState
    if ($null -eq $state) {
        $staleStreak++
        if ($now -gt $warmupDeadlineUtc -and $staleStreak -ge 5) {
            $failReasons.Add('WorldSim state stream could not be read for five consecutive polls.')
            break
        }
        continue
    }

    if ([string]$state.sessionId -ne $sessionId) {
        $failReasons.Add('WorldSim session changed during qualification; evidence from different worlds cannot be combined.')
        break
    }
    if ([int]$state.processId -ne [int]$worldProcess.ProcessId) {
        $failReasons.Add('WorldSim state stream no longer belongs to the visible process.')
        break
    }
    if ([int]$state.seed -ne [int]$initial.seed) {
        $failReasons.Add('WorldSim world seed changed during qualification; evidence across resets cannot be combined.')
        break
    }

    $last = $state
    $generated = [DateTimeOffset]::Parse([string]$state.generatedUtc)
    $ageSeconds = ($now - $generated).TotalSeconds
    if ($ageSeconds -gt 5.0 -or -not [bool]$state.running) {
        $staleStreak++
    }
    else {
        $staleStreak = 0
    }

    if (-not [bool]$state.brainConnected) {
        $disconnectedStreak++
    }
    else {
        $disconnectedStreak = 0
    }

    if ($now -gt $warmupDeadlineUtc) {
        if ($staleStreak -ge 5) {
            $failReasons.Add('WorldSim state stream became stale.')
            break
        }
        if ($disconnectedStreak -ge 10) {
            $failReasons.Add('WorldSim had no fresh Control Program telemetry for ten consecutive polls.')
            break
        }
    }

    $samples.Add([pscustomobject]@{
        wallClockUtc = $now.ToString('o')
        stateAgeSeconds = [Math]::Round($ageSeconds, 3)
        brainConnected = [bool]$state.brainConnected
        distanceTravelled = [double]$state.distanceTravelled
        visitedTerrainCells = [int]$state.visitedTerrainCells
        neuronalMotorDispatchTotal = [long]$state.neuronalMotorDispatchTotal
        neuronalLocomotorDispatchTotal = Get-LongValue $state 'neuronalLocomotorDispatchTotal'
        neuronalManipulatorDispatchTotal = Get-LongValue $state 'neuronalManipulatorDispatchTotal'
        interactionAttempts = [long]$state.interactionAttempts
        interactionSuccesses = [long]$state.interactionSuccesses
        retinalFramesAccepted = [long]$state.retinalFramesAccepted
        cochlearFramesAccepted = [long]$state.cochlearFramesAccepted
        physicalBodyFramesAccepted = [long]$state.physicalBodyFramesAccepted
        somaticFramesAccepted = [long]$state.somaticFramesAccepted
        storedEnergyJoules = [double]$state.storedEnergyJoules
        tissueIntegrityFraction = [double]$state.tissueIntegrityFraction
        hydrationFraction = [double]$state.hydrationFraction
        tickFailures = [long]$state.tickFailures
    })

    if (([int]($now - $startedUtc).TotalSeconds % 5) -eq 0) {
        Write-Host ("world t+{0}s motor={1} locomotor={2} manipulator={3} distance={4:0.00} cells={5} retinal={6} cochlear={7} body={8} somatic={9} interactions={10}/{11}" -f
            [int]($now - $startedUtc).TotalSeconds,
            ([long]$state.neuronalMotorDispatchTotal - [long]$initial.neuronalMotorDispatchTotal),
            ((Get-LongValue $state 'neuronalLocomotorDispatchTotal') - (Get-LongValue $initial 'neuronalLocomotorDispatchTotal')),
            ((Get-LongValue $state 'neuronalManipulatorDispatchTotal') - (Get-LongValue $initial 'neuronalManipulatorDispatchTotal')),
            ([double]$state.distanceTravelled - [double]$initial.distanceTravelled),
            ([int]$state.visitedTerrainCells - [int]$initial.visitedTerrainCells),
            ([long]$state.retinalFramesAccepted - [long]$initial.retinalFramesAccepted),
            ([long]$state.cochlearFramesAccepted - [long]$initial.cochlearFramesAccepted),
            ([long]$state.physicalBodyFramesAccepted - [long]$initial.physicalBodyFramesAccepted),
            ([long]$state.somaticFramesAccepted - [long]$initial.somaticFramesAccepted),
            ([long]$state.interactionSuccesses - [long]$initial.interactionSuccesses),
            ([long]$state.interactionAttempts - [long]$initial.interactionAttempts))
    }
}

$motorDelta = (Get-LongValue $last 'neuronalMotorDispatchTotal') - (Get-LongValue $initial 'neuronalMotorDispatchTotal')
$locomotorDelta = (Get-LongValue $last 'neuronalLocomotorDispatchTotal') - (Get-LongValue $initial 'neuronalLocomotorDispatchTotal')
$manipulatorDelta = (Get-LongValue $last 'neuronalManipulatorDispatchTotal') - (Get-LongValue $initial 'neuronalManipulatorDispatchTotal')
$distanceDelta = (Get-DoubleValue $last 'distanceTravelled') - (Get-DoubleValue $initial 'distanceTravelled')
$visitedDelta = (Get-LongValue $last 'visitedTerrainCells') - (Get-LongValue $initial 'visitedTerrainCells')
$interactionAttemptDelta = (Get-LongValue $last 'interactionAttempts') - (Get-LongValue $initial 'interactionAttempts')
$interactionSuccessDelta = (Get-LongValue $last 'interactionSuccesses') - (Get-LongValue $initial 'interactionSuccesses')
$retinalDelta = (Get-LongValue $last 'retinalFramesAccepted') - (Get-LongValue $initial 'retinalFramesAccepted')
$cochlearDelta = (Get-LongValue $last 'cochlearFramesAccepted') - (Get-LongValue $initial 'cochlearFramesAccepted')
$bodyDelta = (Get-LongValue $last 'physicalBodyFramesAccepted') - (Get-LongValue $initial 'physicalBodyFramesAccepted')
$somaticDelta = (Get-LongValue $last 'somaticFramesAccepted') - (Get-LongValue $initial 'somaticFramesAccepted')
$tickFailureDelta = (Get-LongValue $last 'tickFailures') - (Get-LongValue $initial 'tickFailures')

if ($motorDelta -le 0) { $failReasons.Add('No neuronal motor dispatch reached WorldSim.') }
if ($locomotorDelta -le 0) { $failReasons.Add('No neuronal locomotor population dispatch reached WorldSim.') }
if ($distanceDelta -lt $MinimumDistance -and $visitedDelta -lt $MinimumVisitedCells) {
    $failReasons.Add("WorldSim showed insufficient embodied movement: distance=$($distanceDelta.ToString('0.000')), visitedDelta=$visitedDelta")
}
if ($retinalDelta -le 0) { $failReasons.Add('No rendered retinal frame was accepted.') }
if ($cochlearDelta -le 0) { $failReasons.Add('No rendered cochlear frame was accepted.') }
if ($bodyDelta -le 0) { $failReasons.Add('No physical body frame was accepted.') }
if ($somaticDelta -le 0) { $failReasons.Add('No somatic frame was accepted.') }
if (-not $AllowNoInteraction -and $interactionAttemptDelta -le 0) {
    $failReasons.Add('The neuronal manipulator lane produced no physical interaction attempt.')
}
if ($tickFailureDelta -gt 0) { $failReasons.Add("WorldSim recorded $tickFailureDelta tick failures.") }

$passed = $failReasons.Count -eq 0
$finishedUtc = [DateTimeOffset]::UtcNow
$invariant = [System.Globalization.CultureInfo]::InvariantCulture
$summary = @(
    'DNNE WorldSim embodied qualification',
    "result: $(if ($passed) { 'PASS' } else { 'FAIL' })",
    "startedUtc: $($startedUtc.ToString('o'))",
    "finishedUtc: $($finishedUtc.ToString('o'))",
    "startedByHarness: $startedByHarness",
    'worldDetected: True',
    "worldProcessId: $($last.processId)",
    "worldMotorDispatchTotal: $motorDelta",
    "worldLocomotorDispatchTotal: $locomotorDelta",
    "worldManipulatorDispatchTotal: $manipulatorDelta",
    "worldDistanceTravelledDelta: $($distanceDelta.ToString('0.####', $invariant))",
    "worldVisitedTerrainDelta: $visitedDelta",
    "worldInteractionAttemptDelta: $interactionAttemptDelta",
    "worldInteractionSuccessDelta: $interactionSuccessDelta",
    "worldRetinalAcceptedDelta: $retinalDelta",
    "worldCochlearAcceptedDelta: $cochlearDelta",
    "worldPhysicalBodyAcceptedDelta: $bodyDelta",
    "worldSomaticAcceptedDelta: $somaticDelta",
    "worldTickFailureDelta: $tickFailureDelta",
    "statePath: $StatePath"
)
if ($failReasons.Count -gt 0) {
    $summary += 'failReasons:'
    foreach ($reason in $failReasons) { $summary += "  - $reason" }
}

foreach ($path in @($SummaryPath, $SamplesPath)) {
    $directory = Split-Path -Parent $path
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }
}
$summary | Set-Content -LiteralPath $SummaryPath -Encoding UTF8
$samples | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $SamplesPath -Encoding UTF8

Write-Host "WorldSim summary: $SummaryPath"
Write-Host "WorldSim samples: $SamplesPath"
if (-not $passed) {
    throw ('WorldSim embodied qualification failed. ' + ($failReasons -join ' | '))
}

Write-Host 'WorldSim embodied qualification passed.'
