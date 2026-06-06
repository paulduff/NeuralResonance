param(
    [string]$BaseUrl = "http://localhost:5080",
    [int]$MaxSnapshotAgeTicks = 20,
    [int]$MaxNonOkServices = 2,
    [int]$TimeoutSec = 30,
    [int]$PollIntervalMs = 500,
    [switch]$RequireValid
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($TimeoutSec -lt 1) {
    $TimeoutSec = 1
}
if ($PollIntervalMs -lt 50) {
    $PollIntervalMs = 50
}
if ($MaxSnapshotAgeTicks -lt 1) {
    $MaxSnapshotAgeTicks = 1
}
if ($MaxNonOkServices -lt 0) {
    $MaxNonOkServices = 0
}

$validationUri = "{0}/api/v1/admin/validation?maxSnapshotAgeTicks={1}&maxNonOkServices={2}" -f $BaseUrl.TrimEnd('/'), $MaxSnapshotAgeTicks, $MaxNonOkServices
$deadline = (Get-Date).AddSeconds($TimeoutSec)
$last = $null
$lastIssue = $null

Write-Host ("Validating DNNE runtime via {0}" -f $validationUri)

function Get-FieldOrDefault {
    param(
        [object]$Source,
        [string]$Name,
        [object]$DefaultValue
    )

    if ($null -eq $Source) {
        return $DefaultValue
    }

    if (-not ($Source.PSObject.Properties.Name -contains $Name)) {
        return $DefaultValue
    }

    $value = $Source.$Name
    if ($null -eq $value) {
        return $DefaultValue
    }

    return $value
}

while ((Get-Date) -lt $deadline) {
    try {
        $snapshot = Invoke-RestMethod -Uri $validationUri -Method Get -TimeoutSec 10
        $last = $snapshot
        $lastIssue = $null

        $tick = [long](Get-FieldOrDefault -Source $snapshot -Name "Tick" -DefaultValue 0)
        $services = [int](Get-FieldOrDefault -Source $snapshot -Name "ServiceCount" -DefaultValue 0)
        $nonOk = [int](Get-FieldOrDefault -Source $snapshot -Name "NonOkCount" -DefaultValue 0)
        $snapshotAge = [long](Get-FieldOrDefault -Source $snapshot -Name "SnapshotAgeTicks" -DefaultValue -1)
        $isValid = [bool](Get-FieldOrDefault -Source $snapshot -Name "IsValid" -DefaultValue $false)
        $profile = [string](Get-FieldOrDefault -Source $snapshot -Name "Profile" -DefaultValue "unknown")

        Write-Host ("Tick={0} profile={1} services={2} nonOk={3} snapshotAge={4} valid={5}" -f $tick, $profile, $services, $nonOk, $snapshotAge, $isValid)

        if ($snapshot.Checks -ne $null) {
            $checks = @()
            foreach ($name in $snapshot.Checks.PSObject.Properties.Name) {
                $checks += ("{0}={1}" -f $name, [bool]$snapshot.Checks.$name)
            }
            if ($checks.Count -gt 0) {
                Write-Host ("Checks: {0}" -f ($checks -join ", "))
            }
        }

        if (-not $RequireValid -or $isValid) {
            Write-Host "Validation complete."
            return
        }
    }
    catch {
        $lastIssue = $_.Exception.Message
        Write-Host ("Validation probe failed: {0}" -f $lastIssue)
    }

    Start-Sleep -Milliseconds $PollIntervalMs
}

if ($RequireValid) {
    if ($last -ne $null) {
        $lastTick = [long](Get-FieldOrDefault -Source $last -Name "Tick" -DefaultValue 0)
        $lastServiceCount = [int](Get-FieldOrDefault -Source $last -Name "ServiceCount" -DefaultValue 0)
        $lastNonOk = [int](Get-FieldOrDefault -Source $last -Name "NonOkCount" -DefaultValue 0)
        $lastSnapshotAge = [long](Get-FieldOrDefault -Source $last -Name "SnapshotAgeTicks" -DefaultValue -1)
        $lastIsValid = [bool](Get-FieldOrDefault -Source $last -Name "IsValid" -DefaultValue $false)
        throw ("DNNE validation timed out after {0}s. Last: tick={1}, services={2}, nonOk={3}, snapshotAge={4}, valid={5}" -f `
            $TimeoutSec,
            $lastTick,
            $lastServiceCount,
            $lastNonOk,
            $lastSnapshotAge,
            $lastIsValid)
    }

    if (-not [string]::IsNullOrWhiteSpace($lastIssue)) {
        throw ("DNNE validation timed out after {0}s. Last error: {1}" -f $TimeoutSec, $lastIssue)
    }

    throw ("DNNE validation timed out after {0}s with no response." -f $TimeoutSec)
}

if ($last -ne $null) {
    $lastTick = [long](Get-FieldOrDefault -Source $last -Name "Tick" -DefaultValue 0)
    $lastIsValid = [bool](Get-FieldOrDefault -Source $last -Name "IsValid" -DefaultValue $false)
    Write-Host ("Validation window elapsed; last sample tick={0} valid={1}" -f $lastTick, $lastIsValid)
}
elseif (-not [string]::IsNullOrWhiteSpace($lastIssue)) {
    Write-Host ("Validation window elapsed; last error: {0}" -f $lastIssue)
}
else {
    Write-Host "Validation window elapsed with no response."
}
