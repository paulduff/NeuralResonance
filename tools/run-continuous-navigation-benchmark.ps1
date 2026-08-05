param(
    [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "Benchmarks\NeuralResonanceEngine.EmbodiedBenchmarks\NeuralResonanceEngine.EmbodiedBenchmarks.csproj"
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $root "artifacts\continuous-navigation"
}

dotnet run --project $project -c Release -- --mode navigation --output $OutputDirectory
exit $LASTEXITCODE
