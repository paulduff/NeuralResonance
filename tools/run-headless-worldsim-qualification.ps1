param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path (Split-Path -Parent $PSCommandPath) '..')).Path
$project = Join-Path $root 'tests\NeuralResonanceEngine.DNNE.Tests\NeuralResonanceEngine.DNNE.Tests.csproj'
$arguments = @(
    'test',
    $project,
    '--configuration', $Configuration,
    '--nologo',
    '--verbosity', 'minimal',
    '--filter', 'FullyQualifiedName~AvatarWorldDynamicsTests'
)
if ($NoBuild) {
    $arguments += '--no-build'
}

& dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    throw 'Headless WorldSim qualification failed.'
}

Write-Host 'headless WorldSim qualification: PASS'
