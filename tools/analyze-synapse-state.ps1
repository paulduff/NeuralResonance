[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$StateDirectory,

    [string]$OutputPath,

    [string]$ServiceRegistryPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$resolvedDirectory = [IO.Path]::GetFullPath($StateDirectory)
if (-not (Test-Path -LiteralPath $resolvedDirectory -PathType Container)) {
    throw "Synapse state directory does not exist: $resolvedDirectory"
}

$readErrors = [Collections.Generic.List[object]]::new()
$instances = [Collections.Generic.List[object]]::new()
$files = @(Get-ChildItem -LiteralPath $resolvedDirectory -Filter '*.synapses.json' -File | Sort-Object Name)
foreach ($file in $files) {
    try {
        $state = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
        $instanceKey = if ([string]::IsNullOrWhiteSpace([string]$state.InstanceKey)) {
            $file.BaseName -replace '\.synapses$', ''
        }
        else {
            [string]$state.InstanceKey
        }
        $side = 'Unpaired'
        $population = $instanceKey
        if ($instanceKey -match '^(?<side>[LR])_(?<population>.+)$') {
            $side = $Matches.side
            $population = $Matches.population
        }

        $inbound = @($state.Inbound)
        $outbound = @($state.Outbound)
        $allSynapses = @($inbound) + @($outbound)
        $updates = [long](($allSynapses | Measure-Object -Property UpdateCount -Sum).Sum)
        $floorCount = @($allSynapses | Where-Object { [double]$_.VesicleQuanta -le 0.050001 }).Count
        $ceilingCount = @($allSynapses | Where-Object { [double]$_.VesicleQuanta -ge 4.999999 }).Count
        $absolutePlasticity = 0.0
        foreach ($synapse in $allSynapses) {
            if ($null -ne $synapse.PSObject.Properties['TotalAbsolutePlasticityChange']) {
                $absolutePlasticity += [double]$synapse.TotalAbsolutePlasticityChange
            }
        }
        $instances.Add([pscustomobject]@{
            InstanceKey = $instanceKey
            Population = $population
            Side = $side
            SchemaVersion = [int]$state.SchemaVersion
            InboundSynapses = $inbound.Count
            OutboundSynapses = $outbound.Count
            TotalSynapses = $allSynapses.Count
            Updates = $updates
            FloorCount = $floorCount
            CeilingCount = $ceilingCount
            TotalAbsolutePlasticityChange = $absolutePlasticity
            SavedAtUtc = $state.SavedAtUtc
            File = $file.FullName
        })
    }
    catch {
        $readErrors.Add([pscustomobject]@{
            File = $file.FullName
            Error = $_.Exception.Message
        })
    }
}

function Get-NormalizedAsymmetry {
    param([double]$Left, [double]$Right)

    $scale = [Math]::Max(1.0, [Math]::Max([Math]::Abs($Left), [Math]::Abs($Right)))
    return [Math]::Abs($Left - $Right) / $scale
}

$configuredRegistry = if ([string]::IsNullOrWhiteSpace($ServiceRegistryPath)) {
    Join-Path $PSScriptRoot '..\ControlProgram\appsettings.json'
}
else {
    $ServiceRegistryPath
}
$expectedPopulations = @()
if (Test-Path -LiteralPath $configuredRegistry -PathType Leaf) {
    $configuration = Get-Content -LiteralPath $configuredRegistry -Raw | ConvertFrom-Json
    if ($null -ne $configuration.PSObject.Properties['ServiceRegistry']) {
        $expectedPopulations = @($configuration.ServiceRegistry.PSObject.Properties.Name)
    }
}

$observedPopulations = @($instances | Where-Object Side -in @('L', 'R') | Select-Object -ExpandProperty Population)
$populationNames = @($expectedPopulations + $observedPopulations | Sort-Object -Unique)
$bilateral = [Collections.Generic.List[object]]::new()
foreach ($populationName in $populationNames) {
    $left = @($instances | Where-Object { $_.Population -eq $populationName -and $_.Side -eq 'L' }) | Select-Object -First 1
    $right = @($instances | Where-Object { $_.Population -eq $populationName -and $_.Side -eq 'R' }) | Select-Object -First 1
    $leftSynapses = if ($null -eq $left) { 0 } else { $left.TotalSynapses }
    $rightSynapses = if ($null -eq $right) { 0 } else { $right.TotalSynapses }
    $leftUpdates = if ($null -eq $left) { 0 } else { $left.Updates }
    $rightUpdates = if ($null -eq $right) { 0 } else { $right.Updates }
    $bilateral.Add([pscustomobject]@{
        Population = $populationName
        LeftPresent = $null -ne $left
        RightPresent = $null -ne $right
        LeftSynapses = $leftSynapses
        RightSynapses = $rightSynapses
        SynapseAsymmetry = Get-NormalizedAsymmetry -Left $leftSynapses -Right $rightSynapses
        LeftUpdates = $leftUpdates
        RightUpdates = $rightUpdates
        UpdateAsymmetry = Get-NormalizedAsymmetry -Left $leftUpdates -Right $rightUpdates
    })
}

$totalSynapses = [long](($instances | Measure-Object -Property TotalSynapses -Sum).Sum)
$totalUpdates = [long](($instances | Measure-Object -Property Updates -Sum).Sum)
$totalFloor = [long](($instances | Measure-Object -Property FloorCount -Sum).Sum)
$totalCeiling = [long](($instances | Measure-Object -Property CeilingCount -Sum).Sum)
$missingPairs = @($bilateral | Where-Object { -not $_.LeftPresent -or -not $_.RightPresent })
$report = [pscustomobject]@{
    ProtocolVersion = 'dnne.synapse-state-audit.v1'
    GeneratedUtc = [DateTimeOffset]::UtcNow
    StateDirectory = $resolvedDirectory
    FileCount = $files.Count
    ParsedFileCount = $instances.Count
    ExpectedPopulationCount = $expectedPopulations.Count
    ReadErrors = @($readErrors)
    Summary = [pscustomobject]@{
        TotalSynapses = $totalSynapses
        TotalUpdates = $totalUpdates
        FloorCount = $totalFloor
        FloorFraction = if ($totalSynapses -eq 0) { 0.0 } else { $totalFloor / [double]$totalSynapses }
        CeilingCount = $totalCeiling
        CeilingFraction = if ($totalSynapses -eq 0) { 0.0 } else { $totalCeiling / [double]$totalSynapses }
        MissingBilateralPopulations = $missingPairs.Count
    }
    MissingBilateralPopulations = $missingPairs
    BilateralPopulations = @($bilateral | Sort-Object UpdateAsymmetry -Descending)
    Instances = @($instances)
}

Write-Host ("Parsed {0}/{1} generation files; {2:N0} synapses; {3:N0} updates." -f $instances.Count, $files.Count, $totalSynapses, $totalUpdates)
Write-Host ("Weight floors {0:P2}; ceilings {1:P2}; missing bilateral populations {2}." -f $report.Summary.FloorFraction, $report.Summary.CeilingFraction, $missingPairs.Count)
if ($missingPairs.Count -gt 0) {
    Write-Warning ("Missing hemisphere files: " + (($missingPairs | ForEach-Object Population) -join ', '))
}

$bilateral |
    Sort-Object UpdateAsymmetry -Descending |
    Select-Object -First 15 Population, LeftSynapses, RightSynapses, SynapseAsymmetry, LeftUpdates, RightUpdates, UpdateAsymmetry |
    Format-Table -AutoSize

if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
    $destination = [IO.Path]::GetFullPath($OutputPath)
    $parent = Split-Path -Parent $destination
    if (-not [string]::IsNullOrWhiteSpace($parent)) {
        [IO.Directory]::CreateDirectory($parent) | Out-Null
    }
    $temporary = "$destination.$PID.$([Guid]::NewGuid().ToString('N')).tmp"
    try {
        $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $temporary -Encoding utf8
        Move-Item -LiteralPath $temporary -Destination $destination -Force
    }
    finally {
        if (Test-Path -LiteralPath $temporary) {
            Remove-Item -LiteralPath $temporary -Force
        }
    }
    Write-Host "Audit report: $destination"
}

$report
