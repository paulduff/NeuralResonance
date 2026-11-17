param([Parameter(Mandatory=$true)][string]$PackageDirectory,[switch]$InWindow,[string]$LaunchTaskName='')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
$package=(Resolve-Path -LiteralPath $PackageDirectory).Path
$configuration=Get-Content -LiteralPath (Join-Path $package 'configuration.json') -Raw | ConvertFrom-Json
$declared=($configuration.PSObject.Properties.Name -contains 'DeclaredRubralInput') -and [bool]$configuration.DeclaredRubralInput
$testCount=if($declared){150}else{136}
$testCommand=if($declared){'check-tests-declared'}else{'check-tests'}
$auditCommand=if($declared){'audit-declared'}else{'audit'}
$probeCommand=if($declared){'--rubral-declared-input'}else{'--rubral-motor-input'}
if(-not $configuration.IsolatedRubralMotor -or -not $configuration.NoLiveMonitoring -or $configuration.ExpectedConditions -ne 4 -or $configuration.ExpectedBranches -ne 12 -or $configuration.ExpectedTestCases -ne $testCount -or $configuration.NativePhysicsRequested -or $configuration.UpstreamNumericalAccuracyQualified -or $configuration.PhysiologyQualified -or $configuration.AvatarRecoveryAssessed){throw 'Unexpected isolated rubral motor configuration.'}
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
if(-not $InWindow){
 if(Test-Path -LiteralPath (Join-Path $package 'launch.json')){throw 'Already launched; use a fresh package.'}
 $manifestPath=Join-Path $package 'backup-manifest.json';$manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
 if($manifest.Destination -ne $configuration.BackupDestination){throw 'Initial backup destination differs.'}
 if((File-Sha256 $manifestPath) -ne (File-Sha256 (Join-Path $configuration.BackupDestination 'backup-manifest.json'))){throw 'Initial backup manifests differ.'}
 foreach($entry in $manifest.Files){foreach($directory in @($package,$configuration.BackupDestination)){
  if((File-Sha256 (Join-Path $directory $entry.Path)) -ne $entry.Sha256){throw ('Initial source/backup differs: '+$entry.Path)}
 }}
 $taskName=$(if($declared){'DNNE-Rubral-Declared-'}else{'DNNE-Rubral-Motor-'})+[Guid]::NewGuid().ToString('N').Substring(0,12)
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
 foreach($name in @('Dotnet','Python')){
  $executable=$configuration.($name+'Executable');$expected=$configuration.($name+'ExecutableSha256')
  if((File-Sha256 $executable) -ne $expected){throw ($name+' executable changed.')}
 }
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
function Capture-Binaries([string]$Assembly,[string]$Name){
 $entries=@([IO.Directory]::EnumerateFiles((Extended-Path (Split-Path -Parent $Assembly)),'*',[IO.SearchOption]::AllDirectories) | ForEach-Object {
  [ordered]@{Path=$_.Substring(4);Sha256=(File-Sha256 $_)}
 })
 $entries | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $package $Name) -Encoding UTF8
 return $entries
}
$status=[ordered]@{Status='RUNNING';ExitCode=$null;Revision=$configuration.Revision;LiveBrainAccessed=$false;OldSavesLoaded=$false;
 ComponentCausalityChecksPassed=$false;ObservedNeuronalMotorResponse=$false;FocusedTestsPassed=$false;
 ModelExecutionAttempted=$false;StudyExecutionAttempted=$false;UpstreamNumericalAccuracyQualified=$false;
 LearnedContactEfficacyTransferred=$false;NativePhysicsExecuted=$false;MuscleDynamicsExecuted=$false;
 PhysiologyQualified=$false;AvatarRecoveryAssessed=$false;FullUnitSuitePassed=$false}
