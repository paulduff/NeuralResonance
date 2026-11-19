param([Parameter(Mandatory=$true)][string]$PackageDirectory,[switch]$InWindow,[string]$LaunchTaskName='')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
$package=(Resolve-Path -LiteralPath $PackageDirectory).Path
$configuration=Get-Content -LiteralPath (Join-Path $package 'configuration.json') -Raw | ConvertFrom-Json
if(-not $configuration.IsolatedCanalWaveform -or -not $configuration.NoLiveMonitoring -or $configuration.ExpectedQueries -ne 1060 -or
 $configuration.NeuronalModelsExecuted -or $configuration.LiveSelectionChanged -or $configuration.PhysiologyQualified -or $configuration.AvatarRecoveryAssessed){throw 'Unexpected independent waveform configuration.'}
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
 foreach($entry in $configuration.PackageFiles){if((File-Sha256 (Join-Path $package $entry.Path)) -ne $entry.Sha256){throw ('Frozen input changed: '+$entry.Path)}}
 if((File-Sha256 $configuration.PythonExecutable) -ne $configuration.PythonExecutableSha256){throw 'Python executable changed.'}
}
if(-not $InWindow){
 if(Test-Path -LiteralPath (Join-Path $package 'launch.json')){throw 'Already launched; use a fresh package.'}
 $manifestPath=Join-Path $package 'backup-manifest.json';$manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
 if($manifest.Destination -ne $configuration.BackupDestination){throw 'Initial backup destination differs.'}
 if((File-Sha256 $manifestPath) -ne (File-Sha256 (Join-Path $configuration.BackupDestination 'backup-manifest.json'))){throw 'Initial backup manifests differ.'}
 foreach($entry in $manifest.Files){foreach($directory in @($package,$configuration.BackupDestination)){
  if((File-Sha256 (Join-Path $directory $entry.Path)) -ne $entry.Sha256){throw ('Initial backup differs: '+$entry.Path)}
 }}
 Assert-Package
 $taskName='DNNE-Canal-Waveform-'+[Guid]::NewGuid().ToString('N').Substring(0,12)
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
function Python-Stage([string]$Stage){
 $writer=[IO.StreamWriter]::new((Join-Path $package ($Stage+'.log')),$false,[Text.UTF8Encoding]::new($false));$writer.AutoFlush=$true
 $previous=$ErrorActionPreference
 try{
  $ErrorActionPreference='Continue'
  & $configuration.PythonExecutable '-B' (Join-Path $package 'source/tools/canal_waveform_gate.py') $Stage $package 2>&1 |
   ForEach-Object {$line=[string]$_;$writer.WriteLine($line);Write-Host $line} -ErrorAction Stop
  $nativeExit=$LASTEXITCODE
 }finally{$writer.Dispose();$ErrorActionPreference=$previous}
 if($nativeExit -ne 0){throw ($Stage+'.log exited '+$nativeExit)}
}
$status=[ordered]@{Status='RUNNING';ExitCode=$null;Revision=$configuration.Revision;AnalyticControlsPassed=$false;
 IndependentWaveformCriteriaPassed=$false;CoupledSensoryAccuracyQualified=$false;NeuronalModelsExecuted=$false;
 LiveBrainAccessed=$false;OldSavesLoaded=$false;NativeBodyExecuted=$false;PendingCaptureAccessed=$false;
 PhysiologyQualified=$false;AvatarRecoveryAssessed=$false;FullUnitSuitePassed=$false}
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
 $Host.UI.RawUI.WindowTitle='DNNE independent continuous canal input'
 Write-Host ('Results: '+$package) -ForegroundColor Cyan
 Write-Host '15 input controls, 6 Decimal exponential controls and 1,060 timed mechanical queries. No neuronal model or pending capture is accessed.'
 Save-Status 'preflight';Assert-Package;Python-Stage 'preflight'
 Save-Status 'analytic-controls';Python-Stage 'controls'
 $checks=Get-Content -LiteralPath (Join-Path $package 'analytic-controls/input-controls.json') -Raw | ConvertFrom-Json
 $exponential=Get-Content -LiteralPath (Join-Path $package 'analytic-controls/exponential/controls.json') -Raw | ConvertFrom-Json
 if(-not $checks.successful -or $checks.executed -ne 15 -or $checks.skipped.Count -ne 0 -or $exponential.passed -ne 6){throw 'Analytic controls incomplete.'}
 $status.AnalyticControlsPassed=$true
 Save-Status 'independent-waveform';Python-Stage 'study'
 $analysis=Get-Content -LiteralPath (Join-Path $package 'analysis.json') -Raw | ConvertFrom-Json
 if(-not $analysis.completed -or -not $analysis.waveformCriteriaPassed -or $analysis.queries -ne 1060){throw 'Independent waveform criteria incomplete.'}
 foreach($key in @('pendingNativeCaptureAccessed','neuronalModelsExecuted','coupledSensoryAccuracyQualified','anatomicalCalibrationQualified','afferentFiringQualified','avatarBalanceAssessed')){
  if($analysis.$key){throw ('Unexpected qualification: '+$key)}
 }
 $status.IndependentWaveformCriteriaPassed=$true;$status.Status='DIAGNOSTICS_COMPLETE';$status.ExitCode=0
}catch{
 $status.Status='FAILED';$status.ExitCode=1;$status['Error']=$_.Exception.ToString()
 Write-Host ('FAILED: '+$_.Exception.Message) -ForegroundColor Red
}finally{
 if($status.Status -eq 'RUNNING'){$status.Status='INTERRUPTED';$status.ExitCode=2}
 Save-Status 'preservation'
 try{Python-Stage 'postflight';Assert-Package;$status['SourceAndDependenciesPreserved']=$true}
 catch{$status.Status='FAILED';$status.ExitCode=1;$status.IndependentWaveformCriteriaPassed=$false;$status['PreservationError']=$_.Exception.ToString()}
 $status['CompletedUtc']=[DateTime]::UtcNow.ToString('o')
 if($transcribing){Stop-Transcript | Out-Null}
 Save-Status 'complete-before-backup'
 try{& (Join-Path $package 'backup-run.ps1') -Source $package -Destination $configuration.BackupDestination}
 catch{$status.Status='FAILED';$status.ExitCode=1;$status['BackupError']=$_.Exception.ToString();Save-Status 'backup-failed';Write-Host ('BACKUP FAILED: '+$_.Exception.Message) -ForegroundColor Red}
 if($LaunchTaskName){Unregister-ScheduledTask -TaskName $LaunchTaskName -Confirm:$false -ErrorAction SilentlyContinue}
 Write-Host ('========== '+$status.Status+' ==========') -ForegroundColor Cyan
 Write-Host ('Results: '+$package)
 Write-Host 'Tell Orion the independent canal input outcome. Sampled waveform accuracy, connected sensory accuracy, physiology and Avatar balance are separate.'
}
