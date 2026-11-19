param([Parameter(Mandatory=$true)][string]$PackageDirectory,[switch]$InWindow,[string]$LaunchTaskName='')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
$package=(Resolve-Path -LiteralPath $PackageDirectory).Path
$configuration=Get-Content -LiteralPath (Join-Path $package 'configuration.json') -Raw | ConvertFrom-Json
if(-not $configuration.IsolatedCanalBundle -or -not $configuration.NoLiveMonitoring -or $configuration.ExpectedTestCases -ne 204 -or $configuration.LiveSelectionChanged -or $configuration.PhysiologyQualified -or $configuration.AvatarRecoveryAssessed){throw 'Unexpected timed bundle configuration.'}
function Extended-Path([string]$Path){
 if($Path.StartsWith('\\?\')){return $Path}
 if($Path.StartsWith('\\')){return '\\?\UNC\'+$Path.Substring(2)}
 return '\\?\'+[IO.Path]::GetFullPath($Path)
}
function File-Sha256([string]$Path){
 $stream=[IO.File]::OpenRead((Extended-Path $Path));$algorithm=[Security.Cryptography.SHA256]::Create()
 try{return [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-','')}
 finally{$algorithm.Dispose();$stream.Dispose()}
}
function Assert-Package {
 foreach($entry in $configuration.PackageFiles){if((File-Sha256 (Join-Path $package $entry.Path)) -ne $entry.Sha256){throw ('Frozen source/input changed: '+$entry.Path)}}
 foreach($name in @('Dotnet','Python')){
  if((File-Sha256 $configuration.($name+'Executable')) -ne $configuration.($name+'ExecutableSha256')){throw ($name+' executable changed.')}
 }
}
if(-not $InWindow){
 if(Test-Path -LiteralPath (Join-Path $package 'launch.json')){throw 'Already launched; use a fresh package.'}
 $manifestPath=Join-Path $package 'backup-manifest.json';$manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
 if($manifest.Destination -ne $configuration.BackupDestination){throw 'Initial backup destination differs.'}
 if((File-Sha256 $manifestPath) -ne (File-Sha256 (Join-Path $configuration.BackupDestination 'backup-manifest.json'))){throw 'Initial backup manifests differ.'}
 foreach($entry in $manifest.Files){foreach($directory in @($package,$configuration.BackupDestination)){
  if((File-Sha256 (Join-Path $directory $entry.Path)) -ne $entry.Sha256){throw ('Initial source/backup differs: '+$entry.Path)}
 }}
 Assert-Package
 $taskName='DNNE-Canal-Bundle-'+[Guid]::NewGuid().ToString('N').Substring(0,12)
 $shell=Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
 $arguments=@('-NoLogo','-NoProfile','-NoExit','-ExecutionPolicy','Bypass','-File',$PSCommandPath,'-InWindow','-LaunchTaskName',$taskName,'-PackageDirectory',$package)
 $action=New-ScheduledTaskAction -Execute $shell -Argument (($arguments | ForEach-Object {ConvertTo-ProcessArgument $_}) -join ' ') -WorkingDirectory $package
 $principal=New-ScheduledTaskPrincipal -UserId ([Security.Principal.WindowsIdentity]::GetCurrent().Name) -LogonType Interactive -RunLevel Limited
 $settings=New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
 Register-ScheduledTask -TaskName $taskName -Action $action -Principal $principal -Settings $settings | Out-Null
 [ordered]@{TaskName=$taskName;RequestedUtc=[DateTime]::UtcNow.ToString('o');Package=$package;NoLiveMonitoring=$true} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $package 'launch.json') -Encoding UTF8
 Start-ScheduledTask -TaskName $taskName
 Write-Host ('Started independent visible PowerShell task: '+$taskName)
 Write-Host ('Results: '+$package)
 return
}
function Invoke-Logged([string]$Executable,[string[]]$NativeArguments,[string]$LogName){
 $writer=[IO.StreamWriter]::new((Join-Path $package $LogName),$false,[Text.UTF8Encoding]::new($false));$writer.AutoFlush=$true
 $previous=$ErrorActionPreference
 try{
  $ErrorActionPreference='Continue'
  & $Executable @NativeArguments 2>&1 | ForEach-Object {$line=[string]$_;$writer.WriteLine($line);Write-Host $line} -ErrorAction Stop
  $nativeExit=$LASTEXITCODE
 }finally{$writer.Dispose();$ErrorActionPreference=$previous}
 if($nativeExit -ne 0){throw ($LogName+' exited '+$nativeExit)}
}
function Python-Stage([string]$Stage){
 Invoke-Logged $configuration.PythonExecutable @('-B',(Join-Path $package 'source/tools/canal_bundle_gate.py'),$Stage,$package) ('reference-'+$Stage+'.log')
}
function Capture-Binaries([string]$Assembly,[string]$Name){
 $entries=@([IO.Directory]::EnumerateFiles((Extended-Path (Split-Path -Parent $Assembly)),'*',[IO.SearchOption]::AllDirectories) | ForEach-Object {
  [ordered]@{Path=$_.Substring(4);Sha256=(File-Sha256 $_)}
 })
 $entries | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $package $Name) -Encoding UTF8
 return $entries
}
$status=[ordered]@{Status='RUNNING';ExitCode=$null;Revision=$configuration.Revision;IndependentTrajectoryCriteriaPassed=$false;
 ComponentTestsReused=0;FocusedTestsPassed=$false;TimedBundleCriteriaPassed=$false;
 MechanicalCalibrationQualified=$false;StationaryInitializationQualified=$false;SpatialGeometryCalibrated=$false;AfferentFiringQualified=$false;
 LiveBrainAccessed=$false;OldSavesLoaded=$false;NativeBodyExecuted=$false;PhysiologyQualified=$false;AvatarRecoveryAssessed=$false;FullUnitSuitePassed=$false}
$statusPath=Join-Path $package 'window-status.json';$transcribing=$false;$testBinaries=@();$probeBinaries=@();$probe=''
if(Test-Path -LiteralPath $statusPath){throw 'Refusing to overwrite earlier results.'}
function Save-Status([string]$Stage){
 $status['Stage']=$Stage
 $temporary=Join-Path $package 'window-status.tmp'
 $status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $temporary -Encoding UTF8
 Move-Item -LiteralPath $temporary -Destination $statusPath -Force
}
try{
 Start-Transcript -Path (Join-Path $package 'powershell.log') | Out-Null;$transcribing=$true
 $Host.UI.RawUI.WindowTitle='DNNE canal to sensory bundle qualification'
 Write-Host ('Results: '+$package) -ForegroundColor Cyan
 Write-Host '204 component checks, then three recorded-head replays with all accepted mechanics preserved and dense bundle samples compared against saved independent references. No live brain or Unity.'
 Save-Status 'preflight';Assert-Package;Python-Stage 'preflight'
 $build=Join-Path $package 'source/artifacts/canal-bundle-tests'
 Save-Status 'build-tests'
 Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package 'source/tests/NeuralResonanceEngine.DNNE.Tests/NeuralResonanceEngine.DNNE.Tests.csproj'),'-c','Release','--artifacts-path',$build,'--nologo','-v','minimal') 'test-build.log'
 $assembly=Join-Path $build 'bin/NeuralResonanceEngine.DNNE.Tests/release/NeuralResonanceEngine.DNNE.Tests.dll'
 $testOutput=Join-Path $package 'test-results';New-Item -ItemType Directory -Path $testOutput | Out-Null
 $testBinaries=Capture-Binaries $assembly 'test-binaries.json'
 $probeBuild=Join-Path $package 'source/artifacts/canal-bundle-runtime'
 Save-Status 'build-runtime-fingerprint'
 Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package 'source/tools/NativeMotorBridgeProbe/NativeMotorBridgeProbe.csproj'),'-c','Release','--artifacts-path',$probeBuild,'--nologo','-v','minimal') 'probe-build.log'
 $probe=Join-Path $probeBuild 'bin/NativeMotorBridgeProbe/release/NeuralResonanceEngine.DNNE.Tests.dll'
 $probeBinaries=Capture-Binaries $probe 'probe-binaries.json'
 Invoke-Logged $configuration.DotnetExecutable @($probe,'--runtime-fingerprint',(Join-Path $package 'managed-runtime-before.json')) 'managed-runtime-before.log'
 Python-Stage 'runtime';$status['MatchesQualifiedManagedRuntime']=$true
 Save-Status 'focused-tests'
 Invoke-Logged $configuration.DotnetExecutable @('vstest',$assembly,('/TestCaseFilter:'+$configuration.TestFilter),('/ResultsDirectory:'+$testOutput),'/Logger:trx;LogFileName=focused.trx') 'focused-tests.log'
 Python-Stage 'check-tests';$status['FocusedTestsPassed']=$true;$status['PassedFocusedTests']=204
 Save-Status 'native-replay'
 Invoke-Logged $configuration.DotnetExecutable @($probe,'--canal-bundle',(Join-Path $package 'bundle-input.json'),(Join-Path $package 'study')) 'study.log'
 Save-Status 'saved-reference-comparison';Python-Stage 'compare'
 $analysis=Get-Content -LiteralPath (Join-Path $package 'analysis.json') -Raw | ConvertFrom-Json
 if(-not $analysis.completed -or -not $analysis.numericalCriteriaPassed -or -not $analysis.originalMechanicalTrajectoriesExactlyPreserved -or $analysis.acceptedMechanicalSteps -ne 491729 -or $analysis.denseSamples -ne $configuration.ExpectedDenseSamples){throw 'Timed bundle qualification incomplete.'}
 foreach($flag in @('continuousMotionAccuracyQualified','anatomicalCalibrationQualified','connectedSensoryDynamicsExecuted','afferentFiringQualified','avatarBalanceAssessed')){if($analysis.$flag){throw ('Unexpected qualification: '+$flag)}}
 $status.TimedBundleCriteriaPassed=$true;$status.IndependentTrajectoryCriteriaPassed=$true
 $status.Status='DIAGNOSTICS_COMPLETE';$status.ExitCode=0
}catch{
 $status.Status='FAILED';$status.ExitCode=1;$status['Error']=$_.Exception.ToString()
 Write-Host ('FAILED: '+$_.Exception.Message) -ForegroundColor Red
}finally{
 if($status.Status -eq 'RUNNING'){$status.Status='INTERRUPTED';$status.ExitCode=2}
 Save-Status 'preservation'
 try{Python-Stage 'postflight';$status['QualifiedDependenciesPreserved']=$true}
 catch{$status.Status='FAILED';$status.ExitCode=1;$status.IndependentTrajectoryCriteriaPassed=$false;$status['DependencyError']=$_.Exception.ToString()}
 if($probe -and (Test-Path -LiteralPath (Join-Path $package 'managed-runtime-before.json'))){
  try{
   Invoke-Logged $configuration.DotnetExecutable @($probe,'--runtime-fingerprint',(Join-Path $package 'managed-runtime-after.json')) 'managed-runtime-after.log'
   if((File-Sha256 (Join-Path $package 'managed-runtime-before.json')) -ne (File-Sha256 (Join-Path $package 'managed-runtime-after.json'))){throw 'Managed runtime changed during the study.'}
   $status['ManagedRuntimePreserved']=$true
  }catch{$status.Status='FAILED';$status.ExitCode=1;$status.IndependentTrajectoryCriteriaPassed=$false;$status['ManagedRuntimeError']=$_.Exception.ToString()}
 }
 try{Assert-Package;foreach($entry in @($testBinaries)+@($probeBinaries)){if((File-Sha256 $entry.Path) -ne $entry.Sha256){throw ('Built binary changed: '+$entry.Path)}};$status['FrozenPackageUnchanged']=$true}
 catch{$status.Status='FAILED';$status.ExitCode=1;$status.IndependentTrajectoryCriteriaPassed=$false;$status['FrozenSourceError']=$_.Exception.ToString()}
 $status['CompletedUtc']=[DateTime]::UtcNow.ToString('o')
 if($transcribing){Stop-Transcript | Out-Null}
 Save-Status 'complete-before-backup'
 try{& (Join-Path $package 'backup-run.ps1') -Source $package -Destination $configuration.BackupDestination}
 catch{$status.Status='FAILED';$status.ExitCode=1;$status['BackupError']=$_.Exception.ToString();Save-Status 'backup-failed';Write-Host ('BACKUP FAILED: '+$_.Exception.Message) -ForegroundColor Red}
 if($LaunchTaskName){Unregister-ScheduledTask -TaskName $LaunchTaskName -Confirm:$false -ErrorAction SilentlyContinue}
 Write-Host ('========== '+$status.Status+' ==========') -ForegroundColor Cyan
 Write-Host ('Results: '+$package)
 Write-Host 'Tell Orion the timed canal/bundle outcome. Sampled mechanical input, anatomical calibration, connected sensory dynamics and Avatar balance are separate.'
}
