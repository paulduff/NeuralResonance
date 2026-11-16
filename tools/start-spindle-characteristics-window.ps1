param([Parameter(Mandatory=$true)][string]$PackageDirectory,[switch]$InWindow,[string]$LaunchTaskName='')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
$package=(Resolve-Path -LiteralPath $PackageDirectory).Path
$configuration=Get-Content -LiteralPath (Join-Path $package 'configuration.json') -Raw | ConvertFrom-Json
if(-not $configuration.NoLiveMonitoring -or -not $configuration.IsolatedSpindleCharacteristics -or $configuration.ExpectedAnalyticControls -ne 12 -or $configuration.ExpectedStateQueries -ne 25308 -or $configuration.ExpectedHistoryCases -ne 144 -or $configuration.ExpectedCadenceComparisons -ne 96 -or $configuration.ExpectedContinuityProbes -lt 31){throw 'Unexpected transport configuration.'}
function File-Sha256([string]$Path){
 $pathIo=[IO.Path]::GetFullPath($Path);if(-not $pathIo.StartsWith('\\?\')){$pathIo='\\?\'+$pathIo}
 $stream=[IO.File]::OpenRead($pathIo);$hash=[Security.Cryptography.SHA256]::Create()
 try{return [BitConverter]::ToString($hash.ComputeHash($stream)).Replace('-','')}
 finally{$hash.Dispose();$stream.Dispose()}
}
function Assert-Package{
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
 $taskName='DNNE-Spindle-Characteristics-'+[Guid]::NewGuid().ToString('N').Substring(0,12)
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
 $logName='characteristics-'+$Stage+'.log'
 $writer=[IO.StreamWriter]::new((Join-Path $package $logName),$false,[Text.UTF8Encoding]::new($false));$writer.AutoFlush=$true
 try{
  $ErrorActionPreference='Continue'
  & $configuration.PythonExecutable -B (Join-Path $package 'source/tools/analyse-spindle-characteristics.py') $package --stage $Stage 2>&1 | ForEach-Object {$line=[string]$_;$writer.WriteLine($line);Write-Host $line} -ErrorAction Stop
  $nativeExit=$LASTEXITCODE
 }finally{$writer.Dispose();$ErrorActionPreference='Stop'}
 if($nativeExit -ne 0){throw ($logName+' exited '+$nativeExit)}
}
$status=[ordered]@{Status='RUNNING';ExitCode=$null;Revision=$configuration.Revision;TransportNumericsPassed=$false;
 KineticsExecuted=$false;NativeRerun=$false;RootSelected=$false;LiveBrainAccessed=$false;
 SlackTrajectoryAssessed=$false;SensoryFiringAssessed=$false;PhysiologyQualified=$false;AvatarRecoveryAssessed=$false}
$statusPath=Join-Path $package 'window-status.json';$transcribing=$false
if(Test-Path -LiteralPath $statusPath){throw 'Refusing to overwrite an earlier result.'}
try{
 Start-Transcript -Path (Join-Path $package 'powershell.log') | Out-Null;$transcribing=$true
 $Host.UI.RawUI.WindowTitle='DNNE retained spindle population'
 Write-Host ('Results: '+$package) -ForegroundColor Cyan
 Write-Host '12 controls, 25,308 saved translations, 144 exact-shape histories and 96 movement-cadence contrasts. No new kinetics or live body.'
 $status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
 Assert-Package
 foreach($stage in @('preflight','controls','matrix','continuity','histories','summarise')){
  Write-Host ('Stage: '+$stage) -ForegroundColor Cyan
  Invoke-Stage $stage
 }
 $analysis=Get-Content -LiteralPath (Join-Path $package 'characteristic-analysis.json') -Raw | ConvertFrom-Json
 if(-not $analysis.completed -or -not $analysis.transportNumericsPassed -or $analysis.analyticControls -ne 12 -or $analysis.savedStateQueries -ne 25308 -or $analysis.noRootStatesProbed -ne 31 -or $analysis.continuityProbes -ne $configuration.ExpectedContinuityProbes -or $analysis.historyCases -ne 144 -or $analysis.cadenceComparisons -ne 96){throw 'Transport comparison failed or incomplete.'}
 foreach($flag in @('originalBoundsChanged','kineticsExecuted','rootSelected','slackTrajectoryAssessed','sensoryFiringAssessed','physiologyQualified','avatarRecoveryAssessed')){if($analysis.$flag){throw ('Unexpected qualification: '+$flag)}}
 Assert-Package
 $status.TransportNumericsPassed=$true;$status.Status='DIAGNOSTICS_COMPLETE';$status.ExitCode=0
}catch{
 $status.Status='FAILED';$status.ExitCode=1;$status['Error']=$_.Exception.ToString()
 Write-Host ('FAILED: '+$_.Exception.Message) -ForegroundColor Red
}finally{
 if($status.Status -eq 'RUNNING'){$status.Status='INTERRUPTED';$status.ExitCode=2}
 if(Test-Path -LiteralPath (Join-Path $package 'reference-preflight.json')){
  try{Invoke-Stage 'postflight';$status['NumericalEnvironmentUnchanged']=$true}
  catch{$status.Status='FAILED';$status.ExitCode=1;$status.TransportNumericsPassed=$false;$status['NumericalEnvironmentUnchanged']=$false;$status['EnvironmentError']=$_.Exception.ToString()}
 }
 try{Assert-Package;$status['FrozenPackageUnchanged']=$true}
 catch{$status.Status='FAILED';$status.ExitCode=1;$status.TransportNumericsPassed=$false;$status['FrozenPackageError']=$_.Exception.ToString()}
 $status['CompletedUtc']=[DateTime]::UtcNow.ToString('o')
 if($transcribing){Stop-Transcript | Out-Null}
 $status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8
 try{& (Join-Path $package 'backup-run.ps1') -Source $package -Destination $configuration.BackupDestination}
 catch{$status.Status='FAILED';$status.ExitCode=1;$status['BackupError']=$_.Exception.ToString();$status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8;Write-Host ('BACKUP FAILED: '+$_.Exception.Message) -ForegroundColor Red}
 if($LaunchTaskName){Unregister-ScheduledTask -TaskName $LaunchTaskName -Confirm:$false -ErrorAction SilentlyContinue}
 Write-Host ('========== '+$status.Status+' ==========') -ForegroundColor Cyan
 Write-Host ('Results: '+$package)
 Write-Host 'Tell Orion the retained spindle population outcome. Shape and force accuracy, coupled kinetics, sensory physiology and Avatar recovery are separate.'
}
