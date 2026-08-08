param(
    [switch]$CleanStart = $true,
    [switch]$NoBuild = $true,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$NoEditor,
    [switch]$SkipBurnInGate = $false,
    [switch]$PrebuildMissingStructureProjects = $true,
    [int]$StartupTimeoutSec = 300,
    [int]$PollIntervalMs = 500,
    [string]$ControlBaseUrl = 'http://localhost:5080',
    [int]$AllowableNonOkServices = 1,
    [int]$StartupSoftNonOkAllowance = 2,
    [int]$StartupSoftMinTick = 40,
    [int]$StartupSoftMaxSnapshotAgeTicks = 20,
    [switch]$AutoRestartNonOk = $true,
    [int]$MaxRestartAttemptsPerStructure = 2,
    [int]$MaxRestartRequestsPerPass = 6,
    [int]$MinTickBeforeAutoRestart = 50,
    [int]$RestartGracePeriodSec = 15,
    [int]$RestartAttemptCooldownSec = 5,
    [int]$AutoRestartHeavyLoadThreshold = 24,
    [int]$AutoRestartHeavyLoadCooldownSec = 3,
    [int]$RestartRequestTimeoutSec = 3,
    [int]$RestartWarningThrottleSec = 30,
    [int]$RestartThrottleLogIntervalSec = 20,
    [int]$RestartStreakThreshold = 3,
    [int]$StartupHealthTimeoutSec = 12,
    [int]$StateFallbackTimeoutSec = 20,
    [int]$StartupRecoveryGraceSec = 120,
    [switch]$StartEditorOnTimeout,
    [int]$BurnInDurationSec = 1800,
    [int]$BurnInPollIntervalMs = 500,
    [int]$BurnInMaxSnapshotAgeTicks = 25,
    [int]$BurnInSnapshotAgeGraceSec = 20,
    [int]$BurnInNonOkGraceSec = 20,
    [int]$BurnInWarmupSec = 15,
    [int]$BurnInSensoryIntervalSec = 1,
    [int]$BurnInRestartCycleIntervalSec = 600,
    [int]$BurnInRestartRecoveryTimeoutSec = 90,
    [int]$BurnInMaxSensory404 = 2,
    [int]$BurnInMaxSensoryZeroDelivered = 30,
    [int]$BurnInMazeStuckFailAfterSec = 0,
    [int]$BurnInMazeStuckNoProgressWindowSec = 45,
    [float]$BurnInMazeStuckWallImpactMinRatePerSec = 0.8,
    [int]$BurnInMazeStuckMaxMotorDispatchPerPoll = 2,
    [switch]$UseStartupProfileLock = $true,
    [string]$StartupProfilePath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $PSCommandPath
$repoRoot = (Resolve-Path (Join-Path $scriptDir '..')).Path
$controlProj = Join-Path $repoRoot 'ControlProgram\NeuralResonanceEngine.ControlProgram.csproj'
$editorProj = Join-Path $repoRoot 'src\NRE.WpfEditor\NRE.WpfEditor.csproj'
$killScript = Join-Path $repoRoot 'tools\kill-dnne-services.ps1'
$burnInScript = Join-Path $repoRoot 'tools\burnin-dnne.ps1'
$burnInSummaryPath = Join-Path $repoRoot 'tools\_burnin-summary.txt'
$burnInSamplesPath = Join-Path $repoRoot 'tools\_burnin-samples.json'
$startupProfileLockPath = if ([string]::IsNullOrWhiteSpace($StartupProfilePath)) {
    Join-Path $repoRoot 'tools\startup-profile.lock.json'
}
else {
    $StartupProfilePath
}

function New-DnneProcessLogPaths {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $logDirectory = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)) 'NeuralResonanceEngine\logs'
    New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
    $safeName = (($Name -replace '[^A-Za-z0-9]+', '-').Trim('-')).ToLowerInvariant()
    if ([string]::IsNullOrWhiteSpace($safeName)) {
        $safeName = 'dnne-process'
    }

    [pscustomobject]@{
        StdOut = Join-Path $logDirectory ("{0}-stdout.log" -f $safeName)
        StdErr = Join-Path $logDirectory ("{0}-stderr.log" -f $safeName)
    }
}

if (-not (Test-Path $controlProj -PathType Leaf)) {
    throw "Control project not found: $controlProj"
}

if (-not (Test-Path $editorProj -PathType Leaf)) {
    throw "Editor project not found: $editorProj"
}

if (-not $SkipBurnInGate -and -not (Test-Path $burnInScript -PathType Leaf)) {
    throw "Burn-in script not found: $burnInScript"
}

$startupProfile = $null

if ($MaxRestartRequestsPerPass -lt 1) {
    $MaxRestartRequestsPerPass = 1
}
if ($MinTickBeforeAutoRestart -lt 0) {
    $MinTickBeforeAutoRestart = 0
}
if ($StartupSoftNonOkAllowance -lt 0) {
    $StartupSoftNonOkAllowance = 0
}
if ($StartupSoftMinTick -lt 1) {
    $StartupSoftMinTick = 1
}
if ($StartupSoftMaxSnapshotAgeTicks -lt 1) {
    $StartupSoftMaxSnapshotAgeTicks = 1
}
if ($RestartRequestTimeoutSec -lt 1) {
    $RestartRequestTimeoutSec = 1
}
if ($RestartWarningThrottleSec -lt 0) {
    $RestartWarningThrottleSec = 0
}
if ($RestartThrottleLogIntervalSec -lt 0) {
    $RestartThrottleLogIntervalSec = 0
}
if ($RestartStreakThreshold -lt 1) {
    $RestartStreakThreshold = 1
}
if ($StartupHealthTimeoutSec -lt 1) {
    $StartupHealthTimeoutSec = 1
}
if ($StateFallbackTimeoutSec -lt 1) {
    $StateFallbackTimeoutSec = 1
}
if ($StartupRecoveryGraceSec -lt 0) {
    $StartupRecoveryGraceSec = 0
}
if ($AutoRestartHeavyLoadThreshold -lt 1) {
    $AutoRestartHeavyLoadThreshold = 1
}
if ($AutoRestartHeavyLoadCooldownSec -lt 1) {
    $AutoRestartHeavyLoadCooldownSec = 1
}

