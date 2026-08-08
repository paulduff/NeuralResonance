param(
    [string]$ManifestPath = 'deploy/distributed/dnne-deploy.manifest.json',
    [string]$InventoryPath = '',
    [switch]$Quiet,
    [switch]$PassThru
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-RepoRoot {
    $scriptDir = Split-Path -Parent $PSCommandPath
    return (Resolve-Path (Join-Path $scriptDir '..')).Path
}

function Resolve-FromRoot {
    param([string]$Root, [string]$Path)
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $Root $Path))
}

function Get-PropertyValue {
    param([object]$Object, [string]$Name)
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function Add-Problem {
    param(
        [System.Collections.Generic.List[string]]$Collection,
        [string]$Message
    )
    $Collection.Add($Message)
}

$root = Resolve-RepoRoot
$manifestFullPath = Resolve-FromRoot -Root $root -Path $ManifestPath
$manifest = Get-Content -LiteralPath $manifestFullPath -Raw | ConvertFrom-Json
$settings = Get-Content -LiteralPath (Join-Path $root 'ControlProgram\appsettings.json') -Raw | ConvertFrom-Json
$program = Get-Content -LiteralPath (Join-Path $root 'ControlProgram\Program.cs') -Raw
$errors = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()

$projectMap = @{}
foreach ($match in [regex]::Matches($program, '\[StructureId\.([A-Za-z0-9]+)\]\s*=\s*"([^"]+)"')) {
    $id = $match.Groups[1].Value
    $folder = $match.Groups[2].Value
    $project = Get-ChildItem -LiteralPath (Join-Path $root "Structures\$folder") -Filter *.csproj -File -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($null -ne $project) {
        $projectMap[$id] = $project.FullName
    }
}

$registry = @{}
foreach ($property in $settings.ServiceRegistry.PSObject.Properties) {
    $registry[$property.Name] = [string]$property.Value
}
$rightOffset = [int]$settings.HemisphereHosting.RightPortOffset

$apps = @{}
foreach ($property in $manifest.apps.PSObject.Properties) {
    $id = [string]$property.Name
    $app = $property.Value
    $apps[$id] = $app
    $projectPath = Resolve-FromRoot -Root $root -Path ([string]$app.project)
    if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
        Add-Problem $errors "App '$id' project does not exist: $projectPath"
    }

    $platforms = @((Get-PropertyValue -Object $app -Name 'platforms'))
    if ($platforms.Count -eq 0) {
        Add-Problem $errors "App '$id' has no platforms."
    }
    if ([string]$app.role -eq 'wpf' -and (@($platforms | Where-Object { $_ -ne 'windows' }).Count -gt 0)) {
        Add-Problem $errors "WPF app '$id' may only target windows."
    }
}

$deployableNames = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$claimedStructures = @{}
$deployableMap = @{}
foreach ($deployable in @($manifest.deployables)) {
    $name = [string]$deployable.name
    if (-not $deployableNames.Add($name)) {
        Add-Problem $errors "Deployable '$name' is declared more than once."
        continue
    }
    $deployableMap[$name] = $deployable

    $platforms = @((Get-PropertyValue -Object $deployable -Name 'platforms'))
    if ($platforms.Count -eq 0) {
        Add-Problem $errors "Deployable '$name' has no platforms."
    }

    foreach ($appIdValue in @($deployable.apps)) {
        $appId = [string]$appIdValue
        if (-not $apps.ContainsKey($appId)) {
            Add-Problem $errors "Deployable '$name' references unknown app '$appId'."
            continue
        }

        $appPlatforms = @((Get-PropertyValue -Object $apps[$appId] -Name 'platforms'))
        if (@($platforms | Where-Object { $_ -in $appPlatforms }).Count -eq 0) {
            Add-Problem $errors "Deployable '$name' and app '$appId' have no common platform."
        }
    }

    foreach ($structureValue in @($deployable.structures)) {
        $structure = [string]$structureValue
        if ($claimedStructures.ContainsKey($structure)) {
            Add-Problem $errors "Structure '$structure' is owned by both '$($claimedStructures[$structure])' and '$name'."
        }
        else {
            $claimedStructures[$structure] = $name
        }
        if (-not $registry.ContainsKey($structure)) {
            Add-Problem $errors "Structure '$structure' has no ServiceRegistry endpoint."
        }
        if (-not $projectMap.ContainsKey($structure)) {
            Add-Problem $errors "Structure '$structure' has no project mapping."
        }
    }
}

