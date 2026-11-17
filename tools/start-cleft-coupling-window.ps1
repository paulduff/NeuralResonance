param([Parameter(Mandatory=$true)][string]$PackageDirectory,[switch]$InWindow,[string]$LaunchTaskName='')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
$package=(Resolve-Path -LiteralPath $PackageDirectory).Path
$configuration=Get-Content -LiteralPath (Join-Path $package 'configuration.json') -Raw | ConvertFrom-Json
if(-not $configuration.IsolatedCleftConstitutive -or -not $configuration.NoLiveMonitoring -or $configuration.ExpectedTestCases -ne 26 -or $configuration.ExpectedEdgeRows -ne 468 -or $configuration.ExpectedChargeRows -ne 60 -or ($configuration.ExpectedDecimalPrecisions -join ',') -ne '60,90' -or $configuration.TimeIntegrated -or $configuration.SpatialGeometryCalibrated -or $configuration.AfferentFiringQualified -or $configuration.LiveSelectionChanged -or $configuration.PhysiologyQualified -or $configuration.AvatarRecoveryAssessed){throw 'Unexpected isolated cleft configuration.'}
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
 $taskName='DNNE-Cleft-Coupling-'+[Guid]::NewGuid().ToString('N').Substring(0,12)
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
 Invoke-Logged $configuration.PythonExecutable @('-B',(Join-Path $package 'source/tools/analyse-cleft-coupling.py'),$Stage,$package) ('reference-'+$Stage+'.log')
}
function Capture-Binaries([string]$Assembly,[string]$Name){
 $entries=@([IO.Directory]::EnumerateFiles((Extended-Path (Split-Path -Parent $Assembly)),'*',[IO.SearchOption]::AllDirectories) | ForEach-Object {
  [ordered]@{Path=$_.Substring(4);Sha256=(File-Sha256 $_)}
 })
 $entries | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $package $Name) -Encoding UTF8
 return $entries
}
$status=[ordered]@{Status='RUNNING';ExitCode=$null;Revision=$configuration.Revision;FocusedTestsPassed=$false;
 NumericalCriteriaPassed=$false;ComponentStudyExecutionAttempted=$false;ConstitutiveArithmeticEvaluated=$false;
 TimeIntegrated=$false;SpatialGeometryCalibrated=$false;AfferentFiringQualified=$false;NativeBodyExecuted=$false;
 LiveBrainAccessed=$false;OldSavesLoaded=$false;PhysiologyQualified=$false;AvatarRecoveryAssessed=$false;FullUnitSuitePassed=$false}
