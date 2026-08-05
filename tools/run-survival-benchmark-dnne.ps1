param(
    [string]$ControlBaseUrl = 'http://localhost:5080',
    [int]$Seed = 317,
    [int]$Steps = 240,
    [string[]]$Policies = @('control-state-intent', 'rule-safety', 'deterministic-random', 'no-learning-stationary'),
    [string]$InitialBrainStatePath = '',
    [string]$OutputDirectory = '',
    [int]$RequestTimeoutSec = 90
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $PSCommandPath
$baseUrl = $ControlBaseUrl.TrimEnd('/')
$endpoint = "$baseUrl/api/v1/admin/benchmarks/survival/run"
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $scriptDir 'artifacts\survival-benchmark'
}

$body = [ordered]@{
    seed = $Seed
    steps = $Steps
    policies = @($Policies)
}

if (-not [string]::IsNullOrWhiteSpace($InitialBrainStatePath)) {
    if (-not (Test-Path -LiteralPath $InitialBrainStatePath -PathType Leaf)) {
        throw "Initial brain state was not found: $InitialBrainStatePath"
    }

    $body.initialBrainState = Get-Content -LiteralPath $InitialBrainStatePath -Raw | ConvertFrom-Json
}

$requestJson = $body | ConvertTo-Json -Depth 100 -Compress
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

try {
    $response = Invoke-WebRequest -Uri $endpoint -Method Post -ContentType 'application/json' -Body $requestJson -TimeoutSec ([Math]::Max(1, $RequestTimeoutSec))
}
catch {
    throw "Survival benchmark request failed. Ensure the DNNE Control Program is running at $baseUrl. $($_.Exception.Message)"
}

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$jsonPath = Join-Path $OutputDirectory "survival-benchmark-$stamp.json"
$markdownPath = Join-Path $OutputDirectory "survival-benchmark-$stamp.md"
[System.IO.File]::WriteAllText($jsonPath, $response.Content, [System.Text.UTF8Encoding]::new($false))

$result = $response.Content | ConvertFrom-Json
$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# DNNE Deterministic Survival Benchmark')
$lines.Add('')
$lines.Add("- Protocol: ``$($result.protocolVersion)``")
$lines.Add("- Seed: ``$($result.seed)``")
$lines.Add("- Requested steps: ``$($result.requestedSteps)``")
$lines.Add("- Initial state: ``$($result.initialBrainSnapshot.source)`` at tick ``$($result.initialBrainSnapshot.tick)``")
$lines.Add('')
$lines.Add('| Policy | Terminal condition | Steps | Food | Shelter visits | Threat contacts | Mean health | Mean hunger | Intent actions |')
$lines.Add('| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |')
foreach ($episode in $result.episodes) {
    $metrics = $episode.metrics
    $meanHealth = ([double]$metrics.meanHealth).ToString('0.000')
    $meanHunger = ([double]$metrics.meanHunger).ToString('0.000')
    $lines.Add("| $($episode.policy) | $($episode.terminalCondition) | $($episode.stepsExecuted) | $($metrics.foodCollected) | $($metrics.shelterVisits) | $($metrics.threatContacts) | $meanHealth | $meanHunger | $($metrics.intentDrivenActions) |")
}
$lines.Add('')
$lines.Add("The full immutable episode records, initial brain snapshot, per-step observations, actions, and outcomes are in ``$(Split-Path -Leaf $jsonPath)``.")
[System.IO.File]::WriteAllLines($markdownPath, $lines, [System.Text.UTF8Encoding]::new($false))

Write-Host 'DNNE deterministic survival benchmark complete.'
Write-Host "JSON: $jsonPath"
Write-Host "Report: $markdownPath"
foreach ($episode in $result.episodes) {
    Write-Host ("{0}: {1} after {2} steps; food={3}; health={4:N3}" -f $episode.policy, $episode.terminalCondition, $episode.stepsExecuted, $episode.metrics.foodCollected, $episode.finalWorld.health)
}
