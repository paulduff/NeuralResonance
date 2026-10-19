param(
    [string]$OutputPath = '',
    [string]$ControlBaseUrl = 'http://127.0.0.1:5080',
    [ValidateRange(5,1800)][int]$TimeoutSec = 600,
    [ValidateRange(0,20001)][long]$MinimumBrainTick = 81,
    [ValidateRange(2,100)][int]$RequiredAdvances = 10,
    [ValidateRange(0.05,1.0)][double]$MaximumGapSeconds = 1.0
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function New-DnneMotorReadinessState([long]$MinimumTick,[int]$Required,[double]$MaximumGap){
    return @{MinimumTick=$MinimumTick;Required=$Required;MaximumGap=$MaximumGap;LastTick=[long]-1;LastAdvanceSeconds=0.0;Consecutive=0}
}

function Update-DnneMotorReadiness([hashtable]$State,[long]$Tick,[double]$AtSeconds,[double]$RequestSeconds){
    $gap=$AtSeconds-$State.LastAdvanceSeconds
    if($Tick -lt $State.LastTick){$State.Consecutive=0;$State.LastTick=[long]-1}
    if($RequestSeconds -gt $State.MaximumGap){$State.Consecutive=0}
    if($Tick -gt $State.LastTick){
        if($Tick -ge $State.MinimumTick -and $State.LastTick -ge $State.MinimumTick -and
           $gap -le $State.MaximumGap -and $RequestSeconds -le $State.MaximumGap){
            $State.Consecutive++
        }else{$State.Consecutive=0}
        $State.LastAdvanceSeconds=$AtSeconds;$State.LastTick=$Tick
    }elseif($gap -gt $State.MaximumGap){$State.Consecutive=0}
    return $State.Consecutive -ge $State.Required
}

# Dot-sourcing exposes only the pure tracker for offline regression checks.
if($MyInvocation.InvocationName -eq '.'){return}
if([string]::IsNullOrWhiteSpace($OutputPath)){throw 'OutputPath is required.'}
$baseUri=$null
if(-not [Uri]::TryCreate($ControlBaseUrl,[UriKind]::Absolute,[ref]$baseUri) -or -not $baseUri.IsLoopback){throw 'Use a loopback ControlBaseUrl.'}
$destination=[IO.Path]::GetFullPath($OutputPath)
New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
$headers=@{}
if(-not [string]::IsNullOrWhiteSpace($env:NRE_CONTROL_SHARED_SECRET)){$headers['X-NRE-Control-Auth']=$env:NRE_CONTROL_SHARED_SECRET}
$state=New-DnneMotorReadinessState $MinimumBrainTick $RequiredAdvances $MaximumGapSeconds
$samples=[Collections.Generic.List[object]]::new()
$clock=[Diagnostics.Stopwatch]::StartNew();$nextMessage=15.0;$passed=$false
Write-Host ('Waiting for DNNE warmup (tick >= {0}) and {1} advancing motor updates; body freshness limit unchanged.' -f $MinimumBrainTick,$RequiredAdvances) -ForegroundColor Cyan
try {
    while($clock.Elapsed.TotalSeconds -lt $TimeoutSec -and -not $passed){
        $requestClock=[Diagnostics.Stopwatch]::StartNew()
        try {
            $reply=Invoke-RestMethod -Uri ($baseUri.AbsoluteUri.TrimEnd('/')+'/api/v1/neuronal-motor') -Headers $headers -TimeoutSec 2
            if($null -eq $reply.runtime -or $null -eq $reply.runtime.tick){throw 'Motor response has no runtime tick.'}
            $tick=[long]$reply.runtime.tick;$now=$clock.Elapsed.TotalSeconds
            $gap=$now-$state.LastAdvanceSeconds
            $passed=Update-DnneMotorReadiness $state $tick $now $requestClock.Elapsed.TotalSeconds
            $samples.Add([ordered]@{AtSeconds=$now;Tick=$tick;RequestSeconds=$requestClock.Elapsed.TotalSeconds;SecondsSincePriorAdvance=$gap;ConsecutiveAdvances=$state.Consecutive;WarmupComplete=($tick -ge $MinimumBrainTick)})
        }catch{
            $state.Consecutive=0
            $samples.Add([ordered]@{AtSeconds=$clock.Elapsed.TotalSeconds;RequestSeconds=$requestClock.Elapsed.TotalSeconds;Error=$_.Exception.Message;ConsecutiveAdvances=0})
        }
        if($clock.Elapsed.TotalSeconds -ge $nextMessage){
            Write-Host ('Motor readiness: tick={0}, warmup target={1}, fresh advances={2}/{3}, elapsed={4:N0}s' -f $state.LastTick,$MinimumBrainTick,$state.Consecutive,$RequiredAdvances,$clock.Elapsed.TotalSeconds)
            $nextMessage=$clock.Elapsed.TotalSeconds+15
        }
        if(-not $passed){Start-Sleep -Milliseconds 100}
    }
}finally{
    [ordered]@{Passed=$passed;CapturedUtc=[DateTime]::UtcNow.ToString('o');MinimumBrainTick=$MinimumBrainTick;LastBrainTick=$state.LastTick;WarmupComplete=($state.LastTick -ge $MinimumBrainTick);RequiredConsecutiveAdvances=$RequiredAdvances;ConsecutiveAdvances=$state.Consecutive;MaximumAllowedRequestAndAdvanceGapSeconds=$MaximumGapSeconds;ExistingUnityMotorFreshnessSeconds=1.25;TimeoutSeconds=$TimeoutSec;ElapsedSeconds=$clock.Elapsed.TotalSeconds;Samples=$samples} |
        ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $destination -Encoding UTF8
}
if(-not $passed){throw ('Motor readiness timed out after {0}s: tick={1}, warmup target={2}, fresh advances={3}/{4}. Unity was not launched.' -f $TimeoutSec,$state.LastTick,$MinimumBrainTick,$state.Consecutive,$RequiredAdvances)}
Write-Host ('Motor readiness passed after warmup: {0} advancing updates with request and advance gaps <= {1:N2}s.' -f $RequiredAdvances,$MaximumGapSeconds) -ForegroundColor Green
