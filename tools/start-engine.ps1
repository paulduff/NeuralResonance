param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$NoBuild,
    [int]$Port = 5080,
    [switch]$WhatIf
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path (Split-Path -Parent $PSCommandPath) '_start-dnne-project.ps1')

$repoRoot = Get-DnneRepoRoot -ScriptPath $PSCommandPath
$projectPath = Join-Path $repoRoot 'ControlProgram\NeuralResonanceEngine.ControlProgram.csproj'
$baseUrl = "http://localhost:$Port"
$envVars = @{
    PORT = $Port
    ASPNETCORE_URLS = $baseUrl
    ASPNETCORE_ENVIRONMENT = 'Production'
    DOTNET_ENVIRONMENT = 'Production'
    SnapshotEndpoint = "$baseUrl/api/v1/snapshot"
    ControlPublishUrl = "$baseUrl/api/v1/publish/step"
}

Start-DnneProject `
    -ProjectPath $projectPath `
    -FriendlyName 'DNNE Engine' `
    -Configuration $Configuration `
    -NoBuild:$NoBuild `
    -EnvironmentVariables $envVars `
    -WhatIf:$WhatIf | Out-Null
