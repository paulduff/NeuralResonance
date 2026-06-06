param(
    [string]$Endpoint = "http://localhost:5080",
    [string[]]$Commands = @(
        "stop",
        "move forward",
        "turn left",
        "turn right",
        "find shelter",
        "find food",
        "avoid bear"
    ),
    [int]$SettlingMilliseconds = 450
)

$ErrorActionPreference = "Stop"

function Join-Endpoint {
    param(
        [string]$Base,
        [string]$Path
    )

    return ($Base.TrimEnd("/") + "/" + $Path.TrimStart("/"))
}

function Read-Property {
    param(
        [object]$Object,
        [string]$Name,
        [object]$Default = $null
    )

    if ($null -eq $Object) {
        return $Default
    }

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $Default
    }

    return $property.Value
}

function Convert-StructureId {
    param([object]$Value)

    if ($null -eq $Value) {
        return ""
    }

    if ($Value -is [int] -or $Value -is [long]) {
        switch ([int]$Value) {
            64 { return "M1" }
            65 { return "Sma" }
            69 { return "PremotorCortex" }
            77 { return "MotorThalamus" }
            default { return [string]$Value }
        }
    }

    return [string]$Value
}

function Send-EnglishCommand {
    param([string]$Text)

    $body = @{
        text = $Text
        mode = "english"
        hemisphere = "*"
        intensity = 1.0
        burstPerToken = 8
    } | ConvertTo-Json -Depth 8

    Invoke-RestMethod `
        -Method Post `
        -Uri (Join-Endpoint $Endpoint "/api/v1/admin/input/language") `
        -ContentType "application/json" `
        -Body $body
}

function Get-DnneState {
    Invoke-RestMethod -Method Get -Uri (Join-Endpoint $Endpoint "/api/v1/state")
}

function Get-DnneFrame {
    Invoke-RestMethod -Method Get -Uri (Join-Endpoint $Endpoint "/api/v1/frame?include_connectome=0&max_output_log=4&max_spike_log=4&max_dispatch_spikes=240")
}

try {
    $null = Invoke-RestMethod -Method Get -Uri (Join-Endpoint $Endpoint "/api/v1/startup-health?maxNonOkDetails=1")
}
catch {
    Write-Error "DNNE Control Program is not reachable at $Endpoint. Start the engine first, then rerun this script."
}

$results = New-Object System.Collections.Generic.List[object]

foreach ($command in $Commands) {
    $response = Send-EnglishCommand -Text $command
    Start-Sleep -Milliseconds ([Math]::Max(0, $SettlingMilliseconds))

    $state = Get-DnneState
    $frame = Get-DnneFrame

    $intent = Read-Property $state "languageIntent"
    $narration = Read-Property $state "brainNarration"
    $dispatches = @(Read-Property $frame "dispatchSpikes" @())
    $motorDispatches = @(
        $dispatches | Where-Object {
            $source = Convert-StructureId (Read-Property $_ "sourceStructure" "")
            $neuron = [string](Read-Property $_ "sourceNeuronId" "")
            ($source -in @("M1", "Sma", "PremotorCortex", "MotorThalamus")) -and
            $neuron.ToLowerInvariant().Contains("motor_")
        }
    )

    $results.Add([pscustomobject]@{
        Command = $command
        HttpDelivered = Read-Property $response "deliveredSpikes" 0
        MotorIntentDelivered = Read-Property $response "motorIntentDeliveredSpikes" 0
        Active = Read-Property $intent "active" $false
        CommandKey = Read-Property $intent "commandKey" "-"
        MotorDirective = Read-Property $intent "motorDirective" "-"
        Strength = [Math]::Round([double](Read-Property $intent "strength" 0), 3)
        Repetition = Read-Property $intent "repetitionCount" 0
        LearnedBias = [Math]::Round([double](Read-Property $intent "learnedBias" 0), 3)
        Narration = Read-Property $narration "utterance" "-"
        FrameMotorDispatches = $motorDispatches.Count
    })
}

$results | Format-Table -AutoSize

$failed = @(
    $results | Where-Object {
        -not $_.Active -or
        [string]::IsNullOrWhiteSpace($_.CommandKey) -or
        $_.CommandKey -eq "-" -or
        [string]::IsNullOrWhiteSpace($_.Narration) -or
        $_.Narration -eq "-"
    }
)

if ($failed.Count -gt 0) {
    Write-Error ("Language command path probe found {0} command(s) without active brain intent/narration." -f $failed.Count)
}

Write-Host "Language command path probe completed."
