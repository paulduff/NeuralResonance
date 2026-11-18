param([Parameter(Mandatory=$true)][string]$PackageDirectory,[switch]$InWindow,[string]$LaunchTaskName='')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
$package=(Resolve-Path -LiteralPath $PackageDirectory).Path
$configuration=Get-Content -LiteralPath (Join-Path $package 'configuration.json') -Raw | ConvertFrom-Json
if(-not $configuration.IsolatedCanalTrajectories -or -not $configuration.NoLiveMonitoring -or $configuration.ExpectedNativeRuns -ne 45 -or $configuration.ExpectedReferenceCases -ne 15 -or $configuration.ExpectedReferenceControls -ne 6 -or $configuration.ComponentTestsReused -ne 76 -or $configuration.LiveSelectionChanged -or $configuration.PhysiologyQualified -or $configuration.AvatarRecoveryAssessed){throw 'Unexpected published canal trajectory configuration.'}
function Extended-Path([string]$Path){
 if($Path.StartsWith('\\?\')){return $Path}
 if($Path.StartsWith('\\')){return '\\?\UNC\'+$Path.Substring(2)}
 return '\\?\'+[IO.Path]::GetFullPath($Path)
}
function File-Sha256([string]$Path){
 $stream=[IO.File]::OpenRead((Extended-Path $Path));$algorithm=[Security.Cryptography.SHA256]::Create()
 try{return [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-','')}
 finally{$algorithm.Dispose();$stream.Dispose()}
}
function Assert-Package {
 foreach($entry in $configuration.PackageFiles){if((File-Sha256 (Join-Path $package $entry.Path)) -ne $entry.Sha256){throw ('Frozen source/input changed: '+$entry.Path)}}
 foreach($name in @('Dotnet','Python')){
  if((File-Sha256 $configuration.($name+'Executable')) -ne $configuration.($name+'ExecutableSha256')){throw ($name+' executable changed.')}
 }
}
if(-not $InWindow){
 if(Test-Path -LiteralPath (Join-Path $package 'launch.json')){throw 'Already launched; use a fresh package.'}
 $manifestPath=Join-Path $package 'backup-manifest.json';$manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
 if($manifest.Destination -ne $configuration.BackupDestination){throw 'Initial backup destination differs.'}
 if((File-Sha256 $manifestPath) -ne (File-Sha256 (Join-Path $configuration.BackupDestination 'backup-manifest.json'))){throw 'Initial backup manifests differ.'}
 foreach($entry in $manifest.Files){foreach($directory in @($package,$configuration.BackupDestination)){
  if((File-Sha256 (Join-Path $directory $entry.Path)) -ne $entry.Sha256){throw ('Initial source/backup differs: '+$entry.Path)}
 }}
 Assert-Package
 $taskName='DNNE-Canal-Trajectory-'+[Guid]::NewGuid().ToString('N').Substring(0,12)
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
 try{
  $ErrorActionPreference='Continue'
  & $Executable @NativeArguments 2>&1 | ForEach-Object {$line=[string]$_;$writer.WriteLine($line);Write-Host $line} -ErrorAction Stop
  $nativeExit=$LASTEXITCODE
 }finally{$writer.Dispose();$ErrorActionPreference=$previous}
 if($nativeExit -ne 0){throw ($LogName+' exited '+$nativeExit)}
}
function Python-Stage([string]$Stage){
 Invoke-Logged $configuration.PythonExecutable @('-B',(Join-Path $package 'source/tools/canal_trajectory_gate.py'),$Stage,$package) ('reference-'+$Stage+'.log')
}
function Capture-Binaries([string]$Assembly,[string]$Name){
 $entries=@([IO.Directory]::EnumerateFiles((Extended-Path (Split-Path -Parent $Assembly)),'*',[IO.SearchOption]::AllDirectories) | ForEach-Object {
  [ordered]@{Path=$_.Substring(4);Sha256=(File-Sha256 $_)}
 })
 $entries | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $package $Name) -Encoding UTF8
 return $entries
}
$status=[ordered]@{Status='RUNNING';ExitCode=$null;Revision=$configuration.Revision;NumericalCriteriaPassed=$false;
 ReferenceControlsAttempted=$false;ComponentTestsReused=76;PublishedCanalTrajectoryChecksPassed=$false;SourceToSkullFrameQualified=$false;LocalBundleMapQualified=$false;
 MechanicalCalibrationQualified=$false;StationaryInitializationQualified=$false;SpatialGeometryCalibrated=$false;AfferentFiringQualified=$false;
 LiveBrainAccessed=$false;OldSavesLoaded=$false;NativeBodyExecuted=$false;PhysiologyQualified=$false;AvatarRecoveryAssessed=$false;FullUnitSuitePassed=$false}
$statusPath=Join-Path $package 'window-status.json';$transcribing=$false;$testBinaries=@();$probeBinaries=@();$probe=''
if(Test-Path -LiteralPath $statusPath){throw 'Refusing to overwrite earlier results.'}
function Save-Status([string]$Stage){
 $status['Stage']=$Stage
 $temporary=Join-Path $package 'window-status.tmp'
 $status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $temporary -Encoding UTF8
 Move-Item -LiteralPath $temporary -Destination $statusPath -Force
}
try{
 Start-Transcript -Path (Join-Path $package 'powershell.log') | Out-Null;$transcribing=$true
 $Host.UI.RawUI.WindowTitle='DNNE published canal trajectories'
 Write-Host ('Results: '+$package) -ForegroundColor Cyan
 Write-Host '45 native runs and 15 independent reference cases across fast, middle and slow mechanics; six reference controls. The 76 reviewed component checks are reused, not rerun. No live brain/body.'
 Save-Status 'preflight';Assert-Package;Python-Stage 'preflight'
 $probeBuild=Join-Path $package 'source/artifacts/canal-trajectory-runtime'
 Save-Status 'build-runtime-fingerprint'
 Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package 'source/tools/NativeMotorBridgeProbe/NativeMotorBridgeProbe.csproj'),'-c','Release','--artifacts-path',$probeBuild,'--nologo','-v','minimal') 'probe-build.log'
 $probe=Join-Path $probeBuild 'bin/NativeMotorBridgeProbe/release/NeuralResonanceEngine.DNNE.Tests.dll'
 $probeBinaries=Capture-Binaries $probe 'probe-binaries.json'
 Invoke-Logged $configuration.DotnetExecutable @($probe,'--runtime-fingerprint',(Join-Path $package 'managed-runtime-before.json')) 'managed-runtime-before.log'
 Python-Stage 'runtime';$status['MatchesQualifiedManagedRuntime']=$true
 Save-Status 'reference-controls';$status['ReferenceControlsAttempted']=$true;Python-Stage 'controls'
 $controls=Get-Content -LiteralPath (Join-Path $package 'reference-controls/controls.json') -Raw | ConvertFrom-Json
 if(-not $controls.completed -or $controls.expected -ne 6 -or $controls.passed -ne 6){throw 'Independent exponential controls failed or incomplete.'}
 $status['PassedReferenceControls']=6;$status['ComponentTestsReused']=76
 Save-Status 'native-mechanical-trajectories';Python-Stage 'storage'
 Invoke-Logged $configuration.DotnetExecutable @($probe,'--coupled-canal-trajectories',(Join-Path $package 'trajectory-input.json'),(Join-Path $package 'study')) 'native-trajectories.log'
 $capture=Get-Content -LiteralPath (Join-Path $package 'study/results.json') -Raw | ConvertFrom-Json
 if(-not $capture.completed -or @($capture.runs).Count -ne 45 -or @($capture.runs | Where-Object {-not $_.completed}).Count -ne 0){throw 'Native mechanical capture failed or incomplete.'}
 Save-Status 'independent-matrix-exponential';Python-Stage 'storage';Python-Stage 'reference'
 Save-Status 'all-endpoint-comparison';Python-Stage 'compare'
 $analysis=Get-Content -LiteralPath (Join-Path $package 'trajectory-analysis.json') -Raw | ConvertFrom-Json
 if(-not $analysis.completed -or -not $analysis.numericalCriteriaPassed -or @($analysis.cases).Count -ne 15 -or -not $analysis.allNativeEndpointsCompared){throw 'Published canal comparison failed or incomplete.'}
 foreach($flag in @('sourceToSkullFrameQualified','localBundleMapQualified','physiologyQualified','avatarRecoveryAssessed')){if($analysis.$flag){throw ('Unexpected qualification: '+$flag)}}
 $status.NumericalCriteriaPassed=$true;$status.PublishedCanalTrajectoryChecksPassed=$true
 $status.Status='DIAGNOSTICS_COMPLETE';$status.ExitCode=0
}catch{
 $status.Status='FAILED';$status.ExitCode=1;$status['Error']=$_.Exception.ToString()
 Write-Host ('FAILED: '+$_.Exception.Message) -ForegroundColor Red
}finally{
 if($status.Status -eq 'RUNNING'){$status.Status='INTERRUPTED';$status.ExitCode=2}
 Save-Status 'preservation'
 try{Python-Stage 'postflight';$status['QualifiedDependenciesPreserved']=$true}
 catch{$status.Status='FAILED';$status.ExitCode=1;$status.NumericalCriteriaPassed=$false;$status['DependencyError']=$_.Exception.ToString()}
 if($probe -and (Test-Path -LiteralPath (Join-Path $package 'managed-runtime-before.json'))){
  try{
   Invoke-Logged $configuration.DotnetExecutable @($probe,'--runtime-fingerprint',(Join-Path $package 'managed-runtime-after.json')) 'managed-runtime-after.log'
   if((File-Sha256 (Join-Path $package 'managed-runtime-before.json')) -ne (File-Sha256 (Join-Path $package 'managed-runtime-after.json'))){throw 'Managed runtime changed during the study.'}
   $status['ManagedRuntimePreserved']=$true
  }catch{$status.Status='FAILED';$status.ExitCode=1;$status.NumericalCriteriaPassed=$false;$status['ManagedRuntimeError']=$_.Exception.ToString()}
 }
 try{Assert-Package;foreach($entry in @($testBinaries)+@($probeBinaries)){if((File-Sha256 $entry.Path) -ne $entry.Sha256){throw ('Built binary changed: '+$entry.Path)}};$status['FrozenPackageUnchanged']=$true}
 catch{$status.Status='FAILED';$status.ExitCode=1;$status.NumericalCriteriaPassed=$false;$status['FrozenSourceError']=$_.Exception.ToString()}
 $status['CompletedUtc']=[DateTime]::UtcNow.ToString('o')
 if($transcribing){Stop-Transcript | Out-Null}
 Save-Status 'complete-before-backup'
 try{& (Join-Path $package 'backup-run.ps1') -Source $package -Destination $configuration.BackupDestination}
 catch{$status.Status='FAILED';$status.ExitCode=1;$status['BackupError']=$_.Exception.ToString();Save-Status 'backup-failed';Write-Host ('BACKUP FAILED: '+$_.Exception.Message) -ForegroundColor Red}
 if($LaunchTaskName){Unregister-ScheduledTask -TaskName $LaunchTaskName -Confirm:$false -ErrorAction SilentlyContinue}
 Write-Host ('========== '+$status.Status+' ==========') -ForegroundColor Cyan
 Write-Host ('Results: '+$package)
 Write-Host 'Tell Orion the published canal trajectory outcome. Numerical accuracy, anatomical mapping, sensory physiology and Avatar balance are separate.'
}
