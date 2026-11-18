param([Parameter(Mandatory=$true)][string]$PackageDirectory,[switch]$InWindow,[string]$LaunchTaskName='')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
$package=(Resolve-Path -LiteralPath $PackageDirectory).Path
$configuration=Get-Content -LiteralPath (Join-Path $package 'configuration.json') -Raw | ConvertFrom-Json
if(-not $configuration.IsolatedRefinedMotorMatrix -or -not $configuration.NoLiveMonitoring -or $configuration.ExpectedStorageControls -ne 22 -or $configuration.ExpectedNativeCases -ne 7 -or $configuration.ExpectedNativePhysicsSteps -ne 223972 -or $configuration.ExpectedSavedReferences -ne 15 -or $configuration.PriorFocusedTestsReused -ne 287 -or $configuration.ExpectedUnityVersion -ne '6000.3.19f1' -or $configuration.LiveBodySelected -or -not $configuration.NoTimingMeasured -or $configuration.PhysiologyQualified -or $configuration.AvatarRecoveryAssessed){throw 'Unexpected isolated full refined motor configuration.'}
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
 $taskName='DNNE-Refined-Motor-'+[Guid]::NewGuid().ToString('N').Substring(0,12)
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
 StorageControlsPassed=$false;PriorFocusedTestsReused=287;ModelExecutionAttempted=$false;NativePhysicsAttempted=$false;NativeDriverSucceeded=$false;
 IndependentReferencesAttempted=$false;NumericalCriteriaPassed=$false;LiveUnityIntegrated=$false;TimingMeasured=$false;
 ReferenceControlsAttempted=$false;SavedReferenceControlsVerified=$false;SavedReferencesReused=$false;EnvironmentTransitionDeclared=$configuration.EnvironmentTransitionDeclared;
 UpstreamNumericalAccuracyQualified=$false;PhysiologyQualified=$false;AvatarRecoveryAssessed=$false;FullUnitSuitePassed=$false}
