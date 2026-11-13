param([Parameter(Mandatory=$true)][string]$PackageDirectory,[switch]$InWindow,[string]$LaunchTaskName='')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
$package=(Resolve-Path -LiteralPath $PackageDirectory).Path
$configuration=Get-Content -LiteralPath (Join-Path $package 'configuration.json') -Raw | ConvertFrom-Json
if(-not $configuration.NoLiveMonitoring -or $configuration.ExpectedTestCases -ne 120 -or -not $configuration.IsolatedCausalMotor -or -not $configuration.QualifiedMuscleCorePreserved -or $configuration.LiveBodySelected -or -not $configuration.NoTimingMeasured -or $configuration.IndependentCoupledReferenceQualified -or $configuration.PhysiologicalCalibrationQualified -or $configuration.AvatarRecoveryAssessed){throw 'Unexpected isolated causal motor configuration.'}
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
  if((File-Sha256 (Join-Path $directory $entry.Path)) -ne $entry.Sha256){throw ('Initial backup or source differs: '+$entry.Path)}
 }}
 $taskName='DNNE-Causal-Motor-'+[Guid]::NewGuid().ToString('N').Substring(0,12)
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
 foreach($entry in $configuration.PackageFiles){if((File-Sha256 (Join-Path $package $entry.Path)) -ne $entry.Sha256){throw ('Frozen source/input changed: '+$entry.Path)}}
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
 CausalMotorAndRegressionChecksPassed=$false;ModelExecutionAttempted=$false;IncludesFixtureMotorEvents=$true;
 NeuronalModelExecuted=$false;LiveUnityIntegrated=$false;TimingMeasured=$false;IndependentCoupledReferenceQualified=$false;
 PhysiologyQualified=$false;AvatarRecoveryAssessed=$false;FullUnitSuitePassed=$false}
$statusPath=Join-Path $package 'window-status.json';$transcribing=$false
if(Test-Path -LiteralPath $statusPath){throw 'Refusing to overwrite an earlier result.'}
try {
 Start-Transcript -Path (Join-Path $package 'powershell.log') | Out-Null;$transcribing=$true
 $Host.UI.RawUI.WindowTitle='DNNE causal motor and body component checks'
 Write-Host ('Results: '+$package) -ForegroundColor Cyan
 Write-Host '27 causal motor/body checks and 93 muscle/transport regressions. Analytic joint and fixture events; no live Avatar. Report completion to Orion.'
 $status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
 Assert-Package
 $build=Join-Path $package 'source/artifacts/causal-motor-tests'
 Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package 'source/tests/NeuralResonanceEngine.DNNE.Tests/NeuralResonanceEngine.DNNE.Tests.csproj'),'-c','Release','--artifacts-path',$build,'--nologo','-v','minimal') 'test-build.log'
 $assembly=Join-Path $build 'bin/NeuralResonanceEngine.DNNE.Tests/release/NeuralResonanceEngine.DNNE.Tests.dll'
 $testRoot=[IO.DirectoryInfo]::new((Split-Path -Parent $assembly))
 while($null -ne $testRoot -and -not [IO.File]::Exists((Extended-Path (Join-Path $testRoot.FullName 'NeuralResonanceEngine.DNNE.slnx')))){$testRoot=$testRoot.Parent}
 if($null -eq $testRoot -or -not $testRoot.FullName.Equals((Join-Path $package 'source'),[StringComparison]::OrdinalIgnoreCase)){throw 'Tests are not confined to frozen source.'}
 $testOutput=Join-Path $package 'test-results';New-Item -ItemType Directory -Path $testOutput | Out-Null
 $binaryDirectory=Split-Path -Parent $assembly;$binaryIo=Extended-Path $binaryDirectory
 $binaries=@([IO.Directory]::EnumerateFiles($binaryIo,'*',[IO.SearchOption]::AllDirectories) | ForEach-Object {
  [ordered]@{Path=$binaryDirectory+$_.Substring($binaryIo.Length);Sha256=File-Sha256 $_}
 })
 ConvertTo-Json -InputObject @($binaries) -Depth 5 | Set-Content -LiteralPath (Join-Path $package 'test-binaries.json') -Encoding UTF8
 $status.ModelExecutionAttempted=$true
 Invoke-Logged $configuration.DotnetExecutable @('vstest',$assembly,('/TestCaseFilter:'+$configuration.TestFilter),('/ResultsDirectory:'+$testOutput),'/Logger:trx;LogFileName=focused.trx') 'focused-tests.log'
 [xml]$trx=Get-Content -LiteralPath (Join-Path $testOutput 'focused.trx') -Raw
 $ns=[Xml.XmlNamespaceManager]::new($trx.NameTable);$ns.AddNamespace('t','http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
 $results=@($trx.SelectNodes('//t:UnitTestResult',$ns))
 if($results.Count -ne $configuration.ExpectedTestCases -or @($results | Where-Object {$_.outcome -ne 'Passed'}).Count){throw 'Focused tests failed or coverage was incomplete.'}
 $requiredCounts=@{CausalMotorPlantTests=27;DynamicMuscleTests=16;MotorUnitCalciumTests=13;IsometricMotorUnitPlantTests=15;MotorAxonTransportTests=32;MotorAxonReplayBufferTests=16;SolutionManifestTests=1}
 foreach($class in $requiredCounts.Keys){
  if($configuration.ExpectedClassCounts.$class -ne $requiredCounts[$class]){throw ('Unexpected class count: '+$class)}
  $actual=@($results | Where-Object {$_.testName -like ('NeuralResonanceEngine.DNNE.Tests.'+$class+'.*')}).Count
  if($actual -ne $requiredCounts[$class]){throw ('Incorrect test coverage: '+$class)}
 }
 if(@($results.testName | Select-Object -Unique).Count -ne $results.Count){throw 'Repeated test names cannot replace missing checks.'}
 $definitions=@{};foreach($definition in @($trx.SelectNodes('//t:TestDefinitions/t:UnitTest',$ns))){
  $method=$definition.SelectSingleNode('t:TestMethod',$ns)
  $definitions[[string]$definition.id]=([string]$method.className+'.'+[string]$method.name)
 }
 $actualMethods=@{};foreach($result in $results){
  $method=$definitions[[string]$result.testId];if(-not $method){throw 'Test result lacks its method definition.'}
  $actualMethods[$method]++
 }
 $expectedMethods=@($configuration.ExpectedTestMethods.PSObject.Properties)
 if($actualMethods.Count -ne $expectedMethods.Count){throw 'Method coverage differs from frozen source.'}
 foreach($entry in $expectedMethods){if($actualMethods[$entry.Name] -ne $entry.Value){throw ('Missing or repeated method cases: '+$entry.Name)}}
 Assert-Package
 foreach($entry in $binaries){if((File-Sha256 $entry.Path) -ne $entry.Sha256){throw ('Test binary changed: '+$entry.Path)}}
 $status['PassedFocusedTests']=$results.Count;$status.CausalMotorAndRegressionChecksPassed=$true
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
 Write-Host ('Results: '+$package)
 Write-Host 'Tell Orion the causal motor/body outcome. Component causality, independent coupled accuracy, physiology and Avatar recovery are separate.'
}