function Get-MissingStructureBuildOutputs {
    param(
        [string]$RootPath,
        [string]$BuildConfiguration
    )

    $structuresRoot = Join-Path $RootPath 'Structures'
    if (-not (Test-Path $structuresRoot -PathType Container)) {
        return @()
    }

    $missing = @()
    $projects = Get-ChildItem -Path $structuresRoot -Recurse -Filter *.csproj -File
    foreach ($project in $projects) {
        $projectDir = Split-Path -Parent $project.FullName
        $assemblyName = $null
        try {
            [xml]$projectXml = Get-Content -Path $project.FullName -Raw
            $assemblyNode = $projectXml.Project.PropertyGroup |
                ForEach-Object { $_.AssemblyName } |
                Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
                Select-Object -First 1
            if ($null -ne $assemblyNode) {
                $assemblyName = [string]$assemblyNode
            }
        }
        catch {
            # Fallback to project filename if csproj parse fails.
        }

        if ([string]::IsNullOrWhiteSpace($assemblyName)) {
            $assemblyName = [System.IO.Path]::GetFileNameWithoutExtension($project.Name)
        }

        $dllPath = Join-Path $projectDir ("bin\{0}\net8.0\{1}.dll" -f $BuildConfiguration, $assemblyName)
        if (-not (Test-Path $dllPath -PathType Leaf)) {
            $missing += [pscustomobject]@{
                ProjectPath = $project.FullName
                DllPath     = $dllPath
            }
        }
    }

    return $missing
}

function Build-MissingStructureProjects {
    param(
        [object[]]$MissingProjects,
        [string]$BuildConfiguration
    )

    if (-not $MissingProjects -or $MissingProjects.Count -eq 0) {
        return
    }

    Write-Host ("Prebuilding missing structure outputs ({0} project(s))..." -f $MissingProjects.Count)
    foreach ($entry in $MissingProjects) {
        $proj = [string]$entry.ProjectPath
        Write-Host ("  build -> {0}" -f $proj)
        & dotnet build $proj --configuration $BuildConfiguration --nologo --verbosity minimal
        if ($LASTEXITCODE -ne 0) {
            throw ("dotnet build failed for {0}" -f $proj)
        }
    }
}

function Get-ProjectAssemblyName {
    param(
        [string]$ProjectPath
    )

    $assemblyName = $null
    try {
        [xml]$projectXml = Get-Content -Path $ProjectPath -Raw
        $assemblyNode = $projectXml.Project.PropertyGroup |
            ForEach-Object { $_.AssemblyName } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            Select-Object -First 1
        if ($null -ne $assemblyNode) {
            $assemblyName = [string]$assemblyNode
        }
    }
    catch {
        # Fallback to project filename if csproj parse fails.
    }

    if ([string]::IsNullOrWhiteSpace($assemblyName)) {
        $assemblyName = [System.IO.Path]::GetFileNameWithoutExtension($ProjectPath)
    }

    return $assemblyName
}

function Get-LatestProjectOutputDll {
    param(
        [string]$ProjectPath,
        [string]$BuildConfiguration
    )

    $projectDir = Split-Path -Parent $ProjectPath
    $assemblyName = Get-ProjectAssemblyName -ProjectPath $ProjectPath
    $outputCandidates = @(Get-ChildItem -Path (Join-Path $projectDir ("bin\{0}" -f $BuildConfiguration)) -Recurse -Filter ("{0}.dll" -f $assemblyName) -File -ErrorAction SilentlyContinue)
    if ($outputCandidates.Count -eq 0) {
        return $null
    }

    return $outputCandidates | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
}

