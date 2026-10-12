param([string]$UnityExecutable='C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe',[string]$Output)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
if(-not(Test-Path -LiteralPath $UnityExecutable -PathType Leaf)){throw 'Unity 6000.3.19f1 must be installed.'}
if(-not(Test-Path -LiteralPath (Join-Path $repo 'UnityWorld\Assets\Avatar\MikeAlger.fbx'))){& (Join-Path $PSScriptRoot 'prepare-unity-avatar.ps1')}
if([string]::IsNullOrWhiteSpace($Output)){$Output=Join-Path $repo ('artifacts\unity-build-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))}
$Output=[IO.Path]::GetFullPath($Output);New-Item -ItemType Directory -Path $Output -Force | Out-Null
$previous=[Environment]::GetEnvironmentVariable('NRE_UNITY_OUTPUT','Process')
try {
 [Environment]::SetEnvironmentVariable('NRE_UNITY_OUTPUT',$Output,'Process')
 $args=@('-batchmode','-nographics','-projectPath',(Join-Path $repo 'UnityWorld'),'-executeMethod','Nre.UnityWorld.Editor.WorldBuild.BuildWindows','-quit','-logFile',(Join-Path $Output 'build.log'))
 $quoted=($args|ForEach-Object{'"'+$_.Replace('"','\"')+'"'}) -join ' '
 $process=Start-Process -FilePath $UnityExecutable -ArgumentList $quoted -WindowStyle Hidden -PassThru
 # Unity may leave a licensing helper running. Wait for the editor itself,
 # rather than Start-Process -Wait's entire descendant process tree.
 $process.WaitForExit()
 if($process.ExitCode -ne 0){throw ('Unity build failed; inspect '+(Join-Path $Output 'build.log'))}
 $result=Get-Content -LiteralPath (Join-Path $Output 'build-result.json') -Raw | ConvertFrom-Json
 if($result.result -ne 'Succeeded' -or $result.errors -ne 0){throw 'Unity did not confirm build success.'}
 Write-Host ('Built: '+(Join-Path $Output 'DNNE Unity World.exe'))
} finally {[Environment]::SetEnvironmentVariable('NRE_UNITY_OUTPUT',$previous,'Process')}
