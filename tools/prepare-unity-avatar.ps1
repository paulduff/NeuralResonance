param(
 [string]$Archive = (Join-Path (Split-Path $PSScriptRoot -Parent) '..\rigged-t-pose-human-male-w-50-face-blendshapes.zip')
)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$provenance=Get-Content -LiteralPath (Join-Path $repo 'UnityWorld\Assets\Avatar\provenance.json') -Raw | ConvertFrom-Json
if ((Get-FileHash -LiteralPath $Archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $provenance.originalZipSha256) { throw 'The archive does not match the inspected humanoid source.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=[IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $Archive).Path)
try {
 foreach($file in $provenance.files) {
  $entryName=if($file.file.EndsWith('.fbx')){'source/'+$file.file}else{'textures/'+$file.file}
  $entry=$zip.GetEntry($entryName)
  if($null -eq $entry){throw ('Missing archive entry '+$entryName)}
  $destination=Join-Path $repo ('UnityWorld\Assets\Avatar\'+$file.file)
  [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$destination,$true)
  if((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant() -ne $file.sha256){throw ('Model hash mismatch '+$file.file)}
 }
} finally {$zip.Dispose()}
Write-Host 'Verified and installed the operator-supplied humanoid. Credit: Mike Alger, CC BY 4.0.'
