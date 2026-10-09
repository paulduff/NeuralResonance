param(
    [ValidateRange(60, 86400)][int]$DurationSec = 1800,
    [ValidateRange(2, 60)][int]$SampleIntervalSec = 5,
    [string]$TrainingStatusPath = '',
    [string]$BackupRoot = 'D:\DNNE-desktop-runs',
    [switch]$WithEntity,
    [switch]$UseRunningStack,
    [switch]$NoPause,
    [switch]$WhatIf
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if ([string]::IsNullOrWhiteSpace($TrainingStatusPath)) {
    $TrainingStatusPath = Join-Path (Split-Path -Parent $repoRoot) 'EntityLLM\artifacts\dyad-voice-window-20261009\window-status.json'
}
if ($WhatIf) {
    Write-Host 'Plan only: no processes, builds, status reads or runtime sampling.'
    Write-Host "Training completion guard: $TrainingStatusPath"
    Write-Host "Then: qualify, start DNNE and the browser-hosted WorldSim, sample for $DurationSec seconds."
    Write-Host "Entity enabled: $([bool]$WithEntity); backups: $BackupRoot"
    return
}

# Read the training result once at launch. This script never watches training.
if (-not (Test-Path -LiteralPath $TrainingStatusPath -PathType Leaf)) {
    throw 'Training has no saved completion status yet. Run this after the training window finishes.'
}
$training = Get-Content -LiteralPath $TrainingStatusPath -Raw | ConvertFrom-Json
if ($training.Status -ne 'FINISHED' -or $training.ExitCode -ne 0) {
    throw 'Training did not finish successfully. Inspect its results before starting this experiment.'
}
foreach ($port in @(5080, 5090)) {
    $listeners = @(Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)
    if ($UseRunningStack) {
        if ($listeners.Count -eq 0) { throw "The existing stack has no listener on port $port." }
        foreach ($listenerPid in @($listeners.OwningProcess | Select-Object -Unique)) {
            $owner = Get-CimInstance Win32_Process -Filter "ProcessId=$listenerPid"
            $inRepo = $owner -and (
                ($owner.ExecutablePath -and $owner.ExecutablePath.StartsWith($repoRoot + '\', [StringComparison]::OrdinalIgnoreCase)) -or
                ($owner.CommandLine -and $owner.CommandLine.IndexOf($repoRoot, [StringComparison]::OrdinalIgnoreCase) -ge 0))
            if (-not $inRepo) { throw "Port $port is not owned by this DNNE checkout; refusing to reuse it." }
        }
    }
    elseif ($listeners.Count -gt 0) {
        throw "Port $port is already in use. This first-run rig requires a stopped DNNE/editor stack."
    }
}
if ($UseRunningStack -and $WithEntity) {
    throw 'Reuse mode measures the existing brain/world configuration; it cannot enable Entity in an already running DNNE process.'
}
if ($WithEntity -and [string]::IsNullOrWhiteSpace($env:NRE_ENTITY_CHECKPOINT_PATH)) {
    throw 'WithEntity requires an explicitly selected NRE_ENTITY_CHECKPOINT_PATH. Review the candidate first.'
}

$runName = 'desktop-brain-world-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 6)
$output = Join-Path $repoRoot ('artifacts\' + $runName)
New-Item -ItemType Directory -Path $output | Out-Null
$transcript = Join-Path $output 'powershell.log'
$samples = [Collections.Generic.List[object]]::new()
$exitCode = 1
$transcribing = $false
$previousDefaults = $PSDefaultParameterValues.Clone()
$previousEntityEnabled = $env:NRE_ENTITY_ENABLED

function Save-ResultJson([string]$Name, [object]$Value) {
    $Value | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath (Join-Path $output $Name) -Encoding UTF8
}
function Get-ProcessResources {
    $ids = @(Get-CimInstance Win32_Process | Where-Object {
        $_.ProcessId -ne $PID -and (
            ($null -ne $_.CommandLine -and $_.CommandLine.IndexOf($repoRoot, [StringComparison]::OrdinalIgnoreCase) -ge 0) -or
            ($null -ne $_.ExecutablePath -and $_.ExecutablePath.StartsWith($repoRoot + '\', [StringComparison]::OrdinalIgnoreCase)))
    } | ForEach-Object { [int]$_.ProcessId })
    $cpu = 0.0
    $memory = 0L
    if ($ids.Count -gt 0) {
        foreach ($process in @(Get-Process -Id $ids -ErrorAction SilentlyContinue)) {
            $cpu += [double]$process.CPU
            $memory += [long]$process.WorkingSet64
        }
    }
    [pscustomobject]@{ Count = $ids.Count; CpuSeconds = $cpu; WorkingSetBytes = $memory }
}

try {
    Start-Transcript -Path $transcript | Out-Null
    $transcribing = $true
    $Host.UI.RawUI.WindowTitle = 'DNNE and WorldSim - desktop baseline'
    Write-Host "Independent DNNE/WorldSim experiment. Results: $output" -ForegroundColor Cyan
    Write-Host 'This run measures runtime and transport. It does not establish language comprehension or intelligence.'

    $computer = Get-CimInstance Win32_ComputerSystem
    $logicalProcessors = [int]$computer.NumberOfLogicalProcessors
    Save-ResultJson 'hardware.json' ([ordered]@{
        GeneratedUtc = [DateTime]::UtcNow.ToString('o')
        LogicalProcessors = $logicalProcessors
        PhysicalMemoryBytes = [long]$computer.TotalPhysicalMemory
        ProcessorNames = @(Get-CimInstance Win32_Processor | ForEach-Object { $_.Name })
        GpuNames = @(Get-CimInstance Win32_VideoController | ForEach-Object { $_.Name })
        Commit = (& git -C $repoRoot rev-parse HEAD)
        EntityEnabled = [bool]$WithEntity
        ReusedRunningStack = [bool]$UseRunningStack
        DurationSec = $DurationSec
        TrainingStatus = $training
    })
    $secret = $env:NRE_CONTROL_SHARED_SECRET
    if (-not [string]::IsNullOrWhiteSpace($secret)) {
        $PSDefaultParameterValues['Invoke-RestMethod:Headers'] = @{ 'X-NRE-Control-Auth' = $secret }
    }
    $env:NRE_ENTITY_ENABLED = if ($WithEntity) { 'true' } else { 'false' }

    if ($UseRunningStack) {
        Write-Host 'Resuming measurement of the existing DNNE and WorldSim without rebuilding or restarting them.'
        $existingHealth = Invoke-RestMethod 'http://127.0.0.1:5080/api/v1/admin/startup-health' -TimeoutSec 15
        $existingWorld = Invoke-RestMethod 'http://127.0.0.1:5090/editor/api/world-state' -TimeoutSec 15
        if ($existingHealth.serviceCount -le 0 -or -not $existingWorld.available -or
            -not $existingWorld.state.worldReady -or -not $existingWorld.state.brainConnected) {
            throw 'The existing DNNE/world stack is not ready and connected.'
        }
        Save-ResultJson 'reuse-startup-health.json' $existingHealth
        Save-ResultJson 'reuse-world.json' $existingWorld
    }
    else {
        Write-Host 'Checking sensory, body, motor, inquiry and authority boundaries...'
        & dotnet test (Join-Path $repoRoot 'tests\NeuralResonanceEngine.DNNE.Tests\NeuralResonanceEngine.DNNE.Tests.csproj') `
            --configuration Release --verbosity minimal `
            --logger trx --results-directory (Join-Path $output 'tests') `
            --filter 'FullyQualifiedName~AvatarWorldDynamicsTests|FullyQualifiedName~AvatarInquiryApiTests|FullyQualifiedName~HostStructuredLanguageAuthorityBoundaryTests'
        if ($LASTEXITCODE -ne 0) { throw 'Preflight qualification failed.' }

        & (Join-Path $PSScriptRoot 'run-dnne-stack.ps1') -CleanStart:$false -NoBuild:$false -NoEditor `
            -SkipBurnInGate -Configuration Release -StartupTimeoutSec 600 `
            -StartupProfilePath (Join-Path $output 'startup-profile.lock.json') `
            -AllowableNonOkServices 0 -StartupSoftNonOkAllowance 0 -AutoRestartNonOk:$false
        if ($LASTEXITCODE -ne 0) { throw 'DNNE startup did not succeed.' }
        & (Join-Path $PSScriptRoot 'start-blazor-editor.ps1') -Configuration Release -OpenBrowser | Out-Null
    }

    Write-Host 'World view: http://localhost:5090/editor' -ForegroundColor Cyan
    Write-Host 'Leave this window open. DNNE and WorldSim remain running when measurement finishes.'
    $started = [DateTime]::UtcNow
    $deadline = $started.AddSeconds($DurationSec)
    $lastResources = Get-ProcessResources
    $lastResourceTime = [DateTime]::UtcNow
    while ([DateTime]::UtcNow -lt $deadline) {
        $now = [DateTime]::UtcNow
        try {
            $health = Invoke-RestMethod 'http://127.0.0.1:5080/api/v1/admin/startup-health?maxNonOkDetails=256' -TimeoutSec 12
            $world = Invoke-RestMethod 'http://127.0.0.1:5090/editor/api/world-state' -TimeoutSec 12
            if (-not $world.available -or -not $world.state.worldReady) { throw 'WorldSim is not ready.' }
            $resources = Get-ProcessResources
            $seconds = [Math]::Max(.001, ([DateTime]::UtcNow - $lastResourceTime).TotalSeconds)
            $cpuPercent = [Math]::Max(0, ($resources.CpuSeconds - $lastResources.CpuSeconds) / $seconds / $logicalProcessors * 100)
            $sample = [ordered]@{
                Utc = $now.ToString('o'); ElapsedSec = ($now - $started).TotalSeconds
                BrainTick = [long]$health.tick; SnapshotTick = [long]$health.lastSnapshotTick
                Services = [int]$health.serviceCount; NonOk = [int]$health.nonOkCount
                WorldTick = [long]$world.state.worldTick; BrainConnected = [bool]$world.state.brainConnected
                WorldAgeSec = [double]$world.ageSeconds; TickFailures = [long]$world.state.tickFailures
                RetinalFrames = [long]$world.state.retinalFramesAccepted
                BodyFrames = [long]$world.state.physicalBodyFramesAccepted
                MotorDispatch = [long]$world.state.neuronalMotorDispatchTotal
                DistanceTravelled = [double]$world.state.distanceTravelled
                BodyInputFailures = [long]$world.state.bodyInputFailures
                ProcessCount = $resources.Count; CpuPercentOfMachine = $cpuPercent
                WorkingSetGiB = $resources.WorkingSetBytes / 1GB
            }
            $samples.Add([pscustomobject]$sample)
            $sample | ConvertTo-Json -Compress | Add-Content -LiteralPath (Join-Path $output 'samples.jsonl') -Encoding UTF8
            if ($samples.Count -eq 1) {
                Save-ResultJson 'startup-health.json' $health
                Save-ResultJson 'world-initial.json' $world
            }
            Save-ResultJson 'world-latest.json' $world
            $lastResources = $resources
            $lastResourceTime = [DateTime]::UtcNow
            Write-Host ('{0,5:N0}s | brain {1} | services {2}/{3} OK | world {4} | retinal/body {5}/{6} | motor {7} | CPU {8:N1}% | RAM {9:N2} GiB' -f
                $sample.ElapsedSec, $sample.BrainTick, ($sample.Services - $sample.NonOk), $sample.Services,
                $sample.WorldTick, $sample.RetinalFrames, $sample.BodyFrames, $sample.MotorDispatch,
                $sample.CpuPercentOfMachine, $sample.WorkingSetGiB)
        }
        catch {
            $failure = [ordered]@{ Utc = $now.ToString('o'); Error = $_.Exception.Message }
            $failure | ConvertTo-Json -Compress | Add-Content -LiteralPath (Join-Path $output 'sample-errors.jsonl') -Encoding UTF8
            Write-Host ('Sample warning: ' + $_.Exception.Message) -ForegroundColor Yellow
        }
        Start-Sleep -Seconds $SampleIntervalSec
    }
    if ($samples.Count -lt 2) { throw 'Too few successful samples to measure the loop.' }
    $first = $samples[0]
    $last = $samples[$samples.Count - 1]
    $checks = [ordered]@{
        BrainAdvanced = $last.BrainTick -gt $first.BrainTick
        WorldAdvanced = $last.WorldTick -gt $first.WorldTick
        RetinalInputObserved = $last.RetinalFrames -gt $first.RetinalFrames
        BodyInputObserved = $last.BodyFrames -gt $first.BodyFrames
        BrainMotorOutputObserved = $last.MotorDispatch -gt $first.MotorDispatch
        WorldRemainedConnected = @($samples | Where-Object { -not $_.BrainConnected }).Count -eq 0
        ServicesHealthy = @($samples | Where-Object { $_.Services -le 0 -or $_.NonOk -ne 0 }).Count -eq 0
        NoNewWorldTickFailures = $last.TickFailures -eq $first.TickFailures
        NoNewBodyInputFailures = $last.BodyInputFailures -eq $first.BodyInputFailures
        NoSampleErrors = -not (Test-Path -LiteralPath (Join-Path $output 'sample-errors.jsonl'))
    }
    $passed = @($checks.Values | Where-Object { -not $_ }).Count -eq 0
    Save-ResultJson 'summary.json' ([ordered]@{
        CompletedUtc = [DateTime]::UtcNow.ToString('o'); RuntimeChecksPassed = $passed
        Checks = $checks; SuccessfulSamples = $samples.Count
        BrainTicksPerSecond = ($last.BrainTick - $first.BrainTick) / [Math]::Max(.001, ($last.ElapsedSec - $first.ElapsedSec))
        DnneAndWorldCpuMeanPercentOfMachine = ($samples | Measure-Object CpuPercentOfMachine -Average).Average
        DnneAndWorldPeakSummedWorkingSetGiB = ($samples | Measure-Object WorkingSetGiB -Maximum).Maximum
        AdditionalDistanceTravelled = $last.DistanceTravelled - $first.DistanceTravelled
        LanguageComprehensionAssessed = $false
    })
    Save-ResultJson 'COMPLETE.json' ([ordered]@{ CompletedUtc = [DateTime]::UtcNow.ToString('o'); RuntimeChecksPassed = $passed })
    Write-Host "Runtime transport checks passed: $passed. Inspect summary.json separately from model quality."
    $exitCode = 0
}
catch {
    Save-ResultJson 'RUN_FAILED.json' ([ordered]@{ Utc = [DateTime]::UtcNow.ToString('o'); Error = $_.Exception.Message })
    Write-Host ('Experiment failed: ' + $_.Exception.Message) -ForegroundColor Red
}
finally {
    $PSDefaultParameterValues = $previousDefaults
    [Environment]::SetEnvironmentVariable('NRE_ENTITY_ENABLED', $previousEntityEnabled, 'Process')
    try {
        $logRoot = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'NeuralResonanceEngine\logs'
        $savedLogs = Join-Path $output 'process-logs'
        New-Item -ItemType Directory -Force -Path $savedLogs | Out-Null
        foreach ($pattern in @('controlprogram-*.log', 'dnne-blazor-editor-*.log')) {
            foreach ($logFile in @(Get-ChildItem -LiteralPath $logRoot -Filter $pattern -File -ErrorAction SilentlyContinue)) {
                Copy-Item -LiteralPath $logFile.FullName -Destination $savedLogs
            }
        }
    }
    catch { Write-Host ('Process-log snapshot unavailable: ' + $_.Exception.Message) -ForegroundColor Yellow }
    if ($transcribing) { Stop-Transcript | Out-Null }
    if (-not [string]::IsNullOrWhiteSpace($BackupRoot)) {
        try {
            $destination = Join-Path $BackupRoot $runName
            New-Item -ItemType Directory -Force -Path $destination | Out-Null
            foreach ($file in Get-ChildItem -LiteralPath $output -File -Recurse) {
                $relative = $file.FullName.Substring($output.Length).TrimStart('\')
                $target = Join-Path $destination $relative
                New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
                Copy-Item -LiteralPath $file.FullName -Destination $target
                if ((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $target).Hash) {
                    throw "Backup verification failed for $($file.Name)."
                }
            }
            Write-Host "Results copied to $destination"
        }
        catch { Write-Host ('Backup copy failed; original results remain saved: ' + $_.Exception.Message) -ForegroundColor Yellow }
    }
}
$label = if ($exitCode -eq 0) { 'FINISHED' } else { 'FAILED' }
Write-Host "========== $label (exit $exitCode) =========="
Write-Host "Tell Orion to inspect $output"
if (-not $NoPause) { Read-Host 'Press Enter to close this window; the DNNE/world stack remains running' | Out-Null }
exit $exitCode
