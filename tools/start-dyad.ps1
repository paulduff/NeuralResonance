param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$NoBuild,
    [int]$Port = 5080,
    [ValidateSet('World', 'Maze', 'None')]
    [string]$Simulator = 'World',
    [int]$MazeSeed = 317,
    [Parameter(Mandatory = $true)]
    [string]$EntityCheckpointPath,
    [string]$EntityApiUrl = 'http://127.0.0.1:5165',
    [string]$EntityApiKey,
    [string]$EntityChatExamplesPath,
    [string]$EntityIdentityProfilePath,
    [string]$EntityHistoryPath,
    [string]$EntityKnowledgePath,
    [int]$EntityTokens = 80,
    [double]$EntityTemperature = 0.20,
    [int]$EntityTopK = 8,
    [int]$EntitySeed = 1337,
    [int]$EntityTimeoutMs = 60000,
    [switch]$WhatIf
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path (Split-Path -Parent $PSCommandPath) '_start-dnne-project.ps1')

function Get-BoundedInt {
    param([int]$Value, [int]$Minimum, [int]$Maximum)
    return [Math]::Min($Maximum, [Math]::Max($Minimum, $Value))
}

function Get-BoundedDouble {
    param([double]$Value, [double]$Minimum, [double]$Maximum)
    return [Math]::Min($Maximum, [Math]::Max($Minimum, $Value))
}

if ($Port -lt 1 -or $Port -gt 65535) {
    throw 'Port must be between 1 and 65535.'
}

$entityApiUri = [Uri]$EntityApiUrl
if (-not $WhatIf -and $entityApiUri.IsLoopback -and -not (Test-Path -LiteralPath $EntityCheckpointPath -PathType Leaf)) {
    throw "Entity checkpoint not found: $EntityCheckpointPath"
}

$repoRoot = Get-DnneRepoRoot -ScriptPath $PSCommandPath
$controlProjectPath = Join-Path $repoRoot 'ControlProgram\NeuralResonanceEngine.ControlProgram.csproj'
$controlBaseUrl = "http://localhost:$Port"
$environment = @{
    PORT = $Port
    ASPNETCORE_URLS = $controlBaseUrl
    ASPNETCORE_ENVIRONMENT = 'Production'
    DOTNET_ENVIRONMENT = 'Production'
    SnapshotEndpoint = "$controlBaseUrl/api/v1/snapshot"
    ControlPublishUrl = "$controlBaseUrl/api/v1/publish/step"
    NRE_ENTITY_ENABLED = 'true'
    NRE_ENTITY_API_URL = $EntityApiUrl
    NRE_ENTITY_API_KEY = $EntityApiKey
    NRE_ENTITY_CHECKPOINT_PATH = $EntityCheckpointPath
    NRE_ENTITY_CHAT_EXAMPLES_PATH = $EntityChatExamplesPath
    NRE_ENTITY_IDENTITY_PROFILE_PATH = $EntityIdentityProfilePath
    NRE_ENTITY_HISTORY_PATH = $EntityHistoryPath
    NRE_ENTITY_KNOWLEDGE_PATH = $EntityKnowledgePath
    NRE_ENTITY_TOKENS = Get-BoundedInt -Value $EntityTokens -Minimum 16 -Maximum 240
    NRE_ENTITY_TEMPERATURE = (Get-BoundedDouble -Value $EntityTemperature -Minimum 0.05 -Maximum 1.25).ToString('0.00', [System.Globalization.CultureInfo]::InvariantCulture)
    NRE_ENTITY_TOP_K = Get-BoundedInt -Value $EntityTopK -Minimum 1 -Maximum 80
    NRE_ENTITY_SEED = $EntitySeed
    NRE_ENTITY_TIMEOUT_MS = Get-BoundedInt -Value $EntityTimeoutMs -Minimum 1000 -Maximum 300000
}

Start-DnneProject `
    -ProjectPath $controlProjectPath `
    -FriendlyName 'Dyad DNNE Engine' `
    -Configuration $Configuration `
    -NoBuild:$NoBuild `
    -EnvironmentVariables $environment `
    -WhatIf:$WhatIf | Out-Null

switch ($Simulator) {
    'World' {
        & (Join-Path $repoRoot 'tools\start-world-sim.ps1') `
            -Configuration $Configuration `
            -NoBuild:$NoBuild `
            -ControlBaseUrl $controlBaseUrl `
            -WhatIf:$WhatIf
    }
    'Maze' {
        & (Join-Path $repoRoot 'tools\start-maze-sim.ps1') `
            -Configuration $Configuration `
            -NoBuild:$NoBuild `
            -ControlBaseUrl $controlBaseUrl `
            -Seed $MazeSeed `
            -WhatIf:$WhatIf
    }
    'None' {
        Write-Host 'Dyad control program started without a simulator.'
    }
}
