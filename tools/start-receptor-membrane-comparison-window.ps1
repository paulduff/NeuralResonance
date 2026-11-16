param([Parameter(Mandatory=$true)][string]$PackageDirectory,[switch]$InWindow,[string]$LaunchTaskName='')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
$package=(Resolve-Path -LiteralPath $PackageDirectory).Path
$configuration=Get-Content -LiteralPath (Join-Path $package 'configuration.json') -Raw | ConvertFrom-Json
if(-not $configuration.IsolatedReceptorMembraneComparison -or -not $configuration.NoLiveMonitoring -or $configuration.ExpectedCandidateConditions -ne 18 -or $configuration.ExpectedReferenceConditions -ne 18 -or $configuration.ExpectedContactDeliveries -ne 648 -or -not $configuration.PresynapticEventsAreFixtures -or $configuration.PresynapticNeuronsExecuted -or $configuration.NativePhysicsRequested -or $configuration.PhysiologyQualified -or $configuration.AvatarRecoveryAssessed){throw 'Unexpected coupled comparison configuration.'}
if(-not $configuration.SourceOutputComparisonRequested -or $configuration.ExpectedSourceOutputConditions -ne 36 -or $configuration.ExpectedSourceObservations -ne 540036){throw 'Active source/output comparison is missing from the frozen configuration.'}
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
 $taskName='DNNE-Receptor-Comparison-'+[Guid]::NewGuid().ToString('N').Substring(0,12)
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
function Assert-ManagedRuntime([string]$Label){
 $path=Join-Path $package ('managed-runtime-'+$Label+'.json')
 Invoke-Logged $configuration.DotnetExecutable @($probe,'--runtime-fingerprint',$path) ('managed-runtime-'+$Label+'.log')
 if((File-Sha256 $path) -ne (File-Sha256 (Join-Path $package 'reference/qualified-managed-runtime.json'))){throw 'Managed runtime differs from the qualified source gate.'}
}
function Assert-NumericalEnvironment([string]$Label){
 Invoke-Logged $configuration.PythonExecutable @('-B',(Join-Path $package 'source/tools/prepare-receptor-membrane-comparison.py'),'--check-environment',$package) ('numerical-environment-'+$Label+'.log')
}
$probe=Join-Path $package $configuration.QualifiedProbeRelativePath
$status=[ordered]@{Status='RUNNING';ExitCode=$null;Revision=$configuration.Revision;LiveBrainAccessed=$false;OldSavesLoaded=$false;
 CandidateExecutionAttempted=$false;PostsynapticNeuronExecuted=$false;CandidateMatrixComplete=$false;IndependentReferenceAttempted=$false;
 SourceOutputExecutionAttempted=$false;SourceOutputCaptureComplete=$false;SourceOutputAuditPassed=$false;
 CoupledNumericalCriteriaPassed=$false;ManagedRuntimePreserved=$false;NumericalEnvironmentPreserved=$false;
 PresynapticEventsAreFixtures=$true;PresynapticNeuronsExecuted=$false;NativePhysicsExecuted=$false;MuscleDynamicsExecuted=$false;
 PhysiologyQualified=$false;AvatarRecoveryAssessed=$false;FullUnitSuitePassed=$false}
