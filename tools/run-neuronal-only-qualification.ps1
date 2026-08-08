param(
    [ValidateSet('Preflight', 'Live')]
    [string]$Mode = 'Preflight',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$ControlBaseUrl = 'http://localhost:5080',
    [int]$LiveDurationSec = 300,
    [int]$CorticalEpochs = 24,
    [string]$OutputDirectory = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $PSCommandPath
$repoRoot = (Resolve-Path (Join-Path $scriptDir '..')).Path
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot 'artifacts\neuronal-only-qualification'
}

$stamp = [DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmss')
$runDirectory = Join-Path $OutputDirectory $stamp
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
$steps = New-Object System.Collections.Generic.List[object]

function Invoke-QualificationStep {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [scriptblock]$Action
    )

    $safeName = ($Name -replace '[^A-Za-z0-9]+', '-').Trim('-').ToLowerInvariant()
    $logPath = Join-Path $runDirectory "$safeName.log"
    $started = [DateTimeOffset]::UtcNow
    $passed = $false
    $detail = ''
    Write-Host ""
    Write-Host ("== {0} ==" -f $Name)
    try {
        & $Action *>&1 | Tee-Object -FilePath $logPath | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw ("step returned exit code {0}" -f $LASTEXITCODE)
        }

        $passed = $true
        $detail = 'completed successfully'
    }
    catch {
        $detail = $_.Exception.Message
        $detail | Add-Content -Path $logPath -Encoding UTF8
    }

    $finished = [DateTimeOffset]::UtcNow
    $steps.Add([pscustomobject]@{
        name = $Name
        passed = $passed
        detail = $detail
        startedUtc = $started.ToString('o')
        finishedUtc = $finished.ToString('o')
        durationSeconds = [Math]::Round(($finished - $started).TotalSeconds, 3)
        logPath = $logPath
    })
    return $passed
}

function Read-SummaryValue {
    param(
        [string]$Path,
        [string]$Name,
        [string]$Fallback = ''
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $Fallback
    }

    $line = Get-Content -LiteralPath $Path | Where-Object { $_ -like "${Name}:*" } | Select-Object -Last 1
    if ([string]::IsNullOrWhiteSpace($line)) {
        return $Fallback
    }

    return $line.Substring($line.IndexOf(':') + 1).Trim()
}

function Get-MachineEvidence {
    $processorName = [Environment]::GetEnvironmentVariable('PROCESSOR_IDENTIFIER')
    $logicalProcessors = [Environment]::ProcessorCount
    $totalMemoryBytes = 0L
    $osCaption = [Environment]::OSVersion.VersionString
    try {
        $processor = Get-CimInstance Win32_Processor | Select-Object -First 1
        $computer = Get-CimInstance Win32_ComputerSystem
        $operatingSystem = Get-CimInstance Win32_OperatingSystem
        if ($null -ne $processor) {
            $processorName = [string]$processor.Name
        }
        if ($null -ne $computer) {
            $totalMemoryBytes = [long]$computer.TotalPhysicalMemory
        }
        if ($null -ne $operatingSystem) {
            $osCaption = [string]$operatingSystem.Caption
        }
    }
    catch {
        # Environment values still provide a portable minimum record.
    }

    [pscustomobject]@{
        processor = $processorName
        logicalProcessors = $logicalProcessors
        totalMemoryBytes = $totalMemoryBytes
        operatingSystem = $osCaption
        dotnetVersion = (& dotnet --version)
    }
}

