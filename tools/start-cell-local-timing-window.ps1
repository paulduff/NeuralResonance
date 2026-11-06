param(
 [Parameter(Mandatory=$true)][string]$PackageDirectory,
 [switch]$InWindow,
 [string]$LaunchTaskName=''
)
. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
$package=(Resolve-Path -LiteralPath $PackageDirectory).Path
$configuration=Get-Content -LiteralPath (Join-Path $package 'configuration.json') -Raw | ConvertFrom-Json
if(-not $InWindow){
 if(Test-Path -LiteralPath (Join-Path $package 'launch.json')){throw 'Use a new package; this one has already launched.'}
 $taskName='DNNE-Cell-Local-'+[Guid]::NewGuid().ToString('N').Substring(0,12)
 $shell=Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
 $arguments=@('-NoLogo','-NoProfile','-NoExit','-ExecutionPolicy','Bypass','-File',$PSCommandPath,
  '-InWindow','-LaunchTaskName',$taskName,'-PackageDirectory',$package)
 $quoted=($arguments | ForEach-Object {ConvertTo-ProcessArgument $_}) -join ' '
 $action=New-ScheduledTaskAction -Execute $shell -Argument $quoted -WorkingDirectory $package
 $principal=New-ScheduledTaskPrincipal -UserId ([Security.Principal.WindowsIdentity]::GetCurrent().Name) -LogonType Interactive -RunLevel Limited
 $settings=New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::FromHours(24)) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
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
  if((Get-FileHash -LiteralPath (Join-Path $package $entry.Path) -Algorithm SHA256).Hash -ne $entry.Sha256){throw ('Frozen package changed: '+$entry.Path)}
 }
}
function Assert-Protected {
 $verified=0
 foreach($entry in $configuration.ProtectedBackupFiles){
  if((Get-FileHash -LiteralPath $entry.Path -Algorithm SHA256).Hash -ne $entry.Sha256){throw ('Protected file changed: '+$entry.Path)}
  $verified++
  if($verified % 1000 -eq 0){Write-Host ('Verified '+$verified+' / '+$configuration.ProtectedBackupFiles.Count+' protected files')}
 }
 Write-Host ('Verified all '+$verified+' protected files.')
}
function Invoke-Logged([string]$Executable,[string[]]$NativeArguments,[string]$LogName,[switch]$ReturnExit){
 $writer=[IO.StreamWriter]::new((Join-Path $package $LogName),$false,[Text.UTF8Encoding]::new($false))
 $writer.AutoFlush=$true;$previous=$ErrorActionPreference
 try {
  $ErrorActionPreference='Continue'
  & $Executable @NativeArguments 2>&1 | ForEach-Object {$line=[string]$_;$writer.WriteLine($line);Write-Host $line} -ErrorAction Stop
  $nativeExit=$LASTEXITCODE
 } finally {$writer.Dispose();$ErrorActionPreference=$previous}
 if($ReturnExit){return $nativeExit}
 if($nativeExit -ne 0){throw ($LogName+' exited '+$nativeExit)}
}
$status=[ordered]@{Status='RUNNING';ExitCode=$null;Revision=$configuration.Revision;
 FreshCellsOnly=$true;LiveBrainAccessed=$false;OldSavesLoaded=$false;ProductionCellLocalModeEnabled=$false;
 SolverConvergenceQualified=$false;PhysiologicalCalibrationComplete=$false;PhysicalRecoveryAssessed=$false}
