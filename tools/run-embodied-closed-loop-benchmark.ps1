param(
    [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "Benchmarks\NeuralResonanceEngine.EmbodiedBenchmarks\NeuralResonanceEngine.EmbodiedBenchmarks.csproj"
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $root "artifacts\embodied-closed-loop"
}

dotnet run --project $project -c Release -- --output $OutputDirectory
exit $LASTEXITCODE
