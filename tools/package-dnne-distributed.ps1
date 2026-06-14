param(
    [string]$ManifestPath = 'deploy/distributed/dnne-deploy.manifest.json',
    [string[]]$Deployable = @('all'),
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$OutputRoot = 'artifacts/distributed',
    [string]$Runtime = '',
    [switch]$SelfContained,
    [switch]$NoPublish,
    [switch]$Clean,
    [switch]$Zip,
    [switch]$WhatIf
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-RepoRoot {
    $scriptDir = Split-Path -Parent $PSCommandPath
    return (Resolve-Path (Join-Path $scriptDir '..')).Path
}

function Resolve-PathFromRoot {
    param(
        [string]$Root,
        [string]$Path
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $Root $Path))
}

function Get-ObjectProperty {
    param(
        [object]$Object,
        [string]$Name
    )

    return $Object.PSObject.Properties[$Name].Value
}

function Get-ProjectAssemblyName {
    param([string]$ProjectPath)

    $assemblyName = $null
    try {
        [xml]$projectXml = Get-Content -LiteralPath $ProjectPath -Raw
        $assemblyNode = $projectXml.Project.PropertyGroup |
            ForEach-Object { $_.AssemblyName } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            Select-Object -First 1
        if ($null -ne $assemblyNode) {
            $assemblyName = [string]$assemblyNode
        }
    }
    catch {
    }

    if ([string]::IsNullOrWhiteSpace($assemblyName)) {
        $assemblyName = [System.IO.Path]::GetFileNameWithoutExtension($ProjectPath)
    }

    return $assemblyName
}

