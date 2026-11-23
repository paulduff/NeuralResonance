param([Parameter(Mandatory=$true)][string]$PackageDirectory,[switch]$InWindow,[string]$LaunchTaskName='')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
$package=(Resolve-Path -LiteralPath $PackageDirectory).Path
$configuration=Get-Content -LiteralPath (Join-Path $package 'configuration.json') -Raw | ConvertFrom-Json
if(-not $configuration.NoLiveMonitoring -or -not $configuration.NativeRoutedStudy -or $configuration.Cases.Count -ne 7){throw 'Unexpected native routed protocol.'}
function Sha([string]$Path){(Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash}
function Assert-Package {
 foreach($entry in $configuration.PackageFiles){if((Sha (Join-Path $package $entry.Path)) -ne $entry.Sha256){throw ('Frozen input changed: '+$entry.Path)}}
 foreach($name in @('Unity','Python')){if((Sha $configuration.($name+'Executable')) -ne $configuration.($name+'ExecutableSha256')){throw ($name+' executable changed.')}}
}
if(-not $InWindow){
 if(Test-Path -LiteralPath (Join-Path $package 'launch.json')){throw 'Already launched; use a fresh package.'}
 $manifest=Get-Content -LiteralPath (Join-Path $package 'backup-manifest.json') -Raw | ConvertFrom-Json
 if($manifest.Destination -ne $configuration.BackupDestination -or (Sha (Join-Path $package 'backup-manifest.json')) -ne (Sha (Join-Path $configuration.BackupDestination 'backup-manifest.json'))){throw 'Initial backup identities differ.'}
 foreach($entry in $manifest.Files){foreach($directory in @($package,$configuration.BackupDestination)){
  if((Sha (Join-Path $directory $entry.Path)) -ne $entry.Sha256){throw ('Initial source/backup differs: '+$entry.Path)}
 }}
 Assert-Package
 $taskName='DNNE-Native-Routed-'+[Guid]::NewGuid().ToString('N').Substring(0,12)
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
function Capture-Binaries([string]$Assembly,[string]$Name){
 $files=@(Get-ChildItem -LiteralPath (Split-Path $Assembly -Parent) -File -Recurse | Sort-Object FullName)
 $entries=[Collections.Generic.List[object]]::new()
 foreach($file in $files){$entries.Add([ordered]@{Path=$file.FullName;Sha256=Sha $file.FullName})}
 ConvertTo-Json -InputObject $entries.ToArray() -Depth 5 | Set-Content -LiteralPath (Join-Path $package $Name) -Encoding UTF8
 return $entries.ToArray()
}
function Stage([string]$Name){Invoke-Logged $configuration.PythonExecutable @('-I','-B',(Join-Path $package 'source/tools/native_routed_study.py'),$Name,$package) ($Name+'.log')}
function Copy-Tree([string]$Source,[string]$Destination){
 if(Test-Path -LiteralPath $Destination){throw ('Copy destination exists: '+$Destination)}
 [IO.Directory]::CreateDirectory($Destination) | Out-Null
 foreach($path in [IO.Directory]::EnumerateFiles($Source,'*',[IO.SearchOption]::AllDirectories)){
  $target=Join-Path $Destination ($path.Substring($Source.Length).TrimStart('\','/'))
  [IO.Directory]::CreateDirectory((Split-Path $target -Parent)) | Out-Null
  [IO.File]::Copy($path,$target,$false)
  if((Sha $path) -ne (Sha $target)){throw ('Copy differs: '+$path)}
 }
}
$status=[ordered]@{Status='RUNNING';ExitCode=$null;Revision=$configuration.Revision;NoLiveMonitoring=$true;
 NativeUnityExecuted=$false;GeometryFixturesOnly=$true;LiveBrainAccessed=$false;OldSavesLoaded=$false;
 IndependentCoupledTrajectoryQualified=$false;AnatomicalCalibrationQualified=$false;NeuronalAuthorityQualified=$false;AvatarStandingQualified=$false}
$statusPath=Join-Path $package 'window-status.json';$transcribing=$false;$playerFiles=@()
if(Test-Path -LiteralPath $statusPath){throw 'Refusing to overwrite an earlier run.'}
try {
 Start-Transcript -Path (Join-Path $package 'powershell.log') | Out-Null;$transcribing=$true
 $Host.UI.RawUI.WindowTitle='DNNE native curved paths and patellar constraint loads - seven isolated cases'
 Write-Host ('Results: '+$package) -ForegroundColor Cyan
 Write-Host 'Seven full-Avatar cases, 56 physical steps, 490 point forces and 196 constraint moments. Prescribed forces and analytic geometry; no neuronal control or standing qualification. This window stays open after backup.'
 $status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
 Assert-Package;Stage 'preflight'
 Invoke-Logged $configuration.PythonExecutable @('-I','-B',(Join-Path $package 'source/tools/test_unity_avatar_inputs.py')) 'avatar-input-controls.log'
 if(-not (Select-String -LiteralPath (Join-Path $package 'avatar-input-controls.log') -Pattern '^Ran 8 tests in ' -Quiet)){throw 'Required eight Avatar build-input controls incomplete.'}
 $status['PassedAvatarBuildInputChecks']=8;$status['ReusedManagedGeometryCases']=169
 $workProject=Join-Path $configuration.UnityBuildWorkspace 'UnityWorld'
 Copy-Tree (Join-Path $package 'source/UnityWorld') $workProject
 $playerOutput=Join-Path $package 'player';[IO.Directory]::CreateDirectory($playerOutput) | Out-Null
 Write-Host 'Building a separate Unity player for curved muscle paths and paired constraint moments...' -ForegroundColor Cyan
 $priorOutput=[Environment]::GetEnvironmentVariable('NRE_UNITY_OUTPUT','Process')
 try{
  [Environment]::SetEnvironmentVariable('NRE_UNITY_OUTPUT',$playerOutput,'Process')
  $arguments=@('-batchmode','-nographics','-projectPath',$workProject,'-executeMethod','Nre.UnityWorld.Editor.WorldBuild.BuildWindows','-quit','-logFile',(Join-Path $playerOutput 'build.log'))
  $editorProcess=Start-Process -FilePath $configuration.UnityExecutable -ArgumentList (($arguments | ForEach-Object {ConvertTo-ProcessArgument $_}) -join ' ') -WorkingDirectory $workProject -WindowStyle Hidden -PassThru
  if(-not $editorProcess.WaitForExit(1800000)){$editorProcess.Kill();$editorProcess.WaitForExit();throw 'Dedicated Unity build exceeded 30 minutes.'}
  if($editorProcess.ExitCode -ne 0){throw 'Unity build failed; inspect saved build.log.'}
 }finally{[Environment]::SetEnvironmentVariable('NRE_UNITY_OUTPUT',$priorOutput,'Process')}
 $buildResult=Get-Content -LiteralPath (Join-Path $playerOutput 'build-result.json') -Raw | ConvertFrom-Json
 if($buildResult.result -ne 'Succeeded' -or $buildResult.errors -ne 0){throw 'Native Unity build did not succeed.'}
 $sourceUnity=Join-Path $package 'source/UnityWorld'
 foreach($file in Get-ChildItem -LiteralPath $sourceUnity -Filter '*.cs' -Recurse -File){
  $relative=$file.FullName.Substring($sourceUnity.Length).TrimStart('\','/')
  if((Sha $file.FullName) -ne (Sha (Join-Path $workProject $relative))){throw ('Compiled Unity source changed: '+$relative)}
 }
 foreach($folder in @('Assets','Packages','ProjectSettings')){Copy-Tree (Join-Path $workProject $folder) (Join-Path $package ('built-project/'+$folder))}
 $player=Join-Path $playerOutput 'DNNE Unity World.exe';$playerFiles=Capture-Binaries $player 'player-files.json';$status['UnityBuildSucceeded']=$true
 $status.NativeUnityExecuted=$true;$status | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
 $exits=[Collections.Generic.List[object]]::new()
 foreach($case in $configuration.Cases){
  $output=Join-Path $package ('native/'+$case)
  if(Test-Path -LiteralPath $output){throw ('Native case already exists: '+$case)}
  [IO.Directory]::CreateDirectory($output) | Out-Null
  Write-Host ('NATIVE ROUTED MUSCLE '+$case) -ForegroundColor Cyan
  $arguments=@('-batchmode','-nographics','--neuronal-muscle-qualify','1','--native-routed-case',$case,'--output',$output,'-logFile',(Join-Path $output 'player.log'))
  $process=Start-Process -FilePath $player -ArgumentList (($arguments | ForEach-Object {ConvertTo-ProcessArgument $_}) -join ' ') -WorkingDirectory $output -WindowStyle Hidden -PassThru
  $timedOut=-not $process.WaitForExit(180000)
  if($timedOut){$process.Kill();$process.WaitForExit()}
  $exits.Add([ordered]@{Case=$case;ExitCode=$process.ExitCode;TimedOut=$timedOut;CompletedUtc=[DateTime]::UtcNow.ToString('o')})
 }
 ConvertTo-Json -InputObject $exits.ToArray() -Depth 5 | Set-Content -LiteralPath (Join-Path $package 'native-exits.json') -Encoding UTF8
 if(@($exits | Where-Object {$_.TimedOut -or $_.ExitCode -ne 0}).Count){throw 'At least one isolated native case failed; inspect saved native case reports and player logs.'}
 Stage 'analyse';$status['NativeRoutedLoadCriteriaPassed']=$true
 $status.Status='DIAGNOSTICS_COMPLETE';$status.ExitCode=0
}catch{
 $status.Status='FAILED';$status.ExitCode=1;$status['Error']=$_.Exception.ToString();Write-Host ('FAILED: '+$_.Exception.Message) -ForegroundColor Red
}finally{
 try{
  Stage 'preflight';Assert-Package
  foreach($entry in $playerFiles){if((Sha $entry.Path) -ne $entry.Sha256){throw ('Executed binary changed: '+$entry.Path)}}
  $status['FrozenInputsPreserved']=$true
 }catch{$status.Status='FAILED';$status.ExitCode=1;$status['PreservationError']=$_.Exception.ToString()}
 $status['CompletedUtc']=[DateTime]::UtcNow.ToString('o')
 if($transcribing){Stop-Transcript | Out-Null}
 $status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8
 try{& (Join-Path $package 'backup-run.ps1') -Source $package -Destination $configuration.BackupDestination;if(-not $?){throw 'Backup failed.'}}
 catch{$status.Status='FAILED';$status.ExitCode=1;$status['BackupError']=$_.Exception.ToString();$status | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statusPath -Encoding UTF8;Write-Host ('BACKUP FAILED: '+$_.Exception.Message) -ForegroundColor Red}
 if($LaunchTaskName){Unregister-ScheduledTask -TaskName $LaunchTaskName -Confirm:$false -ErrorAction SilentlyContinue}
 Write-Host ('========== '+$status.Status+' ==========') -ForegroundColor Cyan
 Write-Host ('Results: '+$package)
 Write-Host 'Tell Orion the native routed muscle outcome. Curved-path and constraint load delivery, coupled numerical accuracy, anatomical calibration, neuronal authority and Avatar standing are separate.'
}
