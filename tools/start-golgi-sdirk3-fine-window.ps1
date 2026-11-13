param([Parameter(Mandatory=$true)][string]$PackageDirectory,[switch]$CostStudyFinished,[switch]$InWindow,[string]$LaunchTaskName='')
$ErrorActionPreference='Stop'
function Get-EvidenceHash([string]$Path){
 $absolute=[IO.Path]::GetFullPath($Path)
 if(-not $absolute.StartsWith('\\?\')){$absolute='\\?\'+$absolute}
 $stream=[IO.File]::OpenRead($absolute);$algorithm=[Security.Cryptography.SHA256]::Create()
 try {return [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-','')}
 finally {$algorithm.Dispose();$stream.Dispose()}
}
. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
$package=(Resolve-Path -LiteralPath $PackageDirectory).Path
$configuration=Get-Content -LiteralPath (Join-Path $package 'configuration.json') -Raw | ConvertFrom-Json
$solverOrder=3;$expectedConditions=14;$expectedTests=43;$probeMode='--sdirk3-fine'
if(-not $CostStudyFinished){throw 'Wait for the operator to report CUDA cost timing complete; never poll it.'}
if(-not $configuration.BaselineReviewed -or -not $configuration.EnvironmentTransitionReviewed -or -not $configuration.NoLiveMonitoring -or -not $configuration.ReusePassiveReferences -or $configuration.SolverOrder -ne 3 -or $configuration.ExpectedConditions -ne 14 -or $configuration.ExpectedBaselineConditions -ne 38 -or $configuration.ExpectedTestCases -ne 43){throw 'Unexpected finer comparison configuration.'}
if(-not $InWindow){
 if(Test-Path -LiteralPath (Join-Path $package 'launch.json')){throw 'Already launched; use a fresh package.'}
 $manifestPath=Join-Path $package 'backup-manifest.json'
 $manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
 if($manifest.Destination -ne $configuration.BackupDestination){throw 'Initial backup destination differs.'}
 if((Get-EvidenceHash $manifestPath) -ne (Get-EvidenceHash (Join-Path $configuration.BackupDestination 'backup-manifest.json'))){throw 'Initial backup manifests differ.'}
 foreach($entry in $manifest.Files){
  foreach($directory in @($package,$configuration.BackupDestination)){
   if((Get-EvidenceHash (Join-Path $directory $entry.Path)) -ne $entry.Sha256){throw ('Initial backup or source differs: '+$entry.Path)}
  }
 }
 $taskName='DNNE-Golgi-Sdirk3-Fine-'+[Guid]::NewGuid().ToString('N').Substring(0,12)
 $shell=Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
 $arguments=@('-NoLogo','-NoProfile','-NoExit','-ExecutionPolicy','Bypass','-File',$PSCommandPath,'-InWindow','-CostStudyFinished','-LaunchTaskName',$taskName,'-PackageDirectory',$package)
 $action=New-ScheduledTaskAction -Execute $shell -Argument (($arguments | ForEach-Object {ConvertTo-ProcessArgument $_}) -join ' ') -WorkingDirectory $package
 $principal=New-ScheduledTaskPrincipal -UserId ([Security.Principal.WindowsIdentity]::GetCurrent().Name) -LogonType Interactive -RunLevel Limited
 $settings=New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
 Register-ScheduledTask -TaskName $taskName -Action $action -Principal $principal -Settings $settings | Out-Null
 [ordered]@{TaskName=$taskName;RequestedUtc=[DateTime]::UtcNow.ToString('o');Package=$package;NoLiveMonitoring=$true;CostStudyReportedFinished=$true} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $package 'launch.json') -Encoding UTF8
 Start-ScheduledTask -TaskName $taskName
 Write-Host ('Started independent visible PowerShell task: '+$taskName)
 Write-Host ('Results: '+$package)
 return
}
function Assert-Package {
 foreach($entry in $configuration.PackageFiles){if((Get-EvidenceHash (Join-Path $package $entry.Path)) -ne $entry.Sha256){throw ('Frozen input/source changed: '+$entry.Path)}}
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
function Capture-Binaries([string]$Assembly,[string]$Name){
 $entries=@(Get-ChildItem -LiteralPath (Split-Path -Parent $Assembly) -Recurse -File | ForEach-Object {[ordered]@{Path=$_.FullName;Sha256=(Get-EvidenceHash $_.FullName)}})
 $entries | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $package $Name) -Encoding UTF8
 return $entries
}
$status=[ordered]@{Status='RUNNING';ExitCode=$null;Revision=$configuration.Revision;LiveBrainAccessed=$false;OldSavesLoaded=$false;
 SolverOrder=$solverOrder;GpuExecuted=$false;SpeedupMeasured=$false;NumericalCriteriaPassed=$false;PhysiologyQualified=$false;AvatarRecoveryAssessed=$false;FullUnitSuitePassed=$false}
$statusPath=Join-Path $package 'window-status.json';$transcribing=$false
if(Test-Path -LiteralPath $statusPath){throw 'Refusing to overwrite an earlier result.'}
try {
 Start-Transcript -Path (Join-Path $package 'powershell.log') | Out-Null;$transcribing=$true
 $Host.UI.RawUI.WindowTitle='DNNE Golgi SDIRK'+$solverOrder+' - finer reference comparison'
 Write-Host ('Results: '+$package) -ForegroundColor Cyan
 Write-Host ($expectedTests.ToString()+' component checks, reference checks and '+$expectedConditions+' new conditions against 38 retained traces. Every failed condition is retained. Report completion to Orion.')
 $status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
 Assert-Package
 $inventory=[ordered]@{CapturedUtc=[DateTime]::UtcNow.ToString('o');CPU=@(Get-CimInstance Win32_Processor | Select-Object Name,NumberOfCores,NumberOfLogicalProcessors);
  RAMBytes=(Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory;Environment=[ordered]@{DOTNET_TieredCompilation=$env:DOTNET_TieredCompilation;DOTNET_TieredPGO=$env:DOTNET_TieredPGO}}
 $inventory | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $package 'machine.json') -Encoding UTF8
 $build=Join-Path $package 'source/artifacts/golgi-sdirk-tests'
 Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package 'source/tests/NeuralResonanceEngine.DNNE.Tests/NeuralResonanceEngine.DNNE.Tests.csproj'),'-c','Release','--artifacts-path',$build,'--nologo','-v','minimal') 'test-build.log'
 $assembly=Join-Path $build 'bin/NeuralResonanceEngine.DNNE.Tests/release/NeuralResonanceEngine.DNNE.Tests.dll'
 $testRoot=[IO.DirectoryInfo]::new((Split-Path -Parent $assembly))
 while($null -ne $testRoot -and -not (Test-Path -LiteralPath (Join-Path $testRoot.FullName 'NeuralResonanceEngine.DNNE.slnx'))){$testRoot=$testRoot.Parent}
 if($null -eq $testRoot -or -not $testRoot.FullName.Equals((Join-Path $package 'source'),[StringComparison]::OrdinalIgnoreCase)){throw 'Tests are not confined to frozen source.'}
 $testOutput=Join-Path $package 'test-results';New-Item -ItemType Directory -Path $testOutput | Out-Null
 $testBinaries=Capture-Binaries $assembly 'test-binaries.json'
 Invoke-Logged $configuration.DotnetExecutable @('vstest',$assembly,('/TestCaseFilter:'+$configuration.TestFilter),('/ResultsDirectory:'+$testOutput),'/Logger:trx;LogFileName=focused.trx') 'focused-tests.log'
 [xml]$trx=Get-Content -LiteralPath (Join-Path $testOutput 'focused.trx') -Raw
 $ns=[Xml.XmlNamespaceManager]::new($trx.NameTable);$ns.AddNamespace('t','http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
 $results=@($trx.SelectNodes('//t:UnitTestResult',$ns))
 if($results.Count -ne $configuration.ExpectedTestCases -or @($results | Where-Object {$_.outcome -ne 'Passed'}).Count){throw 'Focused tests failed or coverage was incomplete.'}
 foreach($class in $configuration.TestClasses){if(-not @($results | Where-Object {$_.testName -like ('NeuralResonanceEngine.DNNE.Tests.'+$class+'.*')}).Count){throw ('Missing test class: '+$class)}}
 if($configuration.TestClassCounts){foreach($class in $configuration.TestClassCounts.PSObject.Properties){
  if(@($results | Where-Object {$_.testName -like ('NeuralResonanceEngine.DNNE.Tests.'+$class.Name+'.*')}).Count -ne $class.Value){throw ('Incomplete test class: '+$class.Name)}
 }}
 $status['PassedFocusedTests']=$results.Count
 $script=Join-Path $package 'source/tools/analyse-golgi-sdirk3-fine.py'
 Invoke-Logged $configuration.PythonExecutable @('-B',$script,$package,'--preflight') 'reference-preflight.log'
 $status['ReferencePreflightPassed']=$true
 $status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
 $probeBuild=Join-Path $package 'source/artifacts/golgi-sdirk-build'
 Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package 'source/tools/GolgiCellProbe/GolgiCellProbe.csproj'),'-c','Release','--artifacts-path',$probeBuild,'--nologo','-v','minimal') 'probe-build.log'
 $probe=Join-Path $probeBuild 'bin/GolgiCellProbe/release/NeuralResonanceEngine.DNNE.Tests.dll'
 $probeBinaries=Capture-Binaries $probe 'probe-binaries.json'
 Invoke-Logged $configuration.DotnetExecutable @($probe,$probeMode,(Join-Path $package 'sdirk-input.json'),(Join-Path $package 'study')) 'candidate.log'
 Invoke-Logged $configuration.PythonExecutable @('-B',$script,$package) 'analysis.log'
 $analysis=Get-Content -LiteralPath (Join-Path $package 'fine-analysis.json') -Raw | ConvertFrom-Json
 if(-not $analysis.completed -or -not $analysis.numericalCriteriaPassed -or $analysis.caseCount -ne $expectedConditions -or $analysis.completedCases -ne $expectedConditions -or $analysis.assessment.Count -ne 8 -or $analysis.retainedBaselineCases -ne 38 -or $analysis.totalConditions -ne 52 -or $analysis.originalBoundsChanged){throw 'Required numerical evidence incomplete or failed.'}
 Assert-Package
 foreach($entry in @($testBinaries)+@($probeBinaries)){if((Get-EvidenceHash $entry.Path) -ne $entry.Sha256){throw ('Binary changed: '+$entry.Path)}}
 $status.NumericalCriteriaPassed=$true;$status.Status='DIAGNOSTICS_COMPLETE';$status.ExitCode=0
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
 Write-Host ('Tell Orion the Golgi SDIRK'+$solverOrder+' finer comparison outcome. Numerical accuracy, physiological calibration, circuit function and Avatar recovery are separate.')
}
