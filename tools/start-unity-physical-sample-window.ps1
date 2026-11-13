param([Parameter(Mandatory=$true)][string]$PackageDirectory,[switch]$InWindow,[string]$LaunchTaskName='')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
$package=(Resolve-Path -LiteralPath $PackageDirectory).Path
$configuration=Get-Content -LiteralPath (Join-Path $package 'configuration.json') -Raw | ConvertFrom-Json
$combined=$configuration.CombinedBodyContacts -eq $true
$expectedCases=if($combined){74}else{39}
if($combined -and ($configuration.ExpectedContactObservations -ne 3015 -or $configuration.ObservationProtocolVersion -ne 2)){throw 'Unexpected combined native observation scope.'}
if(-not $configuration.NoLiveMonitoring -or $configuration.ExpectedTestCases -ne $expectedCases -or $configuration.LiveBodySelected -or -not $configuration.ActualUnityPhysicsRequired -or -not $configuration.NoTimingMeasured){throw 'Unexpected Unity physical-sample configuration.'}
if($configuration.ExpectedBodySamples -ne 201 -or $configuration.ExpectedActualPhysicsSteps -ne 201 -or $configuration.ExpectedFinalCompleteStep -ne 200 -or $configuration.ExpectedUnityVersion -ne '6000.3.19f1'){throw 'Unexpected physical study scope.'}
[Guid]::ParseExact($configuration.SessionId,'N') | Out-Null
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
 $taskPrefix=if($combined){'DNNE-Unity-Body-Contacts-'}else{'DNNE-Unity-Samples-'}
 $taskName=$taskPrefix+[Guid]::NewGuid().ToString('N').Substring(0,12)
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
 foreach($entry in $configuration.PackageFiles){if((File-Sha256 (Join-Path $package $entry.Path)) -ne $entry.Sha256){throw ('Frozen source/input/dependency changed: '+$entry.Path)}}
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
function Capture-Binaries([string]$Assembly,[string]$Name){
 $directory=Split-Path -Parent $Assembly;$directoryIo=Extended-Path $directory
 $entries=@([IO.Directory]::EnumerateFiles($directoryIo,'*',[IO.SearchOption]::AllDirectories) | ForEach-Object {
  [ordered]@{Path=$directory+$_.Substring($directoryIo.Length);Sha256=File-Sha256 $_}
 })
 ConvertTo-Json -InputObject @($entries) -Depth 5 | Set-Content -LiteralPath (Join-Path $package $Name) -Encoding UTF8
 return $entries
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
 ProducerAndTransportChecksPassed=$false;NeuronalModelExecuted=$false;LiveUnityIntegrated=$false;UnityBuildSucceeded=$false;PhysicsStudyAttempted=$false;UnityPhysicsChecksPassed=$false;TimingMeasured=$false;PhysiologyQualified=$false;AvatarRecoveryAssessed=$false;FullUnitSuitePassed=$false}
