param([Parameter(Mandatory=$true)][string]$PackageDirectory,[switch]$InWindow,[string]$LaunchTaskName='')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
$package=(Resolve-Path -LiteralPath $PackageDirectory).Path
$configuration=Get-Content -LiteralPath (Join-Path $package 'configuration.json') -Raw | ConvertFrom-Json
if(-not $configuration.ReferenceOnlyContinuation -or -not $configuration.OperatorReportedCompletion -or -not $configuration.SelfContainedSourceArchive -or -not $configuration.NoLiveMonitoring -or $configuration.NativePhysicsRerun -or $configuration.NeuronalCandidateRerun -or $configuration.OriginalBoundsChanged -or $configuration.ExpectedIndependentReferences -ne 15 -or $configuration.ExpectedNativePhysicsSteps -ne 55993){throw 'Unexpected reference continuation configuration.'}
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
 if((File-Sha256 $configuration.PythonExecutable) -ne $configuration.PythonExecutableSha256){throw 'Pinned Python executable changed.'}
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
 $taskName='DNNE-Rubral-Comparison-'+[Guid]::NewGuid().ToString('N').Substring(0,12)
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
function Invoke-Logged([string[]]$NativeArguments,[string]$LogName){
 $writer=[IO.StreamWriter]::new((Join-Path $package $LogName),$false,[Text.UTF8Encoding]::new($false));$writer.AutoFlush=$true
 $previous=$ErrorActionPreference
 try{
  $ErrorActionPreference='Continue'
  & $configuration.PythonExecutable @NativeArguments 2>&1 | ForEach-Object {$line=[string]$_;$writer.WriteLine($line);Write-Host $line} -ErrorAction Stop
  $nativeExit=$LASTEXITCODE
 }finally{$writer.Dispose();$ErrorActionPreference=$previous}
 if($nativeExit -ne 0){throw ($LogName+' exited '+$nativeExit)}
}
$status=[ordered]@{Status='RUNNING';ExitCode=$null;Revision=$configuration.Revision;NativeCapture=$configuration.NativeCapture;
 ReferenceOnlyContinuation=$true;NativeCaptureVerified=$false;OriginalCaptureUnchanged=$false;
 LoaderControlsPassed=$false;IndependentReferencesAttempted=$false;NumericalCriteriaPassed=$false;
 NeuronalCandidateRerun=$false;NativePhysicsRerun=$false;OriginalBoundsChanged=$false;
 LiveBrainAccessed=$false;OldSavesLoaded=$false;PhysiologyQualified=$false;AvatarRecoveryAssessed=$false}
