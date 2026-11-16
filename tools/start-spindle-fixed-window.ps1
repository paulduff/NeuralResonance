param([Parameter(Mandatory=$true)][string]$PackageDirectory,[switch]$InWindow,[string]$LaunchTaskName='')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
$package=(Resolve-Path -LiteralPath $PackageDirectory).Path
$configuration=Get-Content -LiteralPath (Join-Path $package 'configuration.json') -Raw | ConvertFrom-Json
if(-not $configuration.NoLiveMonitoring -or -not $configuration.IsolatedSpindleFixedComparison){throw 'Unexpected spindle comparison configuration.'}
if($configuration.ExpectedNativeTrajectories -ne 18 -or $configuration.ExpectedReferenceSolves -ne 18 -or $configuration.ExpectedAnalyticControls -ne 6 -or $configuration.ExpectedReferenceComparisons -ne 18 -or $configuration.ExpectedCandidateComparisons -ne 54){throw 'Unexpected comparison coverage.'}
foreach($flag in @('NativeRerun','LiveBodySelected','MovingLengthAssessed','SlackAssessed','AfferentFiringAssessed','PhysiologyQualified','AvatarRecoveryAssessed')){if($configuration.$flag){throw ('Unexpected qualification: '+$flag)}}
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
 $taskName='DNNE-Spindle-Fixed-'+[Guid]::NewGuid().ToString('N').Substring(0,12)
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
  & $configuration.PythonExecutable -B (Join-Path $package 'source/tools/analyse-spindle-fixed.py') $package --stage $Stage 2>&1 | ForEach-Object {$line=[string]$_;$writer.WriteLine($line);Write-Host $line} -ErrorAction Stop
  $nativeExit=$LASTEXITCODE
 }finally{$writer.Dispose();$ErrorActionPreference=$previous}
 if($nativeExit -ne 0){throw ($logName+' exited '+$nativeExit)}
}
$status=[ordered]@{Status='RUNNING';ExitCode=$null;Revision=$configuration.Revision;LiveBrainAccessed=$false;OldSavesLoaded=$false;
 NativeRerun=$false;ReferenceControlsPassed=$false;ReferenceSolvesAttempted=$false;CapturedFixedLengthAccuracyPassed=$false;
 MovingLengthAssessed=$false;SlackAssessed=$false;AfferentFiringAssessed=$false;PhysiologyQualified=$false;AvatarRecoveryAssessed=$false}
$statusPath=Join-Path $package 'window-status.json';$transcribing=$false
if(Test-Path -LiteralPath $statusPath){throw 'Refusing to overwrite an earlier result.'}
try{
 Start-Transcript -Path (Join-Path $package 'powershell.log') | Out-Null;$transcribing=$true
 $Host.UI.RawUI.WindowTitle='DNNE fixed-length spindle accuracy'
 Write-Host ('Results: '+$package) -ForegroundColor Cyan
 Write-Host 'Reusing 18 completed spindle traces. Checking 6 analytic controls, the analytic Jacobian and 18 independent reference solves. No live Avatar or native rerun.'
 $status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
 Assert-Package
 Invoke-Stage 'preflight'
 Invoke-Stage 'controls'
 $controls=Get-Content -LiteralPath (Join-Path $package 'reference-controls/controls.json') -Raw | ConvertFrom-Json
 if(-not $controls.completed -or -not $controls.passed -or $controls.analytic.Count -ne 6 -or $controls.physiologicalControls){throw 'Reference controls failed or incomplete.'}
 $status.ReferenceControlsPassed=$true;$status.ReferenceSolvesAttempted=$true
 Invoke-Stage 'solve'
 Invoke-Stage 'audit'
 $analysis=Get-Content -LiteralPath (Join-Path $package 'spindle-fixed-analysis.json') -Raw | ConvertFrom-Json
 if(-not $analysis.completed -or -not $analysis.referenceAccuracyPassed -or -not $analysis.capturedFixedLengthAccuracyPassed -or $analysis.nativeTrajectories -ne 18 -or $analysis.references.Count -ne 18 -or $analysis.referenceComparisons.Count -ne 18 -or $analysis.candidateComparisons.Count -ne 54){throw 'Fixed-length comparison failed or incomplete.'}
 foreach($flag in @('nativeRerun','movingLengthAssessed','slackAssessed','afferentFiringAssessed','physiologicalCalibrationAssessed','liveBodyIntegrated','avatarRecoveryAssessed')){if($analysis.$flag){throw ('Unexpected qualification: '+$flag)}}
 Assert-Package
 $status.CapturedFixedLengthAccuracyPassed=$true;$status.Status='DIAGNOSTICS_COMPLETE';$status.ExitCode=0
}catch{
 $status.Status='FAILED';$status.ExitCode=1;$status['Error']=$_.Exception.ToString()
 Write-Host ('FAILED: '+$_.Exception.Message) -ForegroundColor Red
}finally{
 if($status.Status -eq 'RUNNING'){$status.Status='INTERRUPTED';$status.ExitCode=2;$status['Error']='Stopped before completing the study; cause not established.'}
 if(Test-Path -LiteralPath (Join-Path $package 'reference-preflight.json')){
  try{Invoke-Stage 'postflight';$status['ReferenceEnvironmentUnchanged']=$true}
  catch{$status.Status='FAILED';$status.ExitCode=1;$status.CapturedFixedLengthAccuracyPassed=$false;$status['ReferenceEnvironmentUnchanged']=$false;$status['ReferenceEnvironmentError']=$_.Exception.ToString();Write-Host ('REFERENCE ENVIRONMENT FAILED: '+$_.Exception.Message) -ForegroundColor Red}
 }
 try{Assert-Package;$status['FrozenPackageUnchanged']=$true}
 catch{$status.Status='FAILED';$status.ExitCode=1;$status.CapturedFixedLengthAccuracyPassed=$false;$status['FrozenPackageUnchanged']=$false;$status['FrozenPackageError']=$_.Exception.ToString()}
 $status['CompletedUtc']=[DateTime]::UtcNow.ToString('o')
 if($transcribing){Stop-Transcript | Out-Null}
 $status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8
 try{& (Join-Path $package 'backup-run.ps1') -Source $package -Destination $configuration.BackupDestination}
 catch{$status.Status='FAILED';$status.ExitCode=1;$status['BackupError']=$_.Exception.ToString();$status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8;Write-Host ('BACKUP FAILED: '+$_.Exception.Message) -ForegroundColor Red}
 if($LaunchTaskName){Unregister-ScheduledTask -TaskName $LaunchTaskName -Confirm:$false -ErrorAction SilentlyContinue}
 Write-Host ('========== '+$status.Status+' ==========') -ForegroundColor Cyan
 Write-Host ('Results: '+$package)
 Write-Host 'Tell Orion the fixed-length spindle outcome. Numerical accuracy, moving/slack mechanics, afferent firing and Avatar recovery are separate.'
}
