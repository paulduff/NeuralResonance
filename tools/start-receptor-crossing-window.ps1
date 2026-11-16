param([Parameter(Mandatory=$true)][string]$PackageDirectory,[switch]$InWindow,[string]$LaunchTaskName='')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
$package=(Resolve-Path -LiteralPath $PackageDirectory).Path
$configuration=Get-Content -LiteralPath (Join-Path $package 'configuration.json') -Raw | ConvertFrom-Json
if(-not $configuration.NoLiveMonitoring -or -not $configuration.IsolatedReceptorCrossing -or $configuration.ExpectedConditions -ne 18 -or $configuration.ExpectedAnalyticControls -ne 5 -or $configuration.ExpectedEndpointPairs -le 0){throw 'Unexpected crossing diagnostic configuration.'}
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
 $manifestPath=Join-Path $package 'backup-manifest.json'
 $manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
 if($manifest.Destination -ne $configuration.BackupDestination){throw 'Backup destination differs.'}
 if((File-Sha256 $manifestPath) -ne (File-Sha256 (Join-Path $configuration.BackupDestination 'backup-manifest.json'))){throw 'Backup manifests differ.'}
 foreach($entry in $manifest.Files){foreach($directory in @($package,$configuration.BackupDestination)){
  if((File-Sha256 (Join-Path $directory $entry.Path)) -ne $entry.Sha256){throw ('Initial backup differs: '+$entry.Path)}
 }}
 $taskName='DNNE-Receptor-Crossing-'+[Guid]::NewGuid().ToString('N').Substring(0,12)
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
$status=[ordered]@{Status='RUNNING';ExitCode=$null;Revision=$configuration.Revision;DiagnosticCriteriaPassed=$false;
 DerivativeEvaluationAttempted=$false;KineticsIntegrated=$false;ProductionImplementationChanged=$false;
 LiveBrainAccessed=$false;PresynapticNeuronalAuthorityQualified=$false;PhysiologyQualified=$false;AvatarRecoveryAssessed=$false}
$statusPath=Join-Path $package 'window-status.json';$transcribing=$false
if(Test-Path -LiteralPath $statusPath){throw 'Refusing to overwrite an earlier result.'}
try{
 Start-Transcript -Path (Join-Path $package 'powershell.log') | Out-Null;$transcribing=$true
 $Host.UI.RawUI.WindowTitle='DNNE receptor crossing-time diagnosis'
 Write-Host ('Results: '+$package) -ForegroundColor Cyan
 Write-Host 'Checking five analytic controls and saved crossing brackets from 18 completed conditions. No ODE rerun or live brain/body.'
 $status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
 Assert-Package
 $status.DerivativeEvaluationAttempted=$true
 $writer=[IO.StreamWriter]::new((Join-Path $package 'crossing.log'),$false,[Text.UTF8Encoding]::new($false));$writer.AutoFlush=$true
 try{
  $ErrorActionPreference='Continue'
  & $configuration.PythonExecutable -B (Join-Path $package 'source/tools/receptor_crossing_study.py') run --package $package 2>&1 | ForEach-Object {$line=[string]$_;$writer.WriteLine($line);Write-Host $line} -ErrorAction Stop
  $nativeExit=$LASTEXITCODE
 }finally{$writer.Dispose();$ErrorActionPreference='Stop'}
 if($nativeExit -ne 0){throw ('crossing.log exited '+$nativeExit)}
 $analysis=Get-Content -LiteralPath (Join-Path $package 'crossing-analysis.json') -Raw | ConvertFrom-Json
 if(-not $analysis.completed -or -not $analysis.diagnosticCriteriaPassed -or -not $analysis.frozenInputsAndEnvironmentPreserved -or $analysis.controls.Count -ne 5 -or $analysis.conditions.Count -ne 18 -or $analysis.rows.Count -ne $configuration.ExpectedEndpointPairs -or $analysis.refinement.Count -ne 12){throw 'Crossing diagnostic failed or incomplete.'}
 foreach($flag in @('kineticsIntegrated','productionImplementationChanged','presynapticNeuronalAuthorityQualified','physiologyQualified','avatarRecoveryAssessed')){if($analysis.$flag){throw ('Unexpected qualification: '+$flag)}}
 Assert-Package
 $status.DiagnosticCriteriaPassed=$true;$status.Status='DIAGNOSTICS_COMPLETE';$status.ExitCode=0
}catch{
 $status.Status='FAILED';$status.ExitCode=1;$status['Error']=$_.Exception.ToString()
 Write-Host ('FAILED: '+$_.Exception.Message) -ForegroundColor Red
}finally{
 if($status.Status -eq 'RUNNING'){$status.Status='INTERRUPTED';$status.ExitCode=2}
 try{Assert-Package;$status['FrozenPackageUnchanged']=$true}
 catch{$status.Status='FAILED';$status.ExitCode=1;$status.DiagnosticCriteriaPassed=$false;$status['FrozenPackageError']=$_.Exception.ToString()}
 $status['CompletedUtc']=[DateTime]::UtcNow.ToString('o')
 if($transcribing){Stop-Transcript | Out-Null}
 $status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8
 try{& (Join-Path $package 'backup-run.ps1') -Source $package -Destination $configuration.BackupDestination}
 catch{$status.Status='FAILED';$status.ExitCode=1;$status['BackupError']=$_.Exception.ToString();$status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8;Write-Host ('BACKUP FAILED: '+$_.Exception.Message) -ForegroundColor Red}
 if($LaunchTaskName){Unregister-ScheduledTask -TaskName $LaunchTaskName -Confirm:$false -ErrorAction SilentlyContinue}
 Write-Host ('========== '+$status.Status+' ==========') -ForegroundColor Cyan
 Write-Host ('Results: '+$package)
 Write-Host 'Tell Orion the crossing-time diagnosis outcome. Saved-endpoint accuracy, native event production, physiological calibration and Avatar recovery are separate.'
}
