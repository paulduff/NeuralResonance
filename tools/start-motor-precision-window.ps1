param([Parameter(Mandatory=$true)][string]$PackageDirectory,[switch]$InWindow,[string]$LaunchTaskName='')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
$package=(Resolve-Path -LiteralPath $PackageDirectory).Path
$configuration=Get-Content -LiteralPath (Join-Path $package 'configuration.json') -Raw | ConvertFrom-Json
if(-not $configuration.NoLiveMonitoring -or $configuration.Scenarios.Count -ne 4){throw 'Unexpected precision configuration.'}
if(-not $InWindow){
 if(Test-Path -LiteralPath (Join-Path $package 'launch.json')){throw 'Already launched; use a fresh package.'}
 $manifestPath=Join-Path $package 'backup-manifest.json'
 $manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
 if($manifest.Destination -ne $configuration.BackupDestination){throw 'Initial backup destination differs.'}
 if((Get-FileHash -LiteralPath $manifestPath).Hash -ne (Get-FileHash -LiteralPath (Join-Path $configuration.BackupDestination 'backup-manifest.json')).Hash){throw 'Initial backup manifests differ.'}
 foreach($entry in $manifest.Files){
  foreach($directory in @($package,$configuration.BackupDestination)){
   if((Get-FileHash -LiteralPath (Join-Path $directory $entry.Path)).Hash -ne $entry.Sha256){throw ('Initial backup or source differs: '+$entry.Path)}
  }
 }
 $taskName='DNNE-Motor-Precision-'+[Guid]::NewGuid().ToString('N').Substring(0,12)
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
 foreach($entry in $configuration.PackageFiles){if((Get-FileHash -LiteralPath (Join-Path $package $entry.Path)).Hash -ne $entry.Sha256){throw ('Frozen input/source changed: '+$entry.Path)}}
}
function Invoke-Logged([string]$Executable,[string[]]$NativeArguments,[string]$LogName){
 $writer=[IO.StreamWriter]::new((Join-Path $package $LogName),$false,[Text.UTF8Encoding]::new($false));$writer.AutoFlush=$true
 $previous=$ErrorActionPreference
 try {
  $ErrorActionPreference='Continue'
  & $Executable @NativeArguments 2>&1 | ForEach-Object {$line=[string]$_;$writer.WriteLine($line);Write-Host $line} -ErrorAction Stop
  $nativeExit=$LASTEXITCODE
 } finally {$writer.Dispose();$ErrorActionPreference=$previous}
 if($nativeExit -ne 0){throw ($LogName+' exited '+$nativeExit)}
}
$status=[ordered]@{Status='RUNNING';ExitCode=$null;Revision=$configuration.Revision;OriginalRevision=$configuration.OriginalRevision;
 LiveBrainAccessed=$false;OldSavesLoaded=$false;GpuExecuted=$false;SpeedupMeasured=$false;
 ActiveCandidateQualified=$false;PhysiologyQualified=$false;AvatarRecoveryAssessed=$false;FullUnitSuitePassed=$false;OriginalNumericalStatus='FAILED'}
$statusPath=Join-Path $package 'window-status.json';$transcribing=$false
if(Test-Path -LiteralPath $statusPath){throw 'Refusing to overwrite an earlier result.'}
try {
 Start-Transcript -Path (Join-Path $package 'powershell.log') | Out-Null;$transcribing=$true
 $Host.UI.RawUI.WindowTitle='DNNE motor-cell precision - saved-state arithmetic diagnosis'
 Write-Host ('Results: '+$package) -ForegroundColor Cyan
 Write-Host 'Fixed saved states only. No integration, parameter changes or new biological qualification. Report completion to Orion.'
 $status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
 Assert-Package
 $script=Join-Path $package 'source/tools/motoneuron-fixed-state-precision.py'
 Invoke-Logged $configuration.PythonExecutable @('-B',$script,$package,'--stage','select') 'select.log'
 $status['SelectionPassed']=$true
 $status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
 $build=Join-Path $package 'source/artifacts/motor-precision-build'
 Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package 'source/tools/MotoneuronReferenceProbe/MotoneuronReferenceProbe.csproj'),'-c','Release','--artifacts-path',$build,'--nologo','-v','minimal') 'build.log'
 $probe=Join-Path $build 'bin/MotoneuronReferenceProbe/release/NeuralResonanceEngine.DNNE.Tests.dll'
 $binaries=@(Get-ChildItem -LiteralPath (Split-Path -Parent $probe) -Recurse -File | ForEach-Object {[ordered]@{Path=$_.FullName;Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
 $binaries | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $package 'binaries.json') -Encoding UTF8
 Invoke-Logged $configuration.DotnetExecutable @($probe,'--precision',(Join-Path $package 'fixed-states.json'),(Join-Path $package 'candidate')) 'candidate.log'
 Invoke-Logged $configuration.PythonExecutable @('-B',$script,$package,'--stage','analyse') 'analysis.log'
 $analysis=Get-Content -LiteralPath (Join-Path $package 'precision-analysis.json') -Raw | ConvertFrom-Json
 if(-not $analysis.completed -or $analysis.scenarios.Count -ne 4 -or $analysis.evaluatedPoints -le 0 -or ($analysis.evaluatedPoints+$analysis.rejectedReferencePoints.Count) -ne $analysis.requestedPoints -or $analysis.originalNumericalStatus -ne 'FAILED' -or $analysis.odeSolved){throw 'Required diagnostic evidence incomplete.'}
 Assert-Package
 foreach($entry in $binaries){if((Get-FileHash -LiteralPath $entry.Path).Hash -ne $entry.Sha256){throw ('Binary changed: '+$entry.Path)}}
 $status['FixedStatesEvaluated']=$analysis.evaluatedPoints;$status['RejectedReferenceStates']=$analysis.rejectedReferencePoints.Count
 $status.Status='DIAGNOSTICS_COMPLETE';$status.ExitCode=0
} catch {
 $status.Status='FAILED';$status.ExitCode=1;$status['Error']=$_.Exception.ToString()
 Write-Host ('FAILED: '+$_.Exception.Message) -ForegroundColor Red
} finally {
 if($status.Status -eq 'RUNNING'){$status.Status='INTERRUPTED';$status.ExitCode=2;$status['Error']='Pipeline ended before all stages completed; cause not established.'}
 $status['CompletedUtc']=[DateTime]::UtcNow.ToString('o')
 if($transcribing){Stop-Transcript | Out-Null}
 $status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8
 try {& (Join-Path $package 'backup-run.ps1') -Source $package -Destination $configuration.BackupDestination}
 catch {$status.Status='FAILED';$status.ExitCode=1;$status['BackupError']=$_.Exception.ToString();$status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8;Write-Host ('BACKUP FAILED: '+$_.Exception.Message) -ForegroundColor Red}
 if($LaunchTaskName){Unregister-ScheduledTask -TaskName $LaunchTaskName -Confirm:$false -ErrorAction SilentlyContinue}
 Write-Host ('========== '+$status.Status+' ==========') -ForegroundColor Cyan
 Write-Host 'Tell Orion the motor-cell precision diagnosis outcome. Fixed-state arithmetic does not resolve integration accuracy, physiological calibration or Avatar recovery.'
}
