param([Parameter(Mandatory=$true)][string]$PackageDirectory,[switch]$InWindow,[string]$LaunchTaskName='')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
$package=(Resolve-Path -LiteralPath $PackageDirectory).Path
$configuration=Get-Content -LiteralPath (Join-Path $package 'configuration.json') -Raw | ConvertFrom-Json
if(-not $configuration.NoLiveMonitoring -or -not $configuration.NativeArticulatedHeadStudy -or $configuration.ExpectedCases -ne 12 -or $configuration.RequiredComponentCases -ne 341 -or -not $configuration.DeclaredBodyMechanicsStudy){throw 'Unexpected native head protocol.'}
function Sha([string]$Path){(Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash}
function Assert-Package {
 foreach($entry in $configuration.PackageFiles){if((Sha (Join-Path $package $entry.Path)) -ne $entry.Sha256){throw ('Frozen input changed: '+$entry.Path)}}
 foreach($name in @('Dotnet','Python','Unity')){if((Sha $configuration.($name+'Executable')) -ne $configuration.($name+'ExecutableSha256')){throw ($name+' executable changed.')}}
}
if(-not $InWindow){
 if(Test-Path -LiteralPath (Join-Path $package 'launch.json')){throw 'Already launched; use a fresh package.'}
 $manifest=Get-Content -LiteralPath (Join-Path $package 'backup-manifest.json') -Raw | ConvertFrom-Json
 if($manifest.Destination -ne $configuration.BackupDestination -or (Sha (Join-Path $package 'backup-manifest.json')) -ne (Sha (Join-Path $configuration.BackupDestination 'backup-manifest.json'))){throw 'Initial backups differ.'}
 foreach($entry in $manifest.Files){foreach($directory in @($package,$configuration.BackupDestination)){
  if((Sha (Join-Path $directory $entry.Path)) -ne $entry.Sha256){throw ('Initial source/backup differs: '+$entry.Path)}
 }}
 Assert-Package
 $taskName='DNNE-Declared-Body-Mechanics-'+[Guid]::NewGuid().ToString('N').Substring(0,12)
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
function Copy-Tree([string]$Source,[string]$Destination){
 if(Test-Path -LiteralPath $Destination){throw ('Copy destination already exists: '+$Destination)}
 [IO.Directory]::CreateDirectory($Destination) | Out-Null
 foreach($path in [IO.Directory]::EnumerateFiles($Source,'*',[IO.SearchOption]::AllDirectories)){
  $target=Join-Path $Destination ($path.Substring($Source.Length).TrimStart('\','/'))
  [IO.Directory]::CreateDirectory((Split-Path $target -Parent)) | Out-Null
  [IO.File]::Copy($path,$target,$false)
  if((Sha $path) -ne (Sha $target)){throw ('Copy differs: '+$path)}
 }
}
function Capture-Files([string]$Directory,[string]$Name){
 $files=@(Get-ChildItem -LiteralPath $Directory -File -Recurse | Sort-Object FullName)
 Write-Host ('Preserving '+$files.Count+' dependency files: '+$Directory) -ForegroundColor Cyan
 $entries=[Collections.Generic.List[object]]::new();$index=0
 foreach($file in $files){
  $entries.Add([ordered]@{Path=$file.FullName;Sha256=Sha $file.FullName});$index++
  if($index%100 -eq 0 -or $index -eq $files.Count){Write-Host ('Hashed '+$index+' / '+$files.Count+' files ('+$Name+')')}
 }
 ConvertTo-Json -InputObject $entries.ToArray() -Depth 5 | Set-Content -LiteralPath (Join-Path $package $Name) -Encoding UTF8
 return $entries.ToArray()
}
function Verify-Files($entries){foreach($entry in $entries){if((Sha $entry.Path) -ne $entry.Sha256){throw ('Executed dependency changed: '+$entry.Path)}}}
function Stage([string]$stage){Invoke-Logged $configuration.PythonExecutable @('-I','-B',(Join-Path $package 'source/tools/native_body_mechanics_study.py'),$stage,$package) ($stage+'.log')}
$status=[ordered]@{Status='RUNNING';ExitCode=$null;Revision=$configuration.Revision;NoLiveMonitoring=$true;
 RequiredComponentCases=341;NativeExecutionAttempted=$false;NativeHeadDeliveryChecksPassed=$false;
 LiveBrainAccessed=$false;OldSavesLoaded=$false;NeuronalSourceExecuted=$false;MuscleCellExecuted=$false;
 PhysiologicalFiringQualified=$false;AvatarBalanceQualified=$false}
$statusPath=Join-Path $package 'window-status.json';$transcribing=$false;$probe=$null;$binaries=@();$testBinaries=@();$playerFiles=@()
if(Test-Path -LiteralPath $statusPath){throw 'Refusing to overwrite an earlier run.'}
try {
 Start-Transcript -Path (Join-Path $package 'powershell.log') | Out-Null;$transcribing=$true
 $Host.UI.RawUI.WindowTitle='DNNE declared tendon mechanics - original and no joint friction'
 Write-Host ('Results: '+$package) -ForegroundColor Cyan
 Write-Host '341 component checks, then 12 actual body/head cases across both declared settings. No neuronal or muscle-cell source. This window stays open after backup.'
 $status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
 Assert-Package;Stage 'preflight'
 $testBuild=Join-Path $package 'source/artifacts/declared-mechanics-tests'
 Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package 'source/tests/NeuralResonanceEngine.DNNE.Tests/NeuralResonanceEngine.DNNE.Tests.csproj'),'-c','Release','--artifacts-path',$testBuild,'--nologo','-v','minimal') 'test-build.log'
 $assembly=Join-Path $testBuild 'bin/NeuralResonanceEngine.DNNE.Tests/release/NeuralResonanceEngine.DNNE.Tests.dll'
 $testBinaries=Capture-Files (Split-Path $assembly -Parent) 'test-binaries.json'
 $build=Join-Path $package 'source/artifacts/native-head-host'
 Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package 'source/tools/NativeMotorBridgeProbe/NativeMotorBridgeProbe.csproj'),'-c','Release','--artifacts-path',$build,'--nologo','-v','minimal') 'probe-build.log'
 $probe=Join-Path $build 'bin/NativeMotorBridgeProbe/release/NeuralResonanceEngine.DNNE.Tests.dll'
 $binaries=@(Get-ChildItem -LiteralPath (Split-Path $probe -Parent) -File -Recurse | Sort-Object FullName | ForEach-Object {[ordered]@{Path=$_.FullName;Sha256=Sha $_.FullName}})
 ConvertTo-Json -InputObject $binaries -Depth 5 | Set-Content -LiteralPath (Join-Path $package 'probe-binaries.json') -Encoding UTF8
 Invoke-Logged $configuration.DotnetExecutable @($probe,'--runtime-fingerprint',(Join-Path $package 'managed-runtime-before.json')) 'managed-runtime-before.log'
 Stage 'runtime'
 $testOutput=Join-Path $package 'test-results';[IO.Directory]::CreateDirectory($testOutput) | Out-Null
 $env:NRE_SYNAPSE_STATE_DIR=Join-Path $package 'isolated-synapses'
 $env:NRE_RETICULOSPINAL_EVIDENCE_DIR=Join-Path $package 'partition-evidence'
 $env:NRE_DESCENDING_ANATOMY_EVIDENCE_DIR=Join-Path $package 'anatomy-evidence'
 $status['TestExecutionAttempted']=$true
 Invoke-Logged $configuration.DotnetExecutable @('vstest',$assembly,('/TestCaseFilter:'+$configuration.TestFilter),('/ResultsDirectory:'+$testOutput),'/Logger:trx;LogFileName=focused.trx') 'focused-tests.log'
 Stage 'check-tests';$status['PassedFocusedTests']=341
 $workProject=Join-Path $configuration.UnityBuildWorkspace 'UnityWorld'
 Copy-Tree (Join-Path $package 'source/UnityWorld') $workProject
 $playerOutput=Join-Path $package 'player';[IO.Directory]::CreateDirectory($playerOutput) | Out-Null
 Write-Host 'Building a separate Unity player for the full Avatar force paths...' -ForegroundColor Cyan
 $priorOutput=[Environment]::GetEnvironmentVariable('NRE_UNITY_OUTPUT','Process')
 try{
  [Environment]::SetEnvironmentVariable('NRE_UNITY_OUTPUT',$playerOutput,'Process')
  $arguments=@('-batchmode','-nographics','-projectPath',$workProject,'-executeMethod','Nre.UnityWorld.Editor.WorldBuild.BuildWindows','-quit','-logFile',(Join-Path $playerOutput 'build.log'))
  $editorProcess=Start-Process -FilePath $configuration.UnityExecutable -ArgumentList (($arguments | ForEach-Object {ConvertTo-ProcessArgument $_}) -join ' ') -WorkingDirectory $workProject -WindowStyle Hidden -PassThru
  if(-not $editorProcess.WaitForExit(1800000)){$editorProcess.Kill();$editorProcess.WaitForExit();throw 'Dedicated Unity build exceeded 30 minutes.'}
  if($editorProcess.ExitCode -ne 0){throw 'Unity editor build failed; inspect preserved build.log.'}
 }finally{[Environment]::SetEnvironmentVariable('NRE_UNITY_OUTPUT',$priorOutput,'Process')}
 $buildResult=Get-Content -LiteralPath (Join-Path $playerOutput 'build-result.json') -Raw | ConvertFrom-Json
 if($buildResult.result -ne 'Succeeded' -or $buildResult.errors -ne 0){throw 'Native Unity build failed.'}
 $sourceUnity=Join-Path $package 'source/UnityWorld'
 foreach($file in Get-ChildItem -LiteralPath $sourceUnity -Filter '*.cs' -Recurse -File){
  $relative=$file.FullName.Substring($sourceUnity.Length).TrimStart('\','/')
  if((Sha $file.FullName) -ne (Sha (Join-Path $workProject $relative))){throw ('Compiled Unity source changed: '+$relative)}
 }
 foreach($folder in @('Assets','Packages','ProjectSettings')){Copy-Tree (Join-Path $workProject $folder) (Join-Path $package ('built-project/'+$folder))}
 $playerFiles=Capture-Files $playerOutput 'player-files.json'
 $status['UnityBuildSucceeded']=$true

 $status.NativeExecutionAttempted=$true;$status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
 foreach($mode in $configuration.Modes){
  $original=Join-Path $package ('friction-evidence/native/'+$mode+'__quiet')
  $output=Join-Path $package ('conditions/'+$mode+'/native')
  Invoke-Logged $configuration.DotnetExecutable @($probe,'--articulated-head-native-mechanics',(Join-Path $playerOutput 'DNNE Unity World.exe'),$original,$output,$mode) ('native-'+$mode+'.log')
 }

 Stage 'compare'
 $analysis=Get-Content -LiteralPath (Join-Path $package 'analysis.json') -Raw | ConvertFrom-Json
 if(-not $analysis.declaredMechanicsChecksPassed -or $analysis.actualPhysicsSteps -ne 96 -or $analysis.headReceipts -ne 324){throw 'Actual native head evidence incomplete.'}
 $status.NativeHeadDeliveryChecksPassed=$true;$status['DeclaredMechanicsChecksPassed']=$true;$status.Status='DIAGNOSTICS_COMPLETE';$status.ExitCode=0
}catch{
 $status.Status='FAILED';$status.ExitCode=1;$status['Error']=$_.Exception.ToString();Write-Host ('FAILED: '+$_.Exception.Message) -ForegroundColor Red
}finally{
 try{
  Stage 'preflight';Assert-Package
  foreach($entry in @($binaries)+@($testBinaries)+@($playerFiles)){if((Sha $entry.Path) -ne $entry.Sha256){throw ('Executed host binary changed: '+$entry.Path)}}
  $status['FrozenInputsPreserved']=$true
 }catch{$status.Status='FAILED';$status.ExitCode=1;$status['PreservationError']=$_.Exception.ToString()}
 if($probe -and (Test-Path -LiteralPath (Join-Path $package 'managed-runtime-before.json'))){
  try{
   Invoke-Logged $configuration.DotnetExecutable @($probe,'--runtime-fingerprint',(Join-Path $package 'managed-runtime-after.json')) 'managed-runtime-after.log'
   if((Sha (Join-Path $package 'managed-runtime-before.json')) -ne (Sha (Join-Path $package 'managed-runtime-after.json'))){throw 'Selected runtime changed.'}
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
 Write-Host 'Tell Orion the declared body-mechanics outcome. Mechanical selection, native sensory delivery, physiological feedback and Avatar standing are separate.'
}