if($declared){$status['ActualFixtureDeliveryVerified']=$false}
$statusPath=Join-Path $package 'window-status.json';$transcribing=$false;$testBinaries=@();$probeBinaries=@()
if(Test-Path -LiteralPath $statusPath){throw 'Refusing to overwrite earlier results.'}
try{
 Start-Transcript -Path (Join-Path $package 'powershell.log') | Out-Null;$transcribing=$true
 $Host.UI.RawUI.WindowTitle='DNNE neuronal source to receptor and Avatar receiver'
 Write-Host ('Results: '+$package) -ForegroundColor Cyan
 Write-Host ($testCount.ToString()+' focused checks; four fresh 256-cell red nucleus conditions, each with three motor/receiver branches. No live brain or Unity.')
 if($declared){Write-Host 'Each fixture synapse has one explicit receiving neuron. Actual event delivery is checked before motor response.'}
 $status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
 Assert-Package
 $build=Join-Path $package 'source/artifacts/rubral-motor-tests'
 Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package 'source/tests/NeuralResonanceEngine.DNNE.Tests/NeuralResonanceEngine.DNNE.Tests.csproj'),'-c','Release','--artifacts-path',$build,'--nologo','-v','minimal') 'test-build.log'
 $assembly=Join-Path $build 'bin/NeuralResonanceEngine.DNNE.Tests/release/NeuralResonanceEngine.DNNE.Tests.dll'
 $testRoot=[IO.DirectoryInfo]::new((Split-Path -Parent $assembly))
 while($null -ne $testRoot -and -not (Test-Path -LiteralPath (Join-Path $testRoot.FullName 'NeuralResonanceEngine.DNNE.slnx'))){$testRoot=$testRoot.Parent}
 if($null -eq $testRoot -or -not $testRoot.FullName.Equals((Join-Path $package 'source'),[StringComparison]::OrdinalIgnoreCase)){throw 'Tests are not confined to frozen source.'}
 $testOutput=Join-Path $package 'test-results';New-Item -ItemType Directory -Path $testOutput | Out-Null
 $testBinaries=Capture-Binaries $assembly 'test-binaries.json'
 $status.ModelExecutionAttempted=$true
 Invoke-Logged $configuration.DotnetExecutable @('vstest',$assembly,('/TestCaseFilter:'+$configuration.TestFilter),('/ResultsDirectory:'+$testOutput),'/Logger:trx;LogFileName=focused.trx') 'focused-tests.log'
 Invoke-Logged $configuration.PythonExecutable @('-I','-B',(Join-Path $package 'source/tools/rubral_motor_gate.py'),$testCommand,$package) 'test-inventory.log'
 $status['PassedFocusedTests']=$testCount;$status.FocusedTestsPassed=$true
 $probeBuild=Join-Path $package 'source/artifacts/rubral-motor-probe'
 Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package 'source/tools/NativeMotorBridgeProbe/NativeMotorBridgeProbe.csproj'),'-c','Release','--artifacts-path',$probeBuild,'--nologo','-v','minimal') 'probe-build.log'
 $probe=Join-Path $probeBuild 'bin/NativeMotorBridgeProbe/release/NeuralResonanceEngine.DNNE.Tests.dll'
 $probeBinaries=Capture-Binaries $probe 'probe-binaries.json'
 Invoke-Logged $configuration.DotnetExecutable @($probe,'--runtime-fingerprint',(Join-Path $package 'managed-runtime-before.json')) 'managed-runtime-before.log'
 if((File-Sha256 (Join-Path $package 'reference/qualified-managed-runtime.json')) -ne (File-Sha256 (Join-Path $package 'managed-runtime-before.json'))){throw 'Runtime differs from the qualified cubic capture; review before integrating.'}
 $status.StudyExecutionAttempted=$true
 Invoke-Logged $configuration.DotnetExecutable @($probe,$probeCommand,(Join-Path $package 'study')) 'study.log'
 Invoke-Logged $configuration.PythonExecutable @('-I','-B',(Join-Path $package 'source/tools/rubral_motor_gate.py'),$auditCommand,$package) 'analysis.log'
 $analysis=Get-Content -LiteralPath (Join-Path $package 'analysis.json') -Raw | ConvertFrom-Json
 if(-not $analysis.completed -or -not $analysis.componentCausalityChecksPassed -or $analysis.cases.Count -ne 4 -or $analysis.branches -ne 12 -or $analysis.originalFrames -ne 48000 -or $analysis.upstreamNumericalAccuracyQualified -or $analysis.upstreamPhysiologyQualified -or $analysis.learnedContactEfficacyTransferred -or $analysis.liveBrainAccessed -or $analysis.nativePhysicsExecuted -or $analysis.muscleDynamicsExecuted -or $analysis.avatarRecoveryAssessed){throw 'Source/receiver accounting incomplete or scope changed.'}
 if($declared){
  if(-not $analysis.actualFixtureDeliveryVerified -or $analysis.deliveredFixtureEvents -ne 15360 -or $analysis.declaredRecipientsPerActiveCondition -ne 256 -or -not $analysis.originalQuietObservationsPreserved){throw 'Declared recipient delivery or preserved quiet observations failed.'}
  $status.ActualFixtureDeliveryVerified=$true
 }
 Invoke-Logged $configuration.DotnetExecutable @($probe,'--runtime-fingerprint',(Join-Path $package 'managed-runtime-after.json')) 'managed-runtime-after.log'
 if((File-Sha256 (Join-Path $package 'managed-runtime-before.json')) -ne (File-Sha256 (Join-Path $package 'managed-runtime-after.json'))){throw 'Managed runtime dependencies changed during the study.'}
 $status['ManagedRuntimePreserved']=$true
 $status.ComponentCausalityChecksPassed=$true;$status.ObservedNeuronalMotorResponse=[bool]$analysis.observedNeuronalMotorResponse
 $status['SuccessfulConditions']=4;$status.Status='DIAGNOSTICS_COMPLETE';$status.ExitCode=0
 Write-Host ('Observed neuronal-to-motor response: '+$status.ObservedNeuronalMotorResponse) -ForegroundColor Yellow
 Write-Host 'A completed accounting experiment with no motor response does not demonstrate neuronal motor authority.'
}catch{
 $status.Status='FAILED';$status.ExitCode=1;$status['Error']=$_.Exception.ToString()
 Write-Host ('FAILED: '+$_.Exception.Message) -ForegroundColor Red
}finally{
 if($status.Status -eq 'RUNNING'){$status.Status='INTERRUPTED';$status.ExitCode=2;$status['Error']='Pipeline ended before completion; cause not established.'}
 try{Assert-Package;foreach($entry in @($testBinaries)+@($probeBinaries)){if((File-Sha256 $entry.Path) -ne $entry.Sha256){throw ('Built binary changed: '+$entry.Path)}}}catch{$status.Status='FAILED';$status.ExitCode=1;$status['FrozenSourceError']=$_.Exception.ToString()}
 $status['CompletedUtc']=[DateTime]::UtcNow.ToString('o')
 if($transcribing){Stop-Transcript | Out-Null}
 $status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8
 try{& (Join-Path $package 'backup-run.ps1') -Source $package -Destination $configuration.BackupDestination}
 catch{$status.Status='FAILED';$status.ExitCode=1;$status['BackupError']=$_.Exception.ToString();$status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8;Write-Host ('BACKUP FAILED: '+$_.Exception.Message) -ForegroundColor Red}
 if($LaunchTaskName){Unregister-ScheduledTask -TaskName $LaunchTaskName -Confirm:$false -ErrorAction SilentlyContinue}
 Write-Host ('========== '+$status.Status+' ==========') -ForegroundColor Cyan
 Write-Host ('Results: '+$package)
 Write-Host 'Tell Orion the rubral neuronal/motor outcome. Original neuronal event causality, upstream accuracy, physiology, moving body coupling and Avatar recovery are separate.'
}
