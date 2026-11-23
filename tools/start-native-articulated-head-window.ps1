param([Parameter(Mandatory=$true)][string]$PackageDirectory,[switch]$InWindow,[string]$LaunchTaskName='')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
$package=(Resolve-Path -LiteralPath $PackageDirectory).Path
$configuration=Get-Content -LiteralPath (Join-Path $package 'configuration.json') -Raw | ConvertFrom-Json
if(-not $configuration.NoLiveMonitoring -or -not $configuration.NativeArticulatedHeadStudy -or $configuration.ExpectedCases -ne 6 -or $configuration.ComponentCasesReused -ne 331){throw 'Unexpected native head protocol.'}
function Sha([string]$Path){(Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash}
function Assert-Package {
 foreach($entry in $configuration.PackageFiles){if((Sha (Join-Path $package $entry.Path)) -ne $entry.Sha256){throw ('Frozen input changed: '+$entry.Path)}}
 foreach($name in @('Dotnet','Python')){if((Sha $configuration.($name+'Executable')) -ne $configuration.($name+'ExecutableSha256')){throw ($name+' executable changed.')}}
}
if(-not $InWindow){
 if(Test-Path -LiteralPath (Join-Path $package 'launch.json')){throw 'Already launched; use a fresh package.'}
 $manifest=Get-Content -LiteralPath (Join-Path $package 'backup-manifest.json') -Raw | ConvertFrom-Json
 if($manifest.Destination -ne $configuration.BackupDestination -or (Sha (Join-Path $package 'backup-manifest.json')) -ne (Sha (Join-Path $configuration.BackupDestination 'backup-manifest.json'))){throw 'Initial backups differ.'}
 foreach($entry in $manifest.Files){foreach($directory in @($package,$configuration.BackupDestination)){
  if((Sha (Join-Path $directory $entry.Path)) -ne $entry.Sha256){throw ('Initial source/backup differs: '+$entry.Path)}
 }}
 Assert-Package
 $taskName='DNNE-Native-Articulated-Head-'+[Guid]::NewGuid().ToString('N').Substring(0,12)
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
function Invoke-Logged([string]$Executable,[string[]]$NativeArguments,[string]$LogName){
 $writer=[IO.StreamWriter]::new((Join-Path $package $LogName),$false,[Text.UTF8Encoding]::new($false));$writer.AutoFlush=$true
 $previous=$ErrorActionPreference
 try{
  $ErrorActionPreference='Continue'
  & $Executable @NativeArguments 2>&1 | ForEach-Object {$line=if($_ -is [System.Management.Automation.ErrorRecord]){[string]$_.Exception.Message}else{[string]$_};$writer.WriteLine($line);Write-Host $line} -ErrorAction Stop
  $nativeExit=$LASTEXITCODE
 }finally{$writer.Dispose();$ErrorActionPreference=$previous}
 if($nativeExit -ne 0){throw ($LogName+' exited '+$nativeExit)}
}
function Stage([string]$stage){Invoke-Logged $configuration.PythonExecutable @('-I','-B',(Join-Path $package 'source/tools/native_articulated_head_study.py'),$stage,$package) ($stage+'.log')}
$status=[ordered]@{Status='RUNNING';ExitCode=$null;Revision=$configuration.Revision;NoLiveMonitoring=$true;
 ComponentCasesReused=331;NativeExecutionAttempted=$false;NativeHeadDeliveryChecksPassed=$false;
 LiveBrainAccessed=$false;OldSavesLoaded=$false;NeuronalSourceExecuted=$false;MuscleCellExecuted=$false;
 PhysiologicalFiringQualified=$false;AvatarBalanceQualified=$false}
$statusPath=Join-Path $package 'window-status.json';$transcribing=$false;$probe=$null;$binaries=@()
if(Test-Path -LiteralPath $statusPath){throw 'Refusing to overwrite an earlier run.'}
try {
 Start-Transcript -Path (Join-Path $package 'powershell.log') | Out-Null;$transcribing=$true
 $Host.UI.RawUI.WindowTitle='DNNE actual full Avatar head delivery - six native cases'
 Write-Host ('Results: '+$package) -ForegroundColor Cyan
 Write-Host 'Reusing 331 verified component cases and the qualified Unity player. Six new file-bridge cases; no neuronal or muscle-cell source. This window stays open after backup.'
 $status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
 Assert-Package;Stage 'preflight'
 $build=Join-Path $package 'source/artifacts/native-head-host'
 Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package 'source/tools/NativeMotorBridgeProbe/NativeMotorBridgeProbe.csproj'),'-c','Release','--artifacts-path',$build,'--nologo','-v','minimal') 'probe-build.log'
 $probe=Join-Path $build 'bin/NativeMotorBridgeProbe/release/NeuralResonanceEngine.DNNE.Tests.dll'
 $binaries=@(Get-ChildItem -LiteralPath (Split-Path $probe -Parent) -File -Recurse | Sort-Object FullName | ForEach-Object {[ordered]@{Path=$_.FullName;Sha256=Sha $_.FullName}})
 ConvertTo-Json -InputObject $binaries -Depth 5 | Set-Content -LiteralPath (Join-Path $package 'probe-binaries.json') -Encoding UTF8
 Invoke-Logged $configuration.DotnetExecutable @($probe,'--runtime-fingerprint',(Join-Path $package 'managed-runtime-before.json')) 'managed-runtime-before.log'
 Stage 'runtime'
 $status.NativeExecutionAttempted=$true;$status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
 Invoke-Logged $configuration.DotnetExecutable @($probe,'--articulated-head-native',(Join-Path $package $configuration.PlayerRelativePath),(Join-Path $package $configuration.OriginalRelativePath),(Join-Path $package 'native')) 'native-head.log'
 Stage 'compare'
 $analysis=Get-Content -LiteralPath (Join-Path $package 'analysis.json') -Raw | ConvertFrom-Json
 if(-not $analysis.nativeHeadDeliveryChecksPassed -or $analysis.actualPhysicsSteps -ne 48 -or $analysis.headReceipts -ne 162){throw 'Actual native head evidence incomplete.'}
 $status.NativeHeadDeliveryChecksPassed=$true;$status.Status='DIAGNOSTICS_COMPLETE';$status.ExitCode=0
}catch{
 $status.Status='FAILED';$status.ExitCode=1;$status['Error']=$_.Exception.ToString();Write-Host ('FAILED: '+$_.Exception.Message) -ForegroundColor Red
}finally{
 try{
  Stage 'preflight';Assert-Package
  foreach($entry in $binaries){if((Sha $entry.Path) -ne $entry.Sha256){throw ('Executed host binary changed: '+$entry.Path)}}
  $status['FrozenInputsPreserved']=$true
 }catch{$status.Status='FAILED';$status.ExitCode=1;$status['PreservationError']=$_.Exception.ToString()}
 if($probe -and (Test-Path -LiteralPath (Join-Path $package 'managed-runtime-before.json'))){
  try{
   Invoke-Logged $configuration.DotnetExecutable @($probe,'--runtime-fingerprint',(Join-Path $package 'managed-runtime-after.json')) 'managed-runtime-after.log'
   if((Sha (Join-Path $package 'managed-runtime-before.json')) -ne (Sha (Join-Path $package 'managed-runtime-after.json'))){throw 'Selected runtime changed.'}
   $status['ManagedRuntimePreserved']=$true
  }catch{$status.Status='FAILED';$status.ExitCode=1;$status['ManagedRuntimeError']=$_.Exception.ToString()}
 }
 $status['CompletedUtc']=[DateTime]::UtcNow.ToString('o')
 if($transcribing){Stop-Transcript | Out-Null}
 $status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8
 try{& (Join-Path $package 'backup-run.ps1') -Source $package -Destination $configuration.BackupDestination;if(-not $?){throw 'Backup failed.'}}
 catch{$status.Status='FAILED';$status.ExitCode=1;$status['BackupError']=$_.Exception.ToString();$status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8;Write-Host ('BACKUP FAILED: '+$_.Exception.Message) -ForegroundColor Red}
 if($LaunchTaskName){Unregister-ScheduledTask -TaskName $LaunchTaskName -Confirm:$false -ErrorAction SilentlyContinue}
 Write-Host ('========== '+$status.Status+' ==========') -ForegroundColor Cyan
 Write-Host ('Results: '+$package)
 Write-Host 'Tell Orion the actual full-body head-input outcome. Native delivery, sampled kinematics, physiological sensory firing and Avatar standing are separate.'
}
