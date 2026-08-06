param(
    [ValidateSet("Preflight", "Capture", "Campaign")]
    [string]$Mode = "Preflight",
    [string]$ApiBaseUrl = "http://localhost:5080",
    [string]$OutputDirectory = "",
    [string]$InputDirectory = "",
    [string]$ScenarioId = "",
    [ValidateSet("training", "held-out")]
    [string]$Split = "training",
    [int]$Seed = 317,
    [ValidateSet("Shadow", "Assist")]
    [string]$ExpectedMode = "Shadow",
    [string]$LayoutFingerprint = "",
    [ValidateRange(5, 86400)]
    [int]$MaxSeconds = 900,
    [ValidateRange(25, 10000)]
    [int]$PollMilliseconds = 100,
    [ValidateRange(1, 100)]
    [int]$MinimumTrainingScenarios = 3,
    [ValidateRange(1, 100)]
    [int]$MinimumHeldOutScenarios = 3
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "Benchmarks\NeuralResonanceEngine.EmbodiedBenchmarks\NeuralResonanceEngine.EmbodiedBenchmarks.csproj"
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $root "artifacts\neuronal-motor-qualification"
}

$modeValue = "motor-$($Mode.ToLowerInvariant())"
$arguments = @(
    "run",
    "--project", $project,
    "-c", "Release",
    "--",
    "--mode", $modeValue,
    "--output", $OutputDirectory
)

if ($Mode -eq "Capture") {
    if ([string]::IsNullOrWhiteSpace($ScenarioId)) {
        throw "Capture mode requires -ScenarioId."
    }

    if ([string]::IsNullOrWhiteSpace($LayoutFingerprint)) {
        throw "Capture mode requires -LayoutFingerprint from the running world or maze."
    }

    $arguments += @(
        "--api", $ApiBaseUrl,
        "--scenario", $ScenarioId,
        "--split", $Split,
        "--seed", $Seed,
        "--expected-mode", $ExpectedMode,
        "--layout-fingerprint", $LayoutFingerprint,
        "--max-seconds", $MaxSeconds,
        "--poll-ms", $PollMilliseconds
    )
}

if ($Mode -eq "Campaign") {
    if ([string]::IsNullOrWhiteSpace($InputDirectory)) {
        $InputDirectory = $OutputDirectory
    }

    $arguments += @(
        "--input", $InputDirectory,
        "--phase", $ExpectedMode,
        "--minimum-training", $MinimumTrainingScenarios,
        "--minimum-held-out", $MinimumHeldOutScenarios
    )
}

& dotnet @arguments
exit $LASTEXITCODE
