param(
 [Parameter(Mandatory=$true)][string]$PythonExecutable,
 [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference='Stop'
$outputPath=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $outputPath){throw 'Use a new test output directory.'}
New-Item -ItemType Directory -Path $outputPath | Out-Null
$launcher=Join-Path (Split-Path -Parent $PSScriptRoot) 'start-postural-cell-window.ps1'
$tokens=$null;$parseErrors=$null
$tree=[Management.Automation.Language.Parser]::ParseFile($launcher,[ref]$tokens,[ref]$parseErrors)
if($parseErrors){throw ($parseErrors | Out-String)}
$function=$tree.Find({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Invoke-LoggedNative'},$true)
if($null -eq $function){throw 'Logged native helper missing.'}
. ([scriptblock]::Create($function.Extent.Text))
$nativeScript=Join-Path $outputPath 'native output with spaces.py'
@'
import sys
print("native stdout marker")
print("native stderr marker", file=sys.stderr)
sys.exit(int(sys.argv[1]))
'@ | Set-Content -LiteralPath $nativeScript -Encoding UTF8
foreach($expected in @(0,23)){
 $log=Join-Path $outputPath ('exit-'+$expected+'.log')
 $actual=Invoke-LoggedNative $PythonExecutable @($nativeScript,[string]$expected) $log
 if($actual -ne $expected){throw ('Lost native exit code: '+$actual+' expected '+$expected)}
 $text=Get-Content -LiteralPath $log -Raw -Encoding UTF8
 if($text -notmatch 'native stdout marker' -or $text -notmatch 'native stderr marker'){throw 'Native output missing from saved log.'}
 if($ErrorActionPreference -ne 'Stop'){throw 'Helper did not restore caller error handling.'}
}
$rejected=$false
try {Invoke-LoggedNative (Join-Path $outputPath 'missing.exe') @() (Join-Path $outputPath 'missing.log') | Out-Null}
catch {$rejected=$true}
if(-not $rejected){throw 'Missing executable was accepted.'}
[ordered]@{Passed=$true;PowerShellVersion=$PSVersionTable.PSVersion.ToString();ExitCodes=@(0,23);StdoutAndStderrPreserved=$true;MissingExecutableRejected=$true} |
 ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputPath 'result.json') -Encoding UTF8
Write-Host 'Native console capture and exit-code checks passed.'
