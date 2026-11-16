param([Parameter(Mandatory=$true)][string]$PackageDirectory,[switch]$InWindow,[string]$LaunchTaskName='')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
$package=(Resolve-Path -LiteralPath $PackageDirectory).Path
$configuration=Get-Content -LiteralPath (Join-Path $package 'configuration.json') -Raw | ConvertFrom-Json
if(-not $configuration.NoLiveMonitoring -or -not $configuration.IsolatedCapturedNativeComparison -or -not $configuration.VerifiedNativeCapture -or -not $configuration.QualifiedReferenceCorePreserved){throw 'Unexpected captured native comparison configuration.'}
if($configuration.ExpectedNativeCases -ne 4 -or $configuration.ExpectedCapturedSteps -ne 4096 -or $configuration.ExpectedReferenceSolves -ne 12 -or $configuration.ExpectedGeometryControls -ne 49 -or $configuration.ExpectedMechanicalMethodChecks -ne 6 -or $configuration.ExpectedCrossMethodChecks -ne 3){throw 'Unexpected comparison coverage.'}
foreach($flag in @('NativePhysicsReexecuted','LiveBodySelected','PhysicalStepRefinementAssessed','MuscleStepRefinementAssessed','FullCoupledTrajectoryQualified','PhysiologyQualified','AvatarRecoveryAssessed')){if($configuration.$flag){throw ('Unexpected qualification: '+$flag)}}
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
 $manifestPath=Join-Path $package 'backup-manifest.json'
 $manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
 if($manifest.Destination -ne $configuration.BackupDestination){throw 'Initial backup destination differs.'}
 if((File-Sha256 $manifestPath) -ne (File-Sha256 (Join-Path $configuration.BackupDestination 'backup-manifest.json'))){throw 'Initial backup manifests differ.'}
 foreach($entry in $manifest.Files){foreach($directory in @($package,$configuration.BackupDestination)){
  if((File-Sha256 (Join-Path $directory $entry.Path)) -ne $entry.Sha256){throw ('Initial backup or source differs: '+$entry.Path)}
 }}
 $taskName='DNNE-Native-Hinge-Comparison-'+[Guid]::NewGuid().ToString('N').Substring(0,12)
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
function Assert-Package{
 foreach($entry in $configuration.PackageFiles){if((File-Sha256 (Join-Path $package $entry.Path)) -ne $entry.Sha256){throw ('Frozen source/input changed: '+$entry.Path)}}
}
function Invoke-Stage([string]$Stage){
 $logName='reference-'+$Stage+'.log'
 $writer=[IO.StreamWriter]::new((Join-Path $package $logName),$false,[Text.UTF8Encoding]::new($false));$writer.AutoFlush=$true
 $previous=$ErrorActionPreference
 try{
  $ErrorActionPreference='Continue'
  & $configuration.PythonExecutable -B (Join-Path $package 'source/tools/analyse-native-hinge-comparison.py') $package --stage $Stage 2>&1 | ForEach-Object {$line=[string]$_;$writer.WriteLine($line);Write-Host $line} -ErrorAction Stop
  $nativeExit=$LASTEXITCODE
 }finally{$writer.Dispose();$ErrorActionPreference=$previous}
 if($nativeExit -ne 0){throw ($logName+' exited '+$nativeExit)}
}
$status=[ordered]@{Status='RUNNING';ExitCode=$null;Revision=$configuration.Revision;LiveBrainAccessed=$false;OldSavesLoaded=$false;
 NativePhysicsReexecuted=$false;NeuronalModelExecuted=$false;ReferenceControlsPassed=$false;ReferenceSolvesAttempted=$false;
 CapturedTrajectoryAgreementPassed=$false;PhysicalStepRefinementAssessed=$false;MuscleStepRefinementAssessed=$false;
 FullCoupledTrajectoryQualified=$false;PhysiologyQualified=$false;AvatarRecoveryAssessed=$false}