$statusPath=Join-Path $package 'window-status.json';$transcribing=$false;$dependenciesChecked=$false
if(Test-Path -LiteralPath $statusPath){throw 'Refusing to overwrite earlier results.'}
try{
 Start-Transcript -Path (Join-Path $package 'powershell.log') | Out-Null;$transcribing=$true
 $Host.UI.RawUI.WindowTitle='DNNE receptor to motor membrane numerical comparison'
 Write-Host ('Results: '+$package) -ForegroundColor Cyan
 Write-Host '18 candidate conditions, 36 active source/Avatar receiver conditions, six receptor and 36 Jacobian controls, then 18 independent reference integrations. Presynaptic inputs are fixtures; no live body or brain.'
 $status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
 foreach($path in @($package,$configuration.BackupDestination)){
  $drive=[IO.DriveInfo]::new([IO.Path]::GetPathRoot($path))
  if($drive.AvailableFreeSpace -lt $configuration.MinimumFreeBytes){throw 'Insufficient capture/backup space; require 20 GiB free on each drive.'}
 }
 foreach($name in @('OPENBLAS_NUM_THREADS','OMP_NUM_THREADS','MKL_NUM_THREADS','NUMEXPR_NUM_THREADS')){[Environment]::SetEnvironmentVariable($name,'1','Process')}
 Assert-Package
 Assert-ManagedRuntime 'before'
 Assert-NumericalEnvironment 'before'
 $dependenciesChecked=$true;$status.CandidateExecutionAttempted=$true
 Invoke-Logged $configuration.DotnetExecutable @($probe,'--receptor-membrane',(Join-Path $package 'receptor-membrane-input.json'),(Join-Path $package 'membrane-study')) 'candidate.log'
 $candidate=Get-Content -LiteralPath (Join-Path $package 'membrane-study/results.json') -Raw | ConvertFrom-Json
 if(-not $candidate.completed -or $candidate.expectedConditions -ne 18 -or $candidate.cases.Count -ne 18 -or -not $candidate.postsynapticNeuronExecuted -or -not $candidate.presynapticEventsAreFixtures -or $candidate.presynapticNeuronsExecuted -or $candidate.nativePhysicsExecuted -or $candidate.physiologicalCalibrationComplete -or $candidate.avatarRecoveryAssessed){throw 'Candidate recorder incomplete or scope changed.'}
 $status.PostsynapticNeuronExecuted=$true;$status.CandidateMatrixComplete=$true
 Assert-ManagedRuntime 'after-candidate'
 Assert-Package
 $status.SourceOutputExecutionAttempted=$true
 Invoke-Logged $configuration.DotnetExecutable @($probe,'--receptor-motor-output',(Join-Path $package 'receptor-membrane-input.json'),(Join-Path $package 'receptor-motor-output-input.json'),(Join-Path $package 'membrane-study'),(Join-Path $package 'motor-output-study')) 'motor-output.log'
 $outputCapture=Get-Content -LiteralPath (Join-Path $package 'motor-output-study/results.json') -Raw | ConvertFrom-Json
 if(-not $outputCapture.quietHasNoCrossings -or -not $outputCapture.nontrivialExcitedCrossings){throw 'Quiet or nontrivial excited-source requirement failed.'}
 if(-not $outputCapture.completed -or -not $outputCapture.exactObservedStateAndDeliveryChecksPassed -or $outputCapture.cases.Count -ne 36 -or -not $outputCapture.postsynapticNeuronExecuted -or -not $outputCapture.actualAvatarReceiverExecuted -or -not $outputCapture.presynapticEventsAreFixtures -or $outputCapture.presynapticNeuronsExecuted -or $outputCapture.nativePhysicsExecuted -or $outputCapture.muscleDynamicsExecuted -or $outputCapture.independentNumericalAccuracyQualified -or $outputCapture.physiologicalCalibrationComplete -or $outputCapture.avatarRecoveryAssessed){throw 'Active source/receiver capture failed or scope changed.'}
 $status.SourceOutputCaptureComplete=$true
 Assert-ManagedRuntime 'after-output'
 Assert-Package
 $status.IndependentReferenceAttempted=$true
 Invoke-Logged $configuration.PythonExecutable @('-B',(Join-Path $package 'source/tools/receptor_membrane_reference.py'),$package) 'reference.log'
 $analysis=Get-Content -LiteralPath (Join-Path $package 'membrane-reference/comparison.json') -Raw | ConvertFrom-Json
 if(-not $analysis.completed -or -not $analysis.numericalCriteriaPassed -or -not $analysis.frozenInputsAndEnvironmentPreserved -or $analysis.cases.Count -ne 6 -or $analysis.analyticControls.Count -ne 6 -or -not $analysis.quietHasNoCrossings -or -not $analysis.nontrivialExcitedCrossings -or $analysis.candidateAudit.conditions -ne 18 -or $analysis.candidateAudit.deliveredContactEvents -ne 648){throw 'Coupled comparison failed or incomplete.'}
 if(-not $analysis.sourceOutputAudit.completed -or -not $analysis.sourceOutputAudit.exactObservedStateAndDeliveryChecksPassed -or $analysis.sourceOutputAudit.sourceConditions -ne 36 -or $analysis.sourceOutputAudit.observationPoints -ne 540036 -or $analysis.sourceOutputAudit.inputContactDeliveries -ne 1296){throw 'Active source/receiver audit incomplete.'}
 $status.SourceOutputAuditPassed=$true
 foreach($row in $analysis.cases){
  if(-not $row.passed -or $row.jacobianControls.Count -ne 6 -or $row.referenceMethods.Count -ne 3 -or $row.referenceContrasts.Count -ne 3 -or $row.candidateContrasts.Count -ne 3 -or $row.refinement.Count -ne 2){throw ('Incomplete reference matrix: '+$row.key)}
 }
 if(-not $analysis.presynapticEventsAreFixtures -or $analysis.presynapticNeuronsExecuted -or $analysis.physiologicalCalibrationComplete -or $analysis.avatarRecoveryAssessed){throw 'Unexpected biological qualification.'}
 $status.CoupledNumericalCriteriaPassed=$true;$status.Status='DIAGNOSTICS_COMPLETE';$status.ExitCode=0
}catch{
 $status.Status='FAILED';$status.ExitCode=1;$status['Error']=$_.Exception.ToString()
 Write-Host ('FAILED: '+$_.Exception.Message) -ForegroundColor Red
}finally{
 if($status.Status -eq 'RUNNING'){$status.Status='INTERRUPTED';$status.ExitCode=2;$status['Error']='Pipeline ended before completion; cause not established.'}
 if($dependenciesChecked){
  try{Assert-ManagedRuntime 'after';$status.ManagedRuntimePreserved=$true}
  catch{$status.Status='FAILED';$status.ExitCode=1;$status.CoupledNumericalCriteriaPassed=$false;$status['ManagedRuntimeError']=$_.Exception.ToString()}
  try{Assert-NumericalEnvironment 'after';$status.NumericalEnvironmentPreserved=$true}
  catch{$status.Status='FAILED';$status.ExitCode=1;$status.CoupledNumericalCriteriaPassed=$false;$status['NumericalEnvironmentError']=$_.Exception.ToString()}
 }
 try{Assert-Package;$status['FrozenPackageUnchanged']=$true}
 catch{$status.Status='FAILED';$status.ExitCode=1;$status.CoupledNumericalCriteriaPassed=$false;$status['FrozenPackageUnchanged']=$false;$status['FrozenSourceError']=$_.Exception.ToString()}
 $status['CompletedUtc']=[DateTime]::UtcNow.ToString('o')
 if($transcribing){Stop-Transcript | Out-Null}
 $status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8
 try{& (Join-Path $package 'backup-run.ps1') -Source $package -Destination $configuration.BackupDestination}
 catch{$status.Status='FAILED';$status.ExitCode=1;$status['BackupError']=$_.Exception.ToString();$status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8;Write-Host ('BACKUP FAILED: '+$_.Exception.Message) -ForegroundColor Red}
 if($LaunchTaskName){Unregister-ScheduledTask -TaskName $LaunchTaskName -Confirm:$false -ErrorAction SilentlyContinue}
 Write-Host ('========== '+$status.Status+' ==========') -ForegroundColor Cyan
 Write-Host ('Results: '+$package)
 Write-Host 'Tell Orion the receptor membrane numerical outcome. Coupled numerical accuracy, presynaptic neuronal authority, physiology and Avatar recovery are separate.'
}
