param([Parameter(Mandatory=$true)][string]$PackageDirectory,[switch]$InWindow,[string]$LaunchTaskName='')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
$package=(Resolve-Path -LiteralPath $PackageDirectory).Path
$configuration=Get-Content -LiteralPath (Join-Path $package 'configuration.json') -Raw | ConvertFrom-Json
if(-not $configuration.IsolatedSkullKinematics -or -not $configuration.NoLiveMonitoring -or $configuration.ExpectedTestCases -ne 74 -or $configuration.ExpectedConditions -ne 20 -or $configuration.ExpectedNativeCaptures -ne 40 -or $configuration.NativeHeadCaptureRequested -or $configuration.PhysiologyQualified -or $configuration.AvatarRecoveryAssessed){throw 'Unexpected skull component configuration.'}
function Extended-Path([string]$Path){if($Path.StartsWith('\\?\')){return $Path};if($Path.StartsWith('\\')){return '\\?\UNC\'+$Path.Substring(2)};return '\\?\'+[IO.Path]::GetFullPath($Path)}
function File-Sha256([string]$Path){
 $stream=[IO.File]::OpenRead((Extended-Path $Path));$algorithm=[Security.Cryptography.SHA256]::Create()
 try{return [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-','')}finally{$algorithm.Dispose();$stream.Dispose()}
}
function Assert-Package {
 foreach($entry in $configuration.PackageFiles){if((File-Sha256 (Join-Path $package $entry.Path)) -ne $entry.Sha256){throw ('Frozen source/input changed: '+$entry.Path)}}
 foreach($name in @('Dotnet','Python')){if((File-Sha256 $configuration.($name+'Executable')) -ne $configuration.($name+'ExecutableSha256')){throw ($name+' executable changed.')}}
}
if(-not $InWindow){
 if(Test-Path -LiteralPath (Join-Path $package 'launch.json')){throw 'Already launched; prepare a fresh capture.'}
 $manifestPath=Join-Path $package 'backup-manifest.json';$manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
 if($manifest.Destination -ne $configuration.BackupDestination -or (File-Sha256 $manifestPath) -ne (File-Sha256 (Join-Path $configuration.BackupDestination 'backup-manifest.json'))){throw 'Initial backup manifests differ.'}
 foreach($entry in $manifest.Files){foreach($directory in @($package,$configuration.BackupDestination)){if((File-Sha256 (Join-Path $directory $entry.Path)) -ne $entry.Sha256){throw ('Initial backup differs: '+$entry.Path)}}}
 Assert-Package
 $taskName='DNNE-Skull-Kinematics-'+[Guid]::NewGuid().ToString('N').Substring(0,12)
 $shell=Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
 $arguments=@('-NoLogo','-NoProfile','-NoExit','-ExecutionPolicy','Bypass','-File',$PSCommandPath,'-InWindow','-LaunchTaskName',$taskName,'-PackageDirectory',$package)
 $action=New-ScheduledTaskAction -Execute $shell -Argument (($arguments | ForEach-Object {ConvertTo-ProcessArgument $_}) -join ' ') -WorkingDirectory $package
 $principal=New-ScheduledTaskPrincipal -UserId ([Security.Principal.WindowsIdentity]::GetCurrent().Name) -LogonType Interactive -RunLevel Limited
 $settings=New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
 Register-ScheduledTask -TaskName $taskName -Action $action -Principal $principal -Settings $settings | Out-Null
 [ordered]@{TaskName=$taskName;RequestedUtc=[DateTime]::UtcNow.ToString('o');Package=$package;NoLiveMonitoring=$true} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $package 'launch.json') -Encoding UTF8
 Start-ScheduledTask -TaskName $taskName
 Write-Host ('Started independent visible PowerShell task: '+$taskName);Write-Host ('Results: '+$package);return
}
function Invoke-Logged([string]$Executable,[string[]]$NativeArguments,[string]$LogName){
 $writer=[IO.StreamWriter]::new((Join-Path $package $LogName),$false,[Text.UTF8Encoding]::new($false));$writer.AutoFlush=$true;$previous=$ErrorActionPreference
 try{$ErrorActionPreference='Continue';& $Executable @NativeArguments 2>&1 | ForEach-Object {$line=[string]$_;$writer.WriteLine($line);Write-Host $line} -ErrorAction Stop;$nativeExit=$LASTEXITCODE}
 finally{$writer.Dispose();$ErrorActionPreference=$previous}
 if($nativeExit -ne 0){throw ($LogName+' exited '+$nativeExit)}
}
function Capture-Binaries([string]$Assembly,[string]$Name){
 $entries=@([IO.Directory]::EnumerateFiles((Extended-Path (Split-Path -Parent $Assembly)),'*',[IO.SearchOption]::AllDirectories) | ForEach-Object {[ordered]@{Path=$_.Substring(4);Sha256=(File-Sha256 $_)}})
 $entries | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $package $Name) -Encoding UTF8;return $entries
}
$status=[ordered]@{Status='RUNNING';ExitCode=$null;Revision=$configuration.Revision;FocusedTestsPassed=$false;ComponentChecksPassed=$false;
 ModelExecutionAttempted=$false;NativeHeadCaptureExecuted=$false;IndependentHeadTrajectoryQualified=$false;
 NeuronalModelExecuted=$false;LiveBrainAccessed=$false;OldSavesLoaded=$false;PhysiologyQualified=$false;AvatarRecoveryAssessed=$false;FullUnitSuitePassed=$false}
$statusPath=Join-Path $package 'window-status.json';$transcribing=$false;$testBinaries=@();$probeBinaries=@();$probe=''
if(Test-Path -LiteralPath $statusPath){throw 'Refusing to replace earlier evidence.'}
try{
 Start-Transcript -Path (Join-Path $package 'powershell.log') | Out-Null;$transcribing=$true
 $Host.UI.RawUI.WindowTitle='DNNE skull coordinate and clock components'
 Write-Host ('Results: '+$package) -ForegroundColor Cyan
 Write-Host '74 focused checks; 40 C# vestibular traces must exactly reproduce the completed baseline. No native Unity head or live brain.'
 $status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8;Assert-Package
 $build=Join-Path $package 'source/artifacts/skull-tests'
 Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package 'source/tests/NeuralResonanceEngine.DNNE.Tests/NeuralResonanceEngine.DNNE.Tests.csproj'),'-c','Release','--artifacts-path',$build,'--nologo','-v','minimal') 'test-build.log'
 $assembly=Join-Path $build 'bin/NeuralResonanceEngine.DNNE.Tests/release/NeuralResonanceEngine.DNNE.Tests.dll'
 $testRoot=[IO.DirectoryInfo]::new((Split-Path -Parent $assembly))
 while($null -ne $testRoot -and -not (Test-Path -LiteralPath (Join-Path $testRoot.FullName 'NeuralResonanceEngine.DNNE.slnx'))){$testRoot=$testRoot.Parent}
 if($null -eq $testRoot -or -not $testRoot.FullName.Equals((Join-Path $package 'source'),[StringComparison]::OrdinalIgnoreCase)){throw 'Tests are not confined to frozen source.'}
 $testOutput=Join-Path $package 'test-results';New-Item -ItemType Directory -Path $testOutput | Out-Null
 $testBinaries=Capture-Binaries $assembly 'test-binaries.json';$status.ModelExecutionAttempted=$true
 Invoke-Logged $configuration.DotnetExecutable @('vstest',$assembly,('/TestCaseFilter:'+$configuration.TestFilter),('/ResultsDirectory:'+$testOutput),'/Logger:trx;LogFileName=focused.trx') 'focused-tests.log'
 Invoke-Logged $configuration.PythonExecutable @('-I','-B',(Join-Path $package 'source/tools/skull_kinematics_gate.py'),'check-tests',$package) 'test-inventory.log'
 $status.FocusedTestsPassed=$true;$status['PassedFocusedTests']=74
 $build=Join-Path $package 'source/artifacts/skull-probe'
 Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package 'source/tools/NativeMotorBridgeProbe/NativeMotorBridgeProbe.csproj'),'-c','Release','--artifacts-path',$build,'--nologo','-v','minimal') 'probe-build.log'
 $probe=Join-Path $build 'bin/NativeMotorBridgeProbe/release/NeuralResonanceEngine.DNNE.Tests.dll';$probeBinaries=Capture-Binaries $probe 'probe-binaries.json'
 Invoke-Logged $configuration.DotnetExecutable @($probe,'--runtime-fingerprint',(Join-Path $package 'managed-runtime-before.json')) 'managed-runtime-before.log'
 if((File-Sha256 (Join-Path $package 'managed-runtime-before.json')) -ne (File-Sha256 (Join-Path $package 'reference/qualified-managed-runtime.json'))){throw 'Managed dependencies differ from the qualified baseline.'}
 Invoke-Logged $configuration.DotnetExecutable @($probe,'--vestibular-mechanics',(Join-Path $package 'vestibular-input.json'),(Join-Path $package 'study')) 'study.log'
 Invoke-Logged $configuration.PythonExecutable @('-I','-B',(Join-Path $package 'source/tools/skull_kinematics_gate.py'),'compare',$package) 'analysis.log'
 $analysis=Get-Content -LiteralPath (Join-Path $package 'analysis.json') -Raw | ConvertFrom-Json
 if(-not $analysis.componentChecksPassed -or $analysis.tests.passedCases -ne 74 -or $analysis.defaultNativeFilesPreserved -ne 61 -or $analysis.defaultNativeCapturesPreserved -ne 40 -or $analysis.newSkullComponentCases -ne 12){throw 'Head components or preserved default mechanics did not qualify.'}
 foreach($flag in @('nativeHeadCaptureExecuted','independentHeadTrajectoryQualified','continuousHeadMotionAccuracyQualified','neuronalModelExecuted','liveBrainAccessed','physiologyQualified','avatarRecoveryAssessed')){if($analysis.$flag){throw ('Unexpected scope: '+$flag)}}
 $status.ComponentChecksPassed=$true;$status.Status='DIAGNOSTICS_COMPLETE';$status.ExitCode=0
}catch{$status.Status='FAILED';$status.ExitCode=1;$status['Error']=$_.Exception.ToString();Write-Host ('FAILED: '+$_.Exception.Message) -ForegroundColor Red}
finally{
 if($status.Status -eq 'RUNNING'){$status.Status='INTERRUPTED';$status.ExitCode=2}
 if($probe -and (Test-Path -LiteralPath (Join-Path $package 'managed-runtime-before.json'))){
  try{Invoke-Logged $configuration.DotnetExecutable @($probe,'--runtime-fingerprint',(Join-Path $package 'managed-runtime-after.json')) 'managed-runtime-after.log'
   if((File-Sha256 (Join-Path $package 'managed-runtime-before.json')) -ne (File-Sha256 (Join-Path $package 'managed-runtime-after.json'))){throw 'Managed runtime changed.'};$status['ManagedRuntimePreserved']=$true}
  catch{$status.Status='FAILED';$status.ExitCode=1;$status.ComponentChecksPassed=$false;$status['RuntimeError']=$_.Exception.ToString()}
 }
 try{Assert-Package;foreach($entry in @($testBinaries)+@($probeBinaries)){if((File-Sha256 $entry.Path) -ne $entry.Sha256){throw ('Built binary changed: '+$entry.Path)}};$status['FrozenPackageUnchanged']=$true}
 catch{$status.Status='FAILED';$status.ExitCode=1;$status.ComponentChecksPassed=$false;$status['FrozenSourceError']=$_.Exception.ToString()}
 $status['CompletedUtc']=[DateTime]::UtcNow.ToString('o');if($transcribing){Stop-Transcript | Out-Null}
 $status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8
 try{& (Join-Path $package 'backup-run.ps1') -Source $package -Destination $configuration.BackupDestination}
 catch{$status.Status='FAILED';$status.ExitCode=1;$status['BackupError']=$_.Exception.ToString();$status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8;Write-Host ('BACKUP FAILED: '+$_.Exception.Message) -ForegroundColor Red}
 if($LaunchTaskName){Unregister-ScheduledTask -TaskName $LaunchTaskName -Confirm:$false -ErrorAction SilentlyContinue}
 Write-Host ('========== '+$status.Status+' ==========') -ForegroundColor Cyan;Write-Host ('Results: '+$package)
 Write-Host 'Tell Orion the skull coordinate outcome. Component checks, native head sampling, afferent physiology and Avatar balance are separate.'
}
