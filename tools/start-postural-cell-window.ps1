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
 $taskName='DNNE-Postural-Cells-'+[Guid]::NewGuid().ToString('N').Substring(0,12)
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
 FreshCellsOnly=$true;LiveBrainAccessed=$false;PhysicalRecoveryAssessed=$false}
$statusPath=Join-Path $package 'window-status.json'
$transcribing=$false
function Assert-FileHashes($entries){
 foreach($entry in $entries){
  if((Get-FileHash -LiteralPath $entry.Path -Algorithm SHA256).Hash -ne $entry.Sha256){
   throw ('Protected or packaged file changed: '+$entry.Path)
  }
 }
}
function Invoke-LoggedNative([string]$Executable,[string[]]$NativeArguments,[string]$ConsolePath){
 # Windows PowerShell's transcript can omit buffered native stdout. Explicitly
 # save each stage while still showing it in the operator's independent window.
 if(-not (Test-Path -LiteralPath $Executable -PathType Leaf)){throw ('Native executable not found: '+$Executable)}
 $previousErrorPreference=$ErrorActionPreference
 $consoleWriter=[IO.StreamWriter]::new($ConsolePath,$false,[Text.UTF8Encoding]::new($false))
 $consoleWriter.AutoFlush=$true
 try {
  $ErrorActionPreference='Continue'
  & $Executable @NativeArguments 2>&1 | ForEach-Object {
   $nativeLine=[string]$_
   $consoleWriter.WriteLine($nativeLine)
   Write-Host $nativeLine
  } -ErrorAction Stop
  $nativeExitCode=$LASTEXITCODE
 } finally {$consoleWriter.Dispose();$ErrorActionPreference=$previousErrorPreference}
 return $nativeExitCode
}
function Invoke-ProbeStage([string]$name,[switch]$Smoke,[switch]$Baseline){
 $output=Join-Path $package $name
 if(Test-Path -LiteralPath $output){throw ('Refusing to reuse neural state: '+$output)}
 Assert-FileHashes $configuration.PackageFiles
 $arguments=@($configuration.ProbeAssembly,$output)
 $analyser=Join-Path $package 'analysis.py'
 $expected=$configuration.CaseCount
 if($Baseline){
  $arguments=@($configuration.BaselineProbeAssembly,$output)
  $analyser=Join-Path $package 'baseline-analysis.py'
  $expected=72
 }elseif($configuration.PSObject.Properties.Name -contains 'ProbeArguments'){$arguments+=@($configuration.ProbeArguments)}
 if($Smoke){$arguments+='--smoke'}
 Write-Host ('========== '+$name.ToUpperInvariant()+' ==========') -ForegroundColor Cyan
 $probeExit=Invoke-LoggedNative $configuration.DotnetExecutable $arguments (Join-Path $package ($name+'-console.log'))
 if($probeExit -ne 0){throw ($name+' failed with exit '+$probeExit+'; inspect '+(Join-Path $output 'FAILED.json'))}
 $analysisExit=Invoke-LoggedNative $configuration.PythonExecutable @($analyser,$output) (Join-Path $package ($name+'-analysis-console.log'))
 if($analysisExit -ne 0){throw ($name+' accounting/state validation failed with exit '+$analysisExit+'.')}
 $report=Get-Content -LiteralPath (Join-Path $output 'results.json') -Raw | ConvertFrom-Json
 $analysis=Get-Content -LiteralPath (Join-Path $output 'cell-analysis.json') -Raw | ConvertFrom-Json
 if(-not $report.completed -or $report.cases.Count -ne $expected -or -not $analysis.accountingValidated -or -not $analysis.completed){
  throw ($name+' did not complete the required fresh-state matrix.')
 }
 Assert-FileHashes $configuration.ProtectedBackupFiles
}

try {
 Start-Transcript -Path (Join-Path $package 'powershell.log') | Out-Null
 $transcribing=$true
 $Host.UI.RawUI.WindowTitle=if($configuration.PSObject.Properties.Name -contains 'WindowTitle'){$configuration.WindowTitle}else{'DNNE postural cells - response and numerical calibration'}
 $status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
 Write-Host ('Results: '+$package) -ForegroundColor Cyan
 Write-Host 'Fresh isolated cells for each condition. Old saves are protected and never loaded.'
 if($configuration.PSObject.Properties.Name -contains 'StudyDescription'){
  Write-Host $configuration.StudyDescription
 }else{Write-Host 'Three production cell profiles; excitation, inhibition, pacemaker and integration-step controls.'}
 Write-Host ('Short preflight, then '+$configuration.CaseCount+' isolated conditions. See the saved protocol for scope; no whole brain or body dynamics.')
 Assert-FileHashes $configuration.PackageFiles
 Assert-FileHashes $configuration.ProtectedBackupFiles
 Invoke-ProbeStage -name 'preflight' -Smoke
 if($configuration.PSObject.Properties.Name -contains 'BaselineProbeAssembly'){
  Write-Host 'Repeating the frozen previous model on this runtime for a matched comparison.'
  Invoke-ProbeStage -name 'baseline' -Baseline
 }
 Invoke-ProbeStage -name 'study'
 if($configuration.PSObject.Properties.Name -contains 'BaselineProbeAssembly'){
  & $configuration.PythonExecutable (Join-Path $package 'compare.py') $package
  if($LASTEXITCODE -ne 0){throw 'Paired comparison validation failed.'}
 }
 $status.Status='FINISHED';$status.ExitCode=0
 Write-Host 'All cases and accounting checks completed. Orion will assess cell responses and numerical sensitivity from the saved results.' -ForegroundColor Green
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
 if($LaunchTaskName -match '^DNNE-Postural-Cells-[0-9a-f]{12}$'){
  Unregister-ScheduledTask -TaskName $LaunchTaskName -Confirm:$false -ErrorAction SilentlyContinue
 }
 Write-Host ('========== '+$status.Status+' ==========')
 Write-Host 'Tell Orion when this window finishes. Completion and physiological validity are separate.'
}