function Get-ProjectInputFiles {
    param(
        [string]$ProjectPath,
        [hashtable]$VisitedProjects
    )

    $ProjectPath = [System.IO.Path]::GetFullPath($ProjectPath)
    if ($VisitedProjects.ContainsKey($ProjectPath)) {
        return
    }
    $VisitedProjects[$ProjectPath] = $true

    $projectDir = Split-Path -Parent $ProjectPath
    $inputExtensions = @('.cs', '.csproj', '.json', '.resx', '.xaml')
    Get-ChildItem -Path $projectDir -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object {
            $relativePath = $_.FullName.Substring($projectDir.Length).TrimStart('\')
            -not ($relativePath -match '^(bin|obj)\\') -and
            $inputExtensions -contains $_.Extension.ToLowerInvariant()
        } |
        ForEach-Object { $_.FullName }

    try {
        [xml]$projectXml = Get-Content -Path $ProjectPath -Raw
        $references = @($projectXml.Project.ItemGroup.ProjectReference)
        foreach ($reference in $references) {
            $include = [string]$reference.Include
            if ([string]::IsNullOrWhiteSpace($include)) {
                continue
            }

            $referencePath = [System.IO.Path]::GetFullPath((Join-Path $projectDir $include))
            if (Test-Path $referencePath -PathType Leaf) {
                Get-ProjectInputFiles -ProjectPath $referencePath -VisitedProjects $VisitedProjects
            }
        }
    }
    catch {
        # The project's own inputs still participate if reference parsing fails.
    }
}

function Get-LatestProjectInputWriteTimeUtc {
    param(
        [string]$ProjectPath
    )

    $latestInput = Get-ProjectInputFiles -ProjectPath $ProjectPath -VisitedProjects @{} |
        ForEach-Object { Get-Item -LiteralPath $_ -ErrorAction SilentlyContinue } |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1

    if ($null -eq $latestInput) {
        return (Get-Item $ProjectPath).LastWriteTimeUtc
    }

    return $latestInput.LastWriteTimeUtc
}

function Ensure-ProjectFreshBuild {
    param(
        [string]$ProjectPath,
        [string]$DisplayName,
        [string[]]$DependencyOutputs,
        [string]$BuildConfiguration
    )

    if (-not (Test-Path $ProjectPath -PathType Leaf)) {
        throw ("Project not found for freshness check: {0}" -f $ProjectPath)
    }

    $latestInputWriteTimeUtc = Get-LatestProjectInputWriteTimeUtc -ProjectPath $ProjectPath
    $latestOutput = Get-LatestProjectOutputDll -ProjectPath $ProjectPath -BuildConfiguration $BuildConfiguration
    $needsBuild = $false
    $reason = ''

    if ($null -eq $latestOutput) {
        $needsBuild = $true
        $reason = ("missing {0} output" -f $BuildConfiguration)
    }
    elseif ($latestInputWriteTimeUtc -gt $latestOutput.LastWriteTimeUtc) {
        $needsBuild = $true
        $reason = 'project source is newer than output'
    }
    else {
        foreach ($dep in @($DependencyOutputs)) {
            if ([string]::IsNullOrWhiteSpace($dep)) {
                continue
            }

            if (-not (Test-Path $dep -PathType Leaf)) {
                $needsBuild = $true
                $reason = ("missing dependency output: {0}" -f ([System.IO.Path]::GetFileName($dep)))
                break
            }

            $depInfo = Get-Item $dep
            if ($depInfo.LastWriteTimeUtc -gt $latestOutput.LastWriteTimeUtc) {
                $needsBuild = $true
                $reason = ("dependency newer: {0}" -f $depInfo.Name)
                break
            }
        }
    }

    if (-not $needsBuild) {
        return
    }

    Write-Host ("Refreshing stale {0} output ({1})..." -f $DisplayName, $reason)
    & dotnet build $ProjectPath --configuration $BuildConfiguration --nologo --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        throw ("dotnet build failed for stale project refresh: {0}" -f $ProjectPath)
    }
}

function Get-EffectiveAllowableNonOkServices {
    param(
        [int]$ConfiguredAllowable,
        [int]$TotalServices
    )

    $configured = [Math]::Max(0, $ConfiguredAllowable)
    $total = [Math]::Max(0, $TotalServices)
    $ratioAllowance = [int][Math]::Ceiling($total * 0.03)
    $softFloor = if ($total -ge 48) { 2 } else { 1 }
    $adaptive = [Math]::Max($configured, [Math]::Max($softFloor, $ratioAllowance))
    $hardCap = [Math]::Max($configured, 4)
    return [Math]::Min($adaptive, $hardCap)
}

function Get-StartupHealthSnapshot {
    param(
        [string]$BaseUrl,
        [string]$StateUrl,
        [int]$StartupHealthTimeoutSec,
        [int]$StateFallbackTimeoutSec
    )

    $startupUrl = "$($BaseUrl.TrimEnd('/'))/api/v1/admin/startup-health?maxNonOkDetails=256"
    try {
        $health = Invoke-RestMethod -Uri $startupUrl -TimeoutSec $StartupHealthTimeoutSec
        $entries = @()
        if ($null -ne $health.nonOkDetails) {
            foreach ($item in @($health.nonOkDetails)) {
                $entries += [pscustomobject]@{
                    Structure = [string]$item.structure
                    Status    = if ([string]::IsNullOrWhiteSpace([string]$item.status)) { 'UNKNOWN' } else { [string]$item.status }
                    Error     = if ($null -eq $item.error) { '' } else { [string]$item.error }
                }
            }
        }

        return [pscustomobject]@{
            Tick         = [long]$health.tick
            SnapshotTick = [long]$health.lastSnapshotTick
            Total        = [int]$health.serviceCount
            NonOk        = [int]$health.nonOkCount
            NonOkEntries = $entries
        }
    }
    catch {
        $shouldFallback = $false
        $errorMessage = [string]$_.Exception.Message
        if (-not [string]::IsNullOrWhiteSpace($errorMessage) -and $errorMessage -match '\b404\b') {
            $shouldFallback = $true
        }
        elseif ($null -ne $_.Exception.Response) {
            try {
                if ([int]$_.Exception.Response.StatusCode -eq 404) {
                    $shouldFallback = $true
                }
            }
            catch {
                # ignore status-code probing issues and use the message-only heuristic.
            }
        }

        if (-not $shouldFallback) {
            throw
        }

        # Fallback only for older ControlProgram builds that do not expose startup-health.
        $state = Invoke-RestMethod -Uri $StateUrl -TimeoutSec $StateFallbackTimeoutSec
        $serviceTelemetry = @{}
        if ($null -ne $state.serviceTelemetry) {
            $serviceTelemetry = $state.serviceTelemetry
        }

        $total = 0
        $nonOk = 0
        $entries = @()
        foreach ($entry in $serviceTelemetry.PSObject.Properties) {
            $total++
            $entryValue = $entry.Value
            $statusProp = $entryValue.PSObject.Properties['lastStatus']
            $status = if ($null -ne $statusProp -and $null -ne $statusProp.Value) { [string]$statusProp.Value } else { '' }
            if ([string]::IsNullOrWhiteSpace($status) -or -not $status.Equals('OK', [System.StringComparison]::OrdinalIgnoreCase)) {
                $nonOk++
                $errorProp = $entryValue.PSObject.Properties['lastError']
                $errorText = if ($null -ne $errorProp -and $null -ne $errorProp.Value) { [string]$errorProp.Value } else { '' }
                if ([string]::IsNullOrWhiteSpace($errorText)) {
                    $errorText = if ([string]::IsNullOrWhiteSpace($status)) { 'UNKNOWN' } else { $status }
                }

                $entries += [pscustomobject]@{
                    Structure = [string]$entry.Name
                    Status    = if ([string]::IsNullOrWhiteSpace($status)) { 'UNKNOWN' } else { $status }
                    Error     = $errorText
                }
            }
        }

        return [pscustomobject]@{
            Tick         = [long]$state.tick
            SnapshotTick = [long]$state.lastSnapshotTick
            Total        = $total
            NonOk        = $nonOk
            NonOkEntries = $entries
        }
    }
}

