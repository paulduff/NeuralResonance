param([Parameter(Mandatory=$true)][string]$PackageDirectory,[switch]$InWindow,[string]$LaunchTaskName='')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
$package=(Resolve-Path -LiteralPath $PackageDirectory).Path
$configuration=Get-Content -LiteralPath (Join-Path $package 'configuration.json') -Raw | ConvertFrom-Json
if(-not $configuration.NoLiveMonitoring -or -not $configuration.IsolatedEulerianSpindle -or $configuration.ExpectedAnalyticControls -ne 12 -or $configuration.ExpectedFixedCases -ne 6 -or $configuration.ExpectedFixedContrasts -ne 18 -or $configuration.ExpectedMovingSolves -ne 42){throw 'Unexpected continuous spindle configuration.'}
function File-Sha256([string]$Path){
 $pathIo=[IO.Path]::GetFullPath($Path);if(-not $pathIo.StartsWith('\\?\')){$pathIo='\\?\'+$pathIo}
 $stream=[IO.File]::OpenRead($pathIo);$hash=[Security.Cryptography.SHA256]::Create()
 try{return [BitConverter]::ToString($hash.ComputeHash($stream)).Replace('-','')}
 finally{$hash.Dispose();$stream.Dispose()}
}
function Assert-Package{
 if((File-Sha256 $configuration.PythonExecutable) -ne $configuration.PythonExecutableSha256){throw 'Python executable changed.'}
 foreach($entry in $configuration.PackageFiles){if((File-Sha256 (Join-Path $package $entry.Path)) -ne $entry.Sha256){throw ('Frozen source/input changed: '+$entry.Path)}}
}
if(-not $InWindow){
 if(Test-Path -LiteralPath (Join-Path $package 'launch.json')){throw 'Already launched; use a fresh package.'}
 $manifestPath=Join-Path $package 'backup-manifest.json';$manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
 if($manifest.Destination -ne $configuration.BackupDestination){throw 'Backup destination differs.'}
 if((File-Sha256 $manifestPath) -ne (File-Sha256 (Join-Path $configuration.BackupDestination 'backup-manifest.json'))){throw 'Initial backup manifests differ.'}
 foreach($entry in $manifest.Files){foreach($directory in @($package,$configuration.BackupDestination)){
  if((File-Sha256 (Join-Path $directory $entry.Path)) -ne $entry.Sha256){throw ('Initial backup differs: '+$entry.Path)}
 }}
 $taskName='DNNE-Spindle-Eulerian-'+[Guid]::NewGuid().ToString('N').Substring(0,12)
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
function Invoke-Stage([string]$Stage){
 $logName='eulerian-'+$Stage+'.log'
 $writer=[IO.StreamWriter]::new((Join-Path $package $logName),$false,[Text.UTF8Encoding]::new($false));$writer.AutoFlush=$true
 try{
  $ErrorActionPreference='Continue'
  & $configuration.PythonExecutable -B (Join-Path $package 'source/tools/analyse-spindle-eulerian.py') $package --stage $Stage 2>&1 | ForEach-Object {$line=[string]$_;$writer.WriteLine($line);Write-Host $line} -ErrorAction Stop
  $nativeExit=$LASTEXITCODE
 }finally{$writer.Dispose();$ErrorActionPreference='Stop'}
 if($nativeExit -ne 0){throw ($logName+' exited '+$nativeExit)}
}
$status=[ordered]@{Status='RUNNING';ExitCode=$null;Revision=$configuration.Revision;ComponentChecksPassed=$false;
 SampledReferenceDiscretizationCriteriaPassed=$false;CandidateMovingAccuracyQualified=$false;SlackTrajectoryAssessed=$false;
 SensoryFiringAssessed=$false;LiveBrainAccessed=$false;PhysiologyQualified=$false;AvatarRecoveryAssessed=$false}
$statusPath=Join-Path $package 'window-status.json';$transcribing=$false
if(Test-Path -LiteralPath $statusPath){throw 'Refusing to overwrite an earlier result.'}
try{
 Start-Transcript -Path (Join-Path $package 'powershell.log') | Out-Null;$transcribing=$true
 $Host.UI.RawUI.WindowTitle='DNNE independent continuous spindle reference'
 Write-Host ('Results: '+$package) -ForegroundColor Cyan
 Write-Host '12 controls, six fixed cases, and 42 continuous movement solves with temporal/spatial/domain checks. No live brain/body.'
 $status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
 Assert-Package
 foreach($stage in @('preflight','controls','fixed','moving','compare')){
  Write-Host ('Stage: '+$stage) -ForegroundColor Cyan
  Invoke-Stage $stage
 }
 $analysis=Get-Content -LiteralPath (Join-Path $package 'eulerian-analysis.json') -Raw | ConvertFrom-Json
 if(-not $analysis.completed -or -not $analysis.componentChecksPassed -or $analysis.analyticControls -ne 12 -or $analysis.fixedCases -ne 6 -or $analysis.fixedReferenceContrasts -ne 18 -or $analysis.movingSolves -ne 42 -or $analysis.temporalContrasts.Count -ne 12 -or $analysis.spatialContrasts.Count -ne 18 -or $analysis.rangeContrasts.Count -ne 6 -or $analysis.candidateContrasts.Count -ne 18){throw 'Continuous reference checks failed or incomplete.'}
 foreach($flag in @('referenceIsExact','candidateMovingAccuracyQualified','slackAssessed','sensoryFiringAssessed','physiologyQualified','avatarRecoveryAssessed')){if($analysis.$flag){throw ('Unexpected qualification: '+$flag)}}
 Assert-Package
 $status.SampledReferenceDiscretizationCriteriaPassed=[bool]$analysis.sampledReferenceDiscretizationCriteriaPassed
 Write-Host ('Reference discretization criteria passed: '+$status.SampledReferenceDiscretizationCriteriaPassed) -ForegroundColor Yellow
 Write-Host 'Diagnostic completion does not imply the spatial reference is accurate enough; inspect its separate criteria.'
 $status.ComponentChecksPassed=$true;$status.Status='DIAGNOSTICS_COMPLETE';$status.ExitCode=0
}catch{
 $status.Status='FAILED';$status.ExitCode=1;$status['Error']=$_.Exception.ToString()
 Write-Host ('FAILED: '+$_.Exception.Message) -ForegroundColor Red
}finally{
 if($status.Status -eq 'RUNNING'){$status.Status='INTERRUPTED';$status.ExitCode=2}
 if(Test-Path -LiteralPath (Join-Path $package 'reference-preflight.json')){
  try{Invoke-Stage 'postflight';$status['NumericalEnvironmentUnchanged']=$true}
  catch{$status.Status='FAILED';$status.ExitCode=1;$status.ComponentChecksPassed=$false;$status['NumericalEnvironmentUnchanged']=$false;$status['EnvironmentError']=$_.Exception.ToString()}
 }
 try{Assert-Package;$status['FrozenPackageUnchanged']=$true}
 catch{$status.Status='FAILED';$status.ExitCode=1;$status.ComponentChecksPassed=$false;$status['FrozenPackageError']=$_.Exception.ToString()}
 $status['CompletedUtc']=[DateTime]::UtcNow.ToString('o')
 if($transcribing){Stop-Transcript | Out-Null}
 $status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8
 try{& (Join-Path $package 'backup-run.ps1') -Source $package -Destination $configuration.BackupDestination}
 catch{$status.Status='FAILED';$status.ExitCode=1;$status['BackupError']=$_.Exception.ToString();$status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8;Write-Host ('BACKUP FAILED: '+$_.Exception.Message) -ForegroundColor Red}
 if($LaunchTaskName){Unregister-ScheduledTask -TaskName $LaunchTaskName -Confirm:$false -ErrorAction SilentlyContinue}
 Write-Host ('========== '+$status.Status+' ==========') -ForegroundColor Cyan
 Write-Host ('Results: '+$package)
 Write-Host 'Tell Orion the continuous spindle reference outcome. Temporal/spatial reference accuracy, candidate accuracy, sensory physiology and Avatar recovery are separate.'
}
