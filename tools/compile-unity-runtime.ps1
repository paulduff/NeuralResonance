param(
 [Parameter(Mandatory=$true)][string]$OutputDirectory,
 [string]$UnityEditor='C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor'
)
# C# API compilation only. Does not start Unity, instantiate a body, run tests,
# select a runtime mode, or qualify native behavior.
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $output){throw 'Use a fresh compilation output directory.'}
$sources=@(Get-ChildItem -LiteralPath (Join-Path $repo 'UnityWorld/Assets/NRE/Runtime') -Filter '*.cs' -File -Recurse | Sort-Object FullName)
$managed=Join-Path $UnityEditor 'Data/Managed'
$references=@(Get-Item -LiteralPath (Join-Path $managed 'UnityEngine.dll'))+@(Get-ChildItem -LiteralPath (Join-Path $managed 'UnityEngine') -Filter 'UnityEngine*.dll' -File | Sort-Object FullName)
function Record-Files($files){@($files | ForEach-Object { [ordered]@{Path=$_.FullName;Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash} })}
$sourceRecords=Record-Files $sources;$referenceRecords=Record-Files $references
$dotnet=(Get-Command dotnet -CommandType Application).Source
$compilerHash=(Get-FileHash -LiteralPath $dotnet -Algorithm SHA256).Hash
[IO.Directory]::CreateDirectory($output) | Out-Null
$items=@($sources | ForEach-Object {'<Compile Include="'+[Security.SecurityElement]::Escape($_.FullName)+'" />'})
$items+=@($references | ForEach-Object {'<Reference Include="'+[Security.SecurityElement]::Escape($_.FullName)+'"><Private>false</Private></Reference>'})
$project='<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>netstandard2.1</TargetFramework><LangVersion>9.0</LangVersion><EnableDefaultCompileItems>false</EnableDefaultCompileItems><Nullable>disable</Nullable><AssemblyName>UnityRuntimeCompileCheck</AssemblyName></PropertyGroup><ItemGroup>'+($items -join "`n")+'</ItemGroup></Project>'
$projectPath=Join-Path $output 'UnityRuntimeCompile.csproj';[IO.File]::WriteAllText($projectPath,$project)
$oldPreference=$ErrorActionPreference
try {
 $ErrorActionPreference='Continue'
 & $dotnet build $projectPath --nologo -v minimal 2>&1 | ForEach-Object { [string]$_ } | Tee-Object -FilePath (Join-Path $output 'build.log')
 $buildExit=$LASTEXITCODE
} finally {$ErrorActionPreference=$oldPreference}
$unchanged=$true
foreach($entry in @($sourceRecords)+@($referenceRecords)){
 if((Get-FileHash -LiteralPath $entry.Path -Algorithm SHA256).Hash -ne $entry.Sha256){$unchanged=$false}
}
if((Get-FileHash -LiteralPath $dotnet -Algorithm SHA256).Hash -ne $compilerHash){$unchanged=$false}
[ordered]@{ExitCode=$buildExit;InputsUnchanged=$unchanged;Sources=$sourceRecords;UnityReferences=$referenceRecords;
 Compiler=$dotnet;CompilerSha256=$compilerHash;LanguageVersion='9.0';TargetFramework='netstandard2.1';
 UnityEditorExecuted=$false;NativePlayerExecuted=$false;ModelsExecuted=$false;TestsExecuted=$false;NativeBehaviorQualified=$false;
 CompletedUtc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'build-evidence.json') -Encoding UTF8
if($buildExit -ne 0 -or -not $unchanged){throw 'Unity runtime API compilation failed or its inputs changed.'}
Write-Host 'Runtime sources compiled against installed Unity assemblies. No native model or test executed.'