$statusPath=Join-Path $package 'window-status.json';$transcribing=$false
if(Test-Path -LiteralPath $statusPath){throw 'Refusing to overwrite earlier results.'}
function Save-Stage([string]$Stage){
 $status['Stage']=$Stage;$status['UpdatedUtc']=[DateTime]::UtcNow.ToString('o')
 $temporary=$statusPath+'.tmp'
 $status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $temporary -Encoding UTF8
 Move-Item -LiteralPath $temporary -Destination $statusPath -Force
 Write-Host ('Stage: '+$Stage)
}
$continuation=Join-Path $package 'source/tools/rubral_native_continuation.py'
$analysisScript=Join-Path $package 'source/tools/analyse-rubral-native-body.py'
try{
 Start-Transcript -Path (Join-Path $package 'powershell.log') | Out-Null;$transcribing=$true
 $Host.UI.RawUI.WindowTitle='DNNE saved rubral/native motion: independent comparison'
 Write-Host ('Results: '+$package) -ForegroundColor Cyan
 Write-Host 'The original native run is preserved. This window verifies its files and runs the missing reference comparison.'
 Write-Host 'Full verification reads 360,428 files on each drive and may take time. No live monitoring; report the final footer to Orion.'
 Save-Stage 'input preservation'
 Assert-Package
 Invoke-Logged @('-B',$continuation,'verify-input',$package) 'input-review.log'
 $status.NativeCaptureVerified=$true
 Save-Stage 'loader controls'
 Invoke-Logged @('-B',$continuation,'loader-controls',$package) 'loader-controls.log'
 $status.LoaderControlsPassed=$true
 $status.IndependentReferencesAttempted=$true
 Save-Stage 'independent reference solves'
 Invoke-Logged @('-B',$analysisScript,'solve',$package) 'coupled-reference.log'
 Save-Stage 'trajectory comparison'
 Invoke-Logged @('-B',$analysisScript,'analyse',$package) 'coupled-analysis.log'
 $analysis=Get-Content -LiteralPath (Join-Path $package 'analysis.json') -Raw | ConvertFrom-Json
 $native=Get-Content -LiteralPath (Join-Path $package 'native-source-analysis.json') -Raw | ConvertFrom-Json
 if(-not $analysis.completed -or -not $analysis.numericalCriteriaPassed -or $analysis.cases.Count -ne 7 -or $analysis.independentReferences -ne 15 -or -not $analysis.allNativeEndpointsCompared -or $analysis.physicalStepRefinementForThisInputAssessed -or -not $analysis.sampledSingleResolutionOnly -or -not $analysis.originalBoundsPreserved -or $analysis.upstreamNumericalAccuracyQualified -or $analysis.physiologyQualified -or $analysis.avatarRecoveryAssessed){throw 'Numerical evidence incomplete or scope changed.'}
 if(-not $native.completed -or -not $native.sourceBodyAccountingPassed -or -not $native.observedNeuronalMuscleMovement -or -not $native.packetAndRetryStateBitwiseEqual -or $native.actualPhysicsSteps -ne 55993 -or $native.cases.Count -ne 7 -or $native.physiologyQualified -or $native.avatarRecoveryAssessed){throw 'Original source/body accounting incomplete.'}
 $status.NumericalCriteriaPassed=$true;$status.Status='DIAGNOSTICS_COMPLETE';$status.ExitCode=0
 $native.cases | Format-Table case,motorEvents,received,sourceFrames | Out-Host
}catch{
 $status.Status='FAILED';$status.ExitCode=1;$status['Error']=$_.Exception.ToString()
 Write-Host ('FAILED: '+$_.Exception.Message) -ForegroundColor Red
 Save-Stage 'failed stage recorded; final preservation still required'
}finally{
 if($status.Status -eq 'RUNNING'){$status.Status='INTERRUPTED';$status.ExitCode=2}
 Save-Stage 'final runtime preservation'
 try{
  Assert-Package
  Invoke-Logged @('-B',(Join-Path $package 'source/tools/rubral_native_gate.py'),'preflight',$package) 'reference-postflight.log'
  $status['ReferenceEnvironmentPreserved']=$true
  if($status.NativeCaptureVerified){
   Save-Stage 'final original C:/D: preservation'
   Invoke-Logged @('-B',$continuation,'postflight',$package) 'input-postflight.log'
   $status.OriginalCaptureUnchanged=$true
  }
 }catch{$status.Status='FAILED';$status.ExitCode=1;$status['PreservationError']=$_.Exception.ToString();Write-Host ('PRESERVATION FAILED: '+$_.Exception.Message) -ForegroundColor Red}
 $status['CompletedUtc']=[DateTime]::UtcNow.ToString('o')
 Save-Stage 'final result backup'
 if($transcribing){Stop-Transcript | Out-Null}
 $status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8
 try{& (Join-Path $package 'backup-run.ps1') -Source $package -Destination $configuration.BackupDestination}
 catch{$status.Status='FAILED';$status.ExitCode=1;$status['BackupError']=$_.Exception.ToString();$status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8;Write-Host ('BACKUP FAILED: '+$_.Exception.Message) -ForegroundColor Red}
 if($LaunchTaskName){Unregister-ScheduledTask -TaskName $LaunchTaskName -Confirm:$false -ErrorAction SilentlyContinue}
 Write-Host ('========== '+$status.Status+' ==========') -ForegroundColor Cyan
 Write-Host ('Results: '+$package)
 Write-Host 'Tell Orion the saved rubral/native comparison outcome. Numerical accuracy, physiological calibration and Avatar recovery are separate.'
}