foreach ($structure in $registry.Keys) {
    if (-not $claimedStructures.ContainsKey($structure)) {
        Add-Problem $errors "Registered structure '$structure' is not assigned to a deployable."
    }
}
foreach ($structure in $projectMap.Keys) {
    if (-not $registry.ContainsKey($structure)) {
        Add-Problem $warnings "Mapped structure '$structure' is not in ServiceRegistry."
    }
}

$ports = @{}
foreach ($structure in $registry.Keys) {
    try {
        $leftPort = ([Uri]$registry[$structure]).Port
    }
    catch {
        Add-Problem $errors "Structure '$structure' has an invalid endpoint: $($registry[$structure])"
        continue
    }

    foreach ($entry in @(
        [pscustomobject]@{ Key = "L_$structure"; Port = $leftPort },
        [pscustomobject]@{ Key = "R_$structure"; Port = $leftPort + $rightOffset }
    )) {
        if ($entry.Port -lt 1 -or $entry.Port -gt 65535) {
            Add-Problem $errors "Instance '$($entry.Key)' has invalid port $($entry.Port)."
        }
        elseif ($ports.ContainsKey($entry.Port)) {
            Add-Problem $errors "Port $($entry.Port) is shared by '$($ports[$entry.Port])' and '$($entry.Key)'."
        }
        else {
            $ports[$entry.Port] = $entry.Key
        }
    }
}

if (-not [string]::IsNullOrWhiteSpace($InventoryPath)) {
    $inventoryFullPath = Resolve-FromRoot -Root $root -Path $InventoryPath
    $inventory = Get-Content -LiteralPath $inventoryFullPath -Raw | ConvertFrom-Json
    $nodeNames = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $addressOffsets = [System.Collections.Generic.HashSet[int]]::new()
    $assignments = @{}
    foreach ($node in @($inventory.nodes)) {
        $nodeName = [string]$node.name
        $nodePlatform = ([string]$node.platform).ToLowerInvariant()
        if (-not $nodeNames.Add($nodeName)) {
            Add-Problem $errors "Inventory node '$nodeName' is declared more than once."
        }
        if (-not $addressOffsets.Add([int]$node.addressOffset)) {
            Add-Problem $errors "Inventory address offset '$($node.addressOffset)' is reused."
        }
        foreach ($deployableValue in @($node.deployables)) {
            $deployableName = [string]$deployableValue
            if (-not $deployableMap.ContainsKey($deployableName)) {
                Add-Problem $errors "Node '$nodeName' references unknown deployable '$deployableName'."
                continue
            }
            if ($assignments.ContainsKey($deployableName)) {
                Add-Problem $errors "Deployable '$deployableName' is assigned to both '$($assignments[$deployableName])' and '$nodeName'."
            }
            else {
                $assignments[$deployableName] = $nodeName
            }
            $supported = @((Get-PropertyValue -Object $deployableMap[$deployableName] -Name 'platforms'))
            if ($nodePlatform -notin $supported) {
                Add-Problem $errors "Deployable '$deployableName' does not support node '$nodeName' platform '$nodePlatform'."
            }
        }
    }

    foreach ($deployable in @($manifest.deployables)) {
        $requiredValue = Get-PropertyValue -Object $deployable -Name 'required'
        $required = $null -eq $requiredValue -or [bool]$requiredValue
        if ($required -and -not $assignments.ContainsKey([string]$deployable.name)) {
            Add-Problem $errors "Required deployable '$($deployable.name)' is not assigned in the inventory."
        }
    }
}

$report = [pscustomobject]@{
    Schema = 'dnne.distributed-validation.v1'
    Passed = $errors.Count -eq 0
    RegistryStructures = $registry.Count
    AssignedStructures = $claimedStructures.Count
    Deployables = $deployableNames.Count
    ServicePorts = $ports.Count
    Errors = @($errors)
    Warnings = @($warnings)
}

if (-not $Quiet) {
    Write-Host ("distributed validation: {0} structures, {1} deployables, {2} service ports" -f $report.RegistryStructures, $report.Deployables, $report.ServicePorts)
    foreach ($warning in $warnings) { Write-Warning $warning }
    foreach ($problem in $errors) { Write-Error $problem -ErrorAction Continue }
}
if (-not $report.Passed) {
    throw "Distributed deployment validation failed with $($errors.Count) error(s)."
}
if ($PassThru) {
    return $report
}
