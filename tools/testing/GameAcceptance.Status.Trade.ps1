Set-StrictMode -Version Latest

# Local retained evidence only. Never open the recorded plan/tool/config paths.
function Assert-GasTradeReferences([string]$Root, $Owner, $Receipt, $Before, $After) {
    if ($Receipt.state.status -cnotin @('WaitingForSavedTrade','ClientActionPendingOrStateMismatch','SavedTradeObserved')) { throw 'tradeRetainedStatusInvalid' }
    $initial=(Read-GasJson $Root 'receipt.json').value
    $retained=Read-GatRetained $Root $Owner $initial
    Assert-GatState $Receipt.state
    foreach ($key in @('intentSha256','beforeCounterparty')) {
        if ($Receipt.state[$key] -cne $initial.state[$key]) { throw 'tradeRetainedReferenceMismatch' }
    }
    for ($i=0; $i -lt 2; $i++) {
        if ($Receipt.state.beforeSha256[$i] -cne $initial.state.beforeSha256[$i]) { throw 'tradeRetainedReferenceMismatch' }
    }
    if ($Receipt.state.status -ceq 'WaitingForSavedTrade') {
        if ($null -ne $Receipt.after) { throw 'tradeInitialReceiptMismatch' }
        return
    }
    if ($null -eq $After -or $Receipt.state.afterCounterparty -isnot [string] -or
        $Receipt.state.afterCounterparty -cne $Receipt.after.Replace('-after.json','-counterparty-after.json') -or
        $Receipt.state.afterSha256 -isnot [array] -or $Receipt.state.afterSha256.Count -ne 2) { throw 'tradeAfterEvidenceMissing' }
    $other=Read-GatInspection $Root $Receipt.state.afterCounterparty $Owner.counterpartyAccountId $Owner.configSha256
    $primary=Read-GatInspection $Root $Receipt.after $Owner.accountId $Owner.configSha256
    for ($i=0; $i -lt 2; $i++) {
        $record=@($primary,$other)[$i]
        if (-not (Test-GasHash $record.bytes $Receipt.state.afterSha256[$i])) { throw 'tradeAfterEvidenceChanged' }
    }
    $issues=@(Test-GaTradeTransition $retained.a.report $retained.b.report $primary.report $other.report $retained.intent)
    if ($Receipt.state.status -ceq 'SavedTradeObserved' -and $issues.Count) { throw 'tradeSavedTransitionMismatch' }
}