$statusPath=Join-Path $package 'window-status.json';$transcribing=$false
if(Test-Path -LiteralPath $statusPath){throw 'Refusing to overwrite an earlier result.'}
try {
 Start-Transcript -Path (Join-Path $package 'powershell.log') | Out-Null;$transcribing=$true
 $Host.UI.RawUI.WindowTitle=if($combined){'DNNE actual Unity complete body/contact checks'}else{'DNNE actual Unity step and body sample checks'}
 Write-Host ('Results: '+$package) -ForegroundColor Cyan
 Write-Host ($expectedCases.ToString()+' focused checks, a frozen Unity build and 201 real physical steps. No DNNE connection; no live monitoring. Report completion to Orion.')
 $status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
 Assert-Package
 [ordered]@{CapturedUtc=[DateTime]::UtcNow.ToString('o');Cpu=@(Get-CimInstance Win32_Processor | Select-Object Name,NumberOfCores,NumberOfLogicalProcessors);OperatingSystem=Get-CimInstance Win32_OperatingSystem | Select-Object Caption,Version,TotalVisibleMemorySize,FreePhysicalMemory} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $package 'platform.json') -Encoding UTF8
 $build=Join-Path $package 'source/artifacts/unity-physical-sample-tests'
 Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package 'source/tests/NeuralResonanceEngine.DNNE.Tests/NeuralResonanceEngine.DNNE.Tests.csproj'),'-c','Release','--artifacts-path',$build,'--nologo','-v','minimal') 'test-build.log'
 $assembly=Join-Path $build 'bin/NeuralResonanceEngine.DNNE.Tests/release/NeuralResonanceEngine.DNNE.Tests.dll'
 $testRoot=[IO.DirectoryInfo]::new((Split-Path -Parent $assembly))
 while($null -ne $testRoot -and -not [IO.File]::Exists((Extended-Path (Join-Path $testRoot.FullName 'NeuralResonanceEngine.DNNE.slnx')))){$testRoot=$testRoot.Parent}
 if($null -eq $testRoot -or -not $testRoot.FullName.Equals((Join-Path $package 'source'),[StringComparison]::OrdinalIgnoreCase)){throw 'Tests are not confined to frozen source.'}
 $testOutput=Join-Path $package 'test-results';New-Item -ItemType Directory -Path $testOutput | Out-Null
 $testBinaries=Capture-Binaries $assembly 'test-binaries.json'
 Invoke-Logged $configuration.DotnetExecutable @('vstest',$assembly,('/TestCaseFilter:'+$configuration.TestFilter),('/ResultsDirectory:'+$testOutput),'/Logger:trx;LogFileName=focused.trx') 'focused-tests.log'
 [xml]$trx=Get-Content -LiteralPath (Join-Path $testOutput 'focused.trx') -Raw
 $ns=[Xml.XmlNamespaceManager]::new($trx.NameTable);$ns.AddNamespace('t','http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
 $results=@($trx.SelectNodes('//t:UnitTestResult',$ns))
 if($results.Count -ne $configuration.ExpectedTestCases -or @($results | Where-Object {$_.outcome -ne 'Passed'}).Count){throw 'Focused tests failed or coverage was incomplete.'}
 $requiredCounts=@{PhysicalSampleProducerTests=16;PhysicalSampleTransportTests=22;SolutionManifestTests=1}
 if($combined){$requiredCounts.PhysicalObservationTests=31;$requiredCounts.UnityBodyEncodingTests=4}
 foreach($class in $requiredCounts.Keys){
  if($configuration.ExpectedClassCounts.$class -ne $requiredCounts[$class]){throw ('Unexpected class count: '+$class)}
  $actual=@($results | Where-Object {$_.testName -like ('NeuralResonanceEngine.DNNE.Tests.'+$class+'.*')}).Count
  if($actual -ne $requiredCounts[$class]){throw ('Incorrect test coverage: '+$class)}
 }
 if(@($results.testName | Select-Object -Unique).Count -ne $results.Count){throw 'Repeated test names cannot replace missing checks.'}
 $status['PassedFocusedTests']=$results.Count
 Assert-Package
 foreach($entry in @($testBinaries)){if((File-Sha256 $entry.Path) -ne $entry.Sha256){throw ('Binary changed: '+$entry.Path)}}
 $status.ProducerAndTransportChecksPassed=$true
 Write-Host 'Building the isolated Unity bridge in a separate writable project...' -ForegroundColor Cyan
 if((File-Sha256 $configuration.UnityExecutable) -ne $configuration.UnityExecutableSha256){throw 'Unity editor executable changed after preparation.'}
 if(Test-Path -LiteralPath $configuration.UnityBuildWorkspace){throw 'Preserve existing Unity build workspace; use a fresh package.'}
 $workProject=Join-Path $configuration.UnityBuildWorkspace 'UnityWorld'
 Copy-Tree (Join-Path $package 'source/UnityWorld') $workProject
 $playerOutput=Join-Path $package 'player';New-Item -ItemType Directory -Path $playerOutput | Out-Null
 $priorOutput=[Environment]::GetEnvironmentVariable('NRE_UNITY_OUTPUT','Process')
 try {
  [Environment]::SetEnvironmentVariable('NRE_UNITY_OUTPUT',$playerOutput,'Process')
  $unityArguments=@('-batchmode','-nographics','-projectPath',$workProject,'-executeMethod','Nre.UnityWorld.Editor.WorldBuild.BuildWindows','-quit','-logFile',(Join-Path $playerOutput 'build.log'))
  $editorProcess=Start-Process -FilePath $configuration.UnityExecutable -ArgumentList (($unityArguments | ForEach-Object {ConvertTo-ProcessArgument $_}) -join ' ') -WorkingDirectory $workProject -WindowStyle Hidden -PassThru
  if(-not $editorProcess.WaitForExit(1800000)){$editorProcess.Kill();throw 'Dedicated Unity build editor exceeded its 30-minute build allowance.'}
  if($editorProcess.ExitCode -ne 0){throw 'Unity editor build failed; preserve its build.log.'}
 } finally {[Environment]::SetEnvironmentVariable('NRE_UNITY_OUTPUT',$priorOutput,'Process')}
 $buildResult=Get-Content -LiteralPath (Join-Path $playerOutput 'build-result.json') -Raw | ConvertFrom-Json
 if($buildResult.result -ne 'Succeeded' -or $buildResult.errors -ne 0){throw 'Unity did not confirm a successful build.'}
 # Unity can rewrite imported assets/settings. Keep its build workspace separate
 # from frozen inputs, verify every compiled source, and retain generated inputs.
 $sourceUnity=Join-Path $package 'source/UnityWorld';$sourceUnityIo=Extended-Path $sourceUnity
 foreach($path in [IO.Directory]::EnumerateFiles($sourceUnityIo,'*.cs',[IO.SearchOption]::AllDirectories)){
  $relative=$path.Substring($sourceUnityIo.Length+1)
  if((File-Sha256 $path) -ne (File-Sha256 (Join-Path $workProject $relative))){throw ('Compiled Unity source changed: '+$relative)}
 }
 foreach($folder in @('Assets','ProjectSettings','Packages')){Copy-Tree (Join-Path $workProject $folder) (Join-Path $package ('built-project/'+$folder))}
 $playerFiles=Capture-Tree $playerOutput 'player-files.json'
 $status.UnityBuildSucceeded=$true
 Assert-Package
 Write-Host 'Building the actual-body adapter and probe...' -ForegroundColor Cyan
 $probeBuild=Join-Path $package 'source/artifacts/unity-physical-probe'
 Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package 'source/tools/UnityPhysicalSampleProbe/UnityPhysicalSampleProbe.csproj'),'-c','Release','--artifacts-path',$probeBuild,'--nologo','-v','minimal') 'probe-build.log'
 $probe=Join-Path $probeBuild 'bin/UnityPhysicalSampleProbe/release/UnityPhysicalSampleProbe.dll'
 $probeBinaries=Capture-Binaries $probe 'probe-binaries.json'
 $study=Join-Path $package 'study'
 Write-Host 'Running actual physical steps, bounded sample delivery, pauses and an explicit capture-failure intervention...' -ForegroundColor Cyan
 $status.PhysicsStudyAttempted=$true
 $studyArguments=@($probe)
 if($combined){$studyArguments+='--combined'}
 $studyArguments+=@((Join-Path $playerOutput 'DNNE Unity World.exe'),$study,$configuration.SessionId)
 Invoke-Logged $configuration.DotnetExecutable $studyArguments 'study.log'
 $result=Get-Content -LiteralPath (Join-Path $study 'study-result.json') -Raw | ConvertFrom-Json
 $analysis=Get-Content -LiteralPath (Join-Path $study 'saved-analysis.json') -Raw | ConvertFrom-Json
 if(-not $result.completed -or $result.exitCode -ne 0 -or
    $result.unity -ne $configuration.ExpectedUnityVersion -or $result.actualPhysicsSteps -ne 201 -or
    $result.capturedAndConsumedSamples -ne 201 -or $result.completeThroughStep -ne 200 -or
    -not $result.postMovementCaptureFailureStopped -or -not $result.directAndWrongOwnerStepsRejected -or
    -not $analysis.completed -or -not $analysis.originalTypedObservationsExactlyPreserved -or
    $analysis.originalGridSamples -ne 202 -or $analysis.deliveredBodySamples -ne 201 -or
    $analysis.completeThroughStep -ne 200){throw 'Actual Unity physical sample criteria failed.'}
 if($combined){
  if(-not $result.completeBodyAndContactDeliveryChecksPassed -or $result.deliveredContactObservations -ne 3015 -or
     -not $result.bodyProfilePreserved -or -not $result.missingContactInjectionStoppedCompleteness -or
     -not $analysis.bodyProfilePreserved -or -not $analysis.completeBodyAndContactsTransported -or
     $analysis.deliveredContactObservations -ne 3015 -or $analysis.heldBodyAndContactInspections -ne 4 -or
     -not $analysis.missingContactInjectionRejected -or $analysis.nativeSourceActuallyLostContact){throw 'Complete native body/contact criteria failed.'}
  $status['CompleteBodyContactChecksPassed']=$true;$status['DeliveredContactObservations']=3015
 } elseif(-not $result.stepAndBodyDeliveryChecksPassed -or $analysis.heldBodyInspections -ne 4){throw 'Body-only native criteria failed.'}
 foreach($entry in @($testBinaries)+@($probeBinaries)+@($playerFiles)){
  if((File-Sha256 $entry.Path) -ne $entry.Sha256){throw ('Executed binary/player input changed: '+$entry.Path)}
 }
 Assert-Package
 $status.UnityPhysicsChecksPassed=$true;$status['ActualPhysicsSteps']=201;$status['DeliveredBodySamples']=201
 $status['BodySampleCompleteThroughStep']=200;$status.Status='DIAGNOSTICS_COMPLETE';$status.ExitCode=0
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
 if($combined){Write-Host 'Tell Orion the actual Unity body/contact outcome. Complete endpoint delivery, afferent physiology and Avatar recovery are separate.'}
 else {Write-Host 'Tell Orion the actual Unity sample outcome. Physical step/capture integration, live neural coupling, afferent physiology and Avatar recovery are separate.'}
 Write-Host ('Results: '+$package)
}
