param(
 [Parameter(Mandatory=$true)][string]$PackageDirectory,
 [switch]$InWindow,
 [string]$LaunchTaskName=''
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
$package=(Resolve-Path -LiteralPath $PackageDirectory).Path
$configuration=Get-Content -LiteralPath (Join-Path $package 'configuration.json') -Raw | ConvertFrom-Json
if(-not $InWindow){
 if(Test-Path -LiteralPath (Join-Path $package 'launch.json')){throw 'Use a new package; this one has already launched.'}
 $taskName='DNNE-Motor-Axon-'+[Guid]::NewGuid().ToString('N').Substring(0,12)
 $shell=Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
 $arguments=@('-NoLogo','-NoProfile','-NoExit','-ExecutionPolicy','Bypass','-File',$PSCommandPath,
  '-InWindow','-LaunchTaskName',$taskName,'-PackageDirectory',$package)
 $quoted=($arguments | ForEach-Object {ConvertTo-ProcessArgument $_}) -join ' '
 $action=New-ScheduledTaskAction -Execute $shell -Argument $quoted -WorkingDirectory $package
 $principal=New-ScheduledTaskPrincipal -UserId ([Security.Principal.WindowsIdentity]::GetCurrent().Name) -LogonType Interactive -RunLevel Limited
 $settings=New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
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
  if((Get-FileHash -LiteralPath (Join-Path $package $entry.Path) -Algorithm SHA256).Hash -ne $entry.Sha256){throw ('Frozen source changed: '+$entry.Path)}
 }
}
function Invoke-Logged([string]$Executable,[string[]]$NativeArguments,[string]$LogName){
 $writer=[IO.StreamWriter]::new((Join-Path $package $LogName),$false,[Text.UTF8Encoding]::new($false))
 $writer.AutoFlush=$true;$previous=$ErrorActionPreference
 try {
  $ErrorActionPreference='Continue'
  & $Executable @NativeArguments 2>&1 | ForEach-Object {$line=[string]$_;$writer.WriteLine($line);Write-Host $line} -ErrorAction Stop
  $nativeExit=$LASTEXITCODE
 } finally {$writer.Dispose();$ErrorActionPreference=$previous}
 if($nativeExit -ne 0){throw ($LogName+' exited '+$nativeExit)}
}
$status=[ordered]@{Status='RUNNING';ExitCode=$null;Revision=$configuration.Revision;
 LiveBrainAccessed=$false;OldSavesLoaded=$false;LiveMotorPathEnabled=$false;
 NeuronalSourceQualified=$false;MuscleResponseQualified=$false;AvatarRecoveryAssessed=$false}
