param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$NoBuild,
    [string]$ControlBaseUrl = 'http://127.0.0.1:5080',
    [ValidateRange(1024, 65535)]
    [int]$Port = 5090,
    [switch]$ListenAnyIp,
    [string]$AccessKey = $env:NRE_EDITOR_ACCESS_KEY,
    [switch]$OpenBrowser,
    [switch]$WhatIf
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path (Split-Path -Parent $PSCommandPath) '_start-dnne-project.ps1')

$controlUri = $null
if (-not [Uri]::TryCreate($ControlBaseUrl, [UriKind]::Absolute, [ref]$controlUri) -or
    -not $controlUri.IsLoopback) {
    throw 'ControlBaseUrl must use localhost, 127.0.0.1, or another loopback address.'
}

if ($ListenAnyIp -and [string]::IsNullOrWhiteSpace($AccessKey)) {
    throw 'ListenAnyIp requires an access key. Set NRE_EDITOR_ACCESS_KEY before exposing the Editor to the LAN.'
}

$repoRoot = Get-DnneRepoRoot -ScriptPath $PSCommandPath
$projectPath = Join-Path $repoRoot 'src\NRE.BlazorEditor\NRE.BlazorEditor.csproj'
$existingListener = if (-not $WhatIf) {
    Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue |
        Select-Object -First 1
}
if ($existingListener) {
    throw ("Port {0} is already in use by process {1}. Stop the existing editor or choose another port." -f
        $Port, $existingListener.OwningProcess)
}
$envVars = @{
    ASPNETCORE_ENVIRONMENT       = 'Production'
    DOTNET_ENVIRONMENT           = 'Production'
    NRE_EDITOR_CONTROL_BASE_URL  = $controlUri.AbsoluteUri.TrimEnd('/')
    NRE_EDITOR_LISTEN_ANY_IP     = if ($ListenAnyIp) { 'true' } else { 'false' }
    NRE_EDITOR_PORT              = [string]$Port
}
if (-not [string]::IsNullOrWhiteSpace($AccessKey)) {
    $envVars.NRE_EDITOR_ACCESS_KEY = $AccessKey.Trim()
}

Assert-DnneSimulatorExclusive `
    -CurrentSimulator 'DNNE Blazor Editor and headless WorldSim' `
    -BlockedSignatures @('NRE.WpfWorldSim', 'start-world-sim.ps1', 'NRE.WpfMazeSim', 'start-maze-sim.ps1') `
    -WhatIf:$WhatIf

$process = Start-DnneProject `
    -ProjectPath $projectPath `
    -FriendlyName 'DNNE Blazor Editor' `
    -Configuration $Configuration `
    -NoBuild:$NoBuild `
    -EnvironmentVariables $envVars `
    -WindowStyle Hidden `
    -WhatIf:$WhatIf

$editorUrl = "http://localhost:$Port/editor"
if (-not $WhatIf) {
    $ready = $false
    $deadline = [DateTime]::UtcNow.AddSeconds(60)
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($process.HasExited) {
            throw ("DNNE Blazor Editor exited during startup with code {0}. Check its stderr log." -f $process.ExitCode)
        }
        try {
            $state = Invoke-RestMethod -Uri ("http://127.0.0.1:{0}/editor/api/world-state" -f $Port) -TimeoutSec 2
            if ($state.available -and $state.state.worldReady) {
                $ready = $true
                break
            }
        }
        catch {
            Start-Sleep -Milliseconds 250
        }
    }
    if (-not $ready) {
        if (-not $process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
        throw 'DNNE Blazor Editor did not publish a ready headless world within 60 seconds.'
    }
}
Write-Host ("Editor URL: {0}" -f $editorUrl)
if ($ListenAnyIp) {
    Write-Host 'LAN listening is enabled; access requires the configured editor key.'
}
else {
    Write-Host 'Editor is listening on this machine only.'
}

if ($OpenBrowser -and -not $WhatIf) {
    Start-Process $editorUrl | Out-Null
}

$process
