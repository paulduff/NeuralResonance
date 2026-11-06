param([Parameter(Mandatory=$true)][string]$PackageDirectory,[switch]$InWindow,[string]$LaunchTaskName='')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
$package=(Resolve-Path -LiteralPath $PackageDirectory).Path
$configuration=Get-Content -LiteralPath (Join-Path $package 'configuration.json') -Raw | ConvertFrom-Json
$withNumerics=$configuration.PSObject.Properties.Name -contains 'NumericalCandidate' -and $configuration.NumericalCandidate
if(-not $InWindow){
 if(Test-Path -LiteralPath (Join-Path $package 'launch.json')){throw 'Already launched; use a fresh package.'}
 $taskName='DNNE-Cortical-Contact-'+[Guid]::NewGuid().ToString('N').Substring(0,12)
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
$status=[ordered]@{Status='RUNNING';ExitCode=$null;Revision=$configuration.Revision;LiveBrainAccessed=$false;OldSavesLoaded=$false;
 BiologicalConsolidationQualified=$false;OriginalConsolidationAssertionRepaired=$false;FullUnitSuitePassed=$false}
$statusPath=Join-Path $package 'window-status.json';$transcribing=$false
if(Test-Path -LiteralPath $statusPath){throw 'Refusing to overwrite a prior run.'}
try {
 Start-Transcript -Path (Join-Path $package 'powershell.log') | Out-Null;$transcribing=$true
 $Host.UI.RawUI.WindowTitle=if($withNumerics){'DNNE cortical contact precision candidate'}else{'DNNE cortical contact replay - fixed spike histories'}
 Write-Host ('Results: '+$package) -ForegroundColor Cyan
 Write-Host 'Recorded synaptic evidence only. No recurrent brain, body or old save is run.'
 $status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
 Assert-Package
 $build=Join-Path $package 'source/artifacts/contact-replay'
 Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package 'source/tools/MemoryConsolidationProbe/MemoryConsolidationProbe.csproj'),'-c','Release','--artifacts-path',$build,'--nologo','-v','minimal') 'build.log'
 $probe=Join-Path $build 'bin/MemoryConsolidationProbe/release/NeuralResonanceEngine.DNNE.Tests.dll'
 $binaries=@(Get-ChildItem -LiteralPath (Split-Path -Parent $probe) -Recurse -File | ForEach-Object {[ordered]@{Path=$_.FullName;Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
 $binaries | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $package 'probe-binaries.json') -Encoding UTF8
 Invoke-Logged $configuration.DotnetExecutable @($probe,'--contact-replay',(Join-Path $package 'input'),(Join-Path $package 'study')) 'replay.log'
 Invoke-Logged $configuration.PythonExecutable @('-B',(Join-Path $package 'source/tools/analyse-cortical-contact.py'),$package) 'analysis.log'
 $analysis=Get-Content -LiteralPath (Join-Path $package 'study/analysis.json') -Raw | ConvertFrom-Json
 if(-not $analysis.completed -or $analysis.exactRecordedReplays -ne 4 -or $analysis.replayCases -ne $configuration.ExpectedReplayCases -or $analysis.cadenceCases -ne $configuration.ExpectedCadenceCases){throw 'Required replay/cadence evidence incomplete.'}
 if($withNumerics){
  Invoke-Logged $configuration.DotnetExecutable @($probe,'--contact-numerics',(Join-Path $package 'input'),(Join-Path $package 'numerical-study')) 'numerics.log'
  Invoke-Logged $configuration.PythonExecutable @('-B',(Join-Path $package 'source/tools/analyse-cortical-numerics.py'),$package) 'numerical-analysis.log'
  $status['IsolatedNumericalCriteriaPassed']=$true
 }
 Assert-Package
 foreach($entry in $binaries){if((Get-FileHash -LiteralPath $entry.Path).Hash -ne $entry.Sha256){throw ('Binary changed: '+$entry.Path)}}
 $status['RecordedContactStatesReproduced']=$true;$status['DiagnosticCaptureComplete']=$true
 $status.Status='DIAGNOSTICS_COMPLETE';$status.ExitCode=0
} catch {
 $status.Status='FAILED';$status.ExitCode=1;$status['Error']=$_.Exception.ToString()
 Write-Host ('FAILED: '+$_.Exception.Message) -ForegroundColor Red
} finally {
 if($status.Status -eq 'RUNNING'){$status.Status='INTERRUPTED';$status.ExitCode=2;$status['Error']='Pipeline ended before completing checks; cause not established.'}
 $status['CompletedUtc']=[DateTime]::UtcNow.ToString('o')
 if($transcribing){Stop-Transcript | Out-Null}
 $status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8
 try {& (Join-Path $package 'backup-run.ps1') -Source $package -Destination $configuration.BackupDestination}
 catch {$status.Status='FAILED';$status.ExitCode=1;$status['BackupError']=$_.Exception.ToString();$status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8;Write-Host ('BACKUP FAILED: '+$_.Exception.Message) -ForegroundColor Red}
 if($LaunchTaskName){Unregister-ScheduledTask -TaskName $LaunchTaskName -Confirm:$false -ErrorAction SilentlyContinue}
 Write-Host ('========== '+$status.Status+' ==========') -ForegroundColor Cyan
 if($withNumerics){Write-Host 'Tell Orion the cortical contact precision outcome. Numerical accuracy is not receptor calibration, biological consolidation or live integration.'}
 else {Write-Host 'Tell Orion the cortical contact replay outcome. Recorded-state reproduction is not a repaired learning rule or biological consolidation.'}
}
