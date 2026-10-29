Set-StrictMode -Version Latest

function Assert-GasHealthReferences([string]$Root, $Owner, $Receipt, $Initial, $Before, $After) {
    Assert-GasKeys $Owner @('expectedItemUid','expectedItemId','expectedHitpoints')
    if ($Receipt.state.status -cne 'SavedHealthConsumptionObserved' -or $Initial.state.status -cne 'WaitingForSavedHealthConsumption') { throw 'healthRetainedEvidenceMismatch' }
    $intent=Read-GaHealthIntent $Root
    if (-not (Test-GasHash $intent.bytes $Initial.state.intentSha256) -or
        -not (Test-GasHash $Before.bytes $Initial.state.beforeSha256) -or
        -not (Test-GasHash $After.bytes $Receipt.state.afterSha256) -or
        $intent.value.beforeSha256 -cne $Initial.state.beforeSha256 -or
        $intent.value.accountId -ne $Owner.accountId -or $intent.value.characterUid -ne $Before.report.character.uid -or
        [guid]$intent.value.domain -ne [guid]$Before.report.domain) { throw 'healthRetainedEvidenceMismatch' }
    foreach ($state in @($Initial.state,$Receipt.state)) {
        Assert-GasKeys $state @('expectedItemUid','expectedItemId','expectedHitpoints','intent','intentSha256','beforeSha256','liveStoreSynchronized','wireStatus','sourceGateStatus')
        if ($state.intent -cne 'health-intent.json' -or $state.intentSha256 -cne $Initial.state.intentSha256 -or
            $state.beforeSha256 -cne $Initial.state.beforeSha256 -or $state.liveStoreSynchronized -isnot [bool] -or
            $state.liveStoreSynchronized -ne $false -or $state.wireStatus -cne 'WirePending' -or $state.sourceGateStatus -cne 'SourceGatePending') { throw 'healthRetainedEvidenceMismatch' }
        foreach ($key in @('expectedItemUid','expectedItemId','expectedHitpoints')) {
            if ($state[$key] -cne $Owner[$key] -or $state[$key] -cne $intent.value[$key]) { throw 'healthRetainedEvidenceMismatch' }
        }
    }
    # Pure comparison of editable retained JSON; no inspector or host probe.
    if (@(Test-GaHealthTransition $Before.report $After.report $intent.value).Count) { throw 'healthRetainedEvidenceMismatch' }
}
