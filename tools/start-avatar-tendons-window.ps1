param([Parameter(Mandatory=$true)][string]$PackageDirectory,[switch]$InWindow,[string]$LaunchTaskName='')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
$package=(Resolve-Path -LiteralPath $PackageDirectory).Path
$configuration=Get-Content -LiteralPath (Join-Path $package 'configuration.json') -Raw | ConvertFrom-Json
$forceObservation=($configuration.PSObject.Properties.Name -contains 'NativeForceObservationStudy') -and $configuration.NativeForceObservationStudy
$mechanicsDiagnostic=($configuration.PSObject.Properties.Name -contains 'NativeMechanicsDiagnosticStudy') -and $configuration.NativeMechanicsDiagnosticStudy
if($forceObservation -and $mechanicsDiagnostic){throw 'Choose one native study.'}
$expectedCases=if($mechanicsDiagnostic){54}elseif($forceObservation){78}else{51};$expectedSteps=if($mechanicsDiagnostic){432}elseif($forceObservation){624}else{413}
$studyScript=if($mechanicsDiagnostic){'native_tendon_mechanics_study.py'}elseif($forceObservation){'native_tendon_force_study.py'}else{'native_avatar_tendon_study.py'}
if(-not $configuration.NoLiveMonitoring -or -not $configuration.IsolatedFullAvatarTendons -or $configuration.ExpectedNativeCases -ne $expectedCases -or $configuration.ExpectedNativeSteps -ne $expectedSteps -or $configuration.ExpectedTestCases -ne 112){throw 'Unexpected full Avatar mechanical protocol.'}
function Sha([string]$Path){(Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash}
function Assert-Package {
 foreach($entry in $configuration.PackageFiles){if((Sha (Join-Path $package $entry.Path)) -ne $entry.Sha256){throw ('Frozen input changed: '+$entry.Path)}}
 foreach($name in @('Unity','Dotnet','Python')){if((Sha $configuration.($name+'Executable')) -ne $configuration.($name+'ExecutableSha256')){throw ($name+' executable changed.')}}
}
if(-not $InWindow){
 if(Test-Path -LiteralPath (Join-Path $package 'launch.json')){throw 'Already launched; use a fresh package.'}
 $manifest=Get-Content -LiteralPath (Join-Path $package 'backup-manifest.json') -Raw | ConvertFrom-Json
 if($manifest.Destination -ne $configuration.BackupDestination -or (Sha (Join-Path $package 'backup-manifest.json')) -ne (Sha (Join-Path $configuration.BackupDestination 'backup-manifest.json'))){throw 'Initial backup identities differ.'}
 foreach($entry in $manifest.Files){foreach($directory in @($package,$configuration.BackupDestination)){
  if((Sha (Join-Path $directory $entry.Path)) -ne $entry.Sha256){throw ('Initial source/backup differs: '+$entry.Path)}
 }}
 Assert-Package
 $taskPrefix=if($mechanicsDiagnostic){'DNNE-Tendon-Mechanics-'}elseif($forceObservation){'DNNE-Tendon-Force-'}else{'DNNE-Avatar-Tendons-'}
 $taskName=$taskPrefix+[Guid]::NewGuid().ToString('N').Substring(0,12)
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
function Stage([string]$stage){Invoke-Logged $configuration.PythonExecutable @('-I','-B',(Join-Path $package ('source/tools/'+$studyScript)),$stage,$package) ($stage+'.log')}
$status=[ordered]@{Status='RUNNING';ExitCode=$null;Revision=$configuration.Revision;NoLiveMonitoring=$true;NativePhysicsAttempted=$false;MechanicalChecksPassed=$false;NeuronalSourceExecuted=$false;AnatomicalCalibrationQualified=$false;AvatarBalanceQualified=$false}
$statusPath=Join-Path $package 'window-status.json';$transcribing=$false;$runtimeFiles=@();$binaries=@();$playerFiles=@()
if(Test-Path -LiteralPath $statusPath){throw 'Refusing to overwrite an earlier run.'}
try {
 Start-Transcript -Path (Join-Path $package 'powershell.log') | Out-Null;$transcribing=$true
 $Host.UI.RawUI.WindowTitle=if($mechanicsDiagnostic){'DNNE full Avatar motion diagnosis - isolated mechanical variants'}elseif($forceObservation){'DNNE native tendon force observations - paired mechanics'}else{'DNNE full Avatar tendon mechanics - no neuronal standing claim'}
 Write-Host ('Results: '+$package) -ForegroundColor Cyan
 Write-Host ('112 component checks, then '+$expectedCases+' isolated Unity cases. Mechanical fixtures, no live brain. This window stays open after its final backup.')
 $status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
 Assert-Package;Stage 'preflight'
 $runtimeFiles=Capture-Files (Join-Path (Split-Path $configuration.DotnetExecutable -Parent) 'shared') 'managed-runtime-files.json'
 Invoke-Logged $configuration.DotnetExecutable @('--info') 'dotnet-info.log'
 $build=Join-Path $package 'source/artifacts/avatar-tendon-tests'
 Invoke-Logged $configuration.DotnetExecutable @('build',(Join-Path $package 'source/tests/NeuralResonanceEngine.DNNE.Tests/NeuralResonanceEngine.DNNE.Tests.csproj'),'-c','Release','--artifacts-path',$build,'--nologo','-v','minimal') 'test-build.log'
 $assembly=Join-Path $build 'bin/NeuralResonanceEngine.DNNE.Tests/release/NeuralResonanceEngine.DNNE.Tests.dll'
 $binaries=Capture-Files (Split-Path $assembly -Parent) 'test-binaries.json'
 $testOutput=Join-Path $package 'test-results';[IO.Directory]::CreateDirectory($testOutput) | Out-Null
 Invoke-Logged $configuration.DotnetExecutable @('vstest',$assembly,('/TestCaseFilter:'+$configuration.TestFilter),('/ResultsDirectory:'+$testOutput),'/Logger:trx;LogFileName=focused.trx') 'focused-tests.log'
 Stage 'check-tests'
 $status['PassedFocusedTests']=112
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
 $status['UnityBuildSucceeded']=$true;$status.NativePhysicsAttempted=$true
 $index=0
 foreach($case in $configuration.Cases){
  $index++;Write-Host ('NATIVE AVATAR '+$index+'/'+$expectedCases+' '+$case.name) -ForegroundColor Cyan
  $output=Join-Path $package ('native/'+$case.name)
  if(Test-Path -LiteralPath $output){throw 'Native output already exists.'}
  [IO.Directory]::CreateDirectory($output) | Out-Null
  $arguments=@('-batchmode','-nographics','-logFile',(Join-Path $output 'player.log'),'--neuronal-muscle-qualify','1','--tendon-case',$case.condition,'--output',$output)
  if($case.muscle){$arguments+=@('--tendon-muscle',$case.muscle)}
  if(($forceObservation -or $mechanicsDiagnostic) -and $case.observe){$arguments+=@('--tendon-observe-native','1')}
  if($mechanicsDiagnostic){$arguments+=@('--tendon-mechanics',$case.mode)}
  $playerProcess=Start-Process -FilePath (Join-Path $playerOutput 'DNNE Unity World.exe') -ArgumentList (($arguments | ForEach-Object {ConvertTo-ProcessArgument $_}) -join ' ') -WorkingDirectory $playerOutput -WindowStyle Hidden -PassThru
  if(-not $playerProcess.WaitForExit(120000)){$playerProcess.Kill();$playerProcess.WaitForExit();throw ('Native case timed out: '+$case.name)}
  if($playerProcess.ExitCode -ne 0){throw ('Native case failed: '+$case.name+'; inspect its result.json and player.log.')}
 }
 Stage 'analyse';Stage 'preflight'
 Verify-Files $runtimeFiles;Verify-Files $binaries;Verify-Files $playerFiles
 $analysis=Get-Content -LiteralPath (Join-Path $package 'analysis.json') -Raw | ConvertFrom-Json
 if(-not $analysis.mechanicalCriteriaPassed -or $analysis.actualPhysicsSteps -ne $expectedSteps -or $analysis.cases.Count -ne $expectedCases){throw 'Native mechanical accounting incomplete.'}
 $status.MechanicalChecksPassed=$true;$status.Status='DIAGNOSTICS_COMPLETE';$status.ExitCode=0
}catch{
 $status.Status='FAILED';$status.ExitCode=1;$status['Error']=$_.Exception.ToString();Write-Host ('FAILED: '+$_.Exception.Message) -ForegroundColor Red
}finally{
 try{Assert-Package;Verify-Files $runtimeFiles;Verify-Files $binaries;Verify-Files $playerFiles}catch{$status.Status='FAILED';$status.ExitCode=1;$status['PreservationError']=$_.Exception.ToString()}
 $status['CompletedUtc']=[DateTime]::UtcNow.ToString('o')
 if($transcribing){Stop-Transcript | Out-Null}
 $status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8
 try{& (Join-Path $package 'backup-run.ps1') -Source $package -Destination $configuration.BackupDestination;if(-not $?){throw 'Backup failed.'}}
 catch{$status.Status='FAILED';$status.ExitCode=1;$status['BackupError']=$_.Exception.ToString();$status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8;Write-Host ('BACKUP FAILED: '+$_.Exception.Message) -ForegroundColor Red}
 if($LaunchTaskName){Unregister-ScheduledTask -TaskName $LaunchTaskName -Confirm:$false -ErrorAction SilentlyContinue}
 Write-Host ('========== '+$status.Status+' ==========') -ForegroundColor Cyan
 Write-Host ('Results: '+$package)
 if($mechanicsDiagnostic){Write-Host 'Tell Orion the mechanical motion diagnosis outcome. Changed-setting sensitivity, useful joint motion, anatomy, neuronal authority and standing are separate.'}
 elseif($forceObservation){Write-Host 'Tell Orion the native tendon force outcome. Native accumulation, useful joint response, anatomy, neuronal authority and standing are separate.'}
 else{Write-Host 'Tell Orion the full Avatar tendon outcome. Mechanical force delivery, anatomical calibration, neuronal authority and standing are separate.'}
}
