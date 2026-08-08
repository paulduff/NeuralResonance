param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$SkipTests,
    [switch]$SkipCircuitAudit,
    [switch]$FailOnMissingSolutionRefs
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-DnneRepoRoot {
    $cursor = Get-Item -LiteralPath (Split-Path -Parent $PSCommandPath)
    while ($null -ne $cursor) {
        if ((Test-Path (Join-Path $cursor.FullName 'ControlProgram')) -and
            (Test-Path (Join-Path $cursor.FullName 'Structures'))) {
            return $cursor.FullName
        }

        $cursor = $cursor.Parent
    }

    throw 'Could not locate the DNNE repository root.'
}

function Invoke-DotnetStep {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    Write-Host ''
    Write-Host ("== {0} ==" -f $Name)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw ("{0} failed with exit code {1}" -f $Name, $LASTEXITCODE)
    }
}

function Invoke-ScriptStep {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [string]$ScriptPath
    )

    Write-Host ''
    Write-Host ("== {0} ==" -f $Name)
    & powershell -NoProfile -ExecutionPolicy Bypass -File $ScriptPath
    if ($LASTEXITCODE -ne 0) {
        throw ("{0} failed with exit code {1}" -f $Name, $LASTEXITCODE)
    }
}

function Add-Project {
    param(
        [System.Collections.Generic.List[string]]$Projects,
        [System.Collections.Generic.HashSet[string]]$Seen,
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return
    }

    $resolved = (Resolve-Path -LiteralPath $Path).Path
    if ($Seen.Add($resolved)) {
        [void]$Projects.Add($resolved)
    }
}

function Get-SolutionProjectRefs {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SolutionPath
    )

    if (-not (Test-Path -LiteralPath $SolutionPath -PathType Leaf)) {
        return @()
    }

    $solutionDir = Split-Path -Parent $SolutionPath
    $content = Get-Content -LiteralPath $SolutionPath -Raw
    $matches = [regex]::Matches($content, '(?<path>[^"''<>|:*?]+?\.csproj)', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    $refs = New-Object 'System.Collections.Generic.List[string]'
    foreach ($match in $matches) {
        $raw = $match.Groups['path'].Value.Trim()
        $candidate = Join-Path $solutionDir $raw
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            [void]$refs.Add((Resolve-Path -LiteralPath $candidate).Path)
        }
    }

    return $refs
}

$repoRoot = Get-DnneRepoRoot
Push-Location $repoRoot
try {
    Write-Host ("DNNE verification from {0}" -f $repoRoot)
    Write-Host 'This verifies stand-alone projects only; it does not launch the editor or either simulator.'

    $allProjects = Get-ChildItem -Path $repoRoot -Recurse -Filter *.csproj |
        Where-Object {
            $_.FullName -notmatch '\\bin\\' -and
            $_.FullName -notmatch '\\obj\\'
        } |
        ForEach-Object { $_.FullName }

    $solutionRefs = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($solutionPath in @(
            (Join-Path $repoRoot 'NeuralResonanceEngine.DNNE.slnx'),
            (Join-Path $repoRoot 'NeuralResonanceEngine.sln'))) {
        foreach ($ref in Get-SolutionProjectRefs -SolutionPath $solutionPath) {
            [void]$solutionRefs.Add($ref)
        }
    }

    $missingSolutionRefs = @($allProjects | Where-Object { -not $solutionRefs.Contains($_) } | Sort-Object)
    if ($missingSolutionRefs.Count -gt 0) {
        Write-Warning ("{0} project(s) are not referenced by the checked solution files." -f $missingSolutionRefs.Count)
        foreach ($missing in $missingSolutionRefs) {
            Write-Warning ("  {0}" -f (Resolve-Path -LiteralPath $missing -Relative))
        }

        if ($FailOnMissingSolutionRefs) {
            throw 'Solution reference check failed.'
        }
    }

    $projects = [System.Collections.Generic.List[string]]::new()
    $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($relativePath in @(
            'Protocol\NeuralResonanceEngine.Protocol.csproj',
            'Shared.Contracts\NeuralResonanceEngine.Shared.Contracts.csproj',
            'src\NRE.Contracts\NRE.Contracts.csproj',
            'src\NRE.Core\NRE.Core.csproj',
            'src\NRE.Api\NRE.Api.csproj',
            'src\NRE.Blazor\NRE.Blazor.csproj',
            'src\NRE.SimAvatar\NRE.SimAvatar.csproj',
            'ControlProgram\NeuralResonanceEngine.ControlProgram.csproj',
            'src\NRE.WpfEditor\NRE.WpfEditor.csproj',
            'src\NRE.WpfMazeSim\NRE.WpfMazeSim.csproj',
            'src\NRE.WpfWorldSim\NRE.WpfWorldSim.csproj')) {
        Add-Project -Projects $projects -Seen $seen -Path (Join-Path $repoRoot $relativePath)
    }

    foreach ($project in Get-ChildItem -Path (Join-Path $repoRoot 'Structures') -Recurse -Filter *.csproj | Sort-Object FullName) {
        Add-Project -Projects $projects -Seen $seen -Path $project.FullName
    }

    foreach ($project in Get-ChildItem -Path (Join-Path $repoRoot 'tests') -Recurse -Filter *.csproj | Sort-Object FullName) {
        Add-Project -Projects $projects -Seen $seen -Path $project.FullName
    }

    foreach ($project in $allProjects | Sort-Object) {
        Add-Project -Projects $projects -Seen $seen -Path $project
    }

    foreach ($project in $projects) {
        $relative = Resolve-Path -LiteralPath $project -Relative
        Invoke-DotnetStep -Name ("build {0}" -f $relative) -Arguments @(
            'build',
            $project,
            '-c',
            $Configuration,
            '--nologo',
            '--verbosity',
            'minimal')
    }

    if (-not $SkipTests) {
        foreach ($testProject in @(
                'tests\NeuralResonanceEngine.DNNE.Tests\NeuralResonanceEngine.DNNE.Tests.csproj',
                'tests\NRE.Tests\NRE.Tests.csproj')) {
            $fullPath = Join-Path $repoRoot $testProject
            if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
                Write-Host ("Skipping absent test project {0}" -f $testProject)
                continue
            }

            Invoke-DotnetStep -Name ("test {0}" -f $testProject) -Arguments @(
                'test',
                $fullPath,
                '-c',
                $Configuration,
                '--no-build',
                '--nologo',
                '--verbosity',
                'minimal')
        }
    }

    if (-not $SkipCircuitAudit) {
        Invoke-ScriptStep -Name 'DNNE circuit audit' -ScriptPath (Join-Path $repoRoot 'tools\audit-dnne-circuits.ps1')
    }

    Write-Host ''
    Write-Host 'DNNE verification completed successfully.'
}
finally {
    Pop-Location
}
