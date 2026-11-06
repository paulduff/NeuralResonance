param(
 [Parameter(Mandatory=$true)][string]$PackageDirectory,
 [switch]$InWindow,
 [string]$LaunchTaskName=''
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
$package=(Resolve-Path -LiteralPath $PackageDirectory).Path
$configuration=Get-Content -LiteralPath (Join-Path $package 'configuration.json') -Raw | ConvertFrom-Json
$kind=if($configuration.PSObject.Properties.Name -contains 'VerificationKind'){$configuration.VerificationKind}else{'channels'}
if($kind -notin @('cell','channels')){throw 'Unknown Golgi verification kind.'}
$cell=$kind -eq 'cell'
$probeName=if($cell){'GolgiCellProbe'}else{'GolgiChannelProbe'}
$expectedTests=if($cell){25}else{16}
$testFilter=if($cell){'FullyQualifiedName~GolgiChannelKineticsTests|FullyQualifiedName~GolgiReferenceCellTests|FullyQualifiedName~SolutionManifestTests'}else{'FullyQualifiedName~GolgiChannelKineticsTests'}
if(-not $InWindow){
 if(Test-Path -LiteralPath (Join-Path $package 'launch.json')){throw 'Use a new package; this one has already launched.'}
 $taskName='DNNE-Golgi-'+$kind+'-'+[Guid]::NewGuid().ToString('N').Substring(0,12)
 $shell=Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
 $arguments=@('-NoLogo','-NoProfile','-NoExit','-ExecutionPolicy','Bypass','-File',$PSCommandPath,
  '-InWindow','-LaunchTaskName',$taskName,'-PackageDirectory',$package)
 $quoted=($arguments | ForEach-Object {ConvertTo-ProcessArgument $_}) -join ' '
 $action=New-ScheduledTaskAction -Execute $shell -Argument $quoted -WorkingDirectory $package
 $principal=New-ScheduledTaskPrincipal -UserId ([Security.Principal.WindowsIdentity]::GetCurrent().Name) -LogonType Interactive -RunLevel Limited
 $settings=New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::FromHours($(if($cell){24}else{4}))) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
 Register-ScheduledTask -TaskName $taskName -Action $action -Principal $principal -Settings $settings | Out-Null
 [ordered]@{TaskName=$taskName;RequestedUtc=[DateTime]::UtcNow.ToString('o');Package=$package;NoLiveMonitoring=$true} |
  ConvertTo-Json | Set-Content -LiteralPath (Join-Path $package 'launch.json') -Encoding UTF8
 Start-ScheduledTask -TaskName $taskName
 Write-Host ('Started independent visible PowerShell task: '+$taskName)
 Write-Host ('Results: '+$package)
 return
}

