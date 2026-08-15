param(
    [string]$WorkspaceRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $WorkspaceRoot "src\NRE.BlazorEditor\wwwroot\data\brain-atlas.json"
}

$layoutPath = Join-Path $WorkspaceRoot "src\NRE.WpfEditor\MainWindow.Brain3D.Layout.cs"
$atlasPath = Join-Path $WorkspaceRoot "src\NRE.WpfEditor\MainWindow.Brain3D.Atlas.cs"
$territoryPath = Join-Path $WorkspaceRoot "src\NRE.WpfEditor\MainWindow.Brain3D.CorticalTerritories.cs"
$structureIdPath = Join-Path $WorkspaceRoot "Protocol\StructureId.cs"
if (-not (Test-Path -LiteralPath $layoutPath) -or
    -not (Test-Path -LiteralPath $atlasPath) -or
    -not (Test-Path -LiteralPath $territoryPath) -or
    -not (Test-Path -LiteralPath $structureIdPath)) {
    throw "The WPF editor atlas sources were not found under $WorkspaceRoot."
}

$layoutSource = Get-Content -LiteralPath $layoutPath -Raw
$atlasSource = Get-Content -LiteralPath $atlasPath -Raw
$territorySource = Get-Content -LiteralPath $territoryPath -Raw
$structureIdSource = Get-Content -LiteralPath $structureIdPath -Raw
$number = '[-+]?[0-9]+(?:\.[0-9]+)?'
$definitionPattern = [regex]::new(
    'new StructureDefinition\("(?<display>[^"]+)","(?<id>[^"]+)",MmToRender\(new Point3D\(' +
    '(?<x>' + $number + '),(?<y>' + $number + '),(?<z>' + $number + ')\)\),' +
    'Color.FromRgb\((?<r>[0-9]+),(?<g>[0-9]+),(?<b>[0-9]+)\),' +
    '"(?<model>[^"]+)","(?<plasticity>[^"]+)",StructureLayout\.(?<layout>[A-Za-z]+),' +
    '[0-9]+,[0-9]+,[0-9]+,MmToRender\((?<width>' + $number + ')\),' +
    'MmToRender\((?<height>' + $number + ')\),MmToRender\((?<depth>' + $number + ')\),' +
    '(?<pitch>' + $number + '),(?<yaw>' + $number + '),(?<roll>' + $number + ')\)',
    [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
$definitionMatches = $definitionPattern.Matches($layoutSource)
if ($definitionMatches.Count -lt 80) {
    throw "Only $($definitionMatches.Count) editor structure definitions were parsed; expected at least 80."
}

$structureIdSection = [regex]::Match(
    $structureIdSource,
    'public enum StructureId\s*\{(?<body>.*?)\}',
    [System.Text.RegularExpressions.RegexOptions]::Singleline)
if (-not $structureIdSection.Success) {
    throw "The protocol StructureId enum could not be parsed."
}

$protocolStructureIds = [System.Collections.Generic.Dictionary[string, int]]::new([StringComparer]::OrdinalIgnoreCase)
$protocolStructureIndex = 0
$structureIdPattern = [regex]::new(
    '(?m)^\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?:=\s*(?<value>[0-9]+))?\s*,?\s*$')
foreach ($match in $structureIdPattern.Matches($structureIdSection.Groups['body'].Value)) {
    if ($match.Groups['value'].Success) {
        $protocolStructureIndex = [int]$match.Groups['value'].Value
    }

    $protocolStructureIds[$match.Groups['name'].Value] = $protocolStructureIndex
    $protocolStructureIndex++
}
if ($protocolStructureIds.Count -lt 80) {
    throw "Only $($protocolStructureIds.Count) protocol StructureId values were parsed; expected at least 80."
}

$sourceNames = @{}
$sourcePattern = [regex]::new('private const string (?<name>[A-Za-z0-9]+) = "(?<value>[^"]+)";')
foreach ($match in $sourcePattern.Matches($atlasSource)) {
    $sourceNames[$match.Groups['name'].Value] = $match.Groups['value'].Value
}

function Convert-Number([string]$Value) {
    return [double]::Parse($Value, [System.Globalization.CultureInfo]::InvariantCulture)
}

function Resolve-Source([string]$Name) {
    if ($sourceNames.ContainsKey($Name)) {
        return $sourceNames[$Name]
    }

    return $Name
}

$anchorSection = [regex]::Match(
    $layoutSource,
    'private static Point3D GetCorticalStructureAnchor.*?var mm = snapshotId switch\s*\{(?<body>.*?)\};',
    [System.Text.RegularExpressions.RegexOptions]::Singleline)
if (-not $anchorSection.Success) {
    throw "The canonical cortical anchor map could not be parsed."
}

$corticalAnchors = @{}
$anchorPattern = [regex]::new(
    '"(?<id>[^"]+)"\s*=>\s*new Point3D\(\s*(?<x>' + $number + ')\s*,\s*(?<y>' + $number + ')\s*,\s*(?<z>' + $number + ')\s*\)',
    [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
foreach ($match in $anchorPattern.Matches($anchorSection.Groups['body'].Value)) {
    $corticalAnchors[$match.Groups['id'].Value] = @(
        (Convert-Number $match.Groups['x'].Value),
        (Convert-Number $match.Groups['y'].Value),
        (Convert-Number $match.Groups['z'].Value))
}

$corticalProfiles = @{}
$territoryPattern = [regex]::new(
    '"(?<id>[^"]+)"\s*=>\s*new\("(?<name>[^"]+)",\s*' +
    '(?<halfTheta>' + $number + '),\s*(?<halfPhi>' + $number + '),\s*(?<rotation>' + $number + '),\s*' +
    'CorticalTerritoryShape\.(?<shape>[A-Za-z]+),\s*(?<surfaceOffset>' + $number + '),\s*(?<foldRelief>' + $number + ')' +
    '(?:,\s*(?<thetaOffset>' + $number + ')(?:,\s*(?<phiOffset>' + $number + '))?)?\s*\)',
    [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
foreach ($match in $territoryPattern.Matches($territorySource)) {
    $thetaOffset = if ($match.Groups['thetaOffset'].Success) { Convert-Number $match.Groups['thetaOffset'].Value } else { 0.0 }
    $phiOffset = if ($match.Groups['phiOffset'].Success) { Convert-Number $match.Groups['phiOffset'].Value } else { 0.0 }
    $corticalProfiles[$match.Groups['id'].Value] = [ordered]@{
        name = $match.Groups['name'].Value
        halfTheta = Convert-Number $match.Groups['halfTheta'].Value
        halfPhi = Convert-Number $match.Groups['halfPhi'].Value
        rotationDeg = Convert-Number $match.Groups['rotation'].Value
        shape = $match.Groups['shape'].Value
        surfaceOffsetMm = Convert-Number $match.Groups['surfaceOffset'].Value
        foldReliefMm = Convert-Number $match.Groups['foldRelief'].Value
        centerThetaOffset = $thetaOffset
        centerPhiOffset = $phiOffset
    }
}
if ($corticalAnchors.Count -lt 30 -or $corticalProfiles.Count -lt 30) {
    throw "Only $($corticalAnchors.Count) cortical anchors and $($corticalProfiles.Count) territory profiles were parsed."
}

function New-Geometry(
    [string]$Hemisphere,
    [double]$X,
    [double]$Y,
    [double]$Z,
    [double]$Width,
    [double]$Height,
    [double]$Depth,
    [string]$Source) {
    return [pscustomobject]@{
        hemisphere = $Hemisphere
        centerMm = @($X, $Y, $Z)
        dimensionsMm = @($Width, $Height, $Depth)
        source = $Source
    }
}

$atlasProfiles = @{}
$bilateralPattern = [regex]::new(
    '\["(?<id>[^"]+)"\]\s*=\s*Bilateral\(\s*Geometry\(\s*' +
    '(?<lx>' + $number + ')\s*,\s*(?<ly>' + $number + ')\s*,\s*(?<lz>' + $number + ')\s*,\s*' +
    '(?<lw>' + $number + ')\s*,\s*(?<lh>' + $number + ')\s*,\s*(?<ld>' + $number + ')\s*,\s*(?<ls>[A-Za-z0-9]+)\s*\)\s*,\s*' +
    'Geometry\(\s*(?<rx>' + $number + ')\s*,\s*(?<ry>' + $number + ')\s*,\s*(?<rz>' + $number + ')\s*,\s*' +
    '(?<rw>' + $number + ')\s*,\s*(?<rh>' + $number + ')\s*,\s*(?<rd>' + $number + ')\s*,\s*(?<rs>[A-Za-z0-9]+)\s*\)\s*\)',
    [System.Text.RegularExpressions.RegexOptions]::Singleline)
foreach ($match in $bilateralPattern.Matches($atlasSource)) {
    $atlasProfiles[$match.Groups['id'].Value] = @(
        (New-Geometry 'L' (Convert-Number $match.Groups['lx'].Value) (Convert-Number $match.Groups['ly'].Value) (Convert-Number $match.Groups['lz'].Value) (Convert-Number $match.Groups['lw'].Value) (Convert-Number $match.Groups['lh'].Value) (Convert-Number $match.Groups['ld'].Value) (Resolve-Source $match.Groups['ls'].Value)),
        (New-Geometry 'R' (Convert-Number $match.Groups['rx'].Value) (Convert-Number $match.Groups['ry'].Value) (Convert-Number $match.Groups['rz'].Value) (Convert-Number $match.Groups['rw'].Value) (Convert-Number $match.Groups['rh'].Value) (Convert-Number $match.Groups['rd'].Value) (Resolve-Source $match.Groups['rs'].Value))
    )
}

$symmetricPattern = [regex]::new(
    '\["(?<id>[^"]+)"\]\s*=\s*Symmetric\(\s*' +
    '(?<x>' + $number + ')\s*,\s*(?<y>' + $number + ')\s*,\s*(?<z>' + $number + ')\s*,\s*' +
    '(?<w>' + $number + ')\s*,\s*(?<h>' + $number + ')\s*,\s*(?<d>' + $number + ')\s*,\s*(?<source>[A-Za-z0-9]+)\s*\)',
    [System.Text.RegularExpressions.RegexOptions]::Singleline)
foreach ($match in $symmetricPattern.Matches($atlasSource)) {
    $x = [Math]::Abs((Convert-Number $match.Groups['x'].Value))
    $y = Convert-Number $match.Groups['y'].Value
    $z = Convert-Number $match.Groups['z'].Value
    $w = Convert-Number $match.Groups['w'].Value
    $h = Convert-Number $match.Groups['h'].Value
    $d = Convert-Number $match.Groups['d'].Value
    $source = Resolve-Source $match.Groups['source'].Value
    $atlasProfiles[$match.Groups['id'].Value] = @(
        (New-Geometry 'L' (-$x) $y $z $w $h $d $source),
        (New-Geometry 'R' $x $y $z $w $h $d $source)
    )
}

$midlinePattern = [regex]::new(
    '\["(?<id>[^"]+)"\]\s*=\s*Midline\(\s*' +
    '(?<x>' + $number + ')\s*,\s*(?<y>' + $number + ')\s*,\s*(?<z>' + $number + ')\s*,\s*' +
    '(?<w>' + $number + ')\s*,\s*(?<h>' + $number + ')\s*,\s*(?<d>' + $number + ')\s*,\s*(?<source>[A-Za-z0-9]+)\s*\)',
    [System.Text.RegularExpressions.RegexOptions]::Singleline)
foreach ($match in $midlinePattern.Matches($atlasSource)) {
    $atlasProfiles[$match.Groups['id'].Value] = @(
        (New-Geometry 'M' (Convert-Number $match.Groups['x'].Value) (Convert-Number $match.Groups['y'].Value) (Convert-Number $match.Groups['z'].Value) (Convert-Number $match.Groups['w'].Value) (Convert-Number $match.Groups['h'].Value) (Convert-Number $match.Groups['d'].Value) (Resolve-Source $match.Groups['source'].Value))
    )
}

$midlineIds = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
@(
    'CorpusCallosum', 'ReticularFormation', 'PeriaqueductalGray', 'RapheNuclei',
    'CerebellarGranule', 'CerebellarVermis', 'CerebellarLobules',
    'PurkinjeCellLayer', 'MedialSeptalNucleus'
) | ForEach-Object { [void]$midlineIds.Add($_) }

function Resolve-Group([string]$Id, [string]$Layout) {
    if ($Layout -eq 'CorticalSheet') { return 'Cortex' }
    if ($Id -in @('BasolateralAmygdala', 'CentralAmygdala', 'MedialAmygdala', 'CorticalAmygdala', 'BedNucleusStriaTerminalis')) { return 'Amygdala / extended limbic' }
    if ($Id -in @('MedialSeptalNucleus', 'DiagonalBandNucleus')) { return 'Septal basal forebrain' }
    if ($Layout -eq 'HippocampalArc' -or $Id -eq 'EntorhinalCortex') { return 'Medial temporal' }
    if ($Layout -eq 'CerebellarSheet' -or $Id -like 'Cerebellar*' -or $Id -in @('PurkinjeCellLayer', 'DentateNucleus', 'InterposedNuclei', 'FastigialNucleus')) { return 'Cerebellum' }
    if ($Layout -eq 'BrainstemColumn' -or $Id -in @('PontineNuclei', 'SuperiorColliculus', 'InferiorColliculus', 'PeriaqueductalGray')) { return 'Brainstem' }
    if ($Id -match 'Hypothalam|Preoptic|Suprachiasmatic|Supraoptic|^ArcuateNucleus$|Mammillary') { return 'Hypothalamus' }
    if ($Id -match 'Thalamus|Geniculate|Reuniens|Pulvinar|^Trn$') { return 'Thalamus' }
    if ($Id -in @('Striatum', 'NucleusAccumbens', 'VentralPallidum', 'GPe', 'GPi', 'Stn', 'Snr', 'Snc')) { return 'Basal ganglia' }
    if ($Id -in @('Retina', 'Cochlea', 'OlfactoryBulb', 'SomaticAfferents', 'ProprioceptiveAfferents', 'VestibularAfferents', 'VisceralAfferents')) { return 'Sensory interface' }
    return 'Subcortical'
}

$structures = [System.Collections.Generic.List[object]]::new()
foreach ($match in $definitionMatches) {
    $id = $match.Groups['id'].Value
    $layout = $match.Groups['layout'].Value
    if (-not $protocolStructureIds.ContainsKey($id)) {
        throw "Editor structure '$id' is missing from the protocol StructureId enum."
    }
    $defaultX = [Math]::Abs((Convert-Number $match.Groups['x'].Value))
    $defaultY = Convert-Number $match.Groups['y'].Value
    $defaultZ = Convert-Number $match.Groups['z'].Value
    $defaultWidth = Convert-Number $match.Groups['width'].Value
    $defaultHeight = Convert-Number $match.Groups['height'].Value
    $defaultDepth = Convert-Number $match.Groups['depth'].Value

    if ($layout -eq 'CorticalSheet') {
        if (-not $corticalAnchors.ContainsKey($id) -or -not $corticalProfiles.ContainsKey($id)) {
            throw "Cortical structure '$id' is missing its canonical anchor or territory profile."
        }

        $anchor = $corticalAnchors[$id]
        $anchorX = [Math]::Abs([double]$anchor[0])
        $geometries = @(
            (New-Geometry 'L' (-$anchorX) ([double]$anchor[1]) ([double]$anchor[2]) $defaultWidth $defaultHeight $defaultDepth 'DNNE cortical territory atlas'),
            (New-Geometry 'R' $anchorX ([double]$anchor[1]) ([double]$anchor[2]) $defaultWidth $defaultHeight $defaultDepth 'DNNE cortical territory atlas')
        )
    }
    elseif ($atlasProfiles.ContainsKey($id)) {
        $geometries = $atlasProfiles[$id]
    }
    elseif ($midlineIds.Contains($id)) {
        $geometries = @((New-Geometry 'M' 0.0 $defaultY $defaultZ $defaultWidth $defaultHeight $defaultDepth 'DNNE configured anatomy'))
    }
    else {
        $geometries = @(
            (New-Geometry 'L' (-$defaultX) $defaultY $defaultZ $defaultWidth $defaultHeight $defaultDepth 'DNNE cortical territory map'),
            (New-Geometry 'R' $defaultX $defaultY $defaultZ $defaultWidth $defaultHeight $defaultDepth 'DNNE cortical territory map')
        )
    }

    foreach ($geometry in $geometries) {
        $entry = [ordered]@{
            instanceId = "$($geometry.hemisphere)_$id"
            structureId = $id
            protocolStructureId = $protocolStructureIds[$id]
            displayName = $match.Groups['display'].Value
            hemisphere = $geometry.hemisphere
            group = Resolve-Group $id $layout
            layout = $layout
            centerMm = $geometry.centerMm
            dimensionsMm = $geometry.dimensionsMm
            rotationDeg = @(
                (Convert-Number $match.Groups['pitch'].Value),
                (Convert-Number $match.Groups['yaw'].Value),
                (Convert-Number $match.Groups['roll'].Value)
            )
            color = ('#{0:X2}{1:X2}{2:X2}' -f [int]$match.Groups['r'].Value, [int]$match.Groups['g'].Value, [int]$match.Groups['b'].Value)
            neuronModel = $match.Groups['model'].Value
            plasticity = $match.Groups['plasticity'].Value
            source = $geometry.source
        }
        if ($layout -eq 'CorticalSheet') {
            $entry.corticalTerritory = $corticalProfiles[$id]
        }
        $structures.Add([pscustomobject]$entry)
    }
}

$payload = [ordered]@{
    schemaVersion = 2
    coordinateSystem = [ordered]@{
        units = 'millimetres'
        x = 'lateral, left negative and right positive'
        y = 'superior, inferior negative'
        z = 'WPF render atlas: anterior positive and posterior negative'
        anteriorView = 'radiological convention: anatomical right appears on viewer left'
    }
    definitionCount = $definitionMatches.Count
    instanceCount = $structures.Count
    structures = $structures
}

$outputDirectory = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$json = $payload | ConvertTo-Json -Depth 8
[System.IO.File]::WriteAllText($OutputPath, $json + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
Write-Output "Exported $($definitionMatches.Count) definitions and $($structures.Count) instances to $OutputPath"