function Get-RelativePathCompat {
    param(
        [string]$Root,
        [string]$Path
    )

    $rootFull = [System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $pathFull = [System.IO.Path]::GetFullPath($Path)
    if ($pathFull.StartsWith($rootFull, [StringComparison]::OrdinalIgnoreCase)) {
        return $pathFull.Substring($rootFull.Length).TrimStart('\', '/')
    }

    return $pathFull
}

function Get-StructureProjectMap {
    param([string]$Root)

    $programPath = Join-Path $Root 'ControlProgram\Program.cs'
    $program = Get-Content -LiteralPath $programPath -Raw
    $map = @{}
    foreach ($match in [regex]::Matches($program, '\[StructureId\.([A-Za-z0-9]+)\]\s*=\s*"([^"]+)"')) {
        $id = $match.Groups[1].Value
        $folder = $match.Groups[2].Value
        $folderPath = Join-Path (Join-Path $Root 'Structures') $folder
        if (-not (Test-Path $folderPath -PathType Container)) {
            continue
        }

        $project = Get-ChildItem -LiteralPath $folderPath -Filter *.csproj -File -ErrorAction Stop | Select-Object -First 1
        if ($null -eq $project) {
            continue
        }

        $map[$id] = $project.FullName
    }

    return $map
}

function Get-ServiceRegistryMap {
    param([string]$Root)

    $settingsPath = Join-Path $Root 'ControlProgram\appsettings.json'
    $settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
    $registry = @{}
    foreach ($property in $settings.ServiceRegistry.PSObject.Properties) {
        $registry[$property.Name] = [string]$property.Value
    }

    $rightOffset = 1000
    if ($settings.HemisphereHosting -and $settings.HemisphereHosting.RightPortOffset) {
        $rightOffset = [int]$settings.HemisphereHosting.RightPortOffset
    }

    return [pscustomobject]@{
        Registry = $registry
        RightPortOffset = $rightOffset
        HemisphereEnabled = if ($settings.HemisphereHosting) { [bool]$settings.HemisphereHosting.Enabled } else { $true }
    }
}

function Publish-Project {
    param(
        [string]$ProjectPath,
        [string]$OutputPath,
        [string]$Configuration,
        [string]$Runtime,
        [bool]$SelfContained,
        [bool]$NoPublish,
        [bool]$WhatIf
    )

    if ($NoPublish) {
        New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null
        return
    }

    $arguments = @('publish', $ProjectPath, '-c', $Configuration, '-o', $OutputPath, '--nologo', '--verbosity', 'minimal')
    if (-not [string]::IsNullOrWhiteSpace($Runtime)) {
        $arguments += @('-r', $Runtime)
        $arguments += @('--self-contained', ($(if ($SelfContained) { 'true' } else { 'false' })))
    }

    Write-Host ("publish {0} -> {1}" -f $ProjectPath, $OutputPath)
    if ($WhatIf) {
        Write-Host ("WHATIF dotnet {0}" -f ($arguments -join ' '))
        return
    }

    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for $ProjectPath"
    }
}

function New-AppEntry {
    param(
        [string]$Id,
        [string]$Role,
        [string]$ProjectPath,
        [string]$PackagePath,
        [string]$RelativePath
    )

    $assemblyName = Get-ProjectAssemblyName -ProjectPath $ProjectPath
    $exeName = "$assemblyName.exe"
    return [ordered]@{
        Id = $Id
        Role = $Role
        Project = $ProjectPath
        Path = $RelativePath
        EntryDll = "$assemblyName.dll"
        EntryExe = $exeName
    }
}

$repoRoot = Resolve-RepoRoot
$manifestFullPath = Resolve-PathFromRoot -Root $repoRoot -Path $ManifestPath
$outputRootFullPath = Resolve-PathFromRoot -Root $repoRoot -Path $OutputRoot
$manifest = Get-Content -LiteralPath $manifestFullPath -Raw | ConvertFrom-Json
$structureProjects = Get-StructureProjectMap -Root $repoRoot
$serviceRegistry = Get-ServiceRegistryMap -Root $repoRoot

$selectedNames = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($name in $Deployable) {
    [void]$selectedNames.Add($name)
}

$deployables = @($manifest.deployables)
if (-not $selectedNames.Contains('all')) {
    $deployables = @($deployables | Where-Object { $selectedNames.Contains([string]$_.name) })
}
if ($deployables.Count -eq 0) {
    throw "No deployables selected."
}

New-Item -ItemType Directory -Force -Path $outputRootFullPath | Out-Null

$allServiceInstances = @()
foreach ($deployableSpec in $deployables) {
    $name = [string]$deployableSpec.name
    $deployableOutput = Join-Path $outputRootFullPath $name
    if ($Clean -and (Test-Path $deployableOutput)) {
        $resolvedOutput = [System.IO.Path]::GetFullPath($deployableOutput)
        $resolvedRoot = [System.IO.Path]::GetFullPath($outputRootFullPath)
        if (-not $resolvedOutput.StartsWith($resolvedRoot, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to clean outside output root: $resolvedOutput"
        }

        if ($WhatIf) {
            Write-Host ("WHATIF clean {0}" -f $resolvedOutput)
        }
        else {
            Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
        }
    }

    New-Item -ItemType Directory -Force -Path $deployableOutput | Out-Null
    $appsOutput = Join-Path $deployableOutput 'apps'
    New-Item -ItemType Directory -Force -Path $appsOutput | Out-Null

    $appEntries = @()
    foreach ($appId in @($deployableSpec.apps)) {
        $app = Get-ObjectProperty -Object $manifest.apps -Name ([string]$appId)
        if ($null -eq $app) {
            throw "Unknown app in deployable $name`: $appId"
        }

        $projectPath = Resolve-PathFromRoot -Root $repoRoot -Path ([string]$app.project)
        $appOutput = Join-Path $appsOutput ([string]$appId)
        Publish-Project -ProjectPath $projectPath -OutputPath $appOutput -Configuration $Configuration -Runtime $Runtime -SelfContained:$SelfContained -NoPublish:$NoPublish -WhatIf:$WhatIf
        $appEntries += New-AppEntry -Id ([string]$appId) -Role ([string]$app.role) -ProjectPath ([string]$app.project) -PackagePath $appOutput -RelativePath ("apps/{0}" -f $appId)
    }

    $structureEntries = @()
    foreach ($structureId in @($deployableSpec.structures)) {
        $id = [string]$structureId
        if (-not $structureProjects.ContainsKey($id)) {
            throw "No project mapping found for structure $id"
        }
        if (-not $serviceRegistry.Registry.ContainsKey($id)) {
            throw "No ServiceRegistry endpoint found for structure $id"
        }

        $projectPath = $structureProjects[$id]
        $structureOutput = Join-Path $appsOutput $id
        Publish-Project -ProjectPath $projectPath -OutputPath $structureOutput -Configuration $Configuration -Runtime $Runtime -SelfContained:$SelfContained -NoPublish:$NoPublish -WhatIf:$WhatIf
        $assemblyName = Get-ProjectAssemblyName -ProjectPath $projectPath
        $leftUri = [Uri]$serviceRegistry.Registry[$id]
        $leftPort = $leftUri.Port
        $rightPort = $leftPort + $serviceRegistry.RightPortOffset

        $structureEntries += [ordered]@{
            Id = $id
            Role = 'structure'
            Project = Get-RelativePathCompat -Root $repoRoot -Path $projectPath
            Path = ("apps/{0}" -f $id)
            EntryDll = "$assemblyName.dll"
            EntryExe = "$assemblyName.exe"
            LeftPort = $leftPort
            RightPort = $rightPort
        }

        $allServiceInstances += [ordered]@{
            Deployable = $name
            StructureId = $id
            InstanceKey = "L_$id"
            Hemisphere = "L"
            EndpointTemplate = "http://<host-for-$name>:$leftPort"
        }
        $allServiceInstances += [ordered]@{
            Deployable = $name
            StructureId = $id
            InstanceKey = "R_$id"
            Hemisphere = "R"
            EndpointTemplate = "http://<host-for-$name>:$rightPort"
        }
    }

    $deployableDocument = [ordered]@{
        SchemaVersion = 1
        Name = $name
        Description = [string]$deployableSpec.description
        GeneratedAt = [DateTimeOffset]::UtcNow.ToString('o')
        Configuration = $Configuration
        ControlBaseUrlDefault = [string]$manifest.controlBaseUrlDefault
        ListenHostDefault = [string]$manifest.listenHostDefault
        HemisphereHosting = [ordered]@{
            Enabled = $serviceRegistry.HemisphereEnabled
            RightPortOffset = $serviceRegistry.RightPortOffset
        }
        Apps = $appEntries
        Structures = $structureEntries
    }

    $deployableJsonPath = Join-Path $deployableOutput 'deployable.json'
    $deployableDocument | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $deployableJsonPath -Encoding UTF8

    Copy-Item -LiteralPath (Join-Path $repoRoot 'tools\start-dnne-deployable.ps1') -Destination (Join-Path $deployableOutput 'start-deployable.ps1') -Force
    Copy-Item -LiteralPath (Join-Path $repoRoot 'tools\stop-dnne-deployable.ps1') -Destination (Join-Path $deployableOutput 'stop-deployable.ps1') -Force

    if ($Zip) {
        $zipPath = Join-Path $outputRootFullPath "$name.zip"
        if (Test-Path $zipPath) {
            Remove-Item -LiteralPath $zipPath -Force
        }

        if ($WhatIf) {
            Write-Host ("WHATIF zip {0} -> {1}" -f $deployableOutput, $zipPath)
        }
        else {
            Compress-Archive -Path (Join-Path $deployableOutput '*') -DestinationPath $zipPath
        }
    }

    Write-Host ("packaged deployable {0} at {1}" -f $name, $deployableOutput)
}

$template = [ordered]@{
    Notes = @(
        "Replace each <host-for-name> token with the DNS name or IP of the machine running that deployable.",
        "Put the ServiceInstances array into a Control Program appsettings override when running distributed.",
        "Keep StructureProcessHost:AutoStartEnabled=false on the control machine for remote structures."
    )
    ServiceInstances = $allServiceInstances
}
$templatePath = Join-Path $outputRootFullPath 'service-instances.template.json'
$template | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $templatePath -Encoding UTF8
Write-Host ("service instance template: {0}" -f $templatePath)
