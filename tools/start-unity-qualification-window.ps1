param([Parameter(Mandatory=$true)][string]$Player,[string]$BackupRoot='D:\DNNE-desktop-runs\UnityWorld',[ValidateSet('mechanics','muscles','balance','response')][string]$Assessment='mechanics',[switch]$InWindow,[string]$LaunchTaskName)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
if(-not $InWindow){
 if(-not [IO.Path]::IsPathRooted($Player) -or -not(Test-Path -LiteralPath $Player -PathType Leaf)){throw 'Provide the existing built Unity player.'}
 $taskName='DNNE-Unity-Probe-'+[Guid]::NewGuid().ToString('N').Substring(0,12)
 $shell=Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
 $arguments=@('-NoLogo','-NoProfile','-NoExit','-ExecutionPolicy','Bypass','-File',$PSCommandPath,'-InWindow','-LaunchTaskName',$taskName,'-Player',$Player,'-BackupRoot',$BackupRoot,'-Assessment',$Assessment)
 $quoted=($arguments|ForEach-Object{'"'+$_.Replace('"','\"')+'"'}) -join ' '
 $action=New-ScheduledTaskAction -Execute $shell -Argument $quoted -WorkingDirectory $repo
 $principal=New-ScheduledTaskPrincipal -UserId ([Security.Principal.WindowsIdentity]::GetCurrent().Name) -LogonType Interactive -RunLevel Limited
 $settings=New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::FromHours(1)) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
 Register-ScheduledTask -TaskName $taskName -Action $action -Principal $principal -Settings $settings | Out-Null
 Start-ScheduledTask -TaskName $taskName
 Write-Host ('Independent PowerShell qualification started: '+$taskName)
 return
}
$run='unity-qualification-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,6)
if($Assessment -eq 'muscles'){$run=$run.Replace('unity-qualification-','unity-muscle-comparison-')}
if($Assessment -eq 'balance'){$run=$run.Replace('unity-qualification-','unity-balance-capability-')}
if($Assessment -eq 'response'){$run=$run.Replace('unity-qualification-','unity-balance-response-')}
$output=Join-Path $repo ('artifacts\'+$run);New-Item -ItemType Directory -Path $output | Out-Null
$exitCode=1
$transcribing=$false
try {
 Start-Transcript -Path (Join-Path $output 'powershell.log') | Out-Null
 $transcribing=$true
 $Host.UI.RawUI.WindowTitle='Unity physical body qualification (isolated)'
 Write-Host 'Unity mechanics only: no DNNE connection, no neuronal learning.' -ForegroundColor Cyan
 Write-Host ('Results: '+$output)
 $arguments=@('-batchmode','-nographics','--qualify','--output',$output,'-logFile',(Join-Path $output 'physics.log'))
 if($Assessment -eq 'muscles'){$arguments=@('-batchmode','-nographics','--muscle-qualify','--output',$output,'-logFile',(Join-Path $output 'physics.log'));Write-Host 'Comparing raw torque, canonical muscles and full active capacity; free-root gravity, zero/full standing recruitment.' -ForegroundColor Cyan}
 if($Assessment -eq 'balance'){$arguments=@('-batchmode','-nographics','--balance-qualify','--output',$output,'-logFile',(Join-Path $output 'physics.log'));Write-Host '6 isolated cases, 300 seconds each: inherited scalar at nominal/triple strength; motor-unit pools with inherited rates at nominal and published rates at nominal/triple; one nominal pool control with F=R=0 to isolate recruitment timing.' -ForegroundColor Cyan;Write-Host 'Fatigue stays enabled in the five model/strength cases; the sixth is an explicit ablation. Masses, moment arms, passive drives, torque limits and controller stay fixed. Pool states, actual fiber forces and clipping are recorded. No DNNE connection or learning.'}
 if($Assessment -eq 'response'){$arguments=@('-batchmode','-nographics','--response-qualify','--output',$output,'-logFile',(Join-Path $output 'physics.log'));Write-Host '10 isolated response cases: 4 seconds each, small 0.03-radian hip or ankle target pulses from 1 to 1.5 seconds.' -ForegroundColor Cyan;Write-Host 'Repeated controls, positive/negative bilateral and individual hip pulses. Free root; no external push, DNNE connection or learning.'}
 $quoted=($arguments|ForEach-Object{'"'+$_.Replace('"','\"')+'"'}) -join ' '
 $process=Start-Process -FilePath $Player -ArgumentList $quoted -WindowStyle Hidden -PassThru
 $maximumWaitMilliseconds=if($Assessment -eq 'balance'){1200000}else{600000}
 if(-not $process.WaitForExit($maximumWaitMilliseconds)){$process.Kill();throw 'Isolated Unity assessment exceeded its bounded time allowance; its own player was stopped.'}
 $reportName=if($Assessment -eq 'muscles'){'muscle-report.json'}elseif($Assessment -eq 'balance'){'balance-report.json'}elseif($Assessment -eq 'response'){'response-report.json'}else{'qualification.json'}
 $report=Get-Content -LiteralPath (Join-Path $output $reportName) -Raw | ConvertFrom-Json
 $report | Format-List
 if($report.actuators){$report.actuators | Format-Table joint,axis,positiveDisplacement,negativeDisplacement,maximumLimitViolation,passed -AutoSize}
 if($Assessment -eq 'muscles'){
  $report.cases | Select-Object musclesEnabled,preserveActiveCapacity,fixedStandDrive,reportedMuscles,standingHeld,maximumLimitViolation,worstLimitJoint,worstLimitAxis,peakActivation,peakForceNewtons | Format-Table -AutoSize
  if($process.ExitCode -ne 0 -or -not $report.completed -or -not $report.finite -or -not $report.isolated){throw 'Muscle comparison failed to complete finitely in isolation.'}
  Write-Host 'Comparison complete. Fixed-recruitment standing and learned standing are different outcomes.' -ForegroundColor Yellow
 }elseif($Assessment -eq 'response'){
  $report.cases | Select-Object name,leftHipOffset,rightHipOffset,leftAnkleOffset,rightAnkleOffset,finite,@{Name='LongestStandingSec';Expression={[Math]::Round($_.metrics.longestStandingSeconds,2)}},maximumLimitViolation | Format-Table -AutoSize
  if($process.ExitCode -ne 0 -or -not $report.completed -or -not $report.finite -or -not $report.isolated){throw 'Response comparison failed to complete finitely in isolation.'}
  Write-Host 'Response traces saved at 50 Hz. These short pulses assess command effects; standing, neuronal control and learning are not qualified.' -ForegroundColor Yellow
 }elseif($Assessment -eq 'balance'){
  $report.cases | Select-Object name,@{Name='LongestStandingSec';Expression={[Math]::Round($_.metrics.longestStandingSeconds,2)}},@{Name='FinalStandingSec';Expression={[Math]::Round($_.metrics.currentStandingSeconds,2)}},standingHeld,@{Name='MeanActivationSquared';Expression={[Math]::Round($_.meanSquaredActivationWhileStanding,4)}},maximumFatigueWhileStanding,torqueClippedAxes | Format-Table -AutoSize
  if($process.ExitCode -ne 0 -or -not $report.completed -or -not $report.finite -or -not $report.isolated){throw 'Balance capability comparison failed to complete finitely in isolation.'}
  Write-Host ('Any diagnostic standing hold: '+$report.anyDiagnosticStandingHold) -ForegroundColor Yellow
  Write-Host 'Diagnostic feedback is an explicit test fixture. DNNE control and learned standing were not assessed.' -ForegroundColor Yellow
 }elseif($process.ExitCode -ne 0 -or -not $report.completed -or -not $report.qualified){throw 'The physical checks did not qualify. Inspect saved results.'}
 if($Assessment -in @('balance','response')){
  Write-Host 'Checking captured body frames with the actual validator and offline receptors; no live neuronal delivery is claimed.' -ForegroundColor Cyan
  $dotnetTool=Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
  if(-not(Test-Path -LiteralPath $dotnetTool)){throw 'The offline body validator requires the installed .NET 10 SDK.'}
  & $dotnetTool run --project (Join-Path $repo 'tools\UnityBodyValidation\ValidateBody.csproj') -c Release --artifacts-path (Join-Path $repo 'artifacts\unity-body-validation-build') -- $output
  if($LASTEXITCODE -ne 0){throw 'Captured body validation failed. Inspect offline-body-validation.json and saved output.'}
 }
 $exitCode=0
 Write-Host 'ALL ASSESSMENT STAGES COMPLETE. Walking was not assessed.' -ForegroundColor Green
} catch {
 $_ | Out-String | Set-Content -LiteralPath (Join-Path $output 'failure.txt')
 Write-Host $_.Exception.Message -ForegroundColor Red
} finally {
 $status=[ordered]@{Status=$(if($exitCode -eq 0){'FINISHED'}else{'FAILED'});ExitCode=$exitCode;CompletedUtc=[DateTime]::UtcNow.ToString('o');Output=$output}
 $status | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'window-status.json')
 if($transcribing){Stop-Transcript | Out-Null}
 try {
  $backup=Join-Path $BackupRoot $run;New-Item -ItemType Directory -Path $backup -Force | Out-Null
  foreach($file in Get-ChildItem -LiteralPath $output -File){$target=Join-Path $backup $file.Name;Copy-Item -LiteralPath $file.FullName -Destination $target;if((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $target).Hash){throw ('Backup mismatch '+$file.Name)}}
  Write-Host ('Verified D: backup: '+$backup)
 } catch {Write-Host ('Backup failed: '+$_.Exception.Message) -ForegroundColor Red;$status.Status='FAILED';$status.ExitCode=1;$status['BackupError']=$_.Exception.Message;$status|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $output 'window-status.json')}
 if($LaunchTaskName -match '^DNNE-Unity-Probe-[0-9a-f]{12}$'){Unregister-ScheduledTask -TaskName $LaunchTaskName -Confirm:$false -ErrorAction SilentlyContinue}
 Write-Host "========== $($status.Status) =========="
 Write-Host 'Tell Orion to inspect the saved results. Process completion and model quality are separate.'
}
