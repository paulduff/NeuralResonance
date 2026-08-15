param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$NoBuild,
    [string]$ControlBaseUrl = 'http://localhost:5080',
    [int]$Seed = 317,
    [switch]$WhatIf
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path (Split-Path -Parent $PSCommandPath) '_start-dnne-project.ps1')

$repoRoot = Get-DnneRepoRoot -ScriptPath $PSCommandPath
$projectPath = Join-Path $repoRoot 'src\NRE.WpfMazeSim\NRE.WpfMazeSim.csproj'
$envVars = @{
    NRE_CONTROL_ENDPOINTS = $ControlBaseUrl
    CONTROLPROGRAM_BASE_URLS = $ControlBaseUrl
    CONTROLPROGRAM_BASE_URL = $ControlBaseUrl
    NRE_MAZE_SEED = $Seed
}

Assert-DnneSimulatorExclusive `
    -CurrentSimulator 'DNNE Maze Simulator' `
    -BlockedSignatures @('NRE.WpfWorldSim', 'start-world-sim.ps1', 'NRE.BlazorEditor', 'start-blazor-editor.ps1') `
    -WhatIf:$WhatIf

Start-DnneProject `
    -ProjectPath $projectPath `
    -FriendlyName 'DNNE Maze Simulator' `
    -Configuration $Configuration `
    -NoBuild:$NoBuild `
    -EnvironmentVariables $envVars `
    -WhatIf:$WhatIf | Out-Null
