param([Parameter(Mandatory=$true)][string]$PackageDirectory,[switch]$InWindow,[string]$LaunchTaskName='')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
$package=(Resolve-Path -LiteralPath $PackageDirectory).Path
$configuration=Get-Content -LiteralPath (Join-Path $package 'configuration.json') -Raw | ConvertFrom-Json
$closedLoop=($configuration.PSObject.Properties.Name -contains 'ClosedLoopMotorScheduleRegression') -and $configuration.ClosedLoopMotorScheduleRegression
$expectedCases=if($closedLoop){243}else{155}
if(-not $configuration.NoLiveMonitoring -or -not $configuration.FreshMotorEpochRegression -or $configuration.ExpectedTestCases -ne $expectedCases){throw 'Unexpected motor-clock protocol.'}
function Sha([string]$Path){(Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash}
function Assert-Package {
 foreach($entry in $configuration.PackageFiles){if((Sha (Join-Path $package $entry.Path)) -ne $entry.Sha256){throw ('Frozen input changed: '+$entry.Path)}}
 foreach($name in @('Dotnet','Python')){if((Sha $configuration.($name+'Executable')) -ne $configuration.($name+'ExecutableSha256')){throw ($name+' executable changed.')}}
}
if(-not $InWindow){
 if(Test-Path -LiteralPath (Join-Path $package 'launch.json')){throw 'Already launched; use a fresh package.'}
 $manifest=Get-Content -LiteralPath (Join-Path $package 'backup-manifest.json') -Raw | ConvertFrom-Json
 if($manifest.Destination -ne $configuration.BackupDestination -or (Sha (Join-Path $package 'backup-manifest.json')) -ne (Sha (Join-Path $configuration.BackupDestination 'backup-manifest.json'))){throw 'Initial backup identities differ.'}
 foreach($entry in $manifest.Files){foreach($directory in @($package,$configuration.BackupDestination)){
  if((Sha (Join-Path $directory $entry.Path)) -ne $entry.Sha256){throw ('Initial source/backup differs: '+$entry.Path)}
 }}
 Assert-Package
 $taskPrefix=if($closedLoop){'DNNE-Motor-Closure-'}else{'DNNE-Motor-Epoch-'}
 $taskName=$taskPrefix+[Guid]::NewGuid().ToString('N').Substring(0,12)
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
function Capture-Binaries([string]$Assembly,[string]$Name){
 $files=@(Get-ChildItem -LiteralPath (Split-Path $Assembly -Parent) -File -Recurse | Sort-Object FullName)
 $entries=[Collections.Generic.List[object]]::new();$index=0
 foreach($file in $files){
  $entries.Add([ordered]@{Path=$file.FullName;Sha256=Sha $file.FullName});$index++
  if($index%50 -eq 0 -or $index -eq $files.Count){Write-Host ('Binary files hashed: '+$index+' / '+$files.Count+' ('+$Name+')')}
 }
 ConvertTo-Json -InputObject $entries.ToArray() -Depth 5 | Set-Content -LiteralPath (Join-Path $package $Name) -Encoding UTF8
 return $entries.ToArray()
}
function Stage([string]$stage){Invoke-Logged $configuration.PythonExecutable @('-I','-B',(Join-Path $package 'source/tools/fresh_motor_epoch_study.py'),$stage,$package) ($stage+'.log')}
$status=[ordered]@{Status='RUNNING';ExitCode=$null;Revision=$configuration.Revision;NoLiveMonitoring=$true;TestExecutionAttempted=$false;ClockChecksPassed=$false;NativeUnityExecuted=$false;LiveBrainAccessed=$false;OldSavesLoaded=$false;IndependentCoupledTrajectoryQualified=$false;PhysiologicalFiringQualified=$false;AvatarBalanceQualified=$false}
$statusPath=Join-Path $package 'window-status.json';$transcribing=$false;$testBinaries=@();$probeBinaries=@();$probe=$null
if(Test-Path -LiteralPath $statusPath){throw 'Refusing to overwrite an earlier run.'}
try {
 Start-Transcript -Path (Join-Path $package 'powershell.log') | Out-Null;$transcribing=$true
 $Host.UI.RawUI.WindowTitle=if($closedLoop){'DNNE body and motor closure - 243 component/regression cases'}else{'DNNE fresh motor clock - 155 component/regression cases'}
 Write-Host ('Results: '+$package) -ForegroundColor Cyan
 if($closedLoop){Write-Host '14 new split-schedule controls and 229 existing regressions. No Unity or live brain. This window stays open after its final backup.'}
 else{Write-Host '13 new clock controls and 142 existing regressions. No Unity or live brain. This window stays open after its final backup.'}
 $status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
 Assert-Package;Stage 'preflight'
 Invoke-Logged $configuration.DotnetExecutable @('--info') 'dotnet-info.log'
 $build=Join-Path $package 'source/artifacts/motor-epoch-tests'
 Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package 'source/tests/NeuralResonanceEngine.DNNE.Tests/NeuralResonanceEngine.DNNE.Tests.csproj'),'-c','Release','--artifacts-path',$build,'--nologo','-v','minimal') 'test-build.log'
 $assembly=Join-Path $build 'bin/NeuralResonanceEngine.DNNE.Tests/release/NeuralResonanceEngine.DNNE.Tests.dll'
 $testBinaries=Capture-Binaries $assembly 'test-binaries.json'
 $probeBuild=Join-Path $package 'source/artifacts/motor-epoch-runtime'
 Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package 'source/tools/NativeMotorBridgeProbe/NativeMotorBridgeProbe.csproj'),'-c','Release','--artifacts-path',$probeBuild,'--nologo','-v','minimal') 'probe-build.log'
 $probe=Join-Path $probeBuild 'bin/NativeMotorBridgeProbe/release/NeuralResonanceEngine.DNNE.Tests.dll'
 $probeBinaries=Capture-Binaries $probe 'probe-binaries.json'
 Invoke-Logged $configuration.DotnetExecutable @($probe,'--runtime-fingerprint',(Join-Path $package 'managed-runtime-before.json')) 'managed-runtime-before.log'
 Stage 'runtime'
 $testOutput=Join-Path $package 'test-results';[IO.Directory]::CreateDirectory($testOutput) | Out-Null
 $env:NRE_SYNAPSE_STATE_DIR=Join-Path $package 'isolated-synapses'
 $env:NRE_RETICULOSPINAL_EVIDENCE_DIR=Join-Path $package 'partition-evidence'
 $env:NRE_DESCENDING_ANATOMY_EVIDENCE_DIR=Join-Path $package 'anatomy-evidence'
 $status.TestExecutionAttempted=$true
 $status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
 Invoke-Logged $configuration.DotnetExecutable @('vstest',$assembly,('/TestCaseFilter:'+$configuration.TestFilter),('/ResultsDirectory:'+$testOutput),'/Logger:trx;LogFileName=focused.trx') 'focused-tests.log'
 Stage 'check-tests'
 $status['PassedFocusedTests']=$expectedCases;$status.ClockChecksPassed=$true
 if($closedLoop){$status['SplitScheduleChecksPassed']=$true}
 $status.Status='DIAGNOSTICS_COMPLETE';$status.ExitCode=0
}catch{
 $status.Status='FAILED';$status.ExitCode=1;$status['Error']=$_.Exception.ToString();Write-Host ('FAILED: '+$_.Exception.Message) -ForegroundColor Red
}finally{
 try{
  Stage 'preflight';Assert-Package
  foreach($entry in @($testBinaries)+@($probeBinaries)){if((Sha $entry.Path) -ne $entry.Sha256){throw ('Executed binary changed: '+$entry.Path)}}
  $status['FrozenInputsPreserved']=$true
 }catch{$status.Status='FAILED';$status.ExitCode=1;$status['PreservationError']=$_.Exception.ToString()}
 if($probe -and (Test-Path -LiteralPath (Join-Path $package 'managed-runtime-before.json'))){
  try{
   Invoke-Logged $configuration.DotnetExecutable @($probe,'--runtime-fingerprint',(Join-Path $package 'managed-runtime-after.json')) 'managed-runtime-after.log'
   if((Sha (Join-Path $package 'managed-runtime-before.json')) -ne (Sha (Join-Path $package 'managed-runtime-after.json'))){throw 'Selected managed runtime changed.'}
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
 if($closedLoop){Write-Host 'Tell Orion the body/motor closure outcome. Component scheduling, active sensory feedback, coupled accuracy and Avatar standing are separate.'}
 else{Write-Host 'Tell Orion the fresh motor-clock outcome. Component clock ownership, coupled numerical accuracy, physiological firing and Avatar standing are separate.'}
}
