[CmdletBinding()]
param(
    [string]$ControlProgramUrl = "http://localhost:5080",
    [ValidateRange(1, 256)]
    [int]$Limit = 256,
    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"
$protocol = "dyad.population-language-training.v1"
$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $OutputPath = Join-Path $root "artifacts\dyad-adapter-training\accepted-$stamp.jsonl"
}

$uri = $ControlProgramUrl.TrimEnd('/') + "/api/v1/dyad/language/adapter-training?limit=$Limit"
$dataset = Invoke-RestMethod -Method Get -Uri $uri
if ($dataset.protocolVersion -ne $protocol) {
    throw "Unexpected dataset protocol '$($dataset.protocolVersion)'."
}

$records = @($dataset.records)
foreach ($record in $records) {
    if ($record.protocolVersion -ne $protocol -or
        [string]::IsNullOrWhiteSpace([string]$record.targetText) -or
        $record.grounding.isSleeping -or
        -not $record.grounding.neuronalCircuitObserved -or
        -not $record.grounding.neuronalGroundingAvailable -or
        -not $record.grounding.neuronalGrounded -or
        -not $record.grounding.neuronalSpeechAuthorized) {
        throw "The endpoint returned an ineligible adapter-training record."
    }
}

$fullOutputPath = [System.IO.Path]::GetFullPath($OutputPath)
$directory = Split-Path -Parent $fullOutputPath
[System.IO.Directory]::CreateDirectory($directory) | Out-Null
$temporaryPath = "$fullOutputPath.$([Guid]::NewGuid().ToString('N')).tmp"
try {
    $lines = @($records | ForEach-Object { $_ | ConvertTo-Json -Depth 8 -Compress })
    [System.IO.File]::WriteAllLines($temporaryPath, $lines, [System.Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $temporaryPath -Destination $fullOutputPath -Force
}
finally {
    if (Test-Path -LiteralPath $temporaryPath) {
        Remove-Item -LiteralPath $temporaryPath -Force
    }
}

Write-Host "Exported $($records.Count) accepted Dyad adapter-training record(s)."
Write-Host "Dataset: $fullOutputPath"
