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
    $testFilter = 'FullyQualifiedName~NeuronalMotorControlTests|FullyQualifiedName~NeuronalActionSelectionTests|FullyQualifiedName~NeuronalLanguageGroundingTests|FullyQualifiedName~NeuronalCognitionAuthorityTests|FullyQualifiedName~AvatarKinematicsTests|FullyQualifiedName~AvatarServiceTests|FullyQualifiedName~HostSurvivalAuthorityBoundaryTests|FullyQualifiedName~HostStructuredLanguageAuthorityBoundaryTests|FullyQualifiedName~SimulatorAuthorityBoundaryTests'

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
    $mazeDetected = $false
    $mazeMotorDispatchTotal = 0
    $mazeProgressTotal = 0
    $burnInSummary = Join-Path $runDirectory 'live-burnin-summary.txt'
    $burnInSamples = Join-Path $runDirectory 'live-burnin-samples.json'

    if ($liveRequested) {
        $validationPassed = Invoke-QualificationStep -Name 'live runtime validation' -Action {
            & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $scriptDir 'validate-dnne.ps1') -BaseUrl $ControlBaseUrl -TimeoutSec 60 -RequireValid
        }

        $burnInPassed = $false
        if ($validationPassed) {
            $burnInPassed = Invoke-QualificationStep -Name 'live neuronal maze burn-in' -Action {
                & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $scriptDir 'burnin-dnne.ps1') -ControlBaseUrl $ControlBaseUrl -DurationSec ([Math]::Max(60, $LiveDurationSec)) -RestartCycleIntervalSec 0 -MazeStuckFailAfterSec ([Math]::Max(60, [Math]::Min(180, $LiveDurationSec))) -SummaryPath $burnInSummary -SamplesPath $burnInSamples
            }
        }

        $mazeDetected = (Read-SummaryValue -Path $burnInSummary -Name 'mazeDetected' -Fallback 'False') -eq 'True'
        [void][int]::TryParse((Read-SummaryValue -Path $burnInSummary -Name 'mazeMotorDispatchTotal' -Fallback '0'), [ref]$mazeMotorDispatchTotal)
        [void][int]::TryParse((Read-SummaryValue -Path $burnInSummary -Name 'mazeProgressTotal' -Fallback '0'), [ref]$mazeProgressTotal)
        $liveGatePassed = $validationPassed -and $burnInPassed -and $mazeDetected -and
            $mazeMotorDispatchTotal -gt 0 -and $mazeProgressTotal -gt 0
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
        protocolVersion = 'dnne.neuronal-only-qualification.v1'
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
            mazeDetected = $mazeDetected
            mazeMotorDispatchTotal = $mazeMotorDispatchTotal
            mazeProgressTotal = $mazeProgressTotal
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
        "- Maze detected: $mazeDetected",
        "- Maze motor dispatches: $mazeMotorDispatchTotal",
        "- Maze progress events: $mazeProgressTotal",
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
        'This run contains both offline causal evidence and live neuronal maze evidence.'
    }
    else {
        'This run does not qualify embodied behaviour. A passing `-Mode Live` run with a visible/running maze is still required.'
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
