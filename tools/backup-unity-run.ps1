param([Parameter(Mandatory=$true)][string]$Source,[Parameter(Mandatory=$true)][string]$Destination)
$ErrorActionPreference='Stop'
$sourceDirectory=(Resolve-Path -LiteralPath $Source).Path.TrimEnd('\')
$destinationDirectory=[IO.Path]::GetFullPath($Destination).TrimEnd('\')
if($destinationDirectory.Equals($sourceDirectory,[StringComparison]::OrdinalIgnoreCase) -or $destinationDirectory.StartsWith($sourceDirectory+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Backup must be outside the source run directory.'}
New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
$entries=[Collections.Generic.List[object]]::new()
foreach($file in Get-ChildItem -LiteralPath $sourceDirectory -File -Recurse | Where-Object {$_.Name -ne 'backup-manifest.json'}){
 $relative=$file.FullName.Substring($sourceDirectory.Length+1);$target=Join-Path $destinationDirectory $relative
 New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
 $snapshot=$file.Extension -eq '.log'
 if($snapshot){
  # Unity keeps its log open for writing. Capture a bounded byte snapshot with
  # shared read access, hash those exact bytes, and verify the saved snapshot.
  $stream=[IO.File]::Open($file.FullName,[IO.FileMode]::Open,[IO.FileAccess]::Read,([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
  $memory=[IO.MemoryStream]::new()
  try {
   $remaining=$stream.Length;$buffer=New-Object byte[] 65536
   while($remaining -gt 0){$read=$stream.Read($buffer,0,[int][Math]::Min($buffer.Length,$remaining));if($read -le 0){throw ('Log shortened during capture: '+$relative)};$memory.Write($buffer,0,$read);$remaining-=$read}
   $bytes=$memory.ToArray()
  } finally {$stream.Dispose();$memory.Dispose()}
  $algorithm=[Security.Cryptography.SHA256]::Create()
  try {$expected=[BitConverter]::ToString($algorithm.ComputeHash($bytes)).Replace('-','')} finally {$algorithm.Dispose()}
  [IO.File]::WriteAllBytes($target,$bytes)
 } else {
  Copy-Item -LiteralPath $file.FullName -Destination $target
  $expected=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
 }
 if($expected -ne (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash){throw ('Backup mismatch: '+$relative)}
 $entries.Add([ordered]@{Path=$relative;Sha256=$expected;Bytes=(Get-Item -LiteralPath $target).Length;LogByteSnapshot=$snapshot})
}
$manifest=[ordered]@{CapturedUtc=[DateTime]::UtcNow.ToString('o');Source=$sourceDirectory;Destination=$destinationDirectory;Files=$entries}
$manifestPath=Join-Path $sourceDirectory 'backup-manifest.json'
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $destinationDirectory 'backup-manifest.json')
if((Get-FileHash -LiteralPath $manifestPath).Hash -ne (Get-FileHash -LiteralPath (Join-Path $destinationDirectory 'backup-manifest.json')).Hash){throw 'Backup manifest mismatch.'}
Write-Host ('Verified D: backup: '+$destinationDirectory+' ('+$entries.Count+' files plus manifest; logs are byte snapshots)')
