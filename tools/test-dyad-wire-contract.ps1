param(
    [Parameter(Mandatory = $true)]
    [string]$PeerContractPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path (Split-Path -Parent $PSCommandPath) '..')).Path
$localPath = Join-Path $root 'contracts\dyad-wire-contract.v1.json'
$peerPath = [System.IO.Path]::GetFullPath($PeerContractPath)
if (-not (Test-Path -LiteralPath $peerPath -PathType Leaf)) {
    throw "Peer Dyad contract not found: $peerPath"
}

$localHash = (Get-FileHash -LiteralPath $localPath -Algorithm SHA256).Hash.ToLowerInvariant()
$peerHash = (Get-FileHash -LiteralPath $peerPath -Algorithm SHA256).Hash.ToLowerInvariant()
if (-not [string]::Equals($localHash, $peerHash, [StringComparison]::Ordinal)) {
    throw "Dyad wire contract mismatch: local=$localHash peer=$peerHash"
}

Write-Host "Dyad wire contract compatible: sha256:$localHash"
