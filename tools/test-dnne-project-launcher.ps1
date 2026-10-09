param([string]$DotnetExecutable = '')

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_start-dnne-project.ps1')
if ([string]::IsNullOrWhiteSpace($DotnetExecutable)) {
    $DotnetExecutable = (Get-Command dotnet -CommandType Application -ErrorAction Stop).Source
}
$previousToolPath = $env:PATH
$process = $null
try {
    $env:PATH = (Split-Path -Parent $DotnetExecutable) + ';' + $previousToolPath
    $root = Get-DnneRepoRoot -ScriptPath $PSCommandPath
    $directory = Join-Path $root ('artifacts\launcher-regression-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $directory | Out-Null
    $project = Join-Path $directory 'LauncherProbe.csproj'
    [IO.File]::WriteAllText($project, '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>')
    [IO.File]::WriteAllText((Join-Path $directory 'Program.cs'), 'System.Console.WriteLine("Launcher probe completed.");')

    # Exercise a real build with stdout, then a real hidden child process.
    # The original bug returned build strings alongside the process object.
    $returned = @(Start-DnneProject -ProjectPath $project -FriendlyName 'DNNE launcher regression probe' `
        -Configuration Release -WindowStyle Hidden)
    if ($returned.Count -ne 1 -or $returned[0] -isnot [Diagnostics.Process]) {
        throw "Launcher returned $($returned.Count) success-stream objects instead of exactly one Process."
    }
    $process = $returned[0]
    $null = $process.HasExited
    if (-not $process.WaitForExit(30000)) { throw 'Launcher probe did not exit within 30 seconds.' }
    if ($process.ExitCode -ne 0) { throw "Launcher probe exited with $($process.ExitCode)." }
    [ordered]@{ Passed = $true; ReturnedObjects = $returned.Count; ProcessPropertyAccessible = $true; ExitCode = $process.ExitCode } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $directory 'RESULT.json') -Encoding UTF8
    Write-Host 'PASS: build output stays on the console; the caller receives exactly one usable Process.'
}
finally {
    $env:PATH = $previousToolPath
    if ($process -is [Diagnostics.Process] -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -ErrorAction SilentlyContinue
    }
}