$statusPath=Join-Path $package 'window-status.json';$transcribing=$false;$withMotoneuron=$false
if(Test-Path -LiteralPath $statusPath){throw 'Refusing to overwrite an earlier run.'}
try {
 Start-Transcript -Path (Join-Path $package 'powershell.log') | Out-Null;$transcribing=$true
 $withReference=$configuration.PSObject.Properties.Name -contains 'GolgiBaseline' -and $null -ne $configuration.GolgiBaseline
 $withCalcium=$configuration.PSObject.Properties.Name -contains 'MuscleCalciumReference' -and $null -ne $configuration.MuscleCalciumReference
 $withMotoneuron=$configuration.PSObject.Properties.Name -contains 'MotoneuronReference' -and $null -ne $configuration.MotoneuronReference
 $Host.UI.RawUI.WindowTitle=if($withMotoneuron){'DNNE motor-cell channels and passive coupling'}elseif($withCalcium){'DNNE isolated motor-unit calcium and force'}elseif($withReference){'DNNE motor transport retry and Golgi reference refinement'}else{'DNNE motor-axon transport - isolated verification'}
 Write-Host ('Results: '+$package) -ForegroundColor Cyan
 if($withMotoneuron){Write-Host 'Reference motor-cell channels and passive cable. Active firing and the body loop are not assessed.'}
 else {Write-Host 'Original event identity/timing, packet integrity and shared simulation clock. No live brain or body.'}
 $status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
 Assert-Package
 $build=Join-Path $package 'source/artifacts/motor-axon-tests'
 Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package 'source/tests/NeuralResonanceEngine.DNNE.Tests/NeuralResonanceEngine.DNNE.Tests.csproj'),
  '-c','Release','--artifacts-path',$build,'--nologo','-v','minimal') 'build.log'
 $assembly=Join-Path $build 'bin/NeuralResonanceEngine.DNNE.Tests/release/NeuralResonanceEngine.DNNE.Tests.dll'
 $testRoot=[IO.DirectoryInfo]::new((Split-Path -Parent $assembly))
 while($null -ne $testRoot -and -not (Test-Path -LiteralPath (Join-Path $testRoot.FullName 'NeuralResonanceEngine.DNNE.slnx'))){$testRoot=$testRoot.Parent}
 if($null -eq $testRoot -or -not $testRoot.FullName.Equals((Join-Path $package 'source'),[StringComparison]::OrdinalIgnoreCase)){throw 'Tests are not confined to frozen source.'}
 $binaries=@(Get-ChildItem -LiteralPath (Split-Path -Parent $assembly) -Recurse -File |
  ForEach-Object {[ordered]@{Path=$_.FullName;Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})
 $binaries | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $package 'build-binaries.json') -Encoding UTF8
 $testOutput=Join-Path $package 'test-results';New-Item -ItemType Directory -Path $testOutput | Out-Null
 if($withMotoneuron){Write-Host '========== MOTOR-CELL CHANNEL AND PASSIVE TESTS ==========' -ForegroundColor Cyan}
 elseif($withCalcium){Write-Host '========== MOTOR TRANSPORT AND CALCIUM TESTS ==========' -ForegroundColor Cyan}
 else {Write-Host '========== MOTOR-AXON TRANSPORT TESTS ==========' -ForegroundColor Cyan}
 Invoke-Logged $configuration.DotnetExecutable @('vstest',$assembly,('/TestCaseFilter:'+$configuration.TestFilter),
  ('/ResultsDirectory:'+$testOutput),'/Logger:trx;LogFileName=motor-axon.trx') 'tests.log'
 [xml]$trx=Get-Content -LiteralPath (Join-Path $testOutput 'motor-axon.trx') -Raw
 $ns=[Xml.XmlNamespaceManager]::new($trx.NameTable);$ns.AddNamespace('t','http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
 $results=@($trx.SelectNodes('//t:UnitTestResult',$ns))
 if($results.Count -ne $configuration.ExpectedTestCases -or @($results | Where-Object {$_.outcome -ne 'Passed'}).Count){throw 'Required focused/manifest tests did not all pass.'}
 if($withMotoneuron){$status['PassedMotoneuronAndManifestTests']=$results.Count}
 elseif($withCalcium){$status['PassedTransportCalciumAndManifestTests']=$results.Count}
 else {$status['PassedMotorTransportAndManifestTests']=$results.Count}
 $status | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $statusPath -Encoding UTF8
 if($withMotoneuron){
  Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package 'source/tools/MotoneuronReferenceProbe/MotoneuronReferenceProbe.csproj'),
   '-c','Release','--artifacts-path',$build,'--nologo','-v','minimal') 'motoneuron-build.log'
  $probe=Join-Path $build 'bin/MotoneuronReferenceProbe/release/NeuralResonanceEngine.DNNE.Tests.dll'
  $probeBinaries=@(Get-ChildItem -LiteralPath (Split-Path -Parent $probe) -Recurse -File |
   ForEach-Object {[ordered]@{Path=$_.FullName;Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})
  $probeBinaries | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $package 'motoneuron-build-binaries.json') -Encoding UTF8
  Invoke-Logged $configuration.DotnetExecutable @($probe,(Join-Path $package 'motoneuron-study')) 'motoneuron-study.log'
  Invoke-Logged $configuration.PythonExecutable @('-B',(Join-Path $package 'source/tools/motoneuron-reference-check.py'),$package) 'motoneuron-reference.log'
  foreach($entry in $probeBinaries){if((Get-FileHash -LiteralPath $entry.Path -Algorithm SHA256).Hash -ne $entry.Sha256){throw ('Probe binary changed: '+$entry.Path)}}
  $status['MotoneuronChannelAndPassiveCriteriaPassed']=$true
  $status['MotoneuronActiveFiringQualified']=$false
  $status | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $statusPath -Encoding UTF8
 }
 if($withCalcium){
  Write-Host '========== ISOLATED MOTOR-UNIT CALCIUM ==========' -ForegroundColor Cyan
  Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package 'source/tools/MotorUnitCalciumProbe/MotorUnitCalciumProbe.csproj'),
   '-c','Release','--artifacts-path',$build,'--nologo','-v','minimal') 'calcium-build.log'
  $probe=Join-Path $build 'bin/MotorUnitCalciumProbe/release/NeuralResonanceEngine.DNNE.Tests.dll'
  $probeBinaries=@(Get-ChildItem -LiteralPath (Split-Path -Parent $probe) -Recurse -File |
   ForEach-Object {[ordered]@{Path=$_.FullName;Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})
  $probeBinaries | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $package 'calcium-build-binaries.json') -Encoding UTF8
  Invoke-Logged $configuration.DotnetExecutable @($probe,(Join-Path $package 'calcium-study')) 'calcium-study.log'
  Write-Host '========== INDEPENDENT CALCIUM/FORCE REFERENCE ==========' -ForegroundColor Cyan
  Invoke-Logged $configuration.PythonExecutable @('-B',(Join-Path $package 'source/tools/motor-calcium-reference.py'),$package) 'calcium-reference.log'
  foreach($entry in $probeBinaries){if((Get-FileHash -LiteralPath $entry.Path -Algorithm SHA256).Hash -ne $entry.Sha256){throw ('Probe binary changed: '+$entry.Path)}}
  $status['MuscleCalciumNumericalCriteriaPassed']=$true
  $status | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $statusPath -Encoding UTF8
 }
 if($withReference){
  Write-Host '========== GOLGI: TIGHTER AND CROSS-METHOD REFERENCE ==========' -ForegroundColor Cyan
  Write-Host 'Same biological equations, unchanged voltage/timing criteria. Existing candidate traces reused.'
  $threadEnvironment=@{}
  try {
   foreach($name in @('OMP_NUM_THREADS','OPENBLAS_NUM_THREADS','MKL_NUM_THREADS')){
    $threadEnvironment[$name]=[Environment]::GetEnvironmentVariable($name,'Process')
    [Environment]::SetEnvironmentVariable($name,'1','Process')
   }
   Invoke-Logged $configuration.PythonExecutable @('-B',(Join-Path $package 'source/tools/golgi-reference-refinement.py'),$package) 'reference-refinement.log'
   $status['GolgiReferenceCriteriaPassed']=$true
  } finally {foreach($name in $threadEnvironment.Keys){[Environment]::SetEnvironmentVariable($name,$threadEnvironment[$name],'Process')}}
 }
 Assert-Package
 foreach($entry in $binaries){if((Get-FileHash -LiteralPath $entry.Path -Algorithm SHA256).Hash -ne $entry.Sha256){throw ('Binary changed: '+$entry.Path)}}
 $status['PassedTests']=$results.Count;$status.Status='FINISHED';$status.ExitCode=0
} catch {
 $status.Status='FAILED';$status.ExitCode=1;$status['Error']=$_.Exception.ToString()
 $status | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $statusPath -Encoding UTF8
 Write-Host ('FAILED: '+$_.Exception.Message) -ForegroundColor Red
} finally {
 # PowerShell pipeline cancellation can skip catch while still entering finally.
 # Terminal output must never leave a completed capture labelled RUNNING.
 if($status.Status -eq 'RUNNING'){
  $status.Status='INTERRUPTED';$status.ExitCode=2
  $status['Error']='Pipeline ended before completing its checks. Cause not established; inspect transcript and retained stage logs.'
 }
 $status['CompletedUtc']=[DateTime]::UtcNow.ToString('o')
 if($transcribing){Stop-Transcript | Out-Null}
 $status | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $statusPath -Encoding UTF8
 try {& (Join-Path $package 'backup-run.ps1') -Source $package -Destination $configuration.BackupDestination}
 catch {$status.Status='FAILED';$status.ExitCode=1;$status['BackupError']=$_.Exception.ToString();$status | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $statusPath -Encoding UTF8;Write-Host ('BACKUP FAILED: '+$_.Exception.Message) -ForegroundColor Red}
 if($LaunchTaskName){Unregister-ScheduledTask -TaskName $LaunchTaskName -Confirm:$false -ErrorAction SilentlyContinue}
 Write-Host ('========== '+$status.Status+' ==========') -ForegroundColor Cyan
 if($withMotoneuron){Write-Host 'Tell Orion the motor-cell channel/passive outcome. Active firing, circuit function and Avatar recovery remain unqualified.'}
 else {Write-Host 'Tell Orion this run outcome. Neuronal firing, muscle responses and Avatar recovery remain unqualified.'}
}
