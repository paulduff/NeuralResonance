param(
    [string]$OutputPath = "docs/reports/dnne-circuit-audit.md"
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $repoRoot

function Get-StructureIds {
    $source = Get-Content 'Protocol/StructureId.cs' -Raw
    $enumBody = [regex]::Match(
        $source,
        'enum\s+StructureId\s*\{(?<body>[\s\S]*?)\}').Groups['body'].Value
    [regex]::Matches($enumBody, '(?m)^\s*([A-Za-z][A-Za-z0-9]*)\s*,?\s*$') |
        ForEach-Object { $_.Groups[1].Value }
}

function Get-ProjectMap {
    $program = Get-Content 'ControlProgram/Program.cs' -Raw
    $map = @{}
    foreach ($match in [regex]::Matches($program, '\[StructureId\.([A-Za-z0-9]+)\]\s*=\s*"([^"]+)"')) {
        $map[$match.Groups[1].Value] = $match.Groups[2].Value
    }

    $map
}

function Get-ProfileName([string]$structure) {
    switch ($structure) {
        { $_ -in @('Pfc','DorsomedialPrefrontalCortex','VentromedialPrefrontalCortex','FrontalEyeFields','BrocaBa44Ba45','WernickePstgPsts','SupramarginalAngular','OrbitofrontalCortex','Insula','Ppc','TemporalAssociation','InferotemporalCortex','FusiformGyrus','TemporalPole','TemporoparietalJunction','Precuneus','MidcingulateCortex','PremotorCortex','ParahippocampalCortex','PerirhinalCortex','PosteriorCingulate','RetrosplenialCortex','Acc','M1','Sma') } { return 'cortical-association/motor' }
        { $_ -in @('V1','V2','V3','V4','Mt','A1','AuditoryAssociationCortex','S1','SecondarySomatosensoryCortex','EntorhinalCortex','CorpusCallosum') } { return 'primary-sensory/callosal' }
        { $_ -in @('MotorThalamus','Trn','Pulvinar','MediodorsalThalamus','IntralaminarThalamus','LateralGeniculateNucleus','MedialGeniculateNucleus','VentralPosterolateralThalamus','VentralPosteromedialThalamus','AnteriorThalamicNuclei','NucleusReuniens') } { return 'thalamic' }
        { $_ -in @('CerebellarGranule','CerebellarVermis','CerebellarLobules','PurkinjeCellLayer','DentateNucleus','InterposedNuclei','FastigialNucleus','InferiorOlive','PontineNuclei') } { return 'cerebellar/pontine' }
        { $_ -in @('Retina') } { return 'retinal' }
        { $_ -in @('Cochlea','CochlearNucleus','SuperiorOlive','InferiorColliculus') } { return 'auditory-brainstem' }
        { $_ -in @('SomaticAfferents') } { return 'somatic-afferent' }
        { $_ -in @('ProprioceptiveAfferents') } { return 'proprioceptive-afferent' }
        { $_ -in @('VestibularAfferents') } { return 'vestibular-afferent' }
        { $_ -in @('VisceralAfferents') } { return 'visceral-afferent' }
        { $_ -in @('VestibularNuclei','NucleusTractusSolitarius','OlfactoryBulb','ParabrachialComplex') } { return 'sensory-autonomic' }
        { $_ -in @('ArcuateFasciculus') } { return 'white-matter-relay' }
        { $_ -in @('Striatum','GPe','GPi','Stn','Snr') } { return 'basal-ganglia' }
        { $_ -in @('Snc','Vta','LocusCoeruleus','RapheNuclei','NucleusBasalis') } { return 'neuromodulatory' }
        { $_ -in @('VentrolateralPreopticNucleus','SuprachiasmaticNucleus','ParaventricularHypothalamicNucleus','SupraopticNucleus','ArcuateNucleus','LateralHypothalamicArea','VentromedialHypothalamicNucleus','DorsomedialHypothalamicNucleus','MammillaryBodies','BasolateralAmygdala','CentralAmygdala','MedialAmygdala','CorticalAmygdala','BedNucleusStriaTerminalis','NucleusAccumbens','VentralPallidum') } { return 'limbic-homeostatic' }
        { $_ -in @('MedialSeptalNucleus','DiagonalBandNucleus') } { return 'septohippocampal' }
        { $_ -in @('ReticularFormation','PeriaqueductalGray','PedunculopontineNucleus','LaterodorsalTegmentalNucleus') } { return 'brainstem-arousal' }
        { $_ -in @('RedNucleus','PrincipalSensoryTrigeminalNucleus','SpinalTrigeminalNucleus','MesencephalicTrigeminalNucleus','FacialMotorNucleus','OculomotorNucleus','HypoglossalNucleus') } { return 'brainstem-sensorimotor' }
        { $_ -in @('SpinalCordMotor') } { return 'spinal-motor' }
        { $_ -in @('CA1','CA2','CA3','DentateGyrus','Subiculum','Presubiculum','Parasubiculum','Habenula','SuperiorColliculus') } { return 'hippocampal/tectal' }
        default { return 'default-low' }
    }
}

$structureIds = @(Get-StructureIds)
$registeredStructureIds = @(
    (Get-Content 'ControlProgram/appsettings.json' -Raw | ConvertFrom-Json).ServiceRegistry.psobject.Properties.Name
)
$missingFromRegistry = @($structureIds | Where-Object { $_ -notin $registeredStructureIds })
$unknownRegistryEntries = @($registeredStructureIds | Where-Object { $_ -notin $structureIds })
$projectMap = Get-ProjectMap
$projectDirs = @(Get-ChildItem 'Structures' -Directory | Where-Object { Get-ChildItem $_.FullName -Filter '*.csproj' -File | Select-Object -First 1 } | ForEach-Object { $_.Name })
$connectome = Get-Content 'connectivity/dnne-connectivity.json' -Raw | ConvertFrom-Json | ForEach-Object { $_ }

$outbound = @{}
$inbound = @{}
$targets = @{}
$sources = @{}
foreach ($id in $structureIds) {
    $outbound[$id] = 0
    $inbound[$id] = 0
    $targets[$id] = New-Object System.Collections.Generic.HashSet[string]
    $sources[$id] = New-Object System.Collections.Generic.HashSet[string]
}

foreach ($rule in $connectome) {
    $source = [string]$rule.source
    if (-not $outbound.ContainsKey($source)) {
        $outbound[$source] = 0
        $targets[$source] = New-Object System.Collections.Generic.HashSet[string]
    }

    foreach ($connection in @($rule.connections)) {
        $target = [string]$connection.target
        $outbound[$source] = [int]$outbound[$source] + 1
        [void]$targets[$source].Add($target)
        if (-not $inbound.ContainsKey($target)) {
            $inbound[$target] = 0
            $sources[$target] = New-Object System.Collections.Generic.HashSet[string]
        }

        $inbound[$target] = [int]$inbound[$target] + 1
        [void]$sources[$target].Add($source)
    }
}

$rows = foreach ($id in $structureIds) {
    $mappedProject = if ($projectMap.ContainsKey($id)) { $projectMap[$id] } else { '' }
    $hasProject = $mappedProject -and ($projectDirs -contains $mappedProject)
    $outCount = [int]$outbound[$id]
    $inCount = [int]$inbound[$id]
    $profile = Get-ProfileName $id
    $status = if (-not $mappedProject) {
        'MISSING_SERVICE_MAP'
    } elseif (-not $hasProject) {
        'MISSING_PROJECT'
    } elseif ($outCount -eq 0 -and $inCount -eq 0) {
        'DISCONNECTED'
    } elseif ($outCount -eq 0) {
        'SINK_ONLY'
    } elseif ($inCount -eq 0) {
        'SOURCE_ONLY'
    } elseif ($profile -eq 'default-low') {
        'LOW_DEFAULT_PROFILE'
    } else {
        'OK'
    }

    [pscustomobject]@{
        Structure = $id
        Status = $status
        Inbound = $inCount
        Outbound = $outCount
        UniqueSources = $sources[$id].Count
        UniqueTargets = $targets[$id].Count
        Profile = $profile
        Project = $mappedProject
    }
}

$summary = $rows | Group-Object Status | Sort-Object Name | ForEach-Object { [pscustomobject]@{ Status = $_.Name; Count = $_.Count } }
$destination = Join-Path $repoRoot $OutputPath
$destinationDirectory = Split-Path $destination -Parent
if (-not (Test-Path $destinationDirectory)) {
    New-Item -ItemType Directory -Path $destinationDirectory | Out-Null
}

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('# DNNE Circuit Functionality Audit')
$lines.Add('')
$lines.Add(('Generated: {0:yyyy-MM-dd HH:mm:ss zzz}' -f (Get-Date)))
$lines.Add('')
$lines.Add('## Summary')
$lines.Add('')
$lines.Add("- Enum structures: $($structureIds.Count)")
$lines.Add("- Registered structures: $($registeredStructureIds.Count)")
$lines.Add("- Bilateral service instances: $($registeredStructureIds.Count * 2)")
$lines.Add('')
$lines.Add('| Status | Count |')
$lines.Add('| --- | ---: |')
foreach ($item in $summary) {
    $lines.Add("| $($item.Status) | $($item.Count) |")
}
$lines.Add('')
$lines.Add('## Circuit Table')
$lines.Add('')
$lines.Add('| Structure | Status | In | Out | Sources | Targets | Profile | Project |')
$lines.Add('| --- | --- | ---: | ---: | ---: | ---: | --- | --- |')
foreach ($row in ($rows | Sort-Object Status, Structure)) {
    $lines.Add("| $($row.Structure) | $($row.Status) | $($row.Inbound) | $($row.Outbound) | $($row.UniqueSources) | $($row.UniqueTargets) | $($row.Profile) | $($row.Project) |")
}
$lines.Add('')
$lines.Add('## Interpretation')
$lines.Add('')
$lines.Add('- OK means the circuit has a service project, an inbound route, an outbound route, and an explicit background-drive profile.')
$lines.Add('- LOW_DEFAULT_PROFILE means it can route spikes but is still using the anonymous fallback profile, which can make it look quiet.')
$lines.Add('- SINK_ONLY, SOURCE_ONLY, DISCONNECTED, MISSING_SERVICE_MAP, and MISSING_PROJECT are functional problems to review before runtime testing.')

Set-Content -Path $destination -Value $lines -Encoding UTF8
$rows | Sort-Object Status, Structure | Format-Table -AutoSize
Write-Host "`nAudit written to $destination"
if ($missingFromRegistry.Count -gt 0) {
    Write-Error "StructureId members missing from ServiceRegistry: $($missingFromRegistry -join ', ')"
}
if ($unknownRegistryEntries.Count -gt 0) {
    Write-Error "ServiceRegistry entries absent from StructureId: $($unknownRegistryEntries -join ', ')"
}
if (($rows | Where-Object { $_.Status -ne 'OK' }).Count -gt 0 -or
    $missingFromRegistry.Count -gt 0 -or
    $unknownRegistryEntries.Count -gt 0) {
    exit 2
}
