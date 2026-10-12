param([Parameter(Mandatory=$true)][string]$Player,[ValidateRange(10,120)][int]$DurationSec=30,[switch]$Muscles,[string]$BackupRoot='D:\DNNE-desktop-runs\UnityWorld',[switch]$InWindow,[string]$LaunchTaskName)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
if(-not $InWindow){
 if(-not [IO.Path]::IsPathRooted($Player) -or -not(Test-Path -LiteralPath $Player -PathType Leaf)){throw 'Provide the existing built Unity player.'}
 $taskName='DNNE-Unity-Standing-'+[Guid]::NewGuid().ToString('N').Substring(0,12)
 $shell=Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
 $arguments=@('-NoLogo','-NoProfile','-NoExit','-ExecutionPolicy','Bypass','-File',$PSCommandPath,'-InWindow','-LaunchTaskName',$taskName,'-Player',$Player,'-DurationSec',[string]$DurationSec,'-BackupRoot',$BackupRoot)
 if($Muscles){$arguments+='-Muscles'}
 $quoted=($arguments|ForEach-Object{'"'+$_.Replace('"','\"')+'"'}) -join ' '
 $action=New-ScheduledTaskAction -Execute $shell -Argument $quoted -WorkingDirectory $repo
 $principal=New-ScheduledTaskPrincipal -UserId ([Security.Principal.WindowsIdentity]::GetCurrent().Name) -LogonType Interactive -RunLevel Limited
 $settings=New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::FromHours(1)) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
 Register-ScheduledTask -TaskName $taskName -Action $action -Principal $principal -Settings $settings | Out-Null
 Start-ScheduledTask -TaskName $taskName
 Write-Host ('Independent PowerShell standing observation started: '+$taskName)
 return
}
$run='unity-standing-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,6)
$output=Join-Path $repo ('artifacts\'+$run);New-Item -ItemType Directory -Path $output | Out-Null
$exitCode=1;$transcribing=$false;$observationProcess=$null
$headers=@{}
if(-not [string]::IsNullOrWhiteSpace($env:NRE_CONTROL_SHARED_SECRET)){$headers['X-NRE-Control-Auth']=$env:NRE_CONTROL_SHARED_SECRET}
function Save-BrainRead([string]$Name,[string]$Path){
 Invoke-WebRequest -UseBasicParsing -Uri ('http://127.0.0.1:5080'+$Path) -Headers $headers -TimeoutSec 60 -OutFile (Join-Path $output $Name) | Out-Null
}
function Read-Progress([string]$Path){
 # Permit the player to atomically replace the progress file while it is read.
 $stream=[IO.File]::Open($Path,[IO.FileMode]::Open,[IO.FileAccess]::Read,([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
 $reader=$null
 try {$reader=[IO.StreamReader]::new($stream);$reader.ReadToEnd() | ConvertFrom-Json}
 finally {if($reader){$reader.Dispose()}else{$stream.Dispose()}}
}
try {
 Start-Transcript -Path (Join-Path $output 'powershell.log') | Out-Null;$transcribing=$true
 $Host.UI.RawUI.WindowTitle='DNNE / Unity standing observation'
 Write-Host ('Results: '+$output) -ForegroundColor Cyan
 $configuration=[ordered]@{Player=$Player;PlayerSha256=(Get-FileHash -LiteralPath $Player -Algorithm SHA256).Hash;DurationSeconds=$DurationSec;MusclesEnabled=[bool]$Muscles;Plant=$(if($Muscles){'canonical Avatar measured muscles'}else{'simplified joint torque baseline'});PhysicalMuscles=$(if($Muscles){38}else{0});StrengthScale=1;PoolFatigueEnabled=$false;ScriptedBalanceController=$false;StartsPaused=$true;RecoverySupportedSecondsRequired=10;StopOnFall=$false}
 $assembly=Join-Path (Split-Path -Parent $Player) 'DNNE Unity World_Data\Managed\Assembly-CSharp.dll'
 if(Test-Path -LiteralPath $assembly){$configuration['PlayerAssemblySha256']=(Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash}
 $configuration | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'trial-configuration.json') -Encoding UTF8
 Write-Host '1. Recheck the isolated physical plant.'
 $mechanics=Join-Path $output 'mechanics';New-Item -ItemType Directory -Path $mechanics | Out-Null
 $arguments=@('-batchmode','-nographics','--qualify','--output',$mechanics,'-logFile',(Join-Path $mechanics 'physics.log'))
 $quoted=($arguments|ForEach-Object{'"'+$_.Replace('"','\"')+'"'}) -join ' '
 $qualificationProcess=Start-Process -FilePath $Player -ArgumentList $quoted -WindowStyle Hidden -PassThru
 if(-not $qualificationProcess.WaitForExit(180000)){$qualificationProcess.Kill();throw 'Mechanical prerequisite exceeded three minutes.'}
 $qualification=Get-Content -LiteralPath (Join-Path $mechanics 'qualification.json') -Raw | ConvertFrom-Json
 if($qualificationProcess.ExitCode -ne 0 -or -not $qualification.qualified){throw 'Mechanical prerequisite did not qualify; no live brain trial started.'}
 Write-Host 'Mechanical prerequisite passed: all 19 axes and 40 native samples.' -ForegroundColor Green
 $health=Invoke-RestMethod -Uri 'http://127.0.0.1:5080/api/v1/startup-health?maxNonOkDetails=8' -Headers $headers -TimeoutSec 10
 $health | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath (Join-Path $output 'brain-health-before.json') -Encoding UTF8
 if($health.serviceCount -le 0 -or $health.nonOkCount -ne 0 -or $health.telemetryStale){throw 'DNNE service health is not ready for this trial.'}
 Save-BrainRead 'motor-before.json' '/api/v1/neuronal-motor'
 Save-BrainRead 'sensorimotor-before.json' '/api/v1/sensorimotor-timing'
 # This export contains coordinator/connectome/telemetry state. It is not a
 # full per-service neuron/synapse checkpoint. The endpoint briefly quiesces
 # and resumes the coordinator itself; no restart/import/reset is requested.
 Save-BrainRead 'coordinator-before.json' '/api/v1/admin/network/export'
 $preBackup=Join-Path $BackupRoot $run;New-Item -ItemType Directory -Path $preBackup -Force | Out-Null
 foreach($name in @('motor-before.json','sensorimotor-before.json','coordinator-before.json','brain-health-before.json','trial-configuration.json')){
  $source=Join-Path $output $name;$target=Join-Path $preBackup $name;Copy-Item -LiteralPath $source -Destination $target
  if((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $target).Hash){throw ('Pre-trial backup mismatch: '+$name)}
 }
 Write-Host '2. Unity will open paused and acquire the exclusive world connection.' -ForegroundColor Cyan
 Write-Host 'After the ten-second handoff drain, press Play in UNITY to start the trial.' -ForegroundColor Yellow
 Write-Host ('The trial measures '+$DurationSec+' simulation seconds. It supplies no scripted balance/gait command.')
 Write-Host 'A fall does not end the trial. Recovery requires ten continuous seconds of supported standing with fresh neural leg actuation.'
 if($Muscles){Write-Host 'Body plant: 38 original Avatar muscles, canonical recruitment/fatigue, nominal capacity. DNNE is action authority.' -ForegroundColor Cyan}
 Write-Host 'The final Unity view stays open, paused and disconnected. Quality is assessed separately.'
 $arguments=@('--standing-trial',[string]$DurationSec,'--output',$output,'-logFile',(Join-Path $output 'standing.log'))
 if($Muscles){$arguments+='--muscles'}
 $quoted=($arguments|ForEach-Object{'"'+$_.Replace('"','\"')+'"'}) -join ' '
 $observationProcess=Start-Process -FilePath $Player -ArgumentList $quoted -WindowStyle Normal -PassThru
 $timer=[Diagnostics.Stopwatch]::StartNew();$lastPrintedSeconds=-1
 $reportPath=Join-Path $output 'standing-report.json';$progressPath=Join-Path $output 'standing-progress.json'
 while(-not(Test-Path -LiteralPath $reportPath)){
  if($observationProcess.HasExited){throw 'Unity closed before writing its standing report.'}
  if($timer.Elapsed.TotalSeconds -gt 1500){throw 'Standing observation did not finish within 25 minutes.'}
  if(Test-Path -LiteralPath $progressPath){
   try {$progress=Read-Progress $progressPath
    if($progress.simulationSeconds -ge $lastPrintedSeconds+5){
     Write-Host ('sim={0:N1}/{1}s standing={2:N1}s longest={3:N1}s leg-actuation-steps={4} body={5} generated-body-spikes={6} retina={7} falls={8} recoveries={9}' -f $progress.simulationSeconds,$DurationSec,$progress.standingSeconds,$progress.longestStandingSeconds,$progress.stepsWithLegActuation,$progress.bodyFramesAccepted,$progress.bodySpikesGenerated,$progress.retinalFramesAccepted,$progress.fallEvents,$progress.recoveries)
     $lastPrintedSeconds=$progress.simulationSeconds
    }
   } catch {Write-Verbose ('Progress read will retry: '+$_.Exception.Message)}
  }
  Start-Sleep -Seconds 1
 }
 $report=Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
 if($Muscles){
  # Verify the player actually captured the requested physical muscle plant.
  # This check does not establish neuronal delivery or successful balance.
  $samples=0;$invalidMuscleFrames=0
  foreach($line in [IO.File]::ReadLines((Join-Path $output 'standing-trace.jsonl'))){
   $trace=$line | ConvertFrom-Json;$frames=@($trace.body.articulation.musculoskeletal.muscles)
   $identities=@($frames | ForEach-Object {$_.side+':'+$_.name} | Select-Object -Unique)
   $samples++;if($frames.Count -ne 38 -or $identities.Count -ne 38){$invalidMuscleFrames++}
  }
  [ordered]@{Samples=$samples;InvalidMuscleFrames=$invalidMuscleFrames;ExpectedMuscles=38;LiveDeliveryAssessed=$false} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'muscle-capture-check.json') -Encoding UTF8
  if($samples -eq 0 -or $invalidMuscleFrames -ne 0){throw 'Live observation did not capture all 38 physical muscles.'}
 }
 $report | Format-List;$report.metrics | Format-List
 Save-BrainRead 'motor-after.json' '/api/v1/neuronal-motor'
 Save-BrainRead 'sensorimotor-after.json' '/api/v1/sensorimotor-timing'
 Save-BrainRead 'health-after.json' '/api/v1/startup-health?maxNonOkDetails=8'
 Save-BrainRead 'coordinator-after.json' '/api/v1/admin/network/export'
 if(-not $report.integrationChecksPassed){throw 'Observation ended without completing the motor/sensory integration checks.'}
 $exitCode=0
 if($report.standingCriterionMet){Write-Host 'The measured standing criterion was met. Learned standing was not assessed.' -ForegroundColor Green}
 else {Write-Host 'The run completed; the standing criterion was NOT met. Inspect the saved trace.' -ForegroundColor Yellow}
 if($report.metrics.fallEvents -eq 0){Write-Host 'No fall observed; recovery was not tested.' -ForegroundColor Yellow}
 elseif($report.recoveryCriterionMet){Write-Host 'Recovery criterion met: supported standing regained after a fall and held at completion, with neural actuation observed. Learned recovery was not assessed.' -ForegroundColor Green}
 else {Write-Host 'Recovery criterion NOT met. Inspect post-fall commands and body feedback.' -ForegroundColor Yellow}
} catch {
 $_ | Out-String | Set-Content -LiteralPath (Join-Path $output 'failure.txt')
 Write-Host $_.Exception.Message -ForegroundColor Red
 # Stop only the player launched by this window if the script cannot finish.
 # Successful runs keep its paused final view available to the operator.
 if($observationProcess -and -not $observationProcess.HasExited -and -not(Test-Path -LiteralPath (Join-Path $output 'standing-report.json'))){$observationProcess.Kill();$observationProcess.WaitForExit()}
} finally {
 $status=[ordered]@{Status=$(if($exitCode -eq 0){'FINISHED'}else{'FAILED'});ExitCode=$exitCode;CompletedUtc=[DateTime]::UtcNow.ToString('o');Output=$output}
 $status | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'window-status.json')
 if($transcribing){Stop-Transcript | Out-Null}
 try {
  $backup=Join-Path $BackupRoot $run
  & (Join-Path $PSScriptRoot 'backup-unity-run.ps1') -Source $output -Destination $backup
 } catch {Write-Host ('Backup failed: '+$_.Exception.Message) -ForegroundColor Red;$status.Status='FAILED';$status.ExitCode=1;$status['BackupError']=$_.Exception.Message;$status|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $output 'window-status.json')}
 if($LaunchTaskName -match '^DNNE-Unity-Standing-[0-9a-f]{12}$'){Unregister-ScheduledTask -TaskName $LaunchTaskName -Confirm:$false -ErrorAction SilentlyContinue}
 Write-Host "========== $($status.Status) =========="
 Write-Host 'Tell Orion to inspect the saved results. Process completion and standing quality are separate.'
}
