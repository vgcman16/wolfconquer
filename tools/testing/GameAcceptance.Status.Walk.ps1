Set-StrictMode -Version Latest

# Reference/schema checks only. These local editable records are not an
# attestation, live movement assertion or command/inspection authority.
function Assert-GasWalkReferences([string]$Root, $Owner, $Receipt, $Initial, $Before, $After) {
    Assert-GasKeys $Owner @('expectedMap','expectedX','expectedY')
    if ($Receipt.state.status -cne 'SavedWalkObserved' -or $Initial.state.status -cne 'WaitingForSavedWalk') { throw 'walkRetainedEvidenceMismatch' }
    $intent=Read-GaWalkIntent $Root
    if (-not (Test-GasHash $intent.bytes $Initial.state.intentSha256) -or
        -not (Test-GasHash $Before.bytes $Initial.state.beforeSha256) -or
        -not (Test-GasHash $After.bytes $Receipt.state.afterSha256) -or
        $intent.value.beforeSha256 -cne $Initial.state.beforeSha256 -or
        $intent.value.accountId -ne $Owner.accountId -or $intent.value.characterUid -ne $Before.report.character.uid -or
        [guid]$intent.value.domain -ne [guid]$Before.report.domain -or
        $After.report.character.map -ne $intent.value.expectedMap -or $After.report.character.x -ne $intent.value.expectedX -or
        $After.report.character.y -ne $intent.value.expectedY -or
        ($Before.report.character.map -eq $After.report.character.map -and $Before.report.character.x -eq $After.report.character.x -and
         $Before.report.character.y -eq $After.report.character.y)) { throw 'walkRetainedEvidenceMismatch' }
    foreach ($state in @($Initial.state,$Receipt.state)) {
        Assert-GasKeys $state @('expectedMap','expectedX','expectedY','intent','intentSha256','beforeSha256','liveStoreSynchronized','wireStatus','sourceGateStatus')
        if ($state.intent -cne 'walk-intent.json' -or $state.intentSha256 -cne $Initial.state.intentSha256 -or
            $state.beforeSha256 -cne $Initial.state.beforeSha256 -or $state.liveStoreSynchronized -isnot [bool] -or
            $state.liveStoreSynchronized -ne $false -or $state.wireStatus -cne 'WirePending' -or $state.sourceGateStatus -cne 'SourceGatePending') { throw 'walkRetainedEvidenceMismatch' }
        foreach ($key in @('expectedMap','expectedX','expectedY')) {
            if ($state[$key] -cne $Owner[$key] -or $state[$key] -cne $intent.value[$key]) { throw 'walkRetainedEvidenceMismatch' }
        }
    }
    # Recheck the pure saved transition from retained JSON, including unchanged
    # currency, every reported row and sidecar images. This invokes no inspector.
    # Never upgrade old counts-only evidence or editably requested live proof.
    if (@(Test-GaWalkTransition $Before.report $After.report $intent.value).Count) { throw 'walkRetainedEvidenceMismatch' }
}
