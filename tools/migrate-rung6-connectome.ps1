param(
    [string]$Path = 'connectivity/dnne-connectivity.json'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$connectomePath = [IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
$repoPrefix = ([IO.Path]::GetFullPath($repoRoot)).TrimEnd([IO.Path]::DirectorySeparatorChar) +
    [IO.Path]::DirectorySeparatorChar
if (-not $connectomePath.StartsWith($repoPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Connectome path escapes the repository: $connectomePath"
}

$retired = @(
    'Thalamus',
    'Amygdala',
    'Hypothalamus',
    'GlobusPallidus',
    'DeepCerebellarNuclei',
    'Medulla'
)
$sourceMoves = @{
    'Amygdala|Acc' = @('BasolateralAmygdala', 'basolateral_cingulate_salience')
    'Amygdala|Insula' = @('BasolateralAmygdala', 'basolateral_insular_salience')
    'Hypothalamus|RapheNuclei' = @('DorsomedialHypothalamicNucleus', 'dmh_raphe_state_drive')
    'DeepCerebellarNuclei|SuperiorColliculus' = @('DentateNucleus', 'dentatotectal_orienting')
    'DeepCerebellarNuclei|PremotorCortex' = @('DentateNucleus', 'dentate_premotor_planning')
    'Medulla|LocusCoeruleus' = @('ReticularFormation', 'reticular_locus_coeruleus_drive')
    'Medulla|RapheNuclei' = @('ReticularFormation', 'reticular_raphe_drive')
    'Medulla|HypoglossalNucleus' = @('ReticularFormation', 'reticular_hypoglossal_premotor')
}
$targetMoves = @{
    'Pfc|Thalamus' = @('MediodorsalThalamus', 'prefrontal_mediodorsal_feedback')
    'SpinalCordMotor|Thalamus' = @('VentralPosterolateralThalamus', 'spinal_vpl_proprioceptive_feedback')
    'SuperiorColliculus|Thalamus' = @('Pulvinar', 'tectopulvinar_orienting')
    'LocusCoeruleus|Thalamus' = @('IntralaminarThalamus', 'lc_intralaminar_gain')
    'Insula|Amygdala' = @('BasolateralAmygdala', 'insula_basolateral_salience')
    'OlfactoryBulb|Amygdala' = @('CorticalAmygdala', 'olfactory_cortical_amygdala')
    'OrbitofrontalCortex|Amygdala' = @('BasolateralAmygdala', 'orbitofrontal_basolateral_value')
    'TemporalAssociation|Amygdala' = @('BasolateralAmygdala', 'temporal_basolateral_salience')
    'LocusCoeruleus|Amygdala' = @('BasolateralAmygdala', 'lc_basolateral_arousal_bias')
    'Vta|Amygdala' = @('BasolateralAmygdala', 'vta_basolateral_salience')
    'PeriaqueductalGray|Amygdala' = @('CentralAmygdala', 'pag_central_amygdala_feedback')
    'TemporalPole|Amygdala' = @('BasolateralAmygdala', 'temporal_pole_basolateral_context')
    'VentromedialPrefrontalCortex|Amygdala' = @('BasolateralAmygdala', 'vmpfc_basolateral_regulation')
    'VentromedialHypothalamicNucleus|Amygdala' = @('CentralAmygdala', 'vmh_central_amygdala_defense_feedback')
    'FastigialNucleus|Hypothalamus' = @('ParaventricularHypothalamicNucleus', 'fastigial_pvn_autonomic_coordination')
    'Stn|GlobusPallidus' = @('GPe', 'stn_gpe_drive')
}

$input = Get-Content $connectomePath -Raw | ConvertFrom-Json
if ($input -isnot [Array] -or $input.Count -lt 2) {
    throw "Connectome must contain an array of source records; refusing to rewrite $connectomePath"
}
if (@($input | Where-Object { [string]::IsNullOrWhiteSpace([string]$_.source) }).Count -ne 0) {
    throw 'Connectome contains a source record without a source ID.'
}
$buckets = [ordered]@{}
$edgeKeys = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($rule in $input) {
    foreach ($connection in @($rule.connections)) {
        $source = [string]$rule.source
        $target = [string]$connection.target
        if ($source -eq 'Pons') { $source = 'PontineNuclei' }
        if ($target -eq 'Pons') { $target = 'PontineNuclei' }
        if ($source -eq 'BasalForebrain') { $source = 'NucleusBasalis' }
        if ($target -eq 'BasalForebrain') { $target = 'NucleusBasalis' }

        $key = "$source|$target"
        if ($sourceMoves.ContainsKey($key)) {
            $source = $sourceMoves[$key][0]
            $connection.projectionType = $sourceMoves[$key][1]
        }
        elseif ($targetMoves.ContainsKey($key)) {
            $target = $targetMoves[$key][0]
            $connection.projectionType = $targetMoves[$key][1]
        }

        if ($retired -contains $source -or $retired -contains $target) {
            continue
        }
        if ($source -eq 'PontineNuclei' -and
            $target -notin @('CerebellarGranule', 'CerebellarVermis', 'CerebellarLobules')) {
            continue
        }
        if ($target -eq 'PontineNuclei' -and
            $source -notin @('M1', 'Ppc', 'PremotorCortex', 'Sma')) {
            continue
        }
        if ($source -eq 'NucleusBasalis' -and
            $target -notin @('V1', 'Trn', 'Pfc', 'MedialSeptalNucleus')) {
            continue
        }

        $connection.target = $target
        $edgeKey = "$source|$target|$($connection.neurotransmitter)|$($connection.projectionType)"
        if (-not $edgeKeys.Add($edgeKey)) {
            continue
        }
        if (-not $buckets.Contains($source)) {
            $buckets[$source] = [Collections.Generic.List[object]]::new()
        }
        $buckets[$source].Add($connection)
    }
}

$sortedSources = @($buckets.GetEnumerator() | ForEach-Object { [string]$_.Key } | Sort-Object)
$output = @(
foreach ($source in $sortedSources) {
    [ordered]@{
        source = $source
        connections = @($buckets[$source] | Sort-Object target, projectionType)
    }
}
)
$routeCount = ($output | ForEach-Object { @($_.connections).Count } | Measure-Object -Sum).Sum
if ($output.Count -lt 2 -or $routeCount -lt 1) {
    throw 'Connectome migration produced an invalid source or route count.'
}

$json = $output | ConvertTo-Json -Depth 8
$temporaryPath = "$connectomePath.rung6.tmp"
[IO.File]::WriteAllText($temporaryPath, $json + [Environment]::NewLine)
$validated = Get-Content $temporaryPath -Raw | ConvertFrom-Json
if ($validated -isnot [Array] -or $validated.Count -ne $output.Count) {
    Remove-Item -LiteralPath $temporaryPath -Force
    throw 'Serialized connectome failed validation; the original file was not changed.'
}
$backupPath = "$connectomePath.rung6.backup"
[IO.File]::Replace($temporaryPath, $connectomePath, $backupPath)
Remove-Item -LiteralPath $backupPath -Force

$remaining = @($output | Where-Object {
    $_.source -in $retired -or @($_.connections.target | Where-Object { $_ -in $retired }).Count -gt 0
})
if ($remaining.Count -ne 0) {
    throw "Retired structures remain in the connectome."
}

Write-Host ("Rung 6 connectome written: {0} sources, {1} routes." -f
    $output.Count,
    $routeCount)