function Get-StartupReadiness {
    param(
        [int]$Total,
        [int]$NonOk,
        [long]$Tick,
        [long]$SnapshotTick,
        [int]$AllowableNonOk,
        [int]$SoftAllowance,
        [int]$SoftMinTick,
        [int]$SoftMaxSnapshotAgeTicks
    )

    $snapshotAgeTicks = if ($SnapshotTick -ge 0 -and $Tick -ge $SnapshotTick) { ($Tick - $SnapshotTick) } else { [long]::MaxValue }
    $strictReady = ($Total -gt 0 -and $Tick -gt 0 -and $NonOk -le $AllowableNonOk)
    $softAllowable = $AllowableNonOk + [Math]::Max(0, $SoftAllowance)
    $softReady = (
        $Total -gt 0 -and
        $Tick -ge $SoftMinTick -and
        $SnapshotTick -gt 0 -and
        $snapshotAgeTicks -le $SoftMaxSnapshotAgeTicks -and
        $NonOk -le $softAllowable
    )

    return [pscustomobject]@{
        StrictReady      = $strictReady
        SoftReady        = $softReady
        SoftAllowable    = $softAllowable
        SnapshotAgeTicks = $snapshotAgeTicks
    }
}

function Get-StartupProfileParameterMap {
    return @(
        @{ Name = 'StartupTimeoutSec'; Key = 'startupTimeoutSec'; Type = 'int' },
        @{ Name = 'AllowableNonOkServices'; Key = 'allowableNonOkServices'; Type = 'int' },
        @{ Name = 'StartupSoftNonOkAllowance'; Key = 'startupSoftNonOkAllowance'; Type = 'int' },
        @{ Name = 'StartupSoftMinTick'; Key = 'startupSoftMinTick'; Type = 'int' },
        @{ Name = 'StartupSoftMaxSnapshotAgeTicks'; Key = 'startupSoftMaxSnapshotAgeTicks'; Type = 'int' },
        @{ Name = 'RestartGracePeriodSec'; Key = 'restartGracePeriodSec'; Type = 'int' },
        @{ Name = 'RestartAttemptCooldownSec'; Key = 'restartAttemptCooldownSec'; Type = 'int' },
        @{ Name = 'StartupRecoveryGraceSec'; Key = 'startupRecoveryGraceSec'; Type = 'int' },
        @{ Name = 'StartupHealthTimeoutSec'; Key = 'startupHealthTimeoutSec'; Type = 'int' },
        @{ Name = 'StateFallbackTimeoutSec'; Key = 'stateFallbackTimeoutSec'; Type = 'int' }
    )
}

function Import-StartupProfileLock {
    param(
        [string]$ProfilePath
    )

    if (-not (Test-Path $ProfilePath -PathType Leaf)) {
        return $null
    }

    try {
        $raw = Get-Content -Path $ProfilePath -Raw
        if ([string]::IsNullOrWhiteSpace($raw)) {
            return $null
        }

        return $raw | ConvertFrom-Json
    }
    catch {
        Write-Warning ("Startup profile lock ignored (read failure): {0}" -f $_.Exception.Message)
        return $null
    }
}

function Apply-StartupProfileLock {
    param(
        [psobject]$Profile,
        [hashtable]$BoundParameters
    )

    if ($null -eq $Profile) {
        return
    }

    $settings = $Profile.settings
    if ($null -eq $settings) {
        return
    }

    $applied = @()
    foreach ($map in Get-StartupProfileParameterMap) {
        $paramName = [string]$map.Name
        $settingKey = [string]$map.Key

        if ($BoundParameters.ContainsKey($paramName)) {
            continue
        }

        $property = $settings.PSObject.Properties[$settingKey]
        if ($null -eq $property) {
            continue
        }

        $value = $property.Value
        if ($null -eq $value) {
            continue
        }

        switch ($map.Type) {
            'int' { Set-Variable -Name $paramName -Scope Script -Value ([int]$value) }
            default { Set-Variable -Name $paramName -Scope Script -Value $value }
        }

        $applied += ("{0}={1}" -f $paramName, (Get-Variable -Name $paramName -Scope Script).Value)
    }

    if ($applied.Count -gt 0) {
        $version = $Profile.schemaVersion
        Write-Host ("Applied startup profile lock (schema {0}): {1}" -f $version, ($applied -join ', '))
    }
}

function Export-StartupProfileLock {
    param(
        [string]$ProfilePath,
        [string]$StatusSummary,
        [bool]$StrictReady,
        [bool]$SoftReady
    )

    $settings = [ordered]@{}
    foreach ($map in Get-StartupProfileParameterMap) {
        $name = [string]$map.Name
        $key = [string]$map.Key
        $settings[$key] = (Get-Variable -Name $name -Scope Script).Value
    }

    $profile = [ordered]@{
        schemaVersion = 1
        savedAtUtc = [DateTime]::UtcNow.ToString('o')
        readiness = if ($StrictReady) { 'strict' } elseif ($SoftReady) { 'soft' } else { 'unready' }
        summary = $StatusSummary
        settings = $settings
    }

    try {
        $dir = Split-Path -Parent $ProfilePath
        if (-not [string]::IsNullOrWhiteSpace($dir) -and -not (Test-Path $dir -PathType Container)) {
            New-Item -Path $dir -ItemType Directory -Force | Out-Null
        }

        $json = $profile | ConvertTo-Json -Depth 8
        Set-Content -Path $ProfilePath -Value $json -Encoding UTF8
        Write-Host ("Startup profile lock saved: {0}" -f $ProfilePath)
    }
    catch {
        Write-Warning ("Unable to save startup profile lock: {0}" -f $_.Exception.Message)
    }
}

