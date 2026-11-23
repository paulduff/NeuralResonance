param([Parameter(Mandatory=$true)][string]$PackageDirectory,[switch]$InWindow,[string]$LaunchTaskName='')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
$package=(Resolve-Path -LiteralPath $PackageDirectory).Path
$configuration=Get-Content -LiteralPath (Join-Path $package 'configuration.json') -Raw | ConvertFrom-Json
$closedLoop=($configuration.PSObject.Properties.Name -contains 'ClosedLoopMotorScheduleRegression') -and $configuration.ClosedLoopMotorScheduleRegression
$articulated=($configuration.PSObject.Properties.Name -contains 'ArticulatedTendonBridgeRegression') -and $configuration.ArticulatedTendonBridgeRegression
$attachment=($configuration.PSObject.Properties.Name -contains 'PassiveMotorAttachmentRegression') -and $configuration.PassiveMotorAttachmentRegression
$head=($configuration.PSObject.Properties.Name -contains 'ArticulatedHeadInputRegression') -and $configuration.ArticulatedHeadInputRegression
$initialBalance=($configuration.PSObject.Properties.Name -contains 'MuscleInitialBalanceRegression') -and $configuration.MuscleInitialBalanceRegression
$frameMap=($configuration.PSObject.Properties.Name -contains 'MuscleFrameMapRegression') -and $configuration.MuscleFrameMapRegression
$namedMuscles=($configuration.PSObject.Properties.Name -contains 'NamedMuscleRegression') -and $configuration.NamedMuscleRegression
if($namedMuscles -and (-not $head -or $initialBalance -or $frameMap -or -not $configuration.NativeNamedMuscleStudy)){throw 'Named muscle study requires its full-body regression and native protocol.'}
if($frameMap -and -not $initialBalance){throw 'Anatomical mapping retains equilibrium controls.'}
if($initialBalance -and (-not $closedLoop -or $articulated -or $attachment -or $head)){throw 'Initial muscle balance retains the declared motor/plant regressions.'}
if($head -and -not $attachment){throw 'Head input retains initial physical sampling controls.'}
$expectedCases=if($namedMuscles){359}elseif($frameMap){288}elseif($initialBalance){263}elseif($head){331}elseif($attachment){315}elseif($articulated){295}elseif($closedLoop){243}else{155}
if($attachment -and -not $articulated){throw 'Motor initialization retains the full-body host controls.'}
if($articulated -and -not $closedLoop){throw 'Full-body host controls retain the split-schedule regressions.'}
if(-not $configuration.NoLiveMonitoring -or -not $configuration.FreshMotorEpochRegression -or $configuration.ExpectedTestCases -ne $expectedCases){throw 'Unexpected motor-clock protocol.'}
function Sha([string]$Path){(Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash}
function Assert-Package {
 foreach($entry in $configuration.PackageFiles){if((Sha (Join-Path $package $entry.Path)) -ne $entry.Sha256){throw ('Frozen input changed: '+$entry.Path)}}
 foreach($name in @('Dotnet','Python')){if((Sha $configuration.($name+'Executable')) -ne $configuration.($name+'ExecutableSha256')){throw ($name+' executable changed.')}}
 if($namedMuscles -and (Sha $configuration.UnityExecutable) -ne $configuration.UnityExecutableSha256){throw 'Unity executable changed.'}
}
if(-not $InWindow){
 if(Test-Path -LiteralPath (Join-Path $package 'launch.json')){throw 'Already launched; use a fresh package.'}
 $manifest=Get-Content -LiteralPath (Join-Path $package 'backup-manifest.json') -Raw | ConvertFrom-Json
 if($manifest.Destination -ne $configuration.BackupDestination -or (Sha (Join-Path $package 'backup-manifest.json')) -ne (Sha (Join-Path $configuration.BackupDestination 'backup-manifest.json'))){throw 'Initial backup identities differ.'}
 foreach($entry in $manifest.Files){foreach($directory in @($package,$configuration.BackupDestination)){
  if((Sha (Join-Path $directory $entry.Path)) -ne $entry.Sha256){throw ('Initial source/backup differs: '+$entry.Path)}
 }}
 Assert-Package
 $taskPrefix=if($namedMuscles){'DNNE-Named-Muscle-Native-'}elseif($frameMap){'DNNE-Muscle-Frames-'}elseif($initialBalance){'DNNE-Muscle-Initial-'}elseif($head){'DNNE-Articulated-Head-'}elseif($attachment){'DNNE-Motor-Attachment-'}elseif($articulated){'DNNE-Tendon-Wire-'}elseif($closedLoop){'DNNE-Motor-Closure-'}else{'DNNE-Motor-Epoch-'}
 $taskName=$taskPrefix+[Guid]::NewGuid().ToString('N').Substring(0,12)
 $shell=Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/powershell.exe'
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
  & $Executable @NativeArguments 2>&1 | ForEach-Object {$line=if($_ -is [System.Management.Automation.ErrorRecord]){[string]$_.Exception.Message}else{[string]$_};$writer.WriteLine($line);Write-Host $line} -ErrorAction Stop
  $nativeExit=$LASTEXITCODE
 }finally{$writer.Dispose();$ErrorActionPreference=$previous}
 if($nativeExit -ne 0){throw ($LogName+' exited '+$nativeExit)}
}
function Capture-Binaries([string]$Assembly,[string]$Name){
 $files=@(Get-ChildItem -LiteralPath (Split-Path $Assembly -Parent) -File -Recurse | Sort-Object FullName)
 $entries=[Collections.Generic.List[object]]::new();$index=0
 foreach($file in $files){
  $entries.Add([ordered]@{Path=$file.FullName;Sha256=Sha $file.FullName});$index++
  if($index%50 -eq 0 -or $index -eq $files.Count){Write-Host ('Binary files hashed: '+$index+' / '+$files.Count+' ('+$Name+')')}
 }
 ConvertTo-Json -InputObject $entries.ToArray() -Depth 5 | Set-Content -LiteralPath (Join-Path $package $Name) -Encoding UTF8
 return $entries.ToArray()
}
function Stage([string]$stage){Invoke-Logged $configuration.PythonExecutable @('-I','-B',(Join-Path $package 'source/tools/fresh_motor_epoch_study.py'),$stage,$package) ($stage+'.log')}
function Copy-Tree([string]$Source,[string]$Destination){
 if(Test-Path -LiteralPath $Destination){throw ('Copy destination exists: '+$Destination)}
 [IO.Directory]::CreateDirectory($Destination) | Out-Null
 foreach($path in [IO.Directory]::EnumerateFiles($Source,'*',[IO.SearchOption]::AllDirectories)){
  $target=Join-Path $Destination ($path.Substring($Source.Length).TrimStart('\','/'))
  [IO.Directory]::CreateDirectory((Split-Path $target -Parent)) | Out-Null
  [IO.File]::Copy($path,$target,$false)
  if((Sha $path) -ne (Sha $target)){throw ('Copy differs: '+$path)}
 }
}
$status=[ordered]@{Status='RUNNING';ExitCode=$null;Revision=$configuration.Revision;NoLiveMonitoring=$true;TestExecutionAttempted=$false;ClockChecksPassed=$false;NativeUnityExecuted=$false;LiveBrainAccessed=$false;OldSavesLoaded=$false;IndependentCoupledTrajectoryQualified=$false;PhysiologicalFiringQualified=$false;AvatarBalanceQualified=$false}
$statusPath=Join-Path $package 'window-status.json';$transcribing=$false;$testBinaries=@();$probeBinaries=@();$playerFiles=@();$probe=$null
if(Test-Path -LiteralPath $statusPath){throw 'Refusing to overwrite an earlier run.'}
try {
 Start-Transcript -Path (Join-Path $package 'powershell.log') | Out-Null;$transcribing=$true
 $Host.UI.RawUI.WindowTitle=if($frameMap){'DNNE lower leg attachment frames - 288 cases and 44 retained points'}elseif($initialBalance){'DNNE fresh muscle equilibrium - 263 cases and independent reference'}elseif($head){'DNNE original full Avatar head input - 331 component cases'}elseif($attachment){'DNNE passive motor attachment - 315 component/regression cases'}elseif($articulated){'DNNE full Avatar bridge - 295 component/regression cases'}elseif($closedLoop){'DNNE body and motor closure - 243 component/regression cases'}else{'DNNE fresh motor clock - 155 component/regression cases'}
 Write-Host ('Results: '+$package) -ForegroundColor Cyan
 if($namedMuscles){$Host.UI.RawUI.WindowTitle='DNNE named muscle paths - 359 checks then 12 actual Unity cases';Write-Host '28 named-path controls plus 331 previous regressions, then a separate Unity build and 12 isolated native cases using ten mapped lower-leg muscles. Known test tensions; no live brain or neuronal authority claim.'}
 elseif($frameMap){Write-Host '25 mapping controls and 263 previous regressions; ten named muscle paths and 44 source points independently checked. Foot scale is a declared shin-ratio proxy, not measured foot anatomy. No Unity or live brain.'}
 elseif($initialBalance){Write-Host '20 fresh muscle controls and 243 existing regressions, then eight independent 80/110-digit equilibria. No Unity or live brain. This window stays open after backup.'}
 elseif($head){Write-Host '16 original head-input controls and 315 previous regressions. Prescribed protocol peer; no Unity or afferent firing.'}
 elseif($attachment){Write-Host '20 new passive-attachment controls and 295 previous regressions. Prescribed protocol peer; no Unity or live brain. This window stays open after its final backup.'}
 elseif($articulated){Write-Host '34 new host bridge controls and 261 previous regressions. Prescribed protocol peer; no Unity or live brain. This window stays open after its final backup.'}
 elseif($closedLoop){Write-Host '14 new split-schedule controls and 229 existing regressions. No Unity or live brain. This window stays open after its final backup.'}
 else{Write-Host '13 new clock controls and 142 existing regressions. No Unity or live brain. This window stays open after its final backup.'}
 $status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
 Assert-Package;Stage 'preflight'
 Invoke-Logged $configuration.DotnetExecutable @('--info') 'dotnet-info.log'
 $build=Join-Path $package 'source/artifacts/motor-epoch-tests'
 Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package 'source/tests/NeuralResonanceEngine.DNNE.Tests/NeuralResonanceEngine.DNNE.Tests.csproj'),'-c','Release','--artifacts-path',$build,'--nologo','-v','minimal') 'test-build.log'
 $assembly=Join-Path $build 'bin/NeuralResonanceEngine.DNNE.Tests/release/NeuralResonanceEngine.DNNE.Tests.dll'
 $testBinaries=Capture-Binaries $assembly 'test-binaries.json'
 $probeBuild=Join-Path $package 'source/artifacts/motor-epoch-runtime'
 Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package 'source/tools/NativeMotorBridgeProbe/NativeMotorBridgeProbe.csproj'),'-c','Release','--artifacts-path',$probeBuild,'--nologo','-v','minimal') 'probe-build.log'
 $probe=Join-Path $probeBuild 'bin/NativeMotorBridgeProbe/release/NeuralResonanceEngine.DNNE.Tests.dll'
 $probeBinaries=Capture-Binaries $probe 'probe-binaries.json'
 Invoke-Logged $configuration.DotnetExecutable @($probe,'--runtime-fingerprint',(Join-Path $package 'managed-runtime-before.json')) 'managed-runtime-before.log'
 Stage 'runtime'
 $testOutput=Join-Path $package 'test-results';[IO.Directory]::CreateDirectory($testOutput) | Out-Null
 $env:NRE_SYNAPSE_STATE_DIR=Join-Path $package 'isolated-synapses'
 $env:NRE_RETICULOSPINAL_EVIDENCE_DIR=Join-Path $package 'partition-evidence'
 $env:NRE_DESCENDING_ANATOMY_EVIDENCE_DIR=Join-Path $package 'anatomy-evidence'
 if($frameMap){$env:NRE_MUSCLE_FRAME_INPUT=Join-Path $package 'anatomical-input/binding-input.json'}else{Remove-Item Env:NRE_MUSCLE_FRAME_INPUT -ErrorAction SilentlyContinue}
 $status.TestExecutionAttempted=$true
 $status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
 Invoke-Logged $configuration.DotnetExecutable @('vstest',$assembly,('/TestCaseFilter:'+$configuration.TestFilter),('/ResultsDirectory:'+$testOutput),'/Logger:trx;LogFileName=focused.trx') 'focused-tests.log'
 Stage 'check-tests'
 $status['PassedFocusedTests']=$expectedCases;$status.ClockChecksPassed=$true
 if($closedLoop){$status['SplitScheduleChecksPassed']=$true}
 if($articulated){$status['HostBridgeChecksPassed']=$true;$status['NativeFullBodyBridgeQualified']=$false}
 if($attachment){$status['PhysicalInitializationChecksPassed']=$true;$status['NativePassiveInitializationQualified']=$false}
 if($head){$status['OriginalHeadInputChecksPassed']=$true;$status['NativeHeadDeliveryQualified']=$false}
 if($initialBalance){$status['InitialMuscleBalanceChecksPassed']=$true;$status['IndependentInitialBalancePassed']=$true;$status['LiveMuscleGeometrySelected']=$false}
 if($frameMap){$status['AnatomicalFrameArithmeticPassed']=$true;$status['AnatomicalCalibrationQualified']=$false}
 if($namedMuscles){
  $status['NamedMuscleProtocolChecksPassed']=$true
  $workProject=Join-Path $configuration.UnityBuildWorkspace 'UnityWorld'
  Copy-Tree (Join-Path $package 'source/UnityWorld') $workProject
  $playerOutput=Join-Path $package 'player';[IO.Directory]::CreateDirectory($playerOutput) | Out-Null
  Write-Host 'Building a separate Unity player for named anatomical paths...' -ForegroundColor Cyan
  $priorOutput=[Environment]::GetEnvironmentVariable('NRE_UNITY_OUTPUT','Process')
  try{
   [Environment]::SetEnvironmentVariable('NRE_UNITY_OUTPUT',$playerOutput,'Process')
   $arguments=@('-batchmode','-nographics','-projectPath',$workProject,'-executeMethod','Nre.UnityWorld.Editor.WorldBuild.BuildWindows','-quit','-logFile',(Join-Path $playerOutput 'build.log'))
   $editorProcess=Start-Process -FilePath $configuration.UnityExecutable -ArgumentList (($arguments | ForEach-Object {ConvertTo-ProcessArgument $_}) -join ' ') -WorkingDirectory $workProject -WindowStyle Hidden -PassThru
   if(-not $editorProcess.WaitForExit(1800000)){$editorProcess.Kill();$editorProcess.WaitForExit();throw 'Dedicated Unity build exceeded 30 minutes.'}
   if($editorProcess.ExitCode -ne 0){throw 'Unity build failed; inspect saved build.log.'}
  }finally{[Environment]::SetEnvironmentVariable('NRE_UNITY_OUTPUT',$priorOutput,'Process')}
  $buildResult=Get-Content -LiteralPath (Join-Path $playerOutput 'build-result.json') -Raw | ConvertFrom-Json
  if($buildResult.result -ne 'Succeeded' -or $buildResult.errors -ne 0){throw 'Native Unity build did not succeed.'}
  $sourceUnity=Join-Path $package 'source/UnityWorld'
  foreach($file in Get-ChildItem -LiteralPath $sourceUnity -Filter '*.cs' -Recurse -File){
   $relative=$file.FullName.Substring($sourceUnity.Length).TrimStart('\','/')
   if((Sha $file.FullName) -ne (Sha (Join-Path $workProject $relative))){throw ('Compiled Unity source changed: '+$relative)}
  }
  foreach($folder in @('Assets','Packages','ProjectSettings')){Copy-Tree (Join-Path $workProject $folder) (Join-Path $package ('built-project/'+$folder))}
  $player=Join-Path $playerOutput 'DNNE Unity World.exe';$playerFiles=Capture-Binaries $player 'player-files.json';$status['UnityBuildSucceeded']=$true
  $status.NativeUnityExecuted=$true;$status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
  Invoke-Logged $configuration.DotnetExecutable @($probe,'--named-muscle-native',$player,(Join-Path $package 'frame-review/mapped-lower-leg-paths.json'),(Join-Path $package 'anatomical-input/binding-input.json'),(Join-Path $package 'native')) 'native-named.log'
  Stage 'named-compare'
  $status['NamedNativeAccountingPassed']=$true;$status['AllNamedPathsResponded']=$true;$status['AnatomicalCalibrationQualified']=$false;$status['NeuronalMotorAuthority']=$false
 }
 $status.Status='DIAGNOSTICS_COMPLETE';$status.ExitCode=0
}catch{
 $status.Status='FAILED';$status.ExitCode=1;$status['Error']=$_.Exception.ToString();Write-Host ('FAILED: '+$_.Exception.Message) -ForegroundColor Red
}finally{
 try{
  Stage 'preflight';Assert-Package
  foreach($entry in @($testBinaries)+@($probeBinaries)+@($playerFiles)){if((Sha $entry.Path) -ne $entry.Sha256){throw ('Executed binary changed: '+$entry.Path)}}
  $status['FrozenInputsPreserved']=$true
 }catch{$status.Status='FAILED';$status.ExitCode=1;$status['PreservationError']=$_.Exception.ToString()}
 if($probe -and (Test-Path -LiteralPath (Join-Path $package 'managed-runtime-before.json'))){
  try{
   Invoke-Logged $configuration.DotnetExecutable @($probe,'--runtime-fingerprint',(Join-Path $package 'managed-runtime-after.json')) 'managed-runtime-after.log'
   if((Sha (Join-Path $package 'managed-runtime-before.json')) -ne (Sha (Join-Path $package 'managed-runtime-after.json'))){throw 'Selected managed runtime changed.'}
   $status['ManagedRuntimePreserved']=$true
  }catch{$status.Status='FAILED';$status.ExitCode=1;$status['ManagedRuntimeError']=$_.Exception.ToString()}
 }
 $status['CompletedUtc']=[DateTime]::UtcNow.ToString('o')
 if($transcribing){Stop-Transcript | Out-Null}
 $status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8
 try{& (Join-Path $package 'backup-run.ps1') -Source $package -Destination $configuration.BackupDestination;if(-not $?){throw 'Backup failed.'}}
 catch{$status.Status='FAILED';$status.ExitCode=1;$status['BackupError']=$_.Exception.ToString();$status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8;Write-Host ('BACKUP FAILED: '+$_.Exception.Message) -ForegroundColor Red}
 if($LaunchTaskName){Unregister-ScheduledTask -TaskName $LaunchTaskName -Confirm:$false -ErrorAction SilentlyContinue}
 Write-Host ('========== '+$status.Status+' ==========') -ForegroundColor Cyan
 Write-Host ('Results: '+$package)
 if($namedMuscles){Write-Host 'Tell Orion the actual named-muscle Unity outcome. Native attachment/force delivery, anatomical fit, neuronal authority and Avatar standing are separate.'}
 elseif($frameMap){Write-Host 'Tell Orion the lower-leg attachment mapping outcome. Coordinate accuracy, anatomical fit, native muscle delivery and Avatar standing are separate.'}
 elseif($initialBalance){Write-Host 'Tell Orion the fresh muscle equilibrium outcome. Initial force balance, coupled trajectories, anatomical calibration, neuronal authority and Avatar standing are separate.'}
 elseif($head){Write-Host 'Tell Orion the acknowledged head-input outcome. Original sample ownership, actual native delivery, afferent physiology and Avatar standing are separate.'}
 elseif($attachment){Write-Host 'Tell Orion the passive motor attachment outcome. Clock/initialization controls, native delivery, physiological feedback and Avatar standing are separate.'}
 elseif($articulated){Write-Host 'Tell Orion the full Avatar host bridge outcome. Protocol controls, native force delivery, sensory physiology and Avatar standing are separate.'}
 elseif($closedLoop){Write-Host 'Tell Orion the body/motor closure outcome. Component scheduling, active sensory feedback, coupled accuracy and Avatar standing are separate.'}
 else{Write-Host 'Tell Orion the fresh motor-clock outcome. Component clock ownership, coupled numerical accuracy, physiological firing and Avatar standing are separate.'}
}