$statusPath=Join-Path $package 'window-status.json';$transcribing=$false
if(Test-Path -LiteralPath $statusPath){throw 'Refusing to overwrite an earlier result.'}
try{
 Start-Transcript -Path (Join-Path $package 'powershell.log') | Out-Null;$transcribing=$true
 $Host.UI.RawUI.WindowTitle='DNNE native hinge trajectory comparison'
 Write-Host ('Results: '+$package) -ForegroundColor Cyan
 Write-Host 'Reusing 4 completed Unity trajectories. Checking nonlinear geometry, 6 reference controls and 12 muscle/hinge reference solves. No live Avatar or new native simulation.'
 $status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
 Assert-Package
 Invoke-Stage 'preflight'
 Invoke-Stage 'controls'
 $controls=Get-Content -LiteralPath (Join-Path $package 'reference-controls/controls.json') -Raw | ConvertFrom-Json
 if(-not $controls.completed -or -not $controls.passed -or $controls.geometryCases.Count -ne 49 -or $controls.mechanicalMethodChecks.Count -ne 6 -or $controls.crossMethodChecks.Count -ne 3 -or $controls.biologicalDynamicsIntegrated){throw 'Reference controls failed or incomplete.'}
 $status.ReferenceControlsPassed=$true;$status.ReferenceSolvesAttempted=$true
 Invoke-Stage 'solve'
 Invoke-Stage 'audit'
 $analysis=Get-Content -LiteralPath (Join-Path $package 'hinge-analysis.json') -Raw | ConvertFrom-Json
 if(-not $analysis.completed -or -not $analysis.capturedTrajectoryAgreementPassed -or $analysis.cases.Count -ne 4 -or $analysis.independentReferences -ne 12 -or $analysis.successfulReferences -ne 12 -or $analysis.capturedNativeSteps -ne 4096){throw 'Captured trajectory comparison failed or incomplete.'}
 foreach($flag in @('nativePhysicsReexecuted','neuronalModelExecuted','nativeJointIntegralAccuracyAssessed','physicalStepRefinementAssessed','muscleStepRefinementAssessed','fullCoupledTrajectoryQualified','physiologyQualified','avatarRecoveryAssessed')){if($analysis.$flag){throw ('Unexpected qualification: '+$flag)}}
 Assert-Package
 $status.CapturedTrajectoryAgreementPassed=$true;$status.Status='DIAGNOSTICS_COMPLETE';$status.ExitCode=0
}catch{
 $status.Status='FAILED';$status.ExitCode=1;$status['Error']=$_.Exception.ToString()
 Write-Host ('FAILED: '+$_.Exception.Message) -ForegroundColor Red
}finally{
 if($status.Status -eq 'RUNNING'){$status.Status='INTERRUPTED';$status.ExitCode=2;$status['Error']='Stopped before completing the study; cause not established.'}
 if(Test-Path -LiteralPath (Join-Path $package 'reference-preflight.json')){
  try{Invoke-Stage 'postflight';$status['ReferenceEnvironmentUnchanged']=$true}
  catch{$status.Status='FAILED';$status.ExitCode=1;$status.CapturedTrajectoryAgreementPassed=$false;$status['ReferenceEnvironmentUnchanged']=$false;$status['ReferenceEnvironmentError']=$_.Exception.ToString();Write-Host ('REFERENCE ENVIRONMENT FAILED: '+$_.Exception.Message) -ForegroundColor Red}
 }
 try{Assert-Package;$status['FrozenPackageUnchanged']=$true}
 catch{$status.Status='FAILED';$status.ExitCode=1;$status.CapturedTrajectoryAgreementPassed=$false;$status['FrozenPackageUnchanged']=$false;$status['FrozenPackageError']=$_.Exception.ToString()}
 $status['CompletedUtc']=[DateTime]::UtcNow.ToString('o')
 if($transcribing){Stop-Transcript | Out-Null}
 $status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8
 try{& (Join-Path $package 'backup-run.ps1') -Source $package -Destination $configuration.BackupDestination}
 catch{$status.Status='FAILED';$status.ExitCode=1;$status['BackupError']=$_.Exception.ToString();$status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8;Write-Host ('BACKUP FAILED: '+$_.Exception.Message) -ForegroundColor Red}
 if($LaunchTaskName){Unregister-ScheduledTask -TaskName $LaunchTaskName -Confirm:$false -ErrorAction SilentlyContinue}
 Write-Host ('========== '+$status.Status+' ==========') -ForegroundColor Cyan
 Write-Host ('Results: '+$package)
 Write-Host 'Tell Orion the native hinge trajectory outcome. Agreement at one resolution, step convergence, physiology and Avatar recovery are separate.'
}