function Assert-Package {
 foreach($entry in $configuration.PackageFiles){
  $file=Join-Path $package $entry.Path
  if((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $entry.Sha256){throw ('Frozen package changed: '+$entry.Path)}
 }
}
function Invoke-Logged([string]$Executable,[string[]]$NativeArguments,[string]$LogName){
 $writer=[IO.StreamWriter]::new((Join-Path $package $LogName),$false,[Text.UTF8Encoding]::new($false))
 $writer.AutoFlush=$true
 $previous=$ErrorActionPreference
 try {
  $ErrorActionPreference='Continue'
  & $Executable @NativeArguments 2>&1 | ForEach-Object { $line=[string]$_; $writer.WriteLine($line); Write-Host $line } -ErrorAction Stop
  $nativeExit=$LASTEXITCODE
 } finally { $writer.Dispose(); $ErrorActionPreference=$previous }
 if($nativeExit -ne 0){throw ($LogName+' exited '+$nativeExit)}
}
$status=[ordered]@{Status='RUNNING';ExitCode=$null;Revision=$configuration.Revision;
 LiveBrainAccessed=$false;OldSavesLoaded=$false;WholeCellPhysiologyQualified=$false;CircuitQualified=$false}
$statusPath=Join-Path $package 'window-status.json'
$transcribing=$false
if(Test-Path -LiteralPath $statusPath){throw 'Refusing to overwrite an earlier run.'}
$priorThreads=@{}
foreach($name in @('OMP_NUM_THREADS','OPENBLAS_NUM_THREADS','MKL_NUM_THREADS')){
 $priorThreads[$name]=[Environment]::GetEnvironmentVariable($name,'Process')
 [Environment]::SetEnvironmentVariable($name,'1','Process')
}
try {
 Start-Transcript -Path (Join-Path $package 'powershell.log') | Out-Null
 $transcribing=$true
 $Host.UI.RawUI.WindowTitle=if($cell){'DNNE Golgi coupled cell - independent numerical comparison'}else{'DNNE Golgi ion channels - isolated verification'}
 Write-Host ('Results: '+$package) -ForegroundColor Cyan
 if($cell){Write-Host 'Isolated Golgi cell: free voltage and calcium, cable conservation, channel controls and independent coupled ODE reference. No live brain or old saves.'}
 else{Write-Host 'Isolated voltage/calcium clamps only. No live brain, no old saves, no whole-cell or Avatar qualification.'}
 $status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
 Assert-Package
 Write-Host '========== BUILDING FROZEN SOURCE ==========' -ForegroundColor Cyan
 $testBuild=Join-Path $package 'source/artifacts/golgi-tests'
 $probeBuild=Join-Path $package 'source/artifacts/golgi-probe'
 Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package 'source/tests/NeuralResonanceEngine.DNNE.Tests/NeuralResonanceEngine.DNNE.Tests.csproj'),
  '-c','Release','--artifacts-path',$testBuild,'--nologo','-v','minimal') 'build-tests.log'
 Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package ('source/tools/'+$probeName+'/'+$probeName+'.csproj')),
  '-c','Release','--artifacts-path',$probeBuild,'--nologo','-v','minimal') 'build-probe.log'
 $testAssembly=Join-Path $testBuild 'bin/NeuralResonanceEngine.DNNE.Tests/release/NeuralResonanceEngine.DNNE.Tests.dll'
 $probeAssembly=Join-Path $probeBuild ('bin/'+$probeName+'/release/NeuralResonanceEngine.DNNE.Tests.dll')
 # Source-reading tests must discover this frozen solution, never the live repo.
 $testRoot=[IO.DirectoryInfo]::new((Split-Path -Parent $testAssembly))
 while($null -ne $testRoot -and -not (Test-Path -LiteralPath (Join-Path $testRoot.FullName 'NeuralResonanceEngine.DNNE.slnx'))){$testRoot=$testRoot.Parent}
 if($null -eq $testRoot -or -not $testRoot.FullName.Equals((Join-Path $package 'source'),[StringComparison]::OrdinalIgnoreCase)){
  throw 'Test assembly is not confined to the frozen source solution.'
 }
 $binaryDirectories=@((Split-Path -Parent $testAssembly),(Split-Path -Parent $probeAssembly))
 if($cell){
  $clampBuild=Join-Path $package 'source/artifacts/golgi-clamp'
  Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package 'source/tools/GolgiChannelProbe/GolgiChannelProbe.csproj'),
   '-c','Release','--artifacts-path',$clampBuild,'--nologo','-v','minimal') 'build-clamp.log'
  $clampAssembly=Join-Path $clampBuild 'bin/GolgiChannelProbe/release/NeuralResonanceEngine.DNNE.Tests.dll'
  $binaryDirectories+=Split-Path -Parent $clampAssembly
 }
 $binaries=@(Get-ChildItem -LiteralPath $binaryDirectories -Recurse -File |
  ForEach-Object {[ordered]@{Path=$_.FullName;Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})
 $binaries | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $package 'build-binaries.json') -Encoding UTF8
 Assert-Package
 $testOutput=Join-Path $package 'test-results'
 New-Item -ItemType Directory -Path $testOutput | Out-Null
 Write-Host '========== FOCUSED MECHANISM TESTS ==========' -ForegroundColor Cyan
 Invoke-Logged $configuration.DotnetExecutable @('vstest',$testAssembly,
  ('/TestCaseFilter:'+$testFilter),('/ResultsDirectory:'+$testOutput),'/Logger:trx;LogFileName=channels.trx') 'tests.log'
 [xml]$trx=Get-Content -LiteralPath (Join-Path $testOutput 'channels.trx') -Raw
 $ns=[Xml.XmlNamespaceManager]::new($trx.NameTable);$ns.AddNamespace('t','http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
 $results=@($trx.SelectNodes('//t:UnitTestResult',$ns))
 if($results.Count -ne $expectedTests -or @($results | Where-Object {$_.outcome -ne 'Passed'}).Count){throw ('The complete '+$expectedTests+'-case focused test set did not pass.')}
 if($cell){
  Write-Host '========== CLAMP REFERENCE ==========' -ForegroundColor Cyan
  Invoke-Logged $configuration.DotnetExecutable @($clampAssembly,(Join-Path $package 'clamps')) 'clamps.log'
  Invoke-Logged $configuration.PythonExecutable @('-B',(Join-Path $package 'analyse-golgi-channels.py'),(Join-Path $package 'clamps'),(Join-Path $package 'reference')) 'clamps-analysis.log'
  Invoke-Logged $configuration.PythonExecutable @('-B',(Join-Path $package 'golgi-active-reference.py'),'--clamps',(Join-Path $package 'reference'),(Join-Path $package 'clamps')) 'gates-reference.log'
 }
 Write-Host '========== CAPTURE MATRIX ==========' -ForegroundColor Cyan
 Invoke-Logged $configuration.DotnetExecutable @($probeAssembly,(Join-Path $package 'study')) 'capture.log'
 Write-Host '========== INDEPENDENT REFERENCE ==========' -ForegroundColor Cyan
 $analyser=if($cell){'analyse-golgi-cell.py'}else{'analyse-golgi-channels.py'}
 Invoke-Logged $configuration.PythonExecutable @('-B',(Join-Path $package $analyser),(Join-Path $package 'study'),(Join-Path $package 'reference')) 'analysis.log'
 if($cell){
  Write-Host '========== ACTIVE CELL: COUPLED BDF REFERENCE ==========' -ForegroundColor Cyan
  Invoke-Logged $configuration.PythonExecutable @('-B',(Join-Path $package 'golgi-active-reference.py'),(Join-Path $package 'study'),(Join-Path $package 'reference'),(Join-Path $package 'clamps')) 'active-reference.log'
 }
 Assert-Package
 foreach($entry in $binaries){if((Get-FileHash -LiteralPath $entry.Path -Algorithm SHA256).Hash -ne $entry.Sha256){throw ('Test binary changed: '+$entry.Path)}}
 $status.Status='FINISHED';$status.ExitCode=0
} catch {
 $status.Status='FAILED';$status.ExitCode=1;$status['Error']=$_.Exception.ToString()
 $status | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $statusPath -Encoding UTF8
 Write-Host ('FAILED: '+$_.Exception.Message) -ForegroundColor Red
} finally {
 foreach($name in $priorThreads.Keys){[Environment]::SetEnvironmentVariable($name,$priorThreads[$name],'Process')}
 $status['CompletedUtc']=[DateTime]::UtcNow.ToString('o')
 if($transcribing){Stop-Transcript | Out-Null}
 $status | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $statusPath -Encoding UTF8
 try { & (Join-Path $package 'backup-run.ps1') -Source $package -Destination $configuration.BackupDestination }
 catch {
  $status.Status='FAILED';$status.ExitCode=1;$status['BackupError']=$_.Exception.ToString()
  $status | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $statusPath -Encoding UTF8
  Write-Host ('BACKUP FAILED: '+$_.Exception.Message) -ForegroundColor Red
 }
 if($LaunchTaskName){Unregister-ScheduledTask -TaskName $LaunchTaskName -Confirm:$false -ErrorAction SilentlyContinue}
 Write-Host ('========== '+$status.Status+' ==========') -ForegroundColor Cyan
 if($cell){Write-Host 'Tell Orion the Golgi-cell outcome. Numerical comparison, physiological calibration and Avatar recovery are separate.'}
 else{Write-Host 'Tell Orion the outcome. These are channel checks; free-cell firing, circuit function and Avatar recovery remain unqualified.'}
}
