param([Parameter(Mandatory=$true)][string]$PackageDirectory,[switch]$InWindow,[string]$LaunchTaskName='')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
$package=(Resolve-Path -LiteralPath $PackageDirectory).Path
$configuration=Get-Content -LiteralPath (Join-Path $package 'configuration.json') -Raw | ConvertFrom-Json
if(-not $configuration.IsolatedContinuousCanalReference -or -not $configuration.NoLiveMonitoring -or
 $configuration.ExpectedControls -ne 15 -or $configuration.ExpectedReferenceCases -ne 6 -or
 $configuration.ExpectedNativeSteps -ne 2293760 -or $configuration.ExpectedUniqueSamplesPerReference -ne 655361 -or
 $configuration.NewNativeCaptureRequested -or $configuration.LiveSelectionChanged -or
 $configuration.PhysiologicalCalibrationQualified -or $configuration.AvatarBalanceAssessed){throw 'Unexpected continuous sensory reference configuration.'}
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
 foreach($entry in $configuration.PackageFiles){
  if((File-Sha256 (Join-Path $package $entry.Path)) -ne $entry.Sha256){throw ('Frozen input changed: '+$entry.Path)}
 }
 if((File-Sha256 $configuration.PythonExecutable) -ne $configuration.PythonExecutableSha256){throw 'Python executable changed.'}
 $review=Get-Content -LiteralPath (Join-Path $package 'reviewed-connected.json') -Raw | ConvertFrom-Json
 if(-not $review.operatorReportedCompletion -or -not $review.connectedAccountingPassed){throw 'Reported completion and reviewed native accounting required.'}
}
if(-not $InWindow){
 if(Test-Path -LiteralPath (Join-Path $package 'launch.json')){throw 'Already launched; preserve this package.'}
 $manifestPath=Join-Path $package 'backup-manifest.json'
 $manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
 if($manifest.Destination -ne $configuration.BackupDestination){throw 'Initial backup destination differs.'}
 if((File-Sha256 $manifestPath) -ne (File-Sha256 (Join-Path $configuration.BackupDestination 'backup-manifest.json'))){throw 'Initial backup manifests differ.'}
 foreach($entry in $manifest.Files){foreach($directory in @($package,$configuration.BackupDestination)){
  if((File-Sha256 (Join-Path $directory $entry.Path)) -ne $entry.Sha256){throw ('Initial backup differs: '+$entry.Path)}
 }}
 Assert-Package
 $taskName='DNNE-Canal-Reference-'+[Guid]::NewGuid().ToString('N').Substring(0,12)
 $shell=Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
 $arguments=@('-NoLogo','-NoProfile','-NoExit','-ExecutionPolicy','Bypass','-File',$PSCommandPath,'-InWindow','-LaunchTaskName',$taskName,'-PackageDirectory',$package)
 $action=New-ScheduledTaskAction -Execute $shell -Argument (($arguments | ForEach-Object {ConvertTo-ProcessArgument $_}) -join ' ') -WorkingDirectory $package
 $principal=New-ScheduledTaskPrincipal -UserId ([Security.Principal.WindowsIdentity]::GetCurrent().Name) -LogonType Interactive -RunLevel Limited
 $settings=New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
 Register-ScheduledTask -TaskName $taskName -Action $action -Principal $principal -Settings $settings | Out-Null
 [ordered]@{TaskName=$taskName;RequestedUtc=[DateTime]::UtcNow.ToString('o');Package=$package;NoLiveMonitoring=$true} |
  ConvertTo-Json | Set-Content -LiteralPath (Join-Path $package 'launch.json') -Encoding UTF8
 Start-ScheduledTask -TaskName $taskName
 Write-Host ('Started independent visible PowerShell task: '+$taskName)
 Write-Host ('Results: '+$package)
 return
}
function Python-Stage([string]$Stage){
 $file=[IO.FileStream]::new((Join-Path $package ($Stage+'.log')),[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::Read)
 $writer=[IO.StreamWriter]::new($file,[Text.UTF8Encoding]::new($false));$writer.AutoFlush=$true
 $previous=$ErrorActionPreference;$nativeExit=$null
 try{
  $ErrorActionPreference='Continue'
  & $configuration.PythonExecutable '-B' (Join-Path $package 'source/tools/canal_afferent_reference_gate.py') $Stage $package 2>&1 |
   ForEach-Object {$line=[string]$_;$writer.WriteLine($line);Write-Host $line} -ErrorAction Stop
  $nativeExit=$LASTEXITCODE
 }finally{$writer.Dispose();$ErrorActionPreference=$previous}
 if($null -eq $nativeExit -or $nativeExit -ne 0){throw ($Stage+'.log exited '+$nativeExit)}
}
$status=[ordered]@{Status='RUNNING';ExitCode=$null;Revision=$configuration.Revision;AnalyticControlsPassed=$false;
 IndependentReferenceAttempted=$false;IndependentReferencesCompleted=$false;SampledContinuousInputAccuracyPassed=$false;
 IdenticalHeldInputAccuracyQualified=$false;EventTimeAccuracyQualified=$false;LiveBrainAuthorityQualified=$false;
 NativeModelsExecuted=$false;LiveBrainAccessed=$false;OldSavesLoaded=$false;NativeBodyExecuted=$false;
 PendingCaptureAccessed=$false;PhysiologicalCalibrationQualified=$false;AvatarBalanceAssessed=$false;FullUnitSuitePassed=$false}
$statusPath=Join-Path $package 'window-status.json';$transcribing=$false
if(Test-Path -LiteralPath $statusPath){throw 'Refusing to overwrite earlier results.'}
function Save-Status([string]$Stage){
 $status['Stage']=$Stage
 $temporary=Join-Path $package 'window-status.tmp'
 $status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $temporary -Encoding UTF8
 Move-Item -LiteralPath $temporary -Destination $statusPath -Force
}
try{
 Start-Transcript -Path (Join-Path $package 'powershell.log') | Out-Null;$transcribing=$true
 $Host.UI.RawUI.WindowTitle='DNNE independent connected sensory reference'
 Write-Host ('Results: '+$package) -ForegroundColor Cyan
 Write-Host '15 analytic controls, six independent continuous references and all 2,293,760 saved native steps. Original initial state, equations and limits retained.'
 Write-Host 'Continuous forcing includes the native held-input error. Firing physiology and Avatar balance are separate. This window remains open after its final backup.'
 Save-Status 'preflight';Assert-Package;Python-Stage 'preflight'
 Save-Status 'analytic-controls';Python-Stage 'controls'
 $checks=Get-Content -LiteralPath (Join-Path $package 'analytic-controls.json') -Raw | ConvertFrom-Json
 if(-not $checks.successful -or -not $checks.exactInventory -or $checks.executed -ne 15 -or
  $checks.skipped.Count -ne 0 -or $checks.errors.Count -ne 0 -or $checks.failures.Count -ne 0){throw 'Analytic controls failed or incomplete.'}
 $status.AnalyticControlsPassed=$true
 $status.IndependentReferenceAttempted=$true
 Save-Status 'continuous-references';Python-Stage 'reference'
 $references=Get-Content -LiteralPath (Join-Path $package 'continuous-reference/results.json') -Raw | ConvertFrom-Json
 if(-not $references.completed -or $references.attempted -ne 6 -or $references.failedRuns.Count -ne 0){throw 'Independent references incomplete.'}
 $status.IndependentReferencesCompleted=$true
 Save-Status 'saved-trajectory-comparison';Python-Stage 'compare'
 $analysis=Get-Content -LiteralPath (Join-Path $package 'continuous-comparison/analysis.json') -Raw | ConvertFrom-Json
 if(-not $analysis.completed -or -not $analysis.sampledContinuousInputAccuracyPassed -or $analysis.comparedNativeSteps -ne 2293760){throw 'Sampled connected trajectory criteria failed or incomplete.'}
 foreach($key in @('originalBoundsChanged','identicalHeldInputAccuracyQualified','eventTimeAccuracyQualified',
  'physiologicalCalibrationQualified','liveBrainAuthorityQualified','avatarBalanceAssessed')){
  if($analysis.$key){throw ('Unexpected qualification: '+$key)}
 }
 $status.SampledContinuousInputAccuracyPassed=$true;$status.Status='DIAGNOSTICS_COMPLETE';$status.ExitCode=0
}catch{
 $status.Status='FAILED';$status.ExitCode=1;$status['Error']=$_.Exception.ToString()
 Write-Host ('FAILED: '+$_.Exception.Message) -ForegroundColor Red
}finally{
 if($status.Status -eq 'RUNNING'){$status.Status='INTERRUPTED';$status.ExitCode=2}
 Save-Status 'preservation'
 try{Python-Stage 'postflight';Assert-Package;$status['SourceInputsAndDependenciesPreserved']=$true}
 catch{$status.Status='FAILED';$status.ExitCode=1;$status.SampledContinuousInputAccuracyPassed=$false;$status['PreservationError']=$_.Exception.ToString()}
 $status['CompletedUtc']=[DateTime]::UtcNow.ToString('o')
 if($transcribing){Stop-Transcript | Out-Null}
 Save-Status 'complete-before-backup'
 try{& (Join-Path $package 'backup-run.ps1') -Source $package -Destination $configuration.BackupDestination}
 catch{$status.Status='FAILED';$status.ExitCode=1;$status['BackupError']=$_.Exception.ToString();Save-Status 'backup-failed';Write-Host ('BACKUP FAILED: '+$_.Exception.Message) -ForegroundColor Red}
 if($LaunchTaskName){Unregister-ScheduledTask -TaskName $LaunchTaskName -Confirm:$false -ErrorAction SilentlyContinue}
 Write-Host ('========== '+$status.Status+' ==========') -ForegroundColor Cyan
 Write-Host ('Results: '+$package)
 Write-Host 'Tell Orion the connected sensory comparison outcome. Sampled numerical accuracy, physiological firing, live brain authority and Avatar balance are separate.'
}