Push-Location $repoRoot
try {
    $commit = (& git rev-parse HEAD).Trim()
    $dirty = -not [string]::IsNullOrWhiteSpace((& git status --short | Out-String).Trim())
    $testProject = Join-Path $repoRoot 'tests\NeuralResonanceEngine.DNNE.Tests\NeuralResonanceEngine.DNNE.Tests.csproj'
    $testResultsDirectory = Join-Path $runDirectory 'tests'
    New-Item -ItemType Directory -Path $testResultsDirectory -Force | Out-Null
    $testFilter = 'FullyQualifiedName~NeuronalMotorControlTests|FullyQualifiedName~NeuronalActionSelectionTests|FullyQualifiedName~NeuronalLanguageGroundingTests|FullyQualifiedName~NeuronalCognitionAuthorityTests|FullyQualifiedName~AvatarKinematicsTests|FullyQualifiedName~AvatarServiceTests|FullyQualifiedName~AvatarNervousSystemTests|FullyQualifiedName~AvatarPhysicalInteractionTests|FullyQualifiedName~HostSomaticAuthorityBoundaryTests|FullyQualifiedName~HostSurvivalAuthorityBoundaryTests|FullyQualifiedName~HostStructuredLanguageAuthorityBoundaryTests|FullyQualifiedName~SimulatorAuthorityBoundaryTests'

    $testsPassed = Invoke-QualificationStep -Name 'neuronal causal and authority tests' -Action {
        & dotnet test $testProject -c $Configuration --nologo --verbosity minimal --filter $testFilter --logger "trx;LogFileName=neuronal-preflight.trx" --results-directory $testResultsDirectory
    }

    $auditRelativePath = "artifacts\neuronal-only-qualification\$stamp\circuit-audit.md"
    $auditPassed = Invoke-QualificationStep -Name 'circuit audit' -Action {
        & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $scriptDir 'audit-dnne-circuits.ps1') -OutputPath $auditRelativePath
    }

    $corticalOutput = Join-Path $runDirectory 'cortical'
    $corticalPassed = Invoke-QualificationStep -Name 'cortical functional benchmark' -Action {
        & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $scriptDir 'run-cortical-functional-benchmark.ps1') -Epochs $CorticalEpochs -OutputDirectory $corticalOutput
    }

    $liveRequested = $Mode -eq 'Live'
    $liveGatePassed = $false
    $worldDetected = $false
    [long]$worldMotorDispatchTotal = 0
    [long]$worldLocomotorDispatchTotal = 0
    [long]$worldManipulatorDispatchTotal = 0
    [double]$worldDistanceTravelledDelta = 0.0
    [long]$worldVisitedTerrainDelta = 0
    [long]$worldInteractionAttemptDelta = 0
    [long]$worldInteractionSuccessDelta = 0
    [long]$worldRetinalAcceptedDelta = 0
    [long]$worldCochlearAcceptedDelta = 0
    [long]$worldPhysicalBodyAcceptedDelta = 0
    [long]$worldSomaticAcceptedDelta = 0
    [long]$worldTickFailureDelta = 0
    $burnInSummary = Join-Path $runDirectory 'live-burnin-summary.txt'
    $burnInSamples = Join-Path $runDirectory 'live-burnin-samples.json'

    if ($liveRequested) {
        $validationPassed = Invoke-QualificationStep -Name 'live runtime validation' -Action {
            & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $scriptDir 'validate-dnne.ps1') -BaseUrl $ControlBaseUrl -TimeoutSec 60 -RequireValid
        }

        $burnInPassed = $false
        if ($validationPassed) {
            $burnInPassed = Invoke-QualificationStep -Name 'live neuronal WorldSim burn-in' -Action {
                & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $scriptDir 'burnin-worldsim.ps1') -Configuration $Configuration -ControlBaseUrl $ControlBaseUrl -DurationSec ([Math]::Max(60, $LiveDurationSec)) -SummaryPath $burnInSummary -SamplesPath $burnInSamples
            }
        }

        $worldDetected = (Read-SummaryValue -Path $burnInSummary -Name 'worldDetected' -Fallback 'False') -eq 'True'
        [void][long]::TryParse((Read-SummaryValue -Path $burnInSummary -Name 'worldMotorDispatchTotal' -Fallback '0'), [ref]$worldMotorDispatchTotal)
        [void][long]::TryParse((Read-SummaryValue -Path $burnInSummary -Name 'worldLocomotorDispatchTotal' -Fallback '0'), [ref]$worldLocomotorDispatchTotal)
        [void][long]::TryParse((Read-SummaryValue -Path $burnInSummary -Name 'worldManipulatorDispatchTotal' -Fallback '0'), [ref]$worldManipulatorDispatchTotal)
        [void][double]::TryParse(
            (Read-SummaryValue -Path $burnInSummary -Name 'worldDistanceTravelledDelta' -Fallback '0'),
            [System.Globalization.NumberStyles]::Float,
            [System.Globalization.CultureInfo]::InvariantCulture,
            [ref]$worldDistanceTravelledDelta)
        [void][long]::TryParse((Read-SummaryValue -Path $burnInSummary -Name 'worldVisitedTerrainDelta' -Fallback '0'), [ref]$worldVisitedTerrainDelta)
        [void][long]::TryParse((Read-SummaryValue -Path $burnInSummary -Name 'worldInteractionAttemptDelta' -Fallback '0'), [ref]$worldInteractionAttemptDelta)
        [void][long]::TryParse((Read-SummaryValue -Path $burnInSummary -Name 'worldInteractionSuccessDelta' -Fallback '0'), [ref]$worldInteractionSuccessDelta)
        [void][long]::TryParse((Read-SummaryValue -Path $burnInSummary -Name 'worldRetinalAcceptedDelta' -Fallback '0'), [ref]$worldRetinalAcceptedDelta)
        [void][long]::TryParse((Read-SummaryValue -Path $burnInSummary -Name 'worldCochlearAcceptedDelta' -Fallback '0'), [ref]$worldCochlearAcceptedDelta)
        [void][long]::TryParse((Read-SummaryValue -Path $burnInSummary -Name 'worldPhysicalBodyAcceptedDelta' -Fallback '0'), [ref]$worldPhysicalBodyAcceptedDelta)
        [void][long]::TryParse((Read-SummaryValue -Path $burnInSummary -Name 'worldSomaticAcceptedDelta' -Fallback '0'), [ref]$worldSomaticAcceptedDelta)
        [void][long]::TryParse((Read-SummaryValue -Path $burnInSummary -Name 'worldTickFailureDelta' -Fallback '0'), [ref]$worldTickFailureDelta)
        $worldProgressObserved = $worldDistanceTravelledDelta -ge 1.0 -or $worldVisitedTerrainDelta -ge 2
        $liveGatePassed = $validationPassed -and $burnInPassed -and $worldDetected -and
            $worldMotorDispatchTotal -gt 0 -and $worldLocomotorDispatchTotal -gt 0 -and $worldProgressObserved -and
            $worldInteractionAttemptDelta -gt 0 -and $worldRetinalAcceptedDelta -gt 0 -and
            $worldCochlearAcceptedDelta -gt 0 -and $worldPhysicalBodyAcceptedDelta -gt 0 -and
            $worldSomaticAcceptedDelta -gt 0 -and $worldTickFailureDelta -eq 0
    }

    $preflightPassed = $testsPassed -and $auditPassed -and $corticalPassed
    $embodiedQualified = $preflightPassed -and $liveRequested -and $liveGatePassed
    $status = if ($embodiedQualified) {
        'PASS'
    }
    elseif ($preflightPassed -and -not $liveRequested) {
        'PREFLIGHT_PASS_LIVE_REQUIRED'
    }
    else {
        'FAIL'
    }

    $result = [pscustomobject]@{
        protocolVersion = 'dnne.neuronal-only-qualification.v2'
        generatedUtc = [DateTimeOffset]::UtcNow.ToString('o')
        mode = $Mode
        status = $status
        preflightPassed = $preflightPassed
        liveRequested = $liveRequested
        liveGatePassed = $liveGatePassed
        embodiedQualified = $embodiedQualified
        repository = [pscustomobject]@{
            commit = $commit
            dirtyAtStart = $dirty
        }
        machine = Get-MachineEvidence
        liveEvidence = [pscustomobject]@{
            controlBaseUrl = $ControlBaseUrl
            durationSeconds = if ($liveRequested) { [Math]::Max(60, $LiveDurationSec) } else { 0 }
            simulator = 'WorldSim'
            worldDetected = $worldDetected
            worldMotorDispatchTotal = $worldMotorDispatchTotal
            worldLocomotorDispatchTotal = $worldLocomotorDispatchTotal
            worldManipulatorDispatchTotal = $worldManipulatorDispatchTotal
            worldDistanceTravelledDelta = $worldDistanceTravelledDelta
            worldVisitedTerrainDelta = $worldVisitedTerrainDelta
            worldInteractionAttemptDelta = $worldInteractionAttemptDelta
            worldInteractionSuccessDelta = $worldInteractionSuccessDelta
            worldRetinalAcceptedDelta = $worldRetinalAcceptedDelta
            worldCochlearAcceptedDelta = $worldCochlearAcceptedDelta
            worldPhysicalBodyAcceptedDelta = $worldPhysicalBodyAcceptedDelta
            worldSomaticAcceptedDelta = $worldSomaticAcceptedDelta
            worldTickFailureDelta = $worldTickFailureDelta
            burnInSummaryPath = if ($liveRequested) { $burnInSummary } else { $null }
            burnInSamplesPath = if ($liveRequested) { $burnInSamples } else { $null }
        }
        steps = $steps
    }

    $jsonPath = Join-Path $runDirectory 'qualification.json'
    $markdownPath = Join-Path $runDirectory 'qualification.md'
    $result | ConvertTo-Json -Depth 8 | Set-Content -Path $jsonPath -Encoding UTF8
    $markdown = @(
        '# DNNE Neuronal-Only Qualification',
        '',
        "- Status: **$status**",
        "- Mode: $Mode",
        "- Commit: ``$commit``",
        "- Offline preflight: $preflightPassed",
        "- Live evidence requested: $liveRequested",
        "- Embodied qualified: $embodiedQualified",
        "- WorldSim detected: $worldDetected",
        "- WorldSim motor dispatches: $worldMotorDispatchTotal",
        "- WorldSim locomotor/manipulator dispatches: $worldLocomotorDispatchTotal/$worldManipulatorDispatchTotal",
        "- WorldSim distance travelled: $worldDistanceTravelledDelta",
        "- WorldSim newly visited terrain cells: $worldVisitedTerrainDelta",
        "- Physical interaction attempts/successes: $worldInteractionAttemptDelta/$worldInteractionSuccessDelta",
        "- Accepted retinal/cochlear/body/somatic frames: $worldRetinalAcceptedDelta/$worldCochlearAcceptedDelta/$worldPhysicalBodyAcceptedDelta/$worldSomaticAcceptedDelta",
        "- WorldSim tick failures: $worldTickFailureDelta",
        '',
        '## Steps',
        '',
        '| Step | Passed | Seconds | Detail |',
        '| --- | --- | ---: | --- |'
    )
    foreach ($step in $steps) {
        $detail = ([string]$step.detail).Replace('|', '\|')
        $markdown += "| $($step.name) | $($step.passed) | $($step.durationSeconds) | $detail |"
    }
    $markdown += ''
    $markdown += if ($embodiedQualified) {
        'This run contains both offline causal evidence and live neuronal WorldSim evidence.'
    }
    else {
        'This run does not qualify embodied behaviour. A passing `-Mode Live` run with the visible WorldSim is still required.'
    }
    $markdown | Set-Content -Path $markdownPath -Encoding UTF8

    Write-Host ""
    Write-Host ("Qualification status: {0}" -f $status)
    Write-Host ("JSON: {0}" -f $jsonPath)
    Write-Host ("Report: {0}" -f $markdownPath)
    if (-not $preflightPassed -or ($liveRequested -and -not $liveGatePassed)) {
        exit 1
    }
}
finally {
    Pop-Location
}
