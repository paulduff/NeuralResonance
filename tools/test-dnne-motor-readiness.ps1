$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'wait-dnne-motor-ready.ps1')
function Assert-Readiness([bool]$Condition,[string]$Message){if(-not $Condition){throw $Message}}

$state=New-DnneMotorReadinessState 81 10 1.0
foreach($tick in 1..80){$ready=Update-DnneMotorReadiness $state $tick ($tick*.01) .005;Assert-Readiness (-not $ready) 'Warmup must not qualify readiness.'}
foreach($tick in 81..90){$ready=Update-DnneMotorReadiness $state $tick ($tick*.01) .005;Assert-Readiness (-not $ready) 'Ten post-warmup advance intervals are required.'}
$ready=Update-DnneMotorReadiness $state 91 .91 .005
Assert-Readiness $ready 'Fresh advancing post-warmup ticks should qualify.'

$state=New-DnneMotorReadinessState 81 10 1.0
foreach($index in 1..20){$ready=Update-DnneMotorReadiness $state 81 ($index*.01) .005;Assert-Readiness (-not $ready) 'Repeated cached ticks must not count as advances.'}

foreach($case in @('stale-gap','slow-request','tick-rollback')){
    $state=New-DnneMotorReadinessState 81 10 1.0
    foreach($tick in 81..86){[void](Update-DnneMotorReadiness $state $tick (($tick-80)*.05) .005)}
    if($case -eq 'stale-gap'){$ready=Update-DnneMotorReadiness $state 87 1.5 .005}
    if($case -eq 'slow-request'){$ready=Update-DnneMotorReadiness $state 87 1.5 1.1}
    if($case -eq 'tick-rollback'){$ready=Update-DnneMotorReadiness $state 2 .35 .005}
    Assert-Readiness (-not $ready -and $state.Consecutive -eq 0) ($case+' must reset the qualifying sequence.')
}

$state=New-DnneMotorReadinessState 81 10 1.0
foreach($tick in 2..16){$ready=Update-DnneMotorReadiness $state $tick (($tick-1)*4.0) .9;Assert-Readiness (-not $ready) 'Slow cold-start ticks must not qualify.'}
Write-Host 'Passed 6 motor-readiness regression scenarios: warmup/fresh, cached, stale gap, slow request, rollback and slow cold start.'