if ($UseStartupProfileLock) {
    $startupProfile = Import-StartupProfileLock -ProfilePath $startupProfileLockPath
    Apply-StartupProfileLock -Profile $startupProfile -BoundParameters $PSBoundParameters
}

if ($CleanStart) {
    if (Test-Path $killScript -PathType Leaf) {
        Write-Host 'Stopping any existing DNNE processes...'
        & $killScript
    }
}

if ($PrebuildMissingStructureProjects) {
    $missingBuildOutputs = @(Get-MissingStructureBuildOutputs -RootPath $repoRoot -BuildConfiguration $Configuration)
    if ($missingBuildOutputs.Count -gt 0) {
        Build-MissingStructureProjects -MissingProjects $missingBuildOutputs -BuildConfiguration $Configuration
    }
}

if ($NoBuild) {
    $protocolDll = Join-Path $repoRoot ("Protocol\bin\{0}\net8.0\NeuralResonanceEngine.Protocol.dll" -f $Configuration)
    $contractsDll = Join-Path $repoRoot ("Shared.Contracts\bin\{0}\net8.0\NeuralResonanceEngine.Shared.Contracts.dll" -f $Configuration)
    Ensure-ProjectFreshBuild -ProjectPath $controlProj -DisplayName 'ControlProgram' -DependencyOutputs @($protocolDll, $contractsDll) -BuildConfiguration $Configuration
    if (-not $NoEditor) {
        Ensure-ProjectFreshBuild -ProjectPath $editorProj -DisplayName 'WPF editor' -DependencyOutputs @() -BuildConfiguration $Configuration
    }
}

$runArgText = if ($NoBuild) {
    "run --no-build --no-launch-profile --configuration $Configuration --project `"$controlProj`" -- --StructureProcessHost:Configuration $Configuration"
}
else {
    "run --no-launch-profile --configuration $Configuration --project `"$controlProj`" -- --StructureProcessHost:Configuration $Configuration"
}

