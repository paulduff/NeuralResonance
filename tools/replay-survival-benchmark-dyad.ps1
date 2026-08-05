param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactPath,
    [string]$ControlBaseUrl = 'http://localhost:5080',
    [string]$Policy = 'control-state-intent',
    [int]$SampleEverySteps = 24,
    [int]$MaxSamples = 4,
    [string]$SessionId = '',
    [string]$CandidateKind = 'interpretation',
    [string]$OutputDirectory = '',
    [int]$RequestTimeoutSec = 180
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $ArtifactPath -PathType Leaf)) {
    throw "Survival benchmark artifact was not found: $ArtifactPath"
}

$scriptDir = Split-Path -Parent $PSCommandPath
$baseUrl = $ControlBaseUrl.TrimEnd('/')
$endpoint = "$baseUrl/api/v1/admin/benchmarks/survival/dyad-replay"
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $scriptDir 'artifacts\dyad-survival-replay'
}

$body = [ordered]@{
    artifact = Get-Content -LiteralPath $ArtifactPath -Raw | ConvertFrom-Json
    policy = $Policy
    sampleEverySteps = $SampleEverySteps
    maxSamples = $MaxSamples
    candidateKind = $CandidateKind
}
if (-not [string]::IsNullOrWhiteSpace($SessionId)) {
    $body.sessionId = $SessionId
}

$requestJson = $body | ConvertTo-Json -Depth 100 -Compress
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

try {
    $response = Invoke-WebRequest -Uri $endpoint -Method Post -ContentType 'application/json' -Body $requestJson -TimeoutSec ([Math]::Max(1, $RequestTimeoutSec))
}
catch {
    throw "Dyad survival replay failed. Ensure the DNNE Control Program is running at $baseUrl. $($_.Exception.Message)"
}

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$jsonPath = Join-Path $OutputDirectory "dyad-survival-replay-$stamp.json"
$markdownPath = Join-Path $OutputDirectory "dyad-survival-replay-$stamp.md"
[System.IO.File]::WriteAllText($jsonPath, $response.Content, [System.Text.UTF8Encoding]::new($false))

$replay = $response.Content | ConvertFrom-Json
$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# Dyad Survival Replay Evaluation')
$lines.Add('')
$lines.Add("- Protocol: ``$($replay.protocolVersion)``")
$lines.Add("- Policy: ``$($replay.policy)``")
$lines.Add("- Session: ``$($replay.sessionId)``")
$lines.Add("- Replay verified: ``$($replay.replayVerified)``")
$lines.Add("- Evidence: $($replay.replayEvidence)")
$lines.Add('')
$lines.Add('| Step | Entity mode | Review decision | Candidate |')
$lines.Add('| ---: | --- | --- | --- |')
foreach ($turn in $replay.turns) {
    $decision = if ($null -eq $turn.review) { 'fallback' } else { $turn.review.decision }
    $text = ([string]$turn.text).Replace('|', '\|').Replace("`r", ' ').Replace("`n", ' ')
    if ($text.Length -gt 140) {
        $text = $text.Substring(0, 137) + '...'
    }

    $lines.Add("| $($turn.step) | $($turn.origin) | $decision | $text |")
}
$lines.Add('')
$lines.Add("The JSON artifact contains each full bounded prompt, Entity version and settings, source references, DNNE grounding snapshot, candidate text, and review decision: ``$(Split-Path -Leaf $jsonPath)``.")
[System.IO.File]::WriteAllLines($markdownPath, $lines, [System.Text.UTF8Encoding]::new($false))

Write-Host 'Dyad survival replay evaluation complete.'
Write-Host "JSON: $jsonPath"
Write-Host "Report: $markdownPath"
foreach ($turn in $replay.turns) {
    Write-Host ("step {0}: {1}; review={2}" -f $turn.step, $turn.origin, $(if ($null -eq $turn.review) { 'fallback' } else { $turn.review.decision }))
}
