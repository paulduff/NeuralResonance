param([Parameter(Mandatory=$true)][string]$Source,[Parameter(Mandatory=$true)][string]$Destination)
$ErrorActionPreference='Stop'
$sourceDirectory=(Resolve-Path -LiteralPath $Source).Path.TrimEnd('\')
$destinationDirectory=[IO.Path]::GetFullPath($Destination).TrimEnd('\')
if($destinationDirectory.Equals($sourceDirectory,[StringComparison]::OrdinalIgnoreCase) -or $destinationDirectory.StartsWith($sourceDirectory+'\',[StringComparison]::OrdinalIgnoreCase) -or $sourceDirectory.StartsWith($destinationDirectory+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Backup and source directories must not contain one another.'}
# Windows PowerShell 5.1 can enumerate a build file then reject Copy-Item at
# MAX_PATH. Use extended absolute paths and .NET I/O for enumeration, copying,
# lengths and hashes consistently; do not omit generated files from evidence.
function Extended-Path([string]$Path){
 if($Path.StartsWith('\\?\')){return $Path}
 if($Path.StartsWith('\\')){return '\\?\UNC\'+$Path.Substring(2)}
 return '\\?\'+$Path
}
function File-Sha256([string]$Path){
 $stream=[IO.File]::Open($Path,[IO.FileMode]::Open,[IO.FileAccess]::Read,([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
 $algorithm=[Security.Cryptography.SHA256]::Create()
 try {return [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-','')}
 finally {$algorithm.Dispose();$stream.Dispose()}
}
$sourceIo=Extended-Path $sourceDirectory;$destinationIo=Extended-Path $destinationDirectory
[IO.Directory]::CreateDirectory($destinationIo) | Out-Null
# Reject links/junctions rather than silently backing up outside the run.
$directories=[Collections.Generic.Stack[string]]::new();$directories.Push($sourceIo)
$files=[Collections.Generic.List[string]]::new()
while($directories.Count){
 $directory=$directories.Pop()
 if(([IO.File]::GetAttributes($directory) -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw ('Backup source contains a directory link: '+$directory)}
 foreach($child in [IO.Directory]::EnumerateDirectories($directory)){$directories.Push($child)}
 foreach($path in [IO.Directory]::EnumerateFiles($directory)){
  if(([IO.File]::GetAttributes($path) -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw ('Backup source contains a file link: '+$path)}
  if(-not $path.Equals($sourceIo+'\backup-manifest.json',[StringComparison]::OrdinalIgnoreCase)){$files.Add($path)}
 }
}
$entries=[Collections.Generic.List[object]]::new()
foreach($path in $files){
 $relative=$path.Substring($sourceIo.Length+1);$target=$destinationIo+'\'+$relative
 [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
 $snapshot=[IO.Path]::GetExtension($path) -eq '.log'
 if($snapshot){
  # Unity keeps its log open for writing. Capture a bounded byte snapshot with
  # shared read access, hash those exact bytes, and verify the saved snapshot.
  $stream=[IO.File]::Open($path,[IO.FileMode]::Open,[IO.FileAccess]::Read,([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
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
  [IO.File]::Copy($path,$target,$true)
  $expected=File-Sha256 $path
 }
 if($expected -ne (File-Sha256 $target)){throw ('Backup mismatch: '+$relative)}
 $entries.Add([ordered]@{Path=$relative;Sha256=$expected;Bytes=([IO.FileInfo]::new($target)).Length;LogByteSnapshot=$snapshot})
}
$manifest=[ordered]@{CapturedUtc=[DateTime]::UtcNow.ToString('o');Source=$sourceDirectory;Destination=$destinationDirectory;Files=$entries}
$manifestPath=$sourceIo+'\backup-manifest.json'
[IO.File]::WriteAllText($manifestPath,($manifest | ConvertTo-Json -Depth 6),[Text.UTF8Encoding]::new($false))
[IO.File]::Copy($manifestPath,$destinationIo+'\backup-manifest.json',$true)
if((File-Sha256 $manifestPath) -ne (File-Sha256 ($destinationIo+'\backup-manifest.json'))){throw 'Backup manifest mismatch.'}
Write-Host ('Verified D: backup: '+$destinationDirectory+' ('+$entries.Count+' files plus manifest; logs are byte snapshots)')
