param([Parameter(Mandatory=$true)][string]$PackageDirectory,[switch]$InWindow,[string]$LaunchTaskName='')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
$package=(Resolve-Path -LiteralPath $PackageDirectory).Path
$configuration=Get-Content -LiteralPath (Join-Path $package 'configuration.json') -Raw | ConvertFrom-Json
$canonical=[bool]$configuration.CanonicalStateCandidate
if(-not $configuration.NoLiveMonitoring -or -not $configuration.NoTimingMeasured -or $configuration.ExpectedTrajectories -ne 30 -or $configuration.ExpectedBoundaryRecords -ne 2 -or $configuration.LiveBodySelected -or ([bool]$configuration.UpstreamEquationsModified -ne $canonical)){throw 'Unexpected spindle reference configuration.'}
if($configuration.RuntimeCompatibility -ne 'octave-ode23-empty-options-struct-v1' -or -not $configuration.ExecutionSourceAdapted){throw 'Declare the preserved original and separate Octave options-binding copy.'}
function Extended-Path([string]$Path){
 if($Path.StartsWith('\\?\')){return $Path}
 if($Path.StartsWith('\\')){return '\\?\UNC\'+$Path.Substring(2)}
 return '\\?\'+[IO.Path]::GetFullPath($Path)
}
function File-Sha256([string]$Path){
 $stream=[IO.File]::OpenRead((Extended-Path $Path));$algorithm=[Security.Cryptography.SHA256]::Create()
 try {return [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-','')}
 finally {$algorithm.Dispose();$stream.Dispose()}
}
if(-not $InWindow){
 if(Test-Path -LiteralPath (Join-Path $package 'launch.json')){throw 'Already launched; use a fresh package.'}
 $manifestPath=Join-Path $package 'backup-manifest.json'
 $manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
 if($manifest.Destination -ne $configuration.BackupDestination){throw 'Initial backup destination differs.'}
 if((File-Sha256 $manifestPath) -ne (File-Sha256 (Join-Path $configuration.BackupDestination 'backup-manifest.json'))){throw 'Initial backup manifests differ.'}
 foreach($entry in $manifest.Files){foreach($directory in @($package,$configuration.BackupDestination)){
  if((File-Sha256 (Join-Path $directory $entry.Path)) -ne $entry.Sha256){throw ('Initial source or backup differs: '+$entry.Path)}
 }}
 $taskPrefix=if($canonical){'DNNE-Spindle-State-'}else{'DNNE-Spindle-Reference-'}
 $taskName=$taskPrefix+[Guid]::NewGuid().ToString('N').Substring(0,12)
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
$status=[ordered]@{Status='RUNNING';ExitCode=$null;Revision=$configuration.Revision;
 NativeExecutionAttempted=$false;CompleteFiniteNativeExecution=$false;UpstreamEquationsModified=$configuration.UpstreamEquationsModified;CanonicalStateCandidate=$canonical;CanonicalStateChecksPassed=$false;ExecutionSourceAdapted=$true;RuntimeCompatibility=$configuration.RuntimeCompatibility;
 NumericalAccuracyQualified=$false;MatlabAgreementAssessed=$false;AfferentMembraneExecuted=$false;
 LiveBrainAccessed=$false;OldSavesLoaded=$false;TimingMeasured=$false;PhysiologyQualified=$false;AvatarRecoveryAssessed=$false}
$statusPath=Join-Path $package 'window-status.json';$transcribing=$false
if(Test-Path -LiteralPath $statusPath){throw 'Refusing to overwrite earlier results.'}
$savedEnvironment=@{}
foreach($name in @('PATH','OCTAVE_HOME','OCTAVE_EXEC_HOME','OCTAVE_PATH','OPENBLAS_NUM_THREADS','OMP_NUM_THREADS')){
 $savedEnvironment[$name]=[Environment]::GetEnvironmentVariable($name,'Process')
}
try {
 Start-Transcript -Path (Join-Path $package 'powershell.log') | Out-Null;$transcribing=$true
 $Host.UI.RawUI.WindowTitle=if($canonical){'DNNE canonical spindle state comparison'}else{'DNNE native spindle mechanics reference'}
 Write-Host ('Results: '+$package) -ForegroundColor Cyan
 Write-Host '30 bag/chain mechanics trajectories and two bookkeeping records, using a declared Octave options-binding copy. No afferent neurons, live body or speed measurement. Report completion to Orion.'
 if($canonical){Write-Host 'Canonical state candidate: comparing against preserved original traces, plus read-only state, trial-force and unsupported-overlap controls.'}
 $status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
 [ordered]@{CapturedUtc=[DateTime]::UtcNow.ToString('o');Cpu=@(Get-CimInstance Win32_Processor | Select-Object Name,NumberOfCores,NumberOfLogicalProcessors);OperatingSystem=Get-CimInstance Win32_OperatingSystem | Select-Object Caption,Version,TotalVisibleMemorySize} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $package 'platform.json') -Encoding UTF8
 $audit=Join-Path $package 'source/tools/spindle-reference.py'
 Invoke-Logged $configuration.PythonExecutable @('-I','-B',$audit,'verify','--package',$package,'--output',(Join-Path $package 'runtime-before.json')) 'runtime-before.log'
 $env:OCTAVE_HOME=$configuration.OctaveHome
 $env:OCTAVE_EXEC_HOME=$configuration.OctaveHome
 $env:OCTAVE_PATH=''
 $env:OPENBLAS_NUM_THREADS='1';$env:OMP_NUM_THREADS='1'
 $env:PATH=(Join-Path $configuration.OctaveHome 'bin')+';'+(Join-Path $configuration.RuntimeDirectory 'usr/bin')+';'+(Join-Path $env:SystemRoot 'System32')+';'+$env:SystemRoot
 $toolsPath=(Join-Path $package 'source/tools').Replace('\','/').Replace("'","''")
 $packageArg=$package.Replace('\','/').Replace("'","''")
 $expression="addpath('$toolsPath'); run_spindle_mechanics_reference('$packageArg');"
 $firstFailure=$null
 try {
  $status.NativeExecutionAttempted=$true
  Invoke-Logged $configuration.OctaveExecutable @('--quiet','--no-gui','--no-init-file','--no-site-file','--no-history','--eval',$expression) 'native-mechanics.log'
  $analysisMode=if($canonical){'analyse-state'}else{'analyse'}
  Invoke-Logged $configuration.PythonExecutable @('-I','-B',$audit,$analysisMode,'--package',$package,'--output',(Join-Path $package 'mechanics-analysis.json')) 'analysis.log'
 } catch {$firstFailure=$_;$status['NativeOrAnalysisError']=$_.Exception.ToString()}
 # Recheck all runtime files and pinned inputs even if the native run failed.
 Invoke-Logged $configuration.PythonExecutable @('-I','-B',$audit,'verify','--package',$package,'--output',(Join-Path $package 'runtime-after.json')) 'runtime-after.log'
 if((File-Sha256 (Join-Path $package 'runtime-before.json')) -ne (File-Sha256 (Join-Path $package 'runtime-after.json'))){throw 'Runtime fingerprint changed.'}
 if($null -ne $firstFailure){throw $firstFailure}
 $analysis=Get-Content -LiteralPath (Join-Path $package 'mechanics-analysis.json') -Raw | ConvertFrom-Json
 if($canonical){
  if(-not $analysis.complete -or -not $analysis.canonicalStateChecksPassed -or @($analysis.trajectories).Count -ne 30 -or @($analysis.boundaryControls.fibers).Count -ne 2 -or @($analysis.sourceComparison.refinementComparisons).Count -ne 20){throw 'Incomplete canonical-state comparison evidence.'}
  $status.CanonicalStateChecksPassed=$true
 }elseif(-not $analysis.complete -or @($analysis.trajectories).Count -ne 30 -or @($analysis.boundaries).Count -ne 2 -or @($analysis.refinementComparisons).Count -ne 20){throw 'Incomplete native mechanics evidence.'}
 $status.CompleteFiniteNativeExecution=$true;$status.Status='DIAGNOSTICS_COMPLETE';$status.ExitCode=0
} catch {
 $status.Status='FAILED';$status.ExitCode=1;$status['Error']=$_.Exception.ToString()
 Write-Host ('FAILED: '+$_.Exception.Message) -ForegroundColor Red
} finally {
 foreach($name in $savedEnvironment.Keys){[Environment]::SetEnvironmentVariable($name,$savedEnvironment[$name],'Process')}
 if($status.Status -eq 'RUNNING'){$status.Status='INTERRUPTED';$status.ExitCode=2;$status['Error']='Execution ended before completing checks; cause not established.'}
 $status['CompletedUtc']=[DateTime]::UtcNow.ToString('o')
 if($transcribing){Stop-Transcript | Out-Null}
 $status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8
 try {& (Join-Path $package 'backup-run.ps1') -Source $package -Destination $configuration.BackupDestination}
 catch {$status.Status='FAILED';$status.ExitCode=1;$status['BackupError']=$_.Exception.ToString();$status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8;Write-Host ('BACKUP FAILED: '+$_.Exception.Message) -ForegroundColor Red}
 if($LaunchTaskName){Unregister-ScheduledTask -TaskName $LaunchTaskName -Confirm:$false -ErrorAction SilentlyContinue}
 Write-Host ('========== '+$status.Status+' ==========') -ForegroundColor Cyan
 if($canonical){Write-Host 'Tell Orion the canonical spindle state outcome. State consistency, independent numerical accuracy, afferent physiology and Avatar recovery are separate.'}
 else{Write-Host 'Tell Orion the Octave-adapted spindle mechanics outcome. Execution, numerical accuracy, sensory physiology and Avatar recovery are separate.'}
 Write-Host ('Results: '+$package)
}
