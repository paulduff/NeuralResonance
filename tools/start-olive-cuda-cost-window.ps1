param([Parameter(Mandatory=$true)][string]$PackageDirectory,[switch]$OtherStudiesFinished,[switch]$InWindow,[string]$LaunchTaskName='')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
$package=(Resolve-Path -LiteralPath $PackageDirectory).Path
$configuration=Get-Content -LiteralPath (Join-Path $package 'configuration.json') -Raw | ConvertFrom-Json
if(-not $configuration.TimingReviewed -or -not $configuration.NoLiveMonitoring -or $configuration.ExpectedCases -ne 6 -or $configuration.ExpectedProcesses -ne 90 -or $configuration.ExpectedTrials -ne 180 -or $configuration.ExpectedTestCases -ne 11){throw 'Unexpected cost configuration.'}
if(-not $OtherStudiesFinished){throw 'Wait for the operator to report other studies finished before timing; no automatic polling.'}
if(-not $InWindow){
 if(Test-Path -LiteralPath (Join-Path $package 'launch.json')){throw 'Already launched; use a fresh package.'}
 $manifestPath=Join-Path $package 'backup-manifest.json'
 $manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
 if($manifest.Destination -ne $configuration.BackupDestination){throw 'Initial backup destination differs.'}
 if((Get-FileHash -LiteralPath $manifestPath).Hash -ne (Get-FileHash -LiteralPath (Join-Path $configuration.BackupDestination 'backup-manifest.json')).Hash){throw 'Initial backup manifests differ.'}
 foreach($entry in $manifest.Files){foreach($directory in @($package,$configuration.BackupDestination)){
  if((Get-FileHash -LiteralPath (Join-Path $directory $entry.Path)).Hash -ne $entry.Sha256){throw ('Initial backup or source differs: '+$entry.Path)}
 }}
 $taskName='DNNE-Olive-Cost-'+[Guid]::NewGuid().ToString('N').Substring(0,12)
 $shell=Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
 $arguments=@('-NoLogo','-NoProfile','-NoExit','-ExecutionPolicy','Bypass','-File',$PSCommandPath,'-InWindow','-OtherStudiesFinished','-LaunchTaskName',$taskName,'-PackageDirectory',$package)
 $action=New-ScheduledTaskAction -Execute $shell -Argument (($arguments | ForEach-Object {ConvertTo-ProcessArgument $_}) -join ' ') -WorkingDirectory $package
 $principal=New-ScheduledTaskPrincipal -UserId ([Security.Principal.WindowsIdentity]::GetCurrent().Name) -LogonType Interactive -RunLevel Limited
 $settings=New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
 Register-ScheduledTask -TaskName $taskName -Action $action -Principal $principal -Settings $settings | Out-Null
 [ordered]@{TaskName=$taskName;RequestedUtc=[DateTime]::UtcNow.ToString('o');Package=$package;NoLiveMonitoring=$true;OtherStudiesReportedFinished=$true} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $package 'launch.json') -Encoding UTF8
 Start-ScheduledTask -TaskName $taskName
 Write-Host ('Started independent visible PowerShell task: '+$taskName)
 Write-Host ('Results: '+$package)
 return
}
function Assert-Package {
 foreach($entry in $configuration.PackageFiles){if((Get-FileHash -LiteralPath (Join-Path $package $entry.Path)).Hash -ne $entry.Sha256){throw ('Frozen source/input/dependency changed: '+$entry.Path)}}
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
 $entries=@(Get-ChildItem -LiteralPath (Split-Path -Parent $Assembly) -Recurse -File | ForEach-Object {[ordered]@{Path=$_.FullName;Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
 $entries | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $package $Name) -Encoding UTF8
 return $entries
}
$status=[ordered]@{Status='RUNNING';ExitCode=$null;Revision=$configuration.Revision;LiveBrainAccessed=$false;OldSavesLoaded=$false;
 CostMeasured=$false;AccuracyPassed=$false;PhysiologyQualified=$false;AvatarRecoveryAssessed=$false;FullUnitSuitePassed=$false}
$statusPath=Join-Path $package 'window-status.json';$transcribing=$false
if(Test-Path -LiteralPath $statusPath){throw 'Refusing to overwrite an earlier result.'}
try {
 Start-Transcript -Path (Join-Path $package 'powershell.log') | Out-Null;$transcribing=$true
 $Host.UI.RawUI.WindowTitle='DNNE olive CUDA - cost and instrumentation comparison'
 Write-Host ('Results: '+$package) -ForegroundColor Cyan
 Write-Host 'Three variants, six cases, five rounds: 90 measured trajectories plus 90 warmups. Avoid other heavy work; report completion to Orion.'
 $status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
 Assert-Package
 [ordered]@{CapturedUtc=[DateTime]::UtcNow.ToString('o');Cpu=@(Get-CimInstance Win32_Processor | Select-Object Name,NumberOfCores,NumberOfLogicalProcessors);OperatingSystem=Get-CimInstance Win32_OperatingSystem | Select-Object Caption,Version,TotalVisibleMemorySize,FreePhysicalMemory} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $package 'platform.json') -Encoding UTF8
 $build=Join-Path $package 'source/artifacts/olive-cost-tests'
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
 $status['PassedFocusedTests']=$results.Count
 $probeBinaries=@();$probes=@{}
 foreach($mode in @('control','host','device')){
  $probeBuild=Join-Path $package ('source/artifacts/olive-cost-build-'+$mode)
  Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package 'source/tools/OliveCudaCostProbe/OliveCudaCostProbe.csproj'),'-c','Release','--artifacts-path',$probeBuild,('-p:OliveCostMode='+$mode),'--nologo','-v','minimal') ($mode+'-build.log')
  $probe=Join-Path $probeBuild 'bin/OliveCudaCostProbe/release/NeuralResonanceEngine.DNNE.Tests.dll'
  $probeBinaries+=@(Capture-Binaries $probe ($mode+'-binaries.json'));$probes[$mode]=$probe
 }
 $previousTiering=$env:DOTNET_TieredCompilation
 try {
  $env:DOTNET_TieredCompilation='0'
  foreach($mode in @('control','host','device')){
   Invoke-Logged $configuration.DotnetExecutable @($probes[$mode],$package,(Join-Path $package ('study/controls/'+$mode)),'controls','0','0') ($mode+'-controls.log')
  }
  $schedule=@(Get-Content -LiteralPath (Join-Path $package 'schedule.json') -Raw | ConvertFrom-Json)
  if($schedule.Count -ne 90){throw 'Incomplete process schedule.'}
  $completedProcesses=[Collections.Generic.List[object]]::new();$index=0
  foreach($entry in $schedule){
   $step=([double]$entry.step).ToString('R',[Globalization.CultureInfo]::InvariantCulture)
   Invoke-Logged $configuration.DotnetExecutable @($probes[$entry.mode],$package,(Join-Path $package ('study/'+$entry.path)),([string]$entry.count),$step,([string]$entry.round)) ('case-'+$index.ToString('D3')+'.log')
   $completedProcesses.Add($entry);$index++
   ConvertTo-Json -InputObject $completedProcesses.ToArray() -Depth 6 | Set-Content -LiteralPath (Join-Path $package 'completed-processes.json') -Encoding UTF8
  }
 } finally {$env:DOTNET_TieredCompilation=$previousTiering}
 Invoke-Logged $configuration.PythonExecutable @('-B',(Join-Path $package 'source/tools/analyse-olive-cuda-cost.py'),$package) 'analysis.log'
 $analysis=Get-Content -LiteralPath (Join-Path $package 'cost-analysis.json') -Raw | ConvertFrom-Json
 if(-not $analysis.completed -or -not $analysis.accuracyPassed -or -not $analysis.phaseAccountingPassed -or $analysis.trials -ne 180 -or $analysis.processes -ne 90 -or $analysis.cases.Count -ne 6 -or -not $analysis.contextCleanupReported){throw 'Required cost/accuracy evidence incomplete.'}
 Assert-Package
 foreach($entry in @($testBinaries)+@($probeBinaries)){if((Get-FileHash -LiteralPath $entry.Path).Hash -ne $entry.Sha256){throw ('Binary changed: '+$entry.Path)}}
 $status.CostMeasured=$true;$status.AccuracyPassed=$true;$status['CompletedTrials']=180;$status.Status='DIAGNOSTICS_COMPLETE';$status.ExitCode=0
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
 Write-Host 'Tell Orion the olive CUDA cost outcome. Profiling overhead, whole-brain performance, physiology and Avatar recovery are separate.'
}
