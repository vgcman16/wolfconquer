Set-StrictMode -Version Latest

function Assert-GasResourceReferences([string]$Root, $Owner, $Receipt, $Initial, $Before, $After, [string]$Scenario) {
    $profile=Get-GarProfile $Scenario; $pins=@('expectedItemUid','expectedItemId') + $profile.expectedKeys
    Assert-GasKeys $Owner $pins
    if ($Receipt.state.status -cne $profile.observed -or $Initial.state.status -cne $profile.waiting) { throw 'resourceRetainedEvidenceMismatch' }
    $intent=Read-GaResourceIntent $Root $Scenario
    if (-not (Test-GasHash $intent.bytes $Initial.state.intentSha256) -or
        -not (Test-GasHash $Before.bytes $Initial.state.beforeSha256) -or -not (Test-GasHash $After.bytes $Receipt.state.afterSha256) -or
        $intent.value.beforeSha256 -cne $Initial.state.beforeSha256 -or $intent.value.accountId -ne $Owner.accountId -or
        $intent.value.characterUid -ne $Before.report.character.uid -or [guid]$intent.value.domain -ne [guid]$Before.report.domain) { throw 'resourceRetainedEvidenceMismatch' }
    foreach ($state in @($Initial.state,$Receipt.state)) {
        Assert-GasKeys $state ($pins + @('intent','intentSha256','beforeSha256','liveStoreSynchronized','wireStatus','sourceGateStatus'))
        if ($state.intent -cne $profile.intent -or $state.intentSha256 -cne $Initial.state.intentSha256 -or
            $state.beforeSha256 -cne $Initial.state.beforeSha256 -or $state.liveStoreSynchronized -isnot [bool] -or
            $state.liveStoreSynchronized -ne $false -or $state.wireStatus -cne 'WirePending' -or $state.sourceGateStatus -cne 'SourceGatePending') { throw 'resourceRetainedEvidenceMismatch' }
        foreach ($key in $pins) {
            if ($state[$key] -cne $Owner[$key] -or $state[$key] -cne $intent.value[$key]) { throw 'resourceRetainedEvidenceMismatch' }
        }
    }
    if ($Scenario -cne 'consume-health') {
        foreach ($record in @($Initial,$Receipt)) { Assert-GarInspectorRecord $record }
    }
    # Editable hashes/receipts are local references. Recheck the complete pure
    # saved transition; do not trust a recorded empty assertions array or PASS.
    if (@(Test-GaResourceTransition $Before.report $After.report $intent.value $Scenario).Count) { throw 'resourceRetainedEvidenceMismatch' }
}
