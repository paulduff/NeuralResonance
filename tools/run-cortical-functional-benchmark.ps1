param(
    [int]$Epochs = 24,
    [string]$OutputDirectory = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $PSCommandPath
$repoRoot = Split-Path -Parent $scriptDir
$project = Join-Path $repoRoot 'Benchmarks\NeuralResonanceEngine.CorticalBenchmarks\NeuralResonanceEngine.CorticalBenchmarks.csproj'
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot 'artifacts\cortical-functional-benchmark'
}

$boundedEpochs = [Math]::Min(200, [Math]::Max(8, $Epochs))
dotnet run --project $project -c Release -- --epochs $boundedEpochs --output $OutputDirectory
if ($LASTEXITCODE -ne 0) {
    throw "DNNE cortical functional benchmark failed with exit code $LASTEXITCODE."
}
