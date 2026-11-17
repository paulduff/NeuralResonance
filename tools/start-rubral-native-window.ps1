param([Parameter(Mandatory=$true)][string]$PackageDirectory,[switch]$InWindow,[string]$LaunchTaskName='')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
$package=(Resolve-Path -LiteralPath $PackageDirectory).Path
$configuration=Get-Content -LiteralPath (Join-Path $package 'configuration.json') -Raw | ConvertFrom-Json
if(-not $configuration.IsolatedRubralNative -or -not $configuration.NoLiveMonitoring -or -not $configuration.QualifiedCouplingCorePreserved -or $configuration.ExpectedTestCases -ne 281 -or $configuration.ExpectedNativeCases -ne 7 -or $configuration.ExpectedNativePhysicsSteps -ne 55993 -or $configuration.ExpectedIndependentReferences -ne 15 -or $configuration.ExpectedUnityVersion -ne '6000.3.19f1' -or $configuration.LiveBodySelected -or -not $configuration.NoTimingMeasured -or $configuration.PhysiologyQualified -or $configuration.AvatarRecoveryAssessed){throw 'Unexpected isolated rubral/native configuration.'}
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
 foreach($name in @('Dotnet','Python','Unity')){
  $executable=$configuration.($name+'Executable');$expected=$configuration.($name+'ExecutableSha256')
  if((File-Sha256 $executable) -ne $expected){throw ($name+' executable changed.')}
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
 $taskName='DNNE-Rubral-Native-'+[Guid]::NewGuid().ToString('N').Substring(0,12)
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
function Copy-Tree([string]$Source,[string]$Destination){
 $sourceIo=Extended-Path $Source;$destinationIo=Extended-Path $Destination
 if([IO.Directory]::Exists($destinationIo)){throw ('Preserve existing destination: '+$Destination)}
 [IO.Directory]::CreateDirectory($destinationIo) | Out-Null
 foreach($path in [IO.Directory]::EnumerateFiles($sourceIo,'*',[IO.SearchOption]::AllDirectories)){
  $target=$destinationIo+$path.Substring($sourceIo.Length)
  [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
  [IO.File]::Copy($path,$target,$false)
  if((File-Sha256 $path) -ne (File-Sha256 $target)){throw ('Copy differs: '+$path)}
 }
}
function Capture-Tree([string]$Directory,[string]$Name){
 $directoryIo=Extended-Path $Directory
 $entries=@([IO.Directory]::EnumerateFiles($directoryIo,'*',[IO.SearchOption]::AllDirectories) | Sort-Object | ForEach-Object {
  [ordered]@{Path=$Directory+$_.Substring($directoryIo.Length);Sha256=(File-Sha256 $_)}
 })
 ConvertTo-Json -InputObject @($entries) -Depth 5 | Set-Content -LiteralPath (Join-Path $package $Name) -Encoding UTF8
 return $entries
}
$status=[ordered]@{Status='RUNNING';ExitCode=$null;Revision=$configuration.Revision;LiveBrainAccessed=$false;OldSavesLoaded=$false;
 FocusedTestsPassed=$false;ModelExecutionAttempted=$false;NativePhysicsAttempted=$false;NativeDriverSucceeded=$false;
 IndependentReferencesAttempted=$false;NumericalCriteriaPassed=$false;LiveUnityIntegrated=$false;TimingMeasured=$false;
 UpstreamNumericalAccuracyQualified=$false;PhysiologyQualified=$false;AvatarRecoveryAssessed=$false;FullUnitSuitePassed=$false}
$statusPath=Join-Path $package 'window-status.json';$transcribing=$false;$testFiles=@();$probeFiles=@();$playerFiles=@();$probe=$null
if(Test-Path -LiteralPath $statusPath){throw 'Refusing to overwrite earlier results.'}
try{
 Start-Transcript -Path (Join-Path $package 'powershell.log') | Out-Null;$transcribing=$true
 $Host.UI.RawUI.WindowTitle='DNNE original rubral events to Avatar muscles and Unity joint'
 Write-Host ('Results: '+$package) -ForegroundColor Cyan
 Write-Host '281 focused checks, seven fresh neuronal/muscle/native conditions and fifteen independent references.'
 Write-Host 'No live brain or Avatar; each isolated body starts paused. Report completion to Orion.'
 $status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
 Assert-Package
 Invoke-Logged $configuration.PythonExecutable @('-B',(Join-Path $package 'source/tools/rubral_native_gate.py'),'preflight',$package) 'reference-preflight.log'
 $build=Join-Path $package 'source/artifacts/rubral-native-tests'
 Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package 'source/tests/NeuralResonanceEngine.DNNE.Tests/NeuralResonanceEngine.DNNE.Tests.csproj'),'-c','Release','--artifacts-path',$build,'--nologo','-v','minimal') 'test-build.log'
 $assembly=Join-Path $build 'bin/NeuralResonanceEngine.DNNE.Tests/release/NeuralResonanceEngine.DNNE.Tests.dll'
 $testRoot=[IO.DirectoryInfo]::new((Split-Path -Parent $assembly))
 while($null -ne $testRoot -and -not [IO.File]::Exists((Extended-Path (Join-Path $testRoot.FullName 'NeuralResonanceEngine.DNNE.slnx')))){$testRoot=$testRoot.Parent}
 if($null -eq $testRoot -or -not $testRoot.FullName.Equals((Join-Path $package 'source'),[StringComparison]::OrdinalIgnoreCase)){throw 'Tests are not confined to frozen source.'}
 $testFiles=Capture-Tree (Split-Path -Parent $assembly) 'test-binaries.json'
 $probeBuild=Join-Path $package 'source/artifacts/rubral-native-probe'
 Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package 'source/tools/NativeMotorBridgeProbe/NativeMotorBridgeProbe.csproj'),'-c','Release','--artifacts-path',$probeBuild,'--nologo','-v','minimal') 'probe-build.log'
 $probeDirectory=Join-Path $probeBuild 'bin/NativeMotorBridgeProbe/release';$probe=Join-Path $probeDirectory 'NeuralResonanceEngine.DNNE.Tests.dll'
 $probeFiles=Capture-Tree $probeDirectory 'probe-binaries.json'
 Invoke-Logged $configuration.DotnetExecutable @($probe,'--runtime-fingerprint',(Join-Path $package 'managed-runtime-before.json')) 'managed-runtime-before.log'
 if((File-Sha256 (Join-Path $package 'managed-runtime-before.json')) -ne (File-Sha256 (Join-Path $package 'reference/qualified-managed-runtime.json'))){throw 'Managed runtime differs from the reviewed circuit gate.'}
 $status.ModelExecutionAttempted=$true
 $testOutput=Join-Path $package 'test-results';New-Item -ItemType Directory -Path $testOutput | Out-Null
 Invoke-Logged $configuration.DotnetExecutable @('vstest',$assembly,('/TestCaseFilter:'+$configuration.TestFilter),('/ResultsDirectory:'+$testOutput),'/Logger:trx;LogFileName=focused.trx') 'focused-tests.log'
 Invoke-Logged $configuration.PythonExecutable @('-B',(Join-Path $package 'source/tools/rubral_native_gate.py'),'check-tests',$package) 'test-inventory.log'
 $status.FocusedTestsPassed=$true;$status['PassedFocusedTests']=281
 Write-Host 'Building a separate Unity player for the original neuronal/muscle path...' -ForegroundColor Cyan
 if(Test-Path -LiteralPath $configuration.UnityBuildWorkspace){throw 'Fresh dedicated build workspace required.'}
 $workProject=Join-Path $configuration.UnityBuildWorkspace 'UnityWorld';Copy-Tree (Join-Path $package 'source/UnityWorld') $workProject
 $playerOutput=Join-Path $package 'player';New-Item -ItemType Directory -Path $playerOutput | Out-Null
 $priorOutput=[Environment]::GetEnvironmentVariable('NRE_UNITY_OUTPUT','Process')
 try{
  [Environment]::SetEnvironmentVariable('NRE_UNITY_OUTPUT',$playerOutput,'Process')
  $arguments=@('-batchmode','-nographics','-projectPath',$workProject,'-executeMethod','Nre.UnityWorld.Editor.MuscleBridgeBuild.BuildWindows','-quit','-logFile',(Join-Path $playerOutput 'build.log'))
  $editorProcess=Start-Process -FilePath $configuration.UnityExecutable -ArgumentList (($arguments | ForEach-Object {ConvertTo-ProcessArgument $_}) -join ' ') -WorkingDirectory $workProject -WindowStyle Hidden -PassThru
  if(-not $editorProcess.WaitForExit(1800000)){$editorProcess.Kill();throw 'Isolated Unity build exceeded 30 minutes.'}
  if($editorProcess.ExitCode -ne 0){throw 'Native Unity build failed; inspect saved build.log.'}
 }finally{[Environment]::SetEnvironmentVariable('NRE_UNITY_OUTPUT',$priorOutput,'Process')}
 $buildResult=Get-Content -LiteralPath (Join-Path $playerOutput 'build-result.json') -Raw | ConvertFrom-Json
 if($buildResult.result -ne 'Succeeded' -or $buildResult.errors -ne 0){throw 'Unity did not confirm build success.'}
 $sourceUnityIo=Extended-Path (Join-Path $package 'source/UnityWorld')
 foreach($path in [IO.Directory]::EnumerateFiles($sourceUnityIo,'*.cs',[IO.SearchOption]::AllDirectories)){
  $relative=$path.Substring($sourceUnityIo.Length+1)
  if((File-Sha256 $path) -ne (File-Sha256 (Join-Path $workProject $relative))){throw ('Compiled Unity source changed: '+$relative)}
 }
 foreach($folder in @('Assets','ProjectSettings','Packages')){Copy-Tree (Join-Path $workProject $folder) (Join-Path $package ('built-project/'+$folder))}
 $playerFiles=Capture-Tree $playerOutput 'player-files.json';$status['UnityBuildSucceeded']=$true
 $status.NativePhysicsAttempted=$true
 Invoke-Logged $configuration.DotnetExecutable @($probe,'--rubral-native-body',(Join-Path $playerOutput 'DNNE Muscle Bridge.exe'),(Join-Path $package 'reference'),(Join-Path $package 'reference/circuit-gate'),(Join-Path $package 'native-bridge')) 'rubral-native.log'
 $status.NativeDriverSucceeded=$true;$status.IndependentReferencesAttempted=$true
 Invoke-Logged $configuration.PythonExecutable @('-B',(Join-Path $package 'source/tools/analyse-rubral-native-body.py'),'solve',$package) 'coupled-reference.log'
 Invoke-Logged $configuration.PythonExecutable @('-B',(Join-Path $package 'source/tools/analyse-rubral-native-body.py'),'analyse',$package) 'coupled-analysis.log'
 $analysis=Get-Content -LiteralPath (Join-Path $package 'analysis.json') -Raw | ConvertFrom-Json
 $native=Get-Content -LiteralPath (Join-Path $package 'native-source-analysis.json') -Raw | ConvertFrom-Json
 if(-not $analysis.completed -or -not $analysis.numericalCriteriaPassed -or $analysis.cases.Count -ne 7 -or $analysis.independentReferences -ne 15 -or -not $analysis.allNativeEndpointsCompared -or $analysis.physicalStepRefinementForThisInputAssessed -or -not $analysis.sampledSingleResolutionOnly -or -not $analysis.originalBoundsPreserved -or $analysis.upstreamNumericalAccuracyQualified -or $analysis.physiologyQualified -or $analysis.avatarRecoveryAssessed){throw 'Numerical evidence incomplete or scope changed.'}
 if(-not $native.completed -or -not $native.sourceBodyAccountingPassed -or -not $native.observedNeuronalMuscleMovement -or -not $native.packetAndRetryStateBitwiseEqual -or $native.actualPhysicsSteps -ne 55993 -or $native.cases.Count -ne 7 -or $native.physiologyQualified -or $native.avatarRecoveryAssessed){throw 'Original source/body accounting incomplete.'}
 $status.NumericalCriteriaPassed=$true;$status['ActualPhysicsSteps']=55993;$status.Status='DIAGNOSTICS_COMPLETE';$status.ExitCode=0
 $native.cases | Format-Table case,motorEvents,received,sourceFrames | Out-Host
}catch{
 $status.Status='FAILED';$status.ExitCode=1;$status['Error']=$_.Exception.ToString()
 Write-Host ('FAILED: '+$_.Exception.Message) -ForegroundColor Red
}finally{
 if($status.Status -eq 'RUNNING'){$status.Status='INTERRUPTED';$status.ExitCode=2;$status['Error']='Pipeline ended before completion; cause not established.'}
 try{
  Invoke-Logged $configuration.PythonExecutable @('-B',(Join-Path $package 'source/tools/rubral_native_gate.py'),'preflight',$package) 'reference-postflight.log'
  $status['ReferenceEnvironmentPreserved']=$true
 }catch{$status.Status='FAILED';$status.ExitCode=1;$status['ReferenceEnvironmentError']=$_.Exception.ToString()}
 if($probe -and (Test-Path -LiteralPath (Join-Path $package 'managed-runtime-before.json'))){
  try{
   Invoke-Logged $configuration.DotnetExecutable @($probe,'--runtime-fingerprint',(Join-Path $package 'managed-runtime-after.json')) 'managed-runtime-after.log'
   if((File-Sha256 (Join-Path $package 'managed-runtime-before.json')) -ne (File-Sha256 (Join-Path $package 'managed-runtime-after.json'))){throw 'Managed runtime dependencies changed during the study.'}
   $status['ManagedRuntimePreserved']=$true
  }catch{$status.Status='FAILED';$status.ExitCode=1;$status['ManagedRuntimeError']=$_.Exception.ToString()}
 }
 try{Assert-Package;foreach($entry in @($testFiles)+@($probeFiles)+@($playerFiles)){if((File-Sha256 $entry.Path) -ne $entry.Sha256){throw ('Built file changed: '+$entry.Path)}}}catch{$status.Status='FAILED';$status.ExitCode=1;$status['FrozenSourceError']=$_.Exception.ToString()}
 $status['CompletedUtc']=[DateTime]::UtcNow.ToString('o')
 if($transcribing){Stop-Transcript | Out-Null}
 $status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8
 try{& (Join-Path $package 'backup-run.ps1') -Source $package -Destination $configuration.BackupDestination}
 catch{$status.Status='FAILED';$status.ExitCode=1;$status['BackupError']=$_.Exception.ToString();$status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8;Write-Host ('BACKUP FAILED: '+$_.Exception.Message) -ForegroundColor Red}
 if($LaunchTaskName){Unregister-ScheduledTask -TaskName $LaunchTaskName -Confirm:$false -ErrorAction SilentlyContinue}
 Write-Host ('========== '+$status.Status+' ==========') -ForegroundColor Cyan
 Write-Host ('Results: '+$package)
 Write-Host 'Tell Orion the rubral neuronal/muscle/native outcome. Original causality, sampled numerical accuracy, physiological calibration and Avatar recovery are separate.'
}
