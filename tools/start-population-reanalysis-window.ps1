param([Parameter(Mandatory=$true)][string]$PackageDirectory,[switch]$InWindow,[string]$LaunchTaskName='')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
$package=(Resolve-Path -LiteralPath $PackageDirectory).Path
$configuration=Get-Content -LiteralPath (Join-Path $package 'configuration.json') -Raw | ConvertFrom-Json
if(-not $configuration.NoLiveMonitoring -or -not $configuration.SavedAnalysisOnly -or $configuration.ExpectedControlTests -ne 8){throw 'Unexpected saved-analysis protocol.'}
function Sha([string]$Path){(Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash}
function Assert-Package {
 foreach($entry in $configuration.PackageFiles){if((Sha (Join-Path $package $entry.Path)) -ne $entry.Sha256){throw ('Frozen input changed: '+$entry.Path)}}
 if((Sha $configuration.PythonExecutable) -ne $configuration.PythonExecutableSha256){throw 'Python executable changed.'}
}
if(-not $InWindow){
 if(Test-Path -LiteralPath (Join-Path $package 'launch.json')){throw 'Already launched; use a fresh package.'}
 $manifest=Get-Content -LiteralPath (Join-Path $package 'backup-manifest.json') -Raw | ConvertFrom-Json
 if($manifest.Destination -ne $configuration.BackupDestination -or (Sha (Join-Path $package 'backup-manifest.json')) -ne (Sha (Join-Path $configuration.BackupDestination 'backup-manifest.json'))){throw 'Initial backup identities differ.'}
 foreach($entry in $manifest.Files){foreach($directory in @($package,$configuration.BackupDestination)){
  if((Sha (Join-Path $directory $entry.Path)) -ne $entry.Sha256){throw ('Initial source/backup differs: '+$entry.Path)}
 }}
 Assert-Package
 $taskName='DNNE-Population-Reanalysis-'+[Guid]::NewGuid().ToString('N').Substring(0,12)
 $shell=Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/powershell.exe'
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
function Stage([string]$Name){
 $writer=[IO.StreamWriter]::new((Join-Path $package ($Name+'.log')),$true,[Text.UTF8Encoding]::new($false));$writer.AutoFlush=$true
 $previous=$ErrorActionPreference
 try{
  $ErrorActionPreference='Continue'
  & $configuration.PythonExecutable '-I' '-B' (Join-Path $package 'source/tools/population_reanalysis.py') $Name $package 2>&1 | ForEach-Object {
   $line=if($_ -is [System.Management.Automation.ErrorRecord]){[string]$_.Exception.Message}else{[string]$_};$writer.WriteLine($line);Write-Host $line
  } -ErrorAction Stop
  $nativeExit=$LASTEXITCODE
 }finally{$writer.Dispose();$ErrorActionPreference=$previous}
 if($nativeExit -ne 0){throw ($Name+'.log exited '+$nativeExit)}
}
$status=[ordered]@{Status='RUNNING';ExitCode=$null;Revision=$configuration.Revision;NoLiveMonitoring=$true;SavedAnalysisOnly=$true;
 ModelsExecuted=$false;NativeUnityExecuted=$false;OriginalBoundsChanged=$false;NeuronalAuthorityQualified=$false;AvatarStandingQualified=$false}
$statusPath=Join-Path $package 'window-status.json';$transcribing=$false
if(Test-Path -LiteralPath $statusPath){throw 'Refusing to overwrite earlier analysis.'}
try{
 Start-Transcript -Path (Join-Path $package 'powershell.log') | Out-Null;$transcribing=$true
 $Host.UI.RawUI.WindowTitle='DNNE saved population analysis - 8 receipt controls and 8 saved native cases'
 Write-Host ('Results: '+$package) -ForegroundColor Cyan
 Write-Host 'Reconcile the exact step/inspect receipts and all saved muscle/body/packet evidence. No Unity or muscle simulation rerun. Original failure and numerical limits retained.'
 $status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
 Assert-Package;Stage 'preservation';Stage 'controls';$status['PassedReceiptControls']=8
 Stage 'compare';$status['SavedPopulationAccountingPassed']=$true
 $status.Status='DIAGNOSTICS_COMPLETE';$status.ExitCode=0
}catch{$status.Status='FAILED';$status.ExitCode=1;$status['Error']=$_.Exception.ToString();Write-Host ('FAILED: '+$_.Exception.Message) -ForegroundColor Red}
finally{
 try{Stage 'preservation';Assert-Package;$status['OriginalCaptureAndBackupsPreserved']=$true}
 catch{$status.Status='FAILED';$status.ExitCode=1;$status['PreservationError']=$_.Exception.ToString()}
 $status['CompletedUtc']=[DateTime]::UtcNow.ToString('o')
 if($transcribing){Stop-Transcript | Out-Null}
 $status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8
 try{& (Join-Path $package 'backup-run.ps1') -Source $package -Destination $configuration.BackupDestination;if(-not $?){throw 'Backup failed.'}}
 catch{$status.Status='FAILED';$status.ExitCode=1;$status['BackupError']=$_.Exception.ToString();$status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8;Write-Host ('BACKUP FAILED: '+$_.Exception.Message) -ForegroundColor Red}
 if($LaunchTaskName){Unregister-ScheduledTask -TaskName $LaunchTaskName -Confirm:$false -ErrorAction SilentlyContinue}
 Write-Host ('========== '+$status.Status+' ==========') -ForegroundColor Cyan
 Write-Host ('Results: '+$package)
 Write-Host 'Tell Orion the saved population analysis outcome. Original causal/force accounting, coupled numerical accuracy, neuronal authority and Avatar standing are separate.'
}