$statusPath=Join-Path $package 'window-status.json';$transcribing=$false;$testFiles=@();$probeFiles=@();$playerFiles=@();$probe=$null
if(Test-Path -LiteralPath $statusPath){throw 'Refusing to overwrite earlier results.'}
function Save-Status([string]$Stage){
 $status['Stage']=$Stage
 $temporary=Join-Path $package 'window-status.tmp'
 $status | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $temporary -Encoding UTF8
 Move-Item -LiteralPath $temporary -Destination $statusPath -Force
 Write-Host ('Stage: '+$Stage)
}
try{
 Start-Transcript -Path (Join-Path $package 'powershell.log') | Out-Null;$transcribing=$true
 $Host.UI.RawUI.WindowTitle='DNNE full refined neuronal motor matrix'
 Write-Host ('Results: '+$package) -ForegroundColor Cyan
 Write-Host '22 new storage lifecycle controls, then all seven original neuronal/motor conditions at refined body and muscle timing. 287 reviewed component tests and 15 completed references are reused; no reference ODE rerun.'
 Write-Host 'No live brain or Avatar; each isolated body starts paused. Report completion to Orion.'
 Save-Status 'preflight'
 Assert-Package
 Invoke-Logged $configuration.PythonExecutable @('-B',(Join-Path $package 'source/tools/refined_motor_gate.py'),'preflight',$package) 'reference-preflight.log'
 $status.SavedReferenceControlsVerified=$true
 if([IO.DriveInfo]::new([IO.Path]::GetPathRoot($package)).AvailableFreeSpace -lt $configuration.MinimumFreeBytesBeforeRun){throw 'Require 20 GiB free before starting native recordings.'}
 if([IO.DriveInfo]::new([IO.Path]::GetPathRoot($configuration.UnityBuildWorkspace)).AvailableFreeSpace -lt 20GB){throw 'Insufficient space for isolated build and D: backup.'}
 Save-Status 'build-runtime'
 $probeBuild=Join-Path $package 'source/artifacts/refined-motor-probe'
 Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package 'source/tools/NativeMotorBridgeProbe/NativeMotorBridgeProbe.csproj'),'-c','Release','--artifacts-path',$probeBuild,'--nologo','-v','minimal') 'probe-build.log'
 $probeDirectory=Join-Path $probeBuild 'bin/NativeMotorBridgeProbe/release';$probe=Join-Path $probeDirectory 'NeuralResonanceEngine.DNNE.Tests.dll'
 $probeFiles=Capture-Tree $probeDirectory 'probe-binaries.json'
 Invoke-Logged $configuration.DotnetExecutable @($probe,'--runtime-fingerprint',(Join-Path $package 'managed-runtime-before.json')) 'managed-runtime-before.log'
 if((File-Sha256 (Join-Path $package 'managed-runtime-before.json')) -ne (File-Sha256 (Join-Path $package 'reference/qualified-managed-runtime.json'))){throw 'Managed runtime differs from the reviewed circuit gate.'}
 Save-Status 'storage-lifecycle-controls'
 Invoke-Logged $configuration.PythonExecutable @('-B',(Join-Path $package 'source/tools/refined_motor_gate.py'),'controls',$package) 'storage-controls.log'
 $controls=Get-Content -LiteralPath (Join-Path $package 'storage-controls/controls.json') -Raw | ConvertFrom-Json
 if(-not $controls.completed -or $controls.expected -ne 22 -or $controls.passed -ne 22){throw 'Storage lifecycle controls failed or incomplete.'}
 $status.StorageControlsPassed=$true;$status['PassedStorageControls']=22
 Save-Status 'build-isolated-native-player'
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
 Save-Status 'initialize-new-capture-ownership'
 Invoke-Logged $configuration.PythonExecutable @('-B',(Join-Path $package 'source/tools/refined_motor_gate.py'),'start-store',$package) 'capture-admission.log'
 $status.NativePhysicsAttempted=$true;$status.ModelExecutionAttempted=$true;Save-Status 'full-refined-native-matrix'
 Invoke-Logged $configuration.DotnetExecutable @($probe,'--rubral-native-compact',(Join-Path $playerOutput 'DNNE Muscle Bridge.exe'),(Join-Path $package 'reference'),(Join-Path $package 'reference/circuit-gate'),$package,$configuration.PythonExecutable,(Join-Path $package 'source/tools/native_capture_store.py')) 'refined-motor.log'
 $status.NativeDriverSucceeded=$true;$status.SavedReferencesReused=$true
 Save-Status 'snapshot-complete-archive-view'
 Invoke-Logged $configuration.PythonExecutable @('-B',(Join-Path $package 'source/tools/refined_motor_gate.py'),'snapshot-view',$package) 'archive-view.log'
 Save-Status 'complete-causal-audit-and-numerical-comparison'
 Invoke-Logged $configuration.PythonExecutable @('-B',(Join-Path $package 'source/tools/analyse-refined-motor.py'),$package) 'coupled-analysis.log'
 $analysis=Get-Content -LiteralPath (Join-Path $package 'analysis.json') -Raw | ConvertFrom-Json
 if(-not $analysis.completed -or -not $analysis.numericalCriteriaPassed -or $analysis.cases.Count -ne 7 -or $analysis.independentReferencesReused -ne 15 -or $analysis.newReferenceSolves -ne 0 -or -not $analysis.allNativeEndpointsReconciled -or -not $analysis.commonGridAccuracyOnly -or -not $analysis.originalBoundsPreserved -or -not $analysis.fullOriginalConditionMatrixAssessed -or -not $analysis.newCaptureArchiveAccountingPassed -or -not $analysis.packetAndRetryStateBitwiseEqual -or $analysis.upstreamNumericalAccuracyQualified -or $analysis.liveBrainAuthorityQualified -or $analysis.physiologyQualified -or $analysis.avatarRecoveryAssessed){throw 'Full refined motor evidence incomplete or scope changed.'}
 $status.NumericalCriteriaPassed=$true;$status['ActualPhysicsSteps']=223972;$status['ArchiveAccountingPassed']=$true;$status.Status='DIAGNOSTICS_COMPLETE';$status.ExitCode=0
 $analysis.cases | Select-Object case,passed,maximumNormalizedByMetric | Format-Table | Out-Host

}catch{
 $status.Status='FAILED';$status.ExitCode=1;$status['Error']=$_.Exception.ToString()
 Write-Host ('FAILED: '+$_.Exception.Message) -ForegroundColor Red
}finally{
 if($status.Status -eq 'RUNNING'){$status.Status='INTERRUPTED';$status.ExitCode=2;$status['Error']='Pipeline ended before completion; cause not established.'}
 try{
  Invoke-Logged $configuration.PythonExecutable @('-B',(Join-Path $package 'source/tools/refined_motor_gate.py'),'preflight',$package) 'reference-postflight.log'
  $status['ReferenceEnvironmentPreserved']=$true
 }catch{$status.Status='FAILED';$status.ExitCode=1;$status.NumericalCriteriaPassed=$false;$status['ReferenceEnvironmentError']=$_.Exception.ToString()}
 if($probe -and (Test-Path -LiteralPath (Join-Path $package 'managed-runtime-before.json'))){
  try{
   Invoke-Logged $configuration.DotnetExecutable @($probe,'--runtime-fingerprint',(Join-Path $package 'managed-runtime-after.json')) 'managed-runtime-after.log'
   if((File-Sha256 (Join-Path $package 'managed-runtime-before.json')) -ne (File-Sha256 (Join-Path $package 'managed-runtime-after.json'))){throw 'Managed runtime dependencies changed during the study.'}
   $status['ManagedRuntimePreserved']=$true
  }catch{$status.Status='FAILED';$status.ExitCode=1;$status.NumericalCriteriaPassed=$false;$status['ManagedRuntimeError']=$_.Exception.ToString()}
 }
 try{Assert-Package;foreach($entry in @($testFiles)+@($probeFiles)+@($playerFiles)){if((File-Sha256 $entry.Path) -ne $entry.Sha256){throw ('Built file changed: '+$entry.Path)}}}catch{$status.Status='FAILED';$status.ExitCode=1;$status.NumericalCriteriaPassed=$false;$status['FrozenSourceError']=$_.Exception.ToString()}
 $status['CompletedUtc']=[DateTime]::UtcNow.ToString('o')
 if($transcribing){Stop-Transcript | Out-Null}
 Save-Status 'final-result-backup'
 try{& (Join-Path $package 'backup-run.ps1') -Source $package -Destination $configuration.BackupDestination}
 catch{$status.Status='FAILED';$status.ExitCode=1;$status['BackupError']=$_.Exception.ToString();$status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8;Write-Host ('BACKUP FAILED: '+$_.Exception.Message) -ForegroundColor Red}
 if($LaunchTaskName){Unregister-ScheduledTask -TaskName $LaunchTaskName -Confirm:$false -ErrorAction SilentlyContinue}
 Write-Host ('========== '+$status.Status+' ==========') -ForegroundColor Cyan
 Write-Host ('Results: '+$package)
 Write-Host 'Tell Orion the full refined motor outcome. Archived causal accounting, sampled numerical accuracy, live brain authority, physiology and Avatar recovery are separate.'
}