Write-Host ("Starting ControlProgram ({0})..." -f $Configuration)
$controlLogs = New-DnneProcessLogPaths -Name 'controlprogram'
$controlEnvironment = @{
    ASPNETCORE_ENVIRONMENT = 'Production'
    ASPNETCORE_URLS = $ControlBaseUrl
    ControlPublishUrl = "$($ControlBaseUrl.TrimEnd('/'))/api/v1/publish/step"
    DOTNET_ENVIRONMENT = 'Production'
    PORT = ([Uri]$ControlBaseUrl).Port
    SnapshotEndpoint = "$($ControlBaseUrl.TrimEnd('/'))/api/v1/snapshot"
}
$previousControlEnvironment = @{}
foreach ($entry in $controlEnvironment.GetEnumerator()) {
    $previousControlEnvironment[$entry.Key] = [Environment]::GetEnvironmentVariable($entry.Key, 'Process')
    [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
}
try {
    $controlProc = Start-Process `
        -FilePath 'dotnet' `
        -ArgumentList $runArgText `
        -WorkingDirectory (Split-Path -Parent $controlProj) `
        -RedirectStandardOutput $controlLogs.StdOut `
        -RedirectStandardError $controlLogs.StdErr `
        -PassThru
}
finally {
    foreach ($entry in $previousControlEnvironment.GetEnumerator()) {
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }
}
Write-Host ("ControlProgram PID: {0}" -f $controlProc.Id)
Write-Host ("ControlProgram stdout: {0}" -f $controlLogs.StdOut)
Write-Host ("ControlProgram stderr: {0}" -f $controlLogs.StdErr)

$baseUrl = $ControlBaseUrl.TrimEnd('/')
$stateUrl = "$baseUrl/api/v1/state"
$restartServiceUrl = "$baseUrl/api/v1/admin/restart-service"
$deadline = [DateTime]::UtcNow.AddSeconds([Math]::Max(10, $StartupTimeoutSec))
$startupBeganUtc = [DateTime]::UtcNow
$lastSummary = 'awaiting state endpoint'
$lastNonOkDetails = ''
$ready = $false
$readyDegraded = $false
$controlExited = $false
$controlExitCode = 0
$restartAttemptsByStructure = @{}
$lastRestartAttemptUtc = [DateTime]::MinValue
$restartFailureLastLoggedByStructure = @{}
$restartFailureLastMessageByStructure = @{}
$nonOkAboveAllowanceStreak = 0
$lastRestartThrottleSummary = ''
$lastRestartThrottleLogUtc = [DateTime]::MinValue
$lastHeavyLoadRestartLogUtc = [DateTime]::MinValue

while ([DateTime]::UtcNow -lt $deadline) {
    if ($controlProc.HasExited) {
        $controlExited = $true
        $controlExitCode = $controlProc.ExitCode
    }

    try {
        $health = Get-StartupHealthSnapshot `
            -BaseUrl $baseUrl `
            -StateUrl $stateUrl `
            -StartupHealthTimeoutSec $StartupHealthTimeoutSec `
            -StateFallbackTimeoutSec $StateFallbackTimeoutSec
        $total = [int]$health.Total
        $nonOk = [int]$health.NonOk
        $nonOkEntries = @($health.NonOkEntries)

        if ($nonOkEntries.Count -gt 0) {
            $preview = $nonOkEntries |
                Sort-Object Structure |
                Select-Object -First 8 |
                ForEach-Object { "$($_.Structure)=$($_.Status) ($($_.Error))" }
            $lastNonOkDetails = $preview -join ', '
            if ($nonOkEntries.Count -gt 8) {
                $lastNonOkDetails += ", ... (+$($nonOkEntries.Count - 8) more)"
            }
        }
        else {
            $lastNonOkDetails = ''
        }

        $tick = [long]$health.Tick
        $snapshotTick = [long]$health.SnapshotTick
        $effectiveAllowableNonOk = Get-EffectiveAllowableNonOkServices -ConfiguredAllowable $AllowableNonOkServices -TotalServices $total
        $readiness = Get-StartupReadiness `
            -Total $total `
            -NonOk $nonOk `
            -Tick $tick `
            -SnapshotTick $snapshotTick `
            -AllowableNonOk $effectiveAllowableNonOk `
            -SoftAllowance $StartupSoftNonOkAllowance `
            -SoftMinTick $StartupSoftMinTick `
            -SoftMaxSnapshotAgeTicks $StartupSoftMaxSnapshotAgeTicks
        $lastSummary = "tick=$tick services=$total nonOk=$nonOk lastSnapshotTick=$snapshotTick snapshotAgeTicks=$($readiness.SnapshotAgeTicks)"
        $lastSummary += " allowableNonOk=$effectiveAllowableNonOk softAllowableNonOk=$($readiness.SoftAllowable)"
        if (-not [string]::IsNullOrWhiteSpace($lastNonOkDetails)) {
            $lastSummary += " nonOkDetails=$lastNonOkDetails"
        }

        if ($nonOkEntries.Count -gt $effectiveAllowableNonOk) {
            $nonOkAboveAllowanceStreak += 1
        }
        else {
            $nonOkAboveAllowanceStreak = 0
        }

        $heavyRestartLoad = ($nonOkEntries.Count -ge $AutoRestartHeavyLoadThreshold)
        $restartPassCooldownSec = if ($heavyRestartLoad) {
            [Math]::Max(1, [Math]::Min($RestartAttemptCooldownSec, $AutoRestartHeavyLoadCooldownSec))
        }
        else {
            $RestartAttemptCooldownSec
        }

        if (
            $AutoRestartNonOk -and
            $nonOkEntries.Count -gt $effectiveAllowableNonOk -and
            ($nonOkAboveAllowanceStreak -ge $RestartStreakThreshold) -and
            ($tick -ge $MinTickBeforeAutoRestart) -and
            (([DateTime]::UtcNow - $startupBeganUtc).TotalSeconds -ge $RestartGracePeriodSec) -and
            (([DateTime]::UtcNow - $lastRestartAttemptUtc).TotalSeconds -ge $restartPassCooldownSec)
        ) {
            $restartRequested = $false
            $restartBudget = if ($heavyRestartLoad) {
                [Math]::Max(1, [Math]::Min($MaxRestartRequestsPerPass, 12))
            }
            else {
                [Math]::Max(1, $MaxRestartRequestsPerPass)
            }
            $restartCandidates = $nonOkEntries |
                Where-Object {
                    $status = [string]$_.Status
                    $errorText = [string]$_.Error
                    if ([string]::IsNullOrWhiteSpace($status)) {
                        return $false
                    }

                    if ($status.Equals('UNKNOWN', [System.StringComparison]::OrdinalIgnoreCase)) {
                        return $false
                    }

                    if ($status.Equals('STARTING', [System.StringComparison]::OrdinalIgnoreCase) -and [string]::IsNullOrWhiteSpace($errorText)) {
                        return $false
                    }

                    $structure = [string]$_.Structure
                    if ([string]::IsNullOrWhiteSpace($structure)) {
                        return $false
                    }

                    $attempts = 0
                    if ($restartAttemptsByStructure.ContainsKey($structure)) {
                        $attempts = [int]$restartAttemptsByStructure[$structure]
                    }
                    if ($attempts -ge $MaxRestartAttemptsPerStructure) {
                        return $false
                    }

                    return $true
                } |
                Sort-Object `
                    @{ Expression = {
                        $structure = [string]$_.Structure
                        if ($restartAttemptsByStructure.ContainsKey($structure)) {
                            [int]$restartAttemptsByStructure[$structure]
                        }
                        else {
                            0
                        }
                    }; Ascending = $true }, `
                    @{ Expression = { [string]$_.Status }; Ascending = $true }, `
                    @{ Expression = { [string]$_.Structure }; Ascending = $true } |
                Select-Object -First $restartBudget

            if ($heavyRestartLoad) {
                $nowUtc = [DateTime]::UtcNow
                if (($nowUtc - $lastHeavyLoadRestartLogUtc).TotalSeconds -ge [Math]::Max(1, $RestartThrottleLogIntervalSec)) {
                    Write-Host ("Restart heavy-load mode: non-OK services={0}, cooldown={1}s, budget={2}/pass." -f $nonOkEntries.Count, $restartPassCooldownSec, $restartBudget)
                    $lastHeavyLoadRestartLogUtc = $nowUtc
                }
            }

            foreach ($entry in $restartCandidates) {
                $structure = [string]$entry.Structure
                if ([string]::IsNullOrWhiteSpace($structure)) {
                    continue
                }

                $attempts = 0
                if ($restartAttemptsByStructure.ContainsKey($structure)) {
                    $attempts = [int]$restartAttemptsByStructure[$structure]
                }
                if ($attempts -ge $MaxRestartAttemptsPerStructure) {
                    continue
                }

                try {
                    $payload = @{ structureId = $structure } | ConvertTo-Json -Compress
                    $null = Invoke-RestMethod -Uri $restartServiceUrl -Method Post -ContentType 'application/json' -Body $payload -TimeoutSec $RestartRequestTimeoutSec
                    $restartAttemptsByStructure[$structure] = $attempts + 1
                    if ($restartFailureLastLoggedByStructure.ContainsKey($structure)) {
                        $restartFailureLastLoggedByStructure.Remove($structure) | Out-Null
                    }
                    if ($restartFailureLastMessageByStructure.ContainsKey($structure)) {
                        $restartFailureLastMessageByStructure.Remove($structure) | Out-Null
                    }
                    Write-Host ("Requested restart for {0} (attempt {1}/{2})" -f $structure, ($attempts + 1), $MaxRestartAttemptsPerStructure)
                    $restartRequested = $true
                }
                catch {
                    $restartAttemptsByStructure[$structure] = $attempts + 1
                    $errorMessage = [string]$_.Exception.Message
                    $nowUtc = [DateTime]::UtcNow
                    $shouldWarn = $true
                    if ($RestartWarningThrottleSec -gt 0 -and $restartFailureLastLoggedByStructure.ContainsKey($structure)) {
                        $lastWarnUtc = [DateTime]$restartFailureLastLoggedByStructure[$structure]
                        $lastWarnMessage = if ($restartFailureLastMessageByStructure.ContainsKey($structure)) { [string]$restartFailureLastMessageByStructure[$structure] } else { '' }
                        $elapsedSec = ($nowUtc - $lastWarnUtc).TotalSeconds
                        if ($elapsedSec -lt $RestartWarningThrottleSec -and $errorMessage -eq $lastWarnMessage) {
                            $shouldWarn = $false
                        }
                    }

                    if ($shouldWarn) {
                        Write-Warning ("Failed restart request for {0}: {1}" -f $structure, $errorMessage)
                        $restartFailureLastLoggedByStructure[$structure] = $nowUtc
                        $restartFailureLastMessageByStructure[$structure] = $errorMessage
                    }
                    $restartRequested = $true
                }
            }

            if ($restartCandidates.Count -gt 0 -and $nonOkEntries.Count -gt $restartCandidates.Count) {
                $throttleSummary = ("Restart throttle: requested {0}/{1} non-OK services this pass." -f $restartCandidates.Count, $nonOkEntries.Count)
                $nowUtc = [DateTime]::UtcNow
                $shouldLogThrottle = $false
                if ($throttleSummary -ne $lastRestartThrottleSummary) {
                    $shouldLogThrottle = $true
                }
                elseif ($RestartThrottleLogIntervalSec -gt 0 -and ($nowUtc - $lastRestartThrottleLogUtc).TotalSeconds -ge $RestartThrottleLogIntervalSec) {
                    $shouldLogThrottle = $true
                }
                elseif ($RestartThrottleLogIntervalSec -eq 0) {
                    $shouldLogThrottle = $true
                }

                if ($shouldLogThrottle) {
                    Write-Host $throttleSummary
                    $lastRestartThrottleSummary = $throttleSummary
                    $lastRestartThrottleLogUtc = $nowUtc
                }
            }

            if ($restartRequested) {
                $lastRestartAttemptUtc = [DateTime]::UtcNow
            }
        }

        if ($readiness.StrictReady) {
            $ready = $true
            break
        }
        if ($readiness.SoftReady) {
            $readyDegraded = $true
            break
        }
    }
    catch {
        $lastSummary = $_.Exception.Message
    }

    Start-Sleep -Milliseconds ([Math]::Max(100, $PollIntervalMs))
}

if (-not $ready -and -not $controlExited) {
    $graceDeadline = [DateTime]::UtcNow.AddSeconds($StartupRecoveryGraceSec)
    while ([DateTime]::UtcNow -lt $graceDeadline) {
        try {
            $health = Get-StartupHealthSnapshot `
                -BaseUrl $baseUrl `
                -StateUrl $stateUrl `
                -StartupHealthTimeoutSec $StartupHealthTimeoutSec `
                -StateFallbackTimeoutSec $StateFallbackTimeoutSec
            $total = [int]$health.Total
            $nonOk = [int]$health.NonOk
            $tick = [long]$health.Tick
            $snapshotTick = [long]$health.SnapshotTick
            $effectiveAllowableNonOk = Get-EffectiveAllowableNonOkServices -ConfiguredAllowable $AllowableNonOkServices -TotalServices $total
            $readiness = Get-StartupReadiness `
                -Total $total `
                -NonOk $nonOk `
                -Tick $tick `
                -SnapshotTick $snapshotTick `
                -AllowableNonOk $effectiveAllowableNonOk `
                -SoftAllowance $StartupSoftNonOkAllowance `
                -SoftMinTick $StartupSoftMinTick `
                -SoftMaxSnapshotAgeTicks $StartupSoftMaxSnapshotAgeTicks
            $lastSummary = "tick=$tick services=$total nonOk=$nonOk lastSnapshotTick=$snapshotTick snapshotAgeTicks=$($readiness.SnapshotAgeTicks) allowableNonOk=$effectiveAllowableNonOk softAllowableNonOk=$($readiness.SoftAllowable)"
            if ($readiness.StrictReady) {
                $ready = $true
                break
            }
            if ($readiness.SoftReady) {
                $readyDegraded = $true
                break
            }
        }
        catch {
            $lastSummary = $_.Exception.Message
        }

        Start-Sleep -Milliseconds 500
    }
}

if (-not $ready) {
    try {
        $health = Get-StartupHealthSnapshot `
            -BaseUrl $baseUrl `
            -StateUrl $stateUrl `
            -StartupHealthTimeoutSec $StartupHealthTimeoutSec `
            -StateFallbackTimeoutSec $StateFallbackTimeoutSec
        $tick = [long]$health.Tick
        $snapshotTick = [long]$health.SnapshotTick
        $total = [int]$health.Total
        $nonOk = [int]$health.NonOk
        $nonOkEntries = @($health.NonOkEntries)

        $effectiveAllowableNonOk = Get-EffectiveAllowableNonOkServices -ConfiguredAllowable $AllowableNonOkServices -TotalServices $total
        $readiness = Get-StartupReadiness `
            -Total $total `
            -NonOk $nonOk `
            -Tick $tick `
            -SnapshotTick $snapshotTick `
            -AllowableNonOk $effectiveAllowableNonOk `
            -SoftAllowance $StartupSoftNonOkAllowance `
            -SoftMinTick $StartupSoftMinTick `
            -SoftMaxSnapshotAgeTicks $StartupSoftMaxSnapshotAgeTicks

        if ($readiness.SoftReady) {
            $readyDegraded = $true
            $lastSummary = "tick=$tick services=$total nonOk=$nonOk lastSnapshotTick=$snapshotTick snapshotAgeTicks=$($readiness.SnapshotAgeTicks) allowableNonOk=$effectiveAllowableNonOk softAllowableNonOk=$($readiness.SoftAllowable) (degraded-accepted)"
            if ($nonOkEntries.Count -gt 0) {
                $preview = $nonOkEntries |
                    Sort-Object Structure |
                    Select-Object -First 8 |
                    ForEach-Object { "$($_.Structure)=$($_.Status) ($($_.Error))" }
                $details = $preview -join ', '
                if ($nonOkEntries.Count -gt 8) {
                    $details += ", ... (+$($nonOkEntries.Count - 8) more)"
                }
                if (-not [string]::IsNullOrWhiteSpace($details)) {
                    $lastSummary += " nonOkDetails=$details"
                }
            }
        }
    }
    catch {
        # keep original timeout behavior if we cannot validate degraded readiness
    }
}

if ($readyDegraded -and -not $ready) {
    Write-Warning ("Startup soft-accepted: {0}" -f $lastSummary)
}

if (-not $ready -and -not $readyDegraded) {
    $exitSuffix = if ($controlExited) { " ControlProgram process exited (PID $($controlProc.Id), code $controlExitCode)." } else { '' }
    $message = "ControlProgram did not reach startup health target before timeout. Last: $lastSummary.$exitSuffix"
    if (-not $StartEditorOnTimeout) {
        throw $message
    }

    Write-Warning $message
}

if (-not $SkipBurnInGate -and ($ready -or $readyDegraded)) {
    Write-Host ("Running burn-in gate for {0}s..." -f $BurnInDurationSec)
    $burnInFailed = $false
    $burnInError = ''
    try {
        & $burnInScript `
            -ControlBaseUrl $baseUrl `
            -DurationSec $BurnInDurationSec `
            -PollIntervalMs $BurnInPollIntervalMs `
            -AllowableNonOkServices $AllowableNonOkServices `
            -MaxSnapshotAgeTicks $BurnInMaxSnapshotAgeTicks `
            -SnapshotAgeGraceSec $BurnInSnapshotAgeGraceSec `
            -NonOkGraceSec $BurnInNonOkGraceSec `
            -WarmupSec $BurnInWarmupSec `
            -SensoryIntervalSec $BurnInSensoryIntervalSec `
            -RestartCycleIntervalSec $BurnInRestartCycleIntervalSec `
            -RestartRecoveryTimeoutSec $BurnInRestartRecoveryTimeoutSec `
            -MaxSensory404 $BurnInMaxSensory404 `
            -MaxSensoryZeroDelivered $BurnInMaxSensoryZeroDelivered `
            -MazeStuckFailAfterSec $BurnInMazeStuckFailAfterSec `
            -MazeStuckNoProgressWindowSec $BurnInMazeStuckNoProgressWindowSec `
            -MazeStuckWallImpactMinRatePerSec $BurnInMazeStuckWallImpactMinRatePerSec `
            -MazeStuckMaxMotorDispatchPerPoll $BurnInMazeStuckMaxMotorDispatchPerPoll `
            -SummaryPath $burnInSummaryPath `
            -SamplesPath $burnInSamplesPath
    }
    catch {
        $burnInFailed = $true
        $burnInError = $_.Exception.Message
    }

    if ($burnInFailed) {
        $burnInMessage = "Burn-in gate failed: $burnInError"
        if (-not $StartEditorOnTimeout) {
            throw $burnInMessage
        }

        Write-Warning $burnInMessage
        Write-Warning ("Burn-in artifacts: summary={0}, samples={1}" -f $burnInSummaryPath, $burnInSamplesPath)
    }
    else {
        Write-Host ("Burn-in gate passed. Artifacts: summary={0}, samples={1}" -f $burnInSummaryPath, $burnInSamplesPath)
    }
}

if (($ready -or $readyDegraded) -and $UseStartupProfileLock) {
    Export-StartupProfileLock `
        -ProfilePath $startupProfileLockPath `
        -StatusSummary $lastSummary `
        -StrictReady:$ready `
        -SoftReady:$readyDegraded
}

if (-not $NoEditor) {
    $editorExe = Join-Path $repoRoot ("src\NRE.WpfEditor\bin\{0}\net10.0-windows\NRE.WpfEditor.exe" -f $Configuration)
    $editorArgText = if ($NoBuild) {
        if (Test-Path $editorExe -PathType Leaf) {
            "run --no-build --no-launch-profile --configuration $Configuration --project `"$editorProj`""
        }
        else {
            Write-Warning ("Editor apphost not found at {0}; falling back to build+run." -f $editorExe)
            "run --no-launch-profile --configuration $Configuration --project `"$editorProj`""
        }
    }
    else {
        "run --no-launch-profile --configuration $Configuration --project `"$editorProj`""
    }

    Write-Host 'Starting WPF editor...'
    $editorLogs = New-DnneProcessLogPaths -Name 'wpf-editor'
    $editorProc = Start-Process `
        -FilePath 'dotnet' `
        -ArgumentList $editorArgText `
        -WorkingDirectory (Split-Path -Parent $editorProj) `
        -RedirectStandardOutput $editorLogs.StdOut `
        -RedirectStandardError $editorLogs.StdErr `
        -PassThru
    Write-Host ("WPF Editor PID: {0}" -f $editorProc.Id)
    Write-Host ("WPF Editor stdout: {0}" -f $editorLogs.StdOut)
    Write-Host ("WPF Editor stderr: {0}" -f $editorLogs.StdErr)
}

if ($readyDegraded) {
    Write-Warning "Startup accepted in degraded mode. Status: $lastSummary"
}
else {
    Write-Host "DNNE stack startup complete. Status: $lastSummary"
}

