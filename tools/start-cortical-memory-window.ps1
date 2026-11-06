param([Parameter(Mandatory=$true)][string]$PackageDirectory,[switch]$InWindow,[string]$LaunchTaskName='')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
$package=(Resolve-Path -LiteralPath $PackageDirectory).Path
$configuration=Get-Content -LiteralPath (Join-Path $package 'configuration.json') -Raw | ConvertFrom-Json
if(-not $InWindow){
 if(Test-Path -LiteralPath (Join-Path $package 'launch.json')){throw 'Already launched; use a new package.'}
 $taskName='DNNE-Cortical-Memory-'+[Guid]::NewGuid().ToString('N').Substring(0,12)
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
 foreach($entry in $configuration.PackageFiles){if((Get-FileHash -LiteralPath (Join-Path $package $entry.Path) -Algorithm SHA256).Hash -ne $entry.Sha256){throw ('Frozen source changed: '+$entry.Path)}}
}
function Invoke-Logged([string]$Executable,[string[]]$NativeArguments,[string]$LogName,[switch]$AllowTestFailure){
 $writer=[IO.StreamWriter]::new((Join-Path $package $LogName),$false,[Text.UTF8Encoding]::new($false));$writer.AutoFlush=$true
 $previous=$ErrorActionPreference
 try {
  $ErrorActionPreference='Continue'
  & $Executable @NativeArguments 2>&1 | ForEach-Object {$line=[string]$_;$writer.WriteLine($line);Write-Host $line} -ErrorAction Stop
  $nativeExit=$LASTEXITCODE
 } finally {$writer.Dispose();$ErrorActionPreference=$previous}
 if($nativeExit -ne 0 -and -not $AllowTestFailure){throw ($LogName+' exited '+$nativeExit)}
 return $nativeExit
}
$status=[ordered]@{Status='RUNNING';ExitCode=$null;Revision=$configuration.Revision;LiveBrainAccessed=$false;OldSavesLoaded=$false;BiologicalConsolidationQualified=$false;FullUnitSuitePassed=$false}
$statusPath=Join-Path $package 'window-status.json';$transcribing=$false
if(Test-Path -LiteralPath $statusPath){throw 'Refusing to overwrite an earlier result.'}
try {
 Start-Transcript -Path (Join-Path $package 'powershell.log') | Out-Null;$transcribing=$true
 $Host.UI.RawUI.WindowTitle='DNNE cortical recipient comparison'
 Write-Host ('Results: '+$package) -ForegroundColor Cyan
 Write-Host 'The original consolidation assertion remains unchanged. Its known failure is captured separately from diagnostic completion.'
 $status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
 Assert-Package
 $build=Join-Path $package 'source/artifacts/memory-tests'
 $null=Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package 'source/tests/NeuralResonanceEngine.DNNE.Tests/NeuralResonanceEngine.DNNE.Tests.csproj'),'-c','Release','--artifacts-path',$build,'--nologo','-v','minimal') 'test-build.log'
 $assembly=Join-Path $build 'bin/NeuralResonanceEngine.DNNE.Tests/release/NeuralResonanceEngine.DNNE.Tests.dll'
 $testRoot=[IO.DirectoryInfo]::new((Split-Path -Parent $assembly))
 while($null -ne $testRoot -and -not (Test-Path -LiteralPath (Join-Path $testRoot.FullName 'NeuralResonanceEngine.DNNE.slnx'))){$testRoot=$testRoot.Parent}
 if($null -eq $testRoot -or -not $testRoot.FullName.Equals((Join-Path $package 'source'),[StringComparison]::OrdinalIgnoreCase)){throw 'Tests are not confined to frozen source.'}
 $testOutput=Join-Path $package 'test-results';New-Item -ItemType Directory -Path $testOutput | Out-Null
 $binaries=@(Get-ChildItem -LiteralPath (Split-Path -Parent $assembly) -Recurse -File | ForEach-Object {[ordered]@{Path=$_.FullName;Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
 $binaries | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $package 'test-binaries.json') -Encoding UTF8
 $exitCode=Invoke-Logged $configuration.DotnetExecutable @('vstest',$assembly,('/TestCaseFilter:'+$configuration.TestFilter),('/ResultsDirectory:'+$testOutput),'/Logger:trx;LogFileName=memory-fixture.trx') 'original-fixture.log' -AllowTestFailure
 [xml]$trx=Get-Content -LiteralPath (Join-Path $testOutput 'memory-fixture.trx') -Raw
 $ns=[Xml.XmlNamespaceManager]::new($trx.NameTable);$ns.AddNamespace('t','http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
 $results=@($trx.SelectNodes('//t:UnitTestResult',$ns))
 if($results.Count -ne $configuration.ExpectedTestCases){throw 'Original fixture/manifest/recipient test coverage incomplete.'}
 $memory=@($results | Where-Object {$_.testName -eq 'NeuralResonanceEngine.DNNE.Tests.NeuronalMemoryTests.RepeatedCorticalExperienceGraduallyRaisesConsolidation'})
 $manifest=@($results | Where-Object {$_.testName -eq 'NeuralResonanceEngine.DNNE.Tests.SolutionManifestTests.Solution_Manifest_Includes_Every_Project_In_The_Repository'})
 if($memory.Count -ne 1 -or $manifest.Count -ne 1 -or $manifest[0].outcome -ne 'Passed' -or $memory[0].outcome -notin @('Passed','Failed')){throw 'Unexpected or incomplete unit outcome.'}
 $targetTests=@($results | Where-Object {$_.testName -like 'NeuralResonanceEngine.DNNE.Tests.DeclaredSynapticTargetTests.*'})
 if($targetTests.Count -ne 8 -or @($targetTests | Where-Object {$_.outcome -ne 'Passed'}).Count){throw 'Declared recipient checks failed or were incomplete.'}
 $status['PassedRecipientTests']=$targetTests.Count
 if($exitCode -notin @(0,1) -or (($exitCode -eq 0) -ne ($memory[0].outcome -eq 'Passed'))){throw 'Process/test outcome mismatch.'}
 $status['OriginalConsolidationTestPassed']=$memory[0].outcome -eq 'Passed'
 $status['OriginalTestProcessExitCode']=$exitCode
 if($memory[0].outcome -eq 'Failed'){
  $message=$memory[0].SelectSingleNode('t:Output/t:ErrorInfo/t:Message',$ns).InnerText
  if($message -notmatch '^initial=.*consolidated=.*strength='){throw 'The original test failed for an unexpected reason.'}
  $status['OriginalConsolidationFailure']=$message
 }
 $null=Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package 'source/tools/MemoryConsolidationProbe/MemoryConsolidationProbe.csproj'),'-c','Release','--artifacts-path',$build,'--nologo','-v','minimal') 'probe-build.log'
 $probe=Join-Path $build 'bin/MemoryConsolidationProbe/release/NeuralResonanceEngine.DNNE.Tests.dll'
 $probeBinaries=@(Get-ChildItem -LiteralPath (Split-Path -Parent $probe) -Recurse -File | ForEach-Object {[ordered]@{Path=$_.FullName;Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
 $probeBinaries | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $package 'probe-binaries.json') -Encoding UTF8
 $null=Invoke-Logged $configuration.DotnetExecutable @($probe,(Join-Path $package 'study')) 'study.log'
 $null=Invoke-Logged $configuration.PythonExecutable @('-B',(Join-Path $package 'source/tools/analyse-cortical-memory.py'),(Join-Path $package 'study')) 'analysis.log'
 Assert-Package
 foreach($entry in @($binaries)+@($probeBinaries)){if((Get-FileHash -LiteralPath $entry.Path).Hash -ne $entry.Sha256){throw ('Binary changed: '+$entry.Path)}}
 $status['DiagnosticCaptureComplete']=$true;$status.Status='DIAGNOSTICS_COMPLETE';$status.ExitCode=0
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
 Write-Host ('Original consolidation test passed: '+$status['OriginalConsolidationTestPassed'])
 Write-Host 'Tell Orion the cortical learning diagnosis outcome. Diagnostic completion is not a repaired learning rule or biological consolidation.'
}
