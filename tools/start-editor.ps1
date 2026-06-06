param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$NoBuild,
    [string]$ControlBaseUrl = 'http://localhost:5080',
    [switch]$WhatIf
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path (Split-Path -Parent $PSCommandPath) '_start-dnne-project.ps1')

$repoRoot = Get-DnneRepoRoot -ScriptPath $PSCommandPath
$projectPath = Join-Path $repoRoot 'src\NRE.WpfEditor\NRE.WpfEditor.csproj'
$envVars = @{
    NRE_CONTROL_ENDPOINTS = $ControlBaseUrl
    CONTROLPROGRAM_BASE_URLS = $ControlBaseUrl
    CONTROLPROGRAM_BASE_URL = $ControlBaseUrl
}

Start-DnneProject `
    -ProjectPath $projectPath `
    -FriendlyName 'DNNE Editor' `
    -Configuration $Configuration `
    -NoBuild:$NoBuild `
    -EnvironmentVariables $envVars `
    -WhatIf:$WhatIf | Out-Null