$statusPath=Join-Path $package 'window-status.json';$transcribing=$false
if(Test-Path -LiteralPath $statusPath){throw 'Refusing to overwrite an earlier run.'}
$priorEnvironment=@{}
foreach($name in @('NRE_SYNAPSE_STATE_DIR','SERVICE_INSTANCE','HEMISPHERE','NRE_MICROTUBULE_MODE')){
 $priorEnvironment[$name]=[Environment]::GetEnvironmentVariable($name,'Process')
}
try {
 Start-Transcript -Path (Join-Path $package 'powershell.log') | Out-Null;$transcribing=$true
 $Host.UI.RawUI.WindowTitle='DNNE cell-local timing - isolated repair comparison'
 Write-Host ('Results: '+$package) -ForegroundColor Cyan
 Write-Host 'Fresh isolated Purkinje cells. Real arrivals are preserved; unrelated events must leave other cells unchanged.'
 Write-Host 'Full unit suite, 8-second preflight, then 48 conditions of 60 seconds each. No live brain or Avatar.'
 $status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
 Assert-Package;Assert-Protected
 # Tests with their own fixtures retain their separate temporary directories.
 # Any test using the default store is confined to this new package as well.
 [Environment]::SetEnvironmentVariable('NRE_SYNAPSE_STATE_DIR',(Join-Path $package 'unit-default-state'),'Process')
 [Environment]::SetEnvironmentVariable('SERVICE_INSTANCE',$null,'Process')
 [Environment]::SetEnvironmentVariable('HEMISPHERE',$null,'Process')
 [Environment]::SetEnvironmentVariable('NRE_MICROTUBULE_MODE','classical','Process')
 $testBuild=Join-Path $package 'build-tests';$probeBuild=Join-Path $package 'build-probe'
 Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package 'source/tests/NeuralResonanceEngine.DNNE.Tests/NeuralResonanceEngine.DNNE.Tests.csproj'),
  '-c','Release','--artifacts-path',$testBuild,'--nologo','-v','minimal') 'build-tests.log'
 Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package 'source/tools/PosturalCellProbe/PosturalCellProbe.csproj'),
  '-c','Release','--artifacts-path',$probeBuild,'--nologo','-v','minimal') 'build-probe.log'
 $testAssembly=Join-Path $testBuild 'bin/NeuralResonanceEngine.DNNE.Tests/release/NeuralResonanceEngine.DNNE.Tests.dll'
 $probeAssembly=Join-Path $probeBuild 'bin/PosturalCellProbe/release/NeuralResonanceEngine.DNNE.Tests.dll'
 $binaries=@(Get-ChildItem -LiteralPath (Split-Path -Parent $testAssembly),(Split-Path -Parent $probeAssembly) -Recurse -File |
  ForEach-Object {[ordered]@{Path=$_.FullName;Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})
 $binaries | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $package 'build-binaries.json') -Encoding UTF8
 Assert-Package
 $testOutput=Join-Path $package 'test-results';New-Item -ItemType Directory -Path $testOutput | Out-Null
 Write-Host '========== FULL DNNE UNIT SUITE ==========' -ForegroundColor Cyan
 $testExit=Invoke-Logged $configuration.DotnetExecutable @('vstest',$testAssembly,
  ('/ResultsDirectory:'+$testOutput),'/Logger:trx;LogFileName=dnne.trx') 'tests.log' -ReturnExit
 [xml]$trx=Get-Content -LiteralPath (Join-Path $testOutput 'dnne.trx') -Raw
 $ns=[Xml.XmlNamespaceManager]::new($trx.NameTable);$ns.AddNamespace('t','http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
 $results=@($trx.SelectNodes('//t:UnitTestResult',$ns))
 $failed=@($results | Where-Object {$_.outcome -ne 'Passed'})
 $focused=@($results | Where-Object {$_.testName -like '*ConnectedTimingTests.*'})
 if($results.Count -lt $configuration.MinimumFullSuiteTests -or $focused.Count -ne $configuration.ConnectedTimingTests -or
  @($focused | Where-Object {$_.outcome -ne 'Passed'}).Count){throw 'Timing tests did not all pass, or the required suite was incomplete.'}
 if($failed.Count -eq 1 -and $failed[0].testName -eq $configuration.KnownUnresolvedFullSuiteFailure -and $testExit -eq 1){
  Write-Host ('KNOWN UNRESOLVED FAILURE: '+$failed[0].testName+'. The whole suite is not green.') -ForegroundColor Yellow
 }elseif($failed.Count -ne 0 -or $testExit -ne 0){throw 'New, unexpected, or incomplete unit-suite failure; stopping before experiments.'}
 $status['UnitTests']=[ordered]@{Total=$results.Count;Passed=$results.Count-$failed.Count;Failed=@($failed | ForEach-Object {$_.testName});TimingPassed=$focused.Count}
 foreach($stage in @('preflight','study')){
  $stagePath=Join-Path $package $stage
  if(Test-Path -LiteralPath $stagePath){throw ('Refusing to reuse '+$stagePath)}
  New-Item -ItemType Directory -Path $stagePath | Out-Null
  Copy-Item -LiteralPath (Join-Path $package 'baseline.json') -Destination $stagePath
  foreach($mode in @('global','local')){
   Write-Host ('========== '+$stage.ToUpperInvariant()+' / '+$mode.ToUpperInvariant()+' ==========') -ForegroundColor Cyan
   $probeArguments=@($probeAssembly,(Join-Path $stagePath $mode),'--boundary-timing',(Join-Path $package 'inputs.json'))
   if($stage -eq 'preflight'){$probeArguments+='--smoke'}
   if($mode -eq 'local'){$probeArguments+='--cell-local'}
   Invoke-Logged $configuration.DotnetExecutable $probeArguments ($stage+'-'+$mode+'.log')
  }
  Invoke-Logged $configuration.PythonExecutable @('-B',(Join-Path $package 'source/tools/analyse-cell-local-timing.py'),$stagePath) ($stage+'-analysis.log')
  if($stage -eq 'preflight'){
   Invoke-Logged $configuration.PythonExecutable @('-B',(Join-Path $package 'source/tools/tests/boundary-timing-analysis.Tests.py'),(Join-Path $stagePath 'global')) 'global-analysis-tests.log'
   Invoke-Logged $configuration.PythonExecutable @('-B',(Join-Path $package 'source/tools/tests/cell-local-timing-analysis.Tests.py'),(Join-Path $stagePath 'local')) 'local-analysis-tests.log'
  }
  Assert-Package
 }
 foreach($entry in $binaries){if((Get-FileHash -LiteralPath $entry.Path -Algorithm SHA256).Hash -ne $entry.Sha256){throw ('Test binary changed: '+$entry.Path)}}
 $status.Status='FINISHED';$status.ExitCode=0
}catch{
 $status.Status='FAILED';$status.ExitCode=1;$status['Error']=$_.Exception.ToString()
 Write-Host ('FAILED: '+$_.Exception.Message) -ForegroundColor Red
}finally{
 foreach($name in $priorEnvironment.Keys){[Environment]::SetEnvironmentVariable($name,$priorEnvironment[$name],'Process')}
 try{Assert-Protected;$status['OldBackupUnchanged']=$true}
 catch{$status.Status='FAILED';$status.ExitCode=1;$status['PreservationError']=$_.Exception.ToString()}
 $status['CompletedUtc']=[DateTime]::UtcNow.ToString('o')
 if($transcribing){Stop-Transcript | Out-Null}
 $status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8
 try{& (Join-Path $package 'backup-run.ps1') -Source $package -Destination $configuration.BackupDestination}
 catch{
  $status.Status='FAILED';$status.ExitCode=1;$status['BackupError']=$_.Exception.ToString()
  $status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8
  Write-Host ('BACKUP FAILED: '+$_.Exception.Message) -ForegroundColor Red
 }
 if($LaunchTaskName -match '^DNNE-Cell-Local-[0-9a-f]{12}$'){Unregister-ScheduledTask -TaskName $LaunchTaskName -Confirm:$false -ErrorAction SilentlyContinue}
 Write-Host ('========== '+$status.Status+' ==========') -ForegroundColor Cyan
 Write-Host 'Tell Orion the outcome. Cell-local timing, solver accuracy and biological function are separate checks.'
}
