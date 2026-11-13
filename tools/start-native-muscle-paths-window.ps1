param([Parameter(Mandatory=$true)][string]$PackageDirectory,[switch]$InWindow,[string]$LaunchTaskName='')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
$package=(Resolve-Path -LiteralPath $PackageDirectory).Path
$configuration=Get-Content -LiteralPath (Join-Path $package 'configuration.json') -Raw | ConvertFrom-Json
if(-not $configuration.NoLiveMonitoring -or $configuration.ExpectedTestCases -ne 75 -or -not $configuration.IsolatedNativeMusclePaths -or -not $configuration.GeneratedUnitySource -or -not $configuration.QualifiedCouplingCorePreserved -or $configuration.LiveBodySelected -or -not $configuration.NoTimingMeasured -or $configuration.NativeUnityQualified -or $configuration.PhysiologicalCalibrationQualified -or $configuration.AvatarRecoveryAssessed){throw 'Unexpected isolated muscle geometry configuration.'}
function Extended-Path([string]$Path){
 if($Path.StartsWith('\\?\')){return $Path}
 if($Path.StartsWith('\\')){return '\\?\UNC\'+$Path.Substring(2)}
 return '\\?\'+[IO.Path]::GetFullPath($Path)
}
function File-Sha256([string]$Path){
 $stream=[IO.File]::OpenRead((Extended-Path $Path));$algorithm=[Security.Cryptography.SHA256]::Create()
 try {return [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-','')}
 finally {$algorithm.Dispose();$stream.Dispose()}
}
if(-not $InWindow){
 if(Test-Path -LiteralPath (Join-Path $package 'launch.json')){throw 'Already launched; use a fresh package.'}
 $manifestPath=Join-Path $package 'backup-manifest.json'
 $manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
 if($manifest.Destination -ne $configuration.BackupDestination){throw 'Initial backup destination differs.'}
 if((File-Sha256 $manifestPath) -ne (File-Sha256 (Join-Path $configuration.BackupDestination 'backup-manifest.json'))){throw 'Initial backup manifests differ.'}
 foreach($entry in $manifest.Files){foreach($directory in @($package,$configuration.BackupDestination)){
  if((File-Sha256 (Join-Path $directory $entry.Path)) -ne $entry.Sha256){throw ('Initial backup or source differs: '+$entry.Path)}
 }}
 $taskName='DNNE-Native-Muscle-Paths-'+[Guid]::NewGuid().ToString('N').Substring(0,12)
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
function Assert-Package {
 foreach($entry in $configuration.PackageFiles){if((File-Sha256 (Join-Path $package $entry.Path)) -ne $entry.Sha256){throw ('Frozen source/input changed: '+$entry.Path)}}
}
function Invoke-Logged([string]$Executable,[string[]]$NativeArguments,[string]$LogName){
 $writer=[IO.StreamWriter]::new((Join-Path $package $LogName),$false,[Text.UTF8Encoding]::new($false));$writer.AutoFlush=$true
 $previous=$ErrorActionPreference
 try {
  $ErrorActionPreference='Continue'
  & $Executable @NativeArguments 2>&1 | ForEach-Object {$line=[string]$_;$writer.WriteLine($line);Write-Host $line} -ErrorAction Stop
  $nativeExit=$LASTEXITCODE
 } finally {$writer.Dispose();$ErrorActionPreference=$previous}
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
  [ordered]@{Path=$Directory+$_.Substring($directoryIo.Length);Sha256=File-Sha256 $_}
 })
 ConvertTo-Json -InputObject @($entries) -Depth 5 | Set-Content -LiteralPath (Join-Path $package $Name) -Encoding UTF8
 return $entries
}
$status=[ordered]@{Status='RUNNING';ExitCode=$null;Revision=$configuration.Revision;LiveBrainAccessed=$false;OldSavesLoaded=$false;
 MusclePathAndRegressionChecksPassed=$false;ModelExecutionAttempted=$false;IncludesFixtureMotorEvents=$true;
 NeuronalModelExecuted=$false;LiveUnityIntegrated=$false;TimingMeasured=$false;NativePointForceChecksPassed=$false;
 PhysiologyQualified=$false;AvatarRecoveryAssessed=$false;FullUnitSuitePassed=$false}
$statusPath=Join-Path $package 'window-status.json';$transcribing=$false
if(Test-Path -LiteralPath $statusPath){throw 'Refusing to overwrite an earlier result.'}
try {
 Start-Transcript -Path (Join-Path $package 'powershell.log') | Out-Null;$transcribing=$true
 $Host.UI.RawUI.WindowTitle='DNNE native muscle point-force checks'
 Write-Host ('Results: '+$package) -ForegroundColor Cyan
 Write-Host '75 geometry/motor regressions, then 24 isolated native Unity point-force cases. No DNNE or live Avatar. Report completion to Orion.'
 $status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
 Assert-Package
 $build=Join-Path $package 'source/artifacts/muscle-path-tests'
 Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package 'source/tests/NeuralResonanceEngine.DNNE.Tests/NeuralResonanceEngine.DNNE.Tests.csproj'),'-c','Release','--artifacts-path',$build,'--nologo','-v','minimal') 'test-build.log'
 $assembly=Join-Path $build 'bin/NeuralResonanceEngine.DNNE.Tests/release/NeuralResonanceEngine.DNNE.Tests.dll'
 $testRoot=[IO.DirectoryInfo]::new((Split-Path -Parent $assembly))
 while($null -ne $testRoot -and -not [IO.File]::Exists((Extended-Path (Join-Path $testRoot.FullName 'NeuralResonanceEngine.DNNE.slnx')))){$testRoot=$testRoot.Parent}
 if($null -eq $testRoot -or -not $testRoot.FullName.Equals((Join-Path $package 'source'),[StringComparison]::OrdinalIgnoreCase)){throw 'Tests are not confined to frozen source.'}
 $testOutput=Join-Path $package 'test-results';New-Item -ItemType Directory -Path $testOutput | Out-Null
 $binaryDirectory=Split-Path -Parent $assembly;$binaryIo=Extended-Path $binaryDirectory
 $binaries=@([IO.Directory]::EnumerateFiles($binaryIo,'*',[IO.SearchOption]::AllDirectories) | ForEach-Object {
  [ordered]@{Path=$binaryDirectory+$_.Substring($binaryIo.Length);Sha256=File-Sha256 $_}
 })
 ConvertTo-Json -InputObject @($binaries) -Depth 5 | Set-Content -LiteralPath (Join-Path $package 'test-binaries.json') -Encoding UTF8
 $status.ModelExecutionAttempted=$true
 Invoke-Logged $configuration.DotnetExecutable @('vstest',$assembly,('/TestCaseFilter:'+$configuration.TestFilter),('/ResultsDirectory:'+$testOutput),'/Logger:trx;LogFileName=focused.trx') 'focused-tests.log'
 [xml]$trx=Get-Content -LiteralPath (Join-Path $testOutput 'focused.trx') -Raw
 $ns=[Xml.XmlNamespaceManager]::new($trx.NameTable);$ns.AddNamespace('t','http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
 $results=@($trx.SelectNodes('//t:UnitTestResult',$ns))
 if($results.Count -ne $configuration.ExpectedTestCases -or @($results | Where-Object {$_.outcome -ne 'Passed'}).Count){throw 'Focused tests failed or coverage was incomplete.'}
 $requiredCounts=@{MusclePathGeometryTests=31;CausalMotorPlantTests=27;DynamicMuscleTests=16;SolutionManifestTests=1}
 foreach($class in $requiredCounts.Keys){
  if($configuration.ExpectedClassCounts.$class -ne $requiredCounts[$class]){throw ('Unexpected class count: '+$class)}
  $actual=@($results | Where-Object {$_.testName -like ('NeuralResonanceEngine.DNNE.Tests.'+$class+'.*')}).Count
  if($actual -ne $requiredCounts[$class]){throw ('Incorrect test coverage: '+$class)}
 }
 if(@($results.testName | Select-Object -Unique).Count -ne $results.Count){throw 'Repeated test names cannot replace missing checks.'}
 $definitions=@{};foreach($definition in @($trx.SelectNodes('//t:TestDefinitions/t:UnitTest',$ns))){
  $method=$definition.SelectSingleNode('t:TestMethod',$ns)
  $definitions[[string]$definition.id]=([string]$method.className+'.'+[string]$method.name)
 }
 $actualMethods=@{};foreach($result in $results){
  $method=$definitions[[string]$result.testId];if(-not $method){throw 'Test result lacks its method definition.'}
  $actualMethods[$method]++
 }
 $expectedMethods=@($configuration.ExpectedTestMethods.PSObject.Properties)
 if($actualMethods.Count -ne $expectedMethods.Count){throw 'Method coverage differs from frozen source.'}
 foreach($entry in $expectedMethods){if($actualMethods[$entry.Name] -ne $entry.Value){throw ('Missing or repeated method cases: '+$entry.Name)}}
 Assert-Package
 foreach($entry in $binaries){if((File-Sha256 $entry.Path) -ne $entry.Sha256){throw ('Test binary changed: '+$entry.Path)}}
 $status['PassedFocusedTests']=$results.Count;$status.MusclePathAndRegressionChecksPassed=$true
 if($configuration.ExpectedNativeCases -ne 24 -or $configuration.ExpectedNativePhysicsSteps -ne 48){throw 'Unexpected native protocol.'}
 Write-Host 'Building a separate Unity player for native muscle point-force checks...' -ForegroundColor Cyan
 if((File-Sha256 $configuration.UnityExecutable) -ne $configuration.UnityExecutableSha256){throw 'Unity editor changed after preparation.'}
 if(Test-Path -LiteralPath $configuration.UnityBuildWorkspace){throw 'Fresh dedicated build workspace required.'}
 $workProject=Join-Path $configuration.UnityBuildWorkspace 'UnityWorld'
 Copy-Tree (Join-Path $package 'source/UnityWorld') $workProject
 $playerOutput=Join-Path $package 'player';New-Item -ItemType Directory -Path $playerOutput | Out-Null
 $priorOutput=[Environment]::GetEnvironmentVariable('NRE_UNITY_OUTPUT','Process')
 try {
  [Environment]::SetEnvironmentVariable('NRE_UNITY_OUTPUT',$playerOutput,'Process')
  $arguments=@('-batchmode','-nographics','-projectPath',$workProject,'-executeMethod','Nre.UnityWorld.Editor.MusclePathBuild.BuildWindows','-quit','-logFile',(Join-Path $playerOutput 'build.log'))
  $editorProcess=Start-Process -FilePath $configuration.UnityExecutable -ArgumentList (($arguments | ForEach-Object {ConvertTo-ProcessArgument $_}) -join ' ') -WorkingDirectory $workProject -WindowStyle Hidden -PassThru
  if(-not $editorProcess.WaitForExit(1800000)){$editorProcess.Kill();throw 'Isolated build exceeded 30 minutes.'}
  if($editorProcess.ExitCode -ne 0){throw 'Native Unity build failed; inspect saved build.log.'}
 } finally {[Environment]::SetEnvironmentVariable('NRE_UNITY_OUTPUT',$priorOutput,'Process')}
 $buildResult=Get-Content -LiteralPath (Join-Path $playerOutput 'build-result.json') -Raw | ConvertFrom-Json
 if($buildResult.result -ne 'Succeeded' -or $buildResult.errors -ne 0){throw 'Unity did not confirm build success.'}
 $sourceUnity=Join-Path $package 'source/UnityWorld';$sourceUnityIo=Extended-Path $sourceUnity
 foreach($path in [IO.Directory]::EnumerateFiles($sourceUnityIo,'*.cs',[IO.SearchOption]::AllDirectories)){
  $relative=$path.Substring($sourceUnityIo.Length+1)
  if((File-Sha256 $path) -ne (File-Sha256 (Join-Path $workProject $relative))){throw ('Compiled Unity source changed: '+$relative)}
 }
 foreach($folder in @('Assets','ProjectSettings','Packages')){Copy-Tree (Join-Path $workProject $folder) (Join-Path $package ('built-project/'+$folder))}
 $playerFiles=Capture-Tree $playerOutput 'player-files.json'
 $status['UnityBuildSucceeded']=$true
 $nativeOutput=Join-Path $package 'native'
 $arguments=@('-batchmode','-nographics','-logFile',(Join-Path $package 'native-player.log'),'--path-output',$nativeOutput,'--path-session',$configuration.SessionId)
 $status['NativePhysicsAttempted']=$true
 Write-Host 'Running 24 native point-force cases and preserving all body states...' -ForegroundColor Cyan
 $playerProcess=Start-Process -FilePath (Join-Path $playerOutput 'DNNE Muscle Paths.exe') -ArgumentList (($arguments | ForEach-Object {ConvertTo-ProcessArgument $_}) -join ' ') -WorkingDirectory $playerOutput -WindowStyle Hidden -PassThru
 if(-not $playerProcess.WaitForExit(300000)){$playerProcess.Kill();throw 'Native fixture exceeded five minutes.'}
 $status['NativeExitCode']=$playerProcess.ExitCode
 if($playerProcess.ExitCode -ne 0){throw 'Native point-force criteria failed; preserve results and original bounds.'}
 $native=Get-Content -LiteralPath (Join-Path $nativeOutput 'results.json') -Raw | ConvertFrom-Json
 if(-not $native.completed -or -not $native.passed -or -not $native.isolated -or $native.neuronalModelExecuted -or $native.avatarLoaded -or $native.anatomicalCalibrationQualified){throw 'Native scope or completion differs.'}
 if($native.unity -ne $configuration.ExpectedUnityVersion -or $native.sessionId -ne $configuration.SessionId -or $native.actualPhysicsSteps -ne 48 -or @($native.cases).Count -ne 24){throw 'Native identity or coverage differs.'}
 if($native.positionBound -ne 2e-6 -or $native.velocityBound -ne 2e-6 -or $native.forceRoundingBound -ne 3e-6 -or $native.powerBound -ne 2e-5){throw 'Native prospective bounds changed.'}
 $index=0
 foreach($bent in @($false,$true)){foreach($rotated in @($false,$true)){foreach($step in @([single].01,[single].001,[single].000125)){foreach($tension in @(0,10)){
  $row=$native.cases[$index]
  if($row.key -ne ('case-'+$index.ToString('D2')) -or -not $row.completed -or -not $row.passed -or $row.error -or $row.bent -ne $bent -or $row.rotated -ne $rotated -or $row.tensionNewtons -ne $tension -or $row.stepSeconds -ne [double]$step -or $row.startStep -ne 2*$index -or $row.endStep -ne 2*($index+1)){throw 'Native case sequence or parameter matrix differs.'}
  foreach($field in @('maximumPositionError','maximumPointVelocityError','maximumLinearVelocityError','maximumAngularVelocityError','maximumClearedForceVelocityError')){
   $value=[double]$row.$field
   if([double]::IsNaN($value) -or [double]::IsInfinity($value) -or $value -lt 0 -or $value -gt 2e-6){throw ('Native numerical bound failed: '+$field)}
  }
  foreach($pair in @(@('maximumForceRoundingError',3e-6),@('nativePowerError',2e-5),@('powerResidualWatts',1e-11))){
   $value=[double]$row.($pair[0]);if([double]::IsNaN($value) -or [double]::IsInfinity($value) -or [Math]::Abs($value) -gt $pair[1]){throw ('Native force/power bound failed: '+$pair[0])}
  }
  $bodyCount=if($bent){3}else{2}
  foreach($capture in @('before','afterForce','afterClear')){if(@($row.$capture).Count -ne $bodyCount){throw 'Native body capture incomplete.'}}
  foreach($suffix in @('-initial.json','-forced.json','.json')){if(-not (Test-Path -LiteralPath (Join-Path $nativeOutput ($row.key+$suffix)))){throw 'Native stage evidence missing.'}}
  $index++
 }}}}
 Assert-Package
 foreach($entry in $playerFiles){if((File-Sha256 $entry.Path) -ne $entry.Sha256){throw ('Built player changed: '+$entry.Path)}}
 $status.NativePointForceChecksPassed=$true;$status['PassedNativeCases']=24;$status['ActualPhysicsSteps']=48
 $status.Status='DIAGNOSTICS_COMPLETE';$status.ExitCode=0
} catch {
 $status.Status='FAILED';$status.ExitCode=1;$status['Error']=$_.Exception.ToString()
 Write-Host ('FAILED: '+$_.Exception.Message) -ForegroundColor Red
} finally {
 if($status.Status -eq 'RUNNING'){$status.Status='INTERRUPTED';$status.ExitCode=2;$status['Error']='Pipeline ended before completing checks; cause not established.'}
 $status['CompletedUtc']=[DateTime]::UtcNow.ToString('o')
 if($transcribing){Stop-Transcript | Out-Null}
 $status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8
 try {& (Join-Path $package 'backup-run.ps1') -Source $package -Destination $configuration.BackupDestination}
 catch {$status.Status='FAILED';$status.ExitCode=1;$status['BackupError']=$_.Exception.ToString();$status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8;Write-Host ('BACKUP FAILED: '+$_.Exception.Message) -ForegroundColor Red}
 if($LaunchTaskName){Unregister-ScheduledTask -TaskName $LaunchTaskName -Confirm:$false -ErrorAction SilentlyContinue}
 Write-Host ('========== '+$status.Status+' ==========') -ForegroundColor Cyan
 Write-Host ('Results: '+$package)
 Write-Host 'Tell Orion the native muscle point-force outcome. Free-body force application, constrained joints, anatomy and Avatar recovery are separate.'
}
