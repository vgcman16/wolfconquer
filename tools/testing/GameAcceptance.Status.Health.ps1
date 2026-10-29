Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'GameAcceptance.Status.Resources.ps1')

function Assert-GasHealthReferences([string]$Root, $Owner, $Receipt, $Initial, $Before, $After) {
    Assert-GasResourceReferences $Root $Owner $Receipt $Initial $Before $After 'consume-health'
}
