#requires -Version 7.4
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$EvidenceParent,
    [uint32]$AccountId = 0,
    [ValidateSet('Json','Table')][string]$Format = 'Json',
    [ValidateRange(1, 1024)][int]$MaximumEvidenceFolders = 128,
    [ValidateRange(1, 512)][int]$MaximumReceiptsPerFolder = 128
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'GameAcceptance.Common.ps1')
. (Join-Path $PSScriptRoot 'GameAcceptance.Mailbox.ps1')
. (Join-Path $PSScriptRoot 'GameAcceptance.Status.Files.ps1')
. (Join-Path $PSScriptRoot 'GameAcceptance.Walk.ps1')
. (Join-Path $PSScriptRoot 'GameAcceptance.Status.Walk.ps1')
. (Join-Path $PSScriptRoot 'GameAcceptance.Health.Assertions.ps1')
. (Join-Path $PSScriptRoot 'GameAcceptance.Health.ps1')
. (Join-Path $PSScriptRoot 'GameAcceptance.Status.Health.ps1')
. (Join-Path $PSScriptRoot 'GameAcceptance.Trade.ps1')
. (Join-Path $PSScriptRoot 'GameAcceptance.Status.Trade.ps1')
. (Join-Path $PSScriptRoot 'GameAcceptance.Status.Saved.ps1')
. (Join-Path $PSScriptRoot 'GameAcceptance.Status.Records.ps1')
. (Join-Path $PSScriptRoot 'GameAcceptance.Status.Summary.ps1')
try {
    $result = Get-GasSummary $EvidenceParent $AccountId $MaximumEvidenceFolders $MaximumReceiptsPerFolder
} catch {
    $result = @{ schemaVersion = 1; readOnly = $true; sourceGateAccepted = $false; liveGameplayProven = $false;
        status = 'InvalidEvidence'; diagnostics = @(@{ evidenceId = $null; reason = 'parentPathOrEvidenceBoundsInvalid' });
        rows = @(Get-GasScenarioIds | ForEach-Object { New-GasPendingRow $_ $(if ($AccountId) { $AccountId } else { $null }) });
        trustBound = 'Read failed; no evidence acceptance, command, probe or mutation was performed.' }
}
if ($Format -ceq 'Json') { $result | ConvertTo-Json -Depth 8 }
else {
    $result.rows | ForEach-Object { [pscustomobject]$_ } |
        Select-Object scenario,accountId,counterpartyAccountId,evidenceId,savedStateResult,evidenceStatus,visualStatus |
        Format-Table -AutoSize | Out-String -Width 240
    Write-Output ('Evidence summary: ' + $result.status + '. Saved observations are local editable records; live gameplay remains unproven.')
    foreach ($issue in $result.diagnostics) { Write-Output ('Invalid evidence: ' + $issue.reason) }
}
# Query exit0 means the bounded summary was read, never an acceptance PASS.
if ($result.status -ceq 'InvalidEvidence') { exit 1 }
exit 0
