param(
 [Parameter(Mandatory=$true)][string]$RunDirectory,
 [Parameter(Mandatory=$true)][string]$SynapseBackupDirectory,
 [Parameter(Mandatory=$true)][string]$ProbeAssembly,
 [string]$DotnetExecutable='',
 [string]$Revision='',
 [string]$BackupRoot='D:\DNNE-desktop-runs\UnityWorld',
 [switch]$Transfer,
 [switch]$InWindow,
 [string]$LaunchTaskName=''
)
. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
$repo=Get-DnneRepoRoot -ScriptPath $PSCommandPath
if(-not $InWindow){
 if(-not $DotnetExecutable){$DotnetExecutable=(Get-Command dotnet -CommandType Application -ErrorAction Stop).Source}
 if(-not $Revision){$Revision=(& git -C $repo rev-parse HEAD)}
 foreach($path in @($RunDirectory,$SynapseBackupDirectory,$ProbeAssembly,$DotnetExecutable)){
  if(-not [IO.Path]::IsPathRooted($path) -or -not(Test-Path -LiteralPath $path)){throw 'Provide existing absolute input and runtime paths.'}
 }
 $taskName='DNNE-Postural-Replay-'+[Guid]::NewGuid().ToString('N').Substring(0,12)
 $shell=Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
 $arguments=@('-NoLogo','-NoProfile','-NoExit','-ExecutionPolicy','Bypass','-File',$PSCommandPath,'-InWindow','-LaunchTaskName',$taskName,'-RunDirectory',$RunDirectory,'-SynapseBackupDirectory',$SynapseBackupDirectory,'-ProbeAssembly',$ProbeAssembly,'-DotnetExecutable',$DotnetExecutable,'-Revision',$Revision,'-BackupRoot',$BackupRoot)
 if($Transfer){$arguments+=@('-Transfer')}
 $quoted=($arguments | ForEach-Object {ConvertTo-ProcessArgument $_}) -join ' '
 $action=New-ScheduledTaskAction -Execute $shell -Argument $quoted -WorkingDirectory $repo
 $principal=New-ScheduledTaskPrincipal -UserId ([Security.Principal.WindowsIdentity]::GetCurrent().Name) -LogonType Interactive -RunLevel Limited
 $settings=New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::FromHours(12)) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
 Register-ScheduledTask -TaskName $taskName -Action $action -Principal $principal -Settings $settings | Out-Null
 Start-ScheduledTask -TaskName $taskName
 Write-Host ('Independent visible PowerShell postural study requested: '+$taskName)
 return
}
$prefix=if($Transfer){'vestibular-transfer'}else{'postural-replay'}
$name=$prefix+'-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,6)
$output=Join-Path $repo ('artifacts\'+$name)
New-Item -ItemType Directory -Path $output | Out-Null
$exitCode=1;$transcribing=$false
try {
 Start-Transcript -Path (Join-Path $output 'powershell.log') | Out-Null;$transcribing=$true
 $Host.UI.RawUI.WindowTitle='DNNE postural circuit - isolated replay'
 Write-Host ('Results: '+$output) -ForegroundColor Cyan
 Write-Host 'Isolated seven-region bilateral circuit. Saved body measurements and copied synapses; no live DNNE or world connection.'
 if($Transfer){
  $Host.UI.RawUI.WindowTitle='DNNE vestibular transfer - isolated signal tracing'
  Write-Host 'Forty cases: intact saved/fresh connections and three selective baseline interventions, with stationary, tilt, support-loss, head-motion, captured-trajectory and input controls.'
  Write-Host 'All interventions affect isolated copies only. No live weights or balance gate change.'
 }else{Write-Host 'Twenty diagnostic cases: saved/fresh state, 24/unrestricted source spikes, stable/fallen/transition/muted/one-sided inputs.'}
 $validationPath=Join-Path (Split-Path -Parent $ProbeAssembly) 'probe-validation.json'
 $validation=Get-Content -LiteralPath $validationPath -Raw | ConvertFrom-Json
 $expectedSmoke=if($Transfer){20}else{6}
 if(-not $validation.SmokePassed -or $validation.SmokeCases -ne $expectedSmoke){throw 'The matching completed preflight matrix is required.'}
 if($Transfer -and ($validation.Study -ne 'vestibular-transfer' -or -not $validation.ObserverTransparencyPassed)){throw 'Transfer-mode preflight and observer transparency checks are required.'}
 foreach($entry in $validation.Assemblies){
  $file=Join-Path (Split-Path -Parent $ProbeAssembly) $entry.Name
  if((Get-FileHash -LiteralPath $file).Hash -ne $entry.Sha256){throw ('Validated binary changed: '+$entry.Name)}
 }
 foreach($entry in $validation.Sources){
  if((Get-FileHash -LiteralPath (Join-Path $repo $entry.Path)).Hash -ne $entry.Sha256){throw ('Validated source changed: '+$entry.Path)}
 }
 Copy-Item -LiteralPath $validationPath -Destination (Join-Path $output 'probe-validation.json')
 $sourceFiles=@('tools/PosturalCircuitProbe/Program.cs','tools/PosturalCircuitProbe/PosturalCircuitProbe.csproj','tools/start-postural-replay-window.ps1')
 if($Transfer){$sourceFiles+=@('tools/PosturalCircuitProbe/TransferStudy.cs','Structures/_SharedRuntime/InboundSynapticObservation.cs','Structures/_SharedRuntime/StructureEngine.cs')}
 foreach($file in $sourceFiles){
  $target=Join-Path $output ('source\'+$file);New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
  Copy-Item -LiteralPath (Join-Path $repo $file) -Destination $target
 }
 [ordered]@{Revision=$Revision;SourceRun=$RunDirectory;SynapseBackup=$SynapseBackupDirectory;ProbeAssembly=$ProbeAssembly;AssemblySha256=(Get-FileHash -LiteralPath $ProbeAssembly).Hash;LiveBrainAccessed=$false;PhysicalRecoveryAssessed=$false} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'configuration.json') -Encoding UTF8
 $studyArguments=@($ProbeAssembly,(Join-Path $output 'study'),$RunDirectory,$SynapseBackupDirectory,$repo)
 if($Transfer){$studyArguments+=@('--transfer')}
 & $DotnetExecutable @studyArguments
 if($LASTEXITCODE -ne 0){throw 'Postural circuit study failed; inspect STUDY_FAILED.json.'}
 $report=Get-Content -LiteralPath (Join-Path $output 'study\results.json') -Raw | ConvertFrom-Json
 $expectedCases=if($Transfer){40}else{20}
 if(-not $report.completed -or $report.caseCount -ne $expectedCases -or -not $report.allCasesFinite -or -not $report.protectedInputsUnchanged){throw 'The study did not complete its finite case matrix with protected inputs unchanged.'}
 $report.results | Format-Table case,finite,sourceBudgetDrops,queueRejected,taggedRelays -AutoSize
 Write-Host 'All cases completed. Neural discrimination is assessed from saved responses; physical recovery was not tested.' -ForegroundColor Yellow
 $exitCode=0
}catch{
 $_ | Out-String | Set-Content -LiteralPath (Join-Path $output 'failure.txt') -Encoding UTF8
 Write-Host $_.Exception.Message -ForegroundColor Red
}finally{
 $status=[ordered]@{Status=$(if($exitCode -eq 0){'FINISHED'}else{'FAILED'});ExitCode=$exitCode;CompletedUtc=[DateTime]::UtcNow.ToString('o');Output=$output;Revision=$Revision;LiveBrainAccessed=$false;PhysicalRecoveryAssessed=$false}
 $status | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'window-status.json') -Encoding UTF8
 if($transcribing){Stop-Transcript | Out-Null}
 try{& (Join-Path $PSScriptRoot 'backup-unity-run.ps1') -Source $output -Destination (Join-Path $BackupRoot $name)}catch{
  $status.Status='FAILED';$status.ExitCode=1;$status['BackupError']=$_.Exception.Message
  $status | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'window-status.json') -Encoding UTF8
  Write-Host ('Backup failed: '+$_.Exception.Message) -ForegroundColor Red
 }
 if($LaunchTaskName -match '^DNNE-Postural-Replay-[0-9a-f]{12}$'){Unregister-ScheduledTask -TaskName $LaunchTaskName -Confirm:$false -ErrorAction SilentlyContinue}
 Write-Host ('========== '+$status.Status+' ==========')
 Write-Host 'Tell Orion when this window finishes. Process completion, neural discrimination and physical recovery are separate.'
}