$statusPath=Join-Path $package 'window-status.json';$transcribing=$false;$testBinaries=@();$probeBinaries=@();$probe=''
if(Test-Path -LiteralPath $statusPath){throw 'Refusing to overwrite earlier results.'}
try{
 Start-Transcript -Path (Join-Path $package 'powershell.log') | Out-Null;$transcribing=$true
 $Host.UI.RawUI.WindowTitle='DNNE cleft ion transport and membrane charge checks'
 Write-Host ('Results: '+$package) -ForegroundColor Cyan
 Write-Host '26 focused checks, 468 ion-transfer cases and 60 capacitor cases. Independent 60/90-digit references; no time integration.'
 $status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
 Assert-Package;Python-Stage 'preflight'
 $build=Join-Path $package 'source/artifacts/cleft-tests'
 Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package 'source/tests/NeuralResonanceEngine.DNNE.Tests/NeuralResonanceEngine.DNNE.Tests.csproj'),'-c','Release','--artifacts-path',$build,'--nologo','-v','minimal') 'test-build.log'
 $assembly=Join-Path $build 'bin/NeuralResonanceEngine.DNNE.Tests/release/NeuralResonanceEngine.DNNE.Tests.dll'
 $testRoot=[IO.DirectoryInfo]::new((Split-Path -Parent $assembly))
 while($null -ne $testRoot -and -not (Test-Path -LiteralPath (Join-Path $testRoot.FullName 'NeuralResonanceEngine.DNNE.slnx'))){$testRoot=$testRoot.Parent}
 if($null -eq $testRoot -or -not $testRoot.FullName.Equals((Join-Path $package 'source'),[StringComparison]::OrdinalIgnoreCase)){throw 'Tests are not confined to frozen source.'}
 $testOutput=Join-Path $package 'test-results';New-Item -ItemType Directory -Path $testOutput | Out-Null
 $testBinaries=Capture-Binaries $assembly 'test-binaries.json'
 Invoke-Logged $configuration.DotnetExecutable @('vstest',$assembly,('/TestCaseFilter:'+$configuration.TestFilter),('/ResultsDirectory:'+$testOutput),'/Logger:trx;LogFileName=focused.trx') 'focused-tests.log'
 Python-Stage 'check-tests';$status['PassedFocusedTests']=26;$status.FocusedTestsPassed=$true
 $probeBuild=Join-Path $package 'source/artifacts/cleft-probe'
 Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package 'source/tools/NativeMotorBridgeProbe/NativeMotorBridgeProbe.csproj'),'-c','Release','--artifacts-path',$probeBuild,'--nologo','-v','minimal') 'probe-build.log'
 $probe=Join-Path $probeBuild 'bin/NativeMotorBridgeProbe/release/NeuralResonanceEngine.DNNE.Tests.dll'
 $probeBinaries=Capture-Binaries $probe 'probe-binaries.json'
 Invoke-Logged $configuration.DotnetExecutable @($probe,'--runtime-fingerprint',(Join-Path $package 'managed-runtime-before.json')) 'managed-runtime-before.log'
 $status.ComponentStudyExecutionAttempted=$true
 Invoke-Logged $configuration.DotnetExecutable @($probe,'--cleft-coupling',(Join-Path $package 'cleft-input.json'),(Join-Path $package 'study')) 'study.log'
 $status.ConstitutiveArithmeticEvaluated=$true
 Python-Stage 'compare'
 $analysis=Get-Content -LiteralPath (Join-Path $package 'analysis.json') -Raw | ConvertFrom-Json
 if(-not $analysis.completed -or -not $analysis.numericalComponentCriteriaPassed -or $analysis.counts.edges -ne 468 -or $analysis.counts.charges -ne 60 -or $analysis.tests.passedCases -ne 26 -or ($analysis.decimalPrecisions -join ',') -ne '60,90'){throw 'Cleft comparison failed or incomplete.'}
 foreach($flag in @('timeIntegrated','spatialGeometryCalibrated','physiologyQualified','avatarRecoveryAssessed','originalBoundsChanged')){if($analysis.$flag){throw ('Unexpected qualification or changed bound: '+$flag)}}
 $status.NumericalCriteriaPassed=$true;$status.Status='DIAGNOSTICS_COMPLETE';$status.ExitCode=0
}catch{
 $status.Status='FAILED';$status.ExitCode=1;$status['Error']=$_.Exception.ToString()
 Write-Host ('FAILED: '+$_.Exception.Message) -ForegroundColor Red
}finally{
 if($status.Status -eq 'RUNNING'){$status.Status='INTERRUPTED';$status.ExitCode=2}
 if(Test-Path -LiteralPath (Join-Path $package 'reference-preflight.json')){
  try{Python-Stage 'postflight';$status['NumericalEnvironmentUnchanged']=$true}
  catch{$status.Status='FAILED';$status.ExitCode=1;$status.NumericalCriteriaPassed=$false;$status['NumericalEnvironmentUnchanged']=$false;$status['EnvironmentError']=$_.Exception.ToString()}
 }
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
 $status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8
 try{& (Join-Path $package 'backup-run.ps1') -Source $package -Destination $configuration.BackupDestination}
 catch{$status.Status='FAILED';$status.ExitCode=1;$status['BackupError']=$_.Exception.ToString();$status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8;Write-Host ('BACKUP FAILED: '+$_.Exception.Message) -ForegroundColor Red}
 if($LaunchTaskName){Unregister-ScheduledTask -TaskName $LaunchTaskName -Confirm:$false -ErrorAction SilentlyContinue}
 Write-Host ('========== '+$status.Status+' ==========') -ForegroundColor Cyan
 Write-Host ('Results: '+$package)
 Write-Host 'Tell Orion the cleft constitutive outcome. Local numerical accuracy, coupled sensory dynamics, physiological calibration and Avatar balance are separate.'
}
