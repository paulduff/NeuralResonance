param(
 [Parameter(Mandatory=$true)][string]$PackageDirectory,
 [switch]$InWindow,
 [string]$LaunchTaskName=''
)
. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
$package=(Resolve-Path -LiteralPath $PackageDirectory).Path
$configuration=Get-Content -LiteralPath (Join-Path $package 'configuration.json') -Raw | ConvertFrom-Json
if(-not $InWindow){
 if(Test-Path -LiteralPath (Join-Path $package 'launch.json')){throw 'This package has already been launched; use a new package.'}
 $taskName='DNNE-Fresh-Postural-'+[Guid]::NewGuid().ToString('N').Substring(0,12)
 $shell=Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
 $arguments=@('-NoLogo','-NoProfile','-NoExit','-ExecutionPolicy','Bypass','-File',$PSCommandPath,
  '-InWindow','-LaunchTaskName',$taskName,'-PackageDirectory',$package)
 $quoted=($arguments | ForEach-Object {ConvertTo-ProcessArgument $_}) -join ' '
 $action=New-ScheduledTaskAction -Execute $shell -Argument $quoted -WorkingDirectory $configuration.Repository
 $principal=New-ScheduledTaskPrincipal -UserId ([Security.Principal.WindowsIdentity]::GetCurrent().Name) -LogonType Interactive -RunLevel Limited
 $settings=New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::FromHours(12)) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
 Register-ScheduledTask -TaskName $taskName -Action $action -Principal $principal -Settings $settings | Out-Null
 [ordered]@{TaskName=$taskName;RequestedUtc=[DateTime]::UtcNow.ToString('o');Package=$package;NoLiveMonitoring=$true} |
  ConvertTo-Json | Set-Content -LiteralPath (Join-Path $package 'launch.json') -Encoding UTF8
 Start-ScheduledTask -TaskName $taskName
 Write-Host ('Started independent visible PowerShell task: '+$taskName)
 Write-Host ('Results: '+$package)
 return
}

$status=[ordered]@{Status='RUNNING';ExitCode=$null;Revision=$configuration.Revision;Output=$package;
 FreshOnly=$true;LiveBrainAccessed=$false;PhysicalRecoveryAssessed=$false}
$statusPath=Join-Path $package 'window-status.json'
$transcribing=$false
function Assert-FileHashes($entries){
 foreach($entry in $entries){
  if((Get-FileHash -LiteralPath $entry.Path -Algorithm SHA256).Hash -ne $entry.Sha256){
   throw ('Protected or packaged file changed: '+$entry.Path)
  }
 }
}
function Invoke-ProbeStage([string]$name,[switch]$Smoke){
 $output=Join-Path $package $name
 if(Test-Path -LiteralPath $output){throw ('Refusing to reuse neural state: '+$output)}
 Assert-FileHashes $configuration.PackageFiles
 $arguments=@($configuration.ProbeAssembly,$output,$configuration.SourceRun,$configuration.SynapseBackup,$configuration.ProbeRepository,'--transfer','--fresh-only')
 if($Smoke){$arguments+='--smoke'}
 Write-Host ('========== '+$name.ToUpperInvariant()+' ==========') -ForegroundColor Cyan
 & $configuration.DotnetExecutable @arguments
 if($LASTEXITCODE -ne 0){throw ($name+' failed; inspect '+(Join-Path $output 'STUDY_FAILED.json'))}
 & $configuration.PythonExecutable (Join-Path $package 'analysis.py') $output
 if($LASTEXITCODE -ne 0){throw ($name+' accounting/state validation failed.')}
 $report=Get-Content -LiteralPath (Join-Path $output 'results.json') -Raw | ConvertFrom-Json
 $analysis=Get-Content -LiteralPath (Join-Path $output 'transfer-analysis.json') -Raw | ConvertFrom-Json
 $expected=if($Smoke){4}else{8}
 if(-not $report.completed -or $report.caseCount -ne $expected -or -not $analysis.freshOnly -or -not $analysis.completed){
  throw ($name+' did not complete the required fresh-state matrix.')
 }
 Assert-FileHashes $configuration.ProtectedBackupFiles
}

try {
 Start-Transcript -Path (Join-Path $package 'powershell.log') | Out-Null
 $transcribing=$true
 $Host.UI.RawUI.WindowTitle='DNNE fresh-state balance pathways - controlled comparison'
 $status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
 Write-Host ('Results: '+$package) -ForegroundColor Cyan
 Write-Host 'Fresh cells and connections for each case. Old saves are protected and never loaded.'
 Write-Host 'Fourteen isolated DNNE instances: seven bilateral balance/posture regions. Original Avatar receptor paths.'
 Write-Host 'Short preflight, then eight controls. This tests sensory transfer and learning; it does not test physical recovery.'
 Assert-FileHashes $configuration.PackageFiles
 Assert-FileHashes $configuration.ProtectedBackupFiles
 Invoke-ProbeStage -name 'preflight' -Smoke
 Invoke-ProbeStage -name 'study'
 $status.Status='FINISHED';$status.ExitCode=0
 Write-Host 'All cases and accounting checks completed. Orion will assess neural discrimination from the saved results.' -ForegroundColor Green
}catch{
 $status.Status='FAILED';$status.ExitCode=1;$status['Error']=$_.Exception.Message
 $_ | Out-String | Set-Content -LiteralPath (Join-Path $package 'failure.txt') -Encoding UTF8
 Write-Host $_.Exception.Message -ForegroundColor Red
}finally{
 try {Assert-FileHashes $configuration.ProtectedBackupFiles;$status['OldBackupUnchanged']=$true}
 catch {$status.Status='FAILED';$status.ExitCode=1;$status['PreservationError']=$_.Exception.Message}
 $status['CompletedUtc']=[DateTime]::UtcNow.ToString('o')
 $status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
 if($transcribing){Stop-Transcript | Out-Null}
 try {& (Join-Path $package 'backup-run.ps1') -Source $package -Destination $configuration.BackupDestination}
 catch {
  $status.Status='FAILED';$status.ExitCode=1;$status['BackupError']=$_.Exception.Message
  $status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
  Write-Host ('Backup failed: '+$_.Exception.Message) -ForegroundColor Red
 }
 if($LaunchTaskName -match '^DNNE-Fresh-Postural-[0-9a-f]{12}$'){
  Unregister-ScheduledTask -TaskName $LaunchTaskName -Confirm:$false -ErrorAction SilentlyContinue
 }
 Write-Host ('========== '+$status.Status+' ==========')
 Write-Host 'Tell Orion when this window finishes. Completion and learned balance are separate.'
}
