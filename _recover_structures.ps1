$root = '"'"'C:\Users\User\source\repos\Folded Archive\NeuralResonanceEngine.DNN\Structures'"'"'
$recoverRoot = '"'"'C:\Users\User\source\repos\Folded Archive\NeuralResonanceEngine.DNN\_recover_all'"'"'
$tool = '"'"'C:\Users\User\source\repos\Folded Archive\NeuralResonanceEngine.DNN\.tools\ilspycmd.exe'"'"'

New-Item -ItemType Directory -Force -Path $recoverRoot | Out-Null
$dirs = Get-ChildItem $root -Directory
$restored = 0
$failed = @()

foreach($dir in $dirs){
  $dll = Get-ChildItem $dir.FullName -Recurse -Filter '"'"'NeuralResonanceEngine.Structures*.dll'"'"' |
    Where-Object { $_.FullName -match '"'"'\\bin\\Debug\\net8\.0\\'"'"' } |
    Select-Object -First 1

  if(-not $dll){
    $failed += $dir.Name
    continue
  }

  $out = Join-Path $recoverRoot $dir.Name
  if(Test-Path $out){ Remove-Item $out -Recurse -Force }
  New-Item -ItemType Directory -Force -Path $out | Out-Null

  & $tool -p -o $out $dll.FullName | Out-Null

  Get-ChildItem $dir.FullName -Filter *.cs -File | Remove-Item -Force
  $csFiles = Get-ChildItem $out -Recurse -Filter *.cs -File |
    Where-Object { $_.FullName -notmatch '"'"'\\Properties\\AssemblyInfo\.cs$'"'"' }

  foreach($cs in $csFiles){
    Copy-Item $cs.FullName -Destination (Join-Path $dir.FullName $cs.Name) -Force
  }

  $restored++
}

Write-Output ("restored=$restored failed=$($failed.Count)")
if($failed.Count -gt 0){ Write-Output ($failed -join '"'"', '"'"') }
