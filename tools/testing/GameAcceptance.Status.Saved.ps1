Set-StrictMode -Version Latest

# Validate retained schema references, not a rerun of the acceptance scenario or
# a new live-state query. The runner's saved assertions remain recorded evidence.
function Assert-GasSavedReferences([string]$Root, $Owner, $Receipt, $Before, $After) {
    $initial = (Read-GasJson $Root 'receipt.json').value
    Assert-GasKeys $initial @('schemaVersion','scenario','accountId','observedUtc','before','after','state','visualAcceptance')
    Assert-GasKeys $initial.state @('status')
    if (-not (Test-GasUInt $initial.schemaVersion 1) -or -not (Test-GasUInt $initial.accountId) -or
        $initial.visualAcceptance -cne 'LiveClientVisualPending' -or $null -ne $initial.after -or $initial.scenario -cne $Owner.scenario -or
        $initial.accountId -ne $Owner.accountId -or $initial.before -cne 'before.json') { throw 'initialReceiptIdentityMismatch' }
    if ($Owner.scenario -ceq 'equip-unequip') {
        if ($Receipt.state.status -cnotin @('SavedEquipObserved','SavedEquipUnequipObserved') -or
            $initial.state.status -cne 'WaitingForEquip' -or
            -not (Test-GasHash $Before.bytes $initial.state.beforeSha256) -or
            -not (Test-GasHash $After.bytes $Receipt.state.afterSha256)) { throw 'equipRetainedEvidenceMismatch' }
        foreach ($state in @($initial.state, $Receipt.state)) {
            Assert-GasKeys $state @('expectedItemUid','expectedItemId','expectedPosition')
            if (-not (Test-GasUInt $state.expectedItemUid) -or -not (Test-GasUInt $state.expectedItemId) -or
                -not (Test-GasUInt $state.expectedPosition 65535) -or $state.expectedItemUid -ne $Owner.expectedItemUid -or $state.expectedItemId -ne $Owner.expectedItemId -or
                $state.expectedPosition -ne $Owner.expectedEquipmentPosition) { throw 'equipPinnedIdentityMismatch' }
        }
    } elseif ($Owner.scenario -ceq 'login-walk-save') {
        Assert-GasWalkReferences $Root $Owner $Receipt $initial $Before $After
    } elseif ($Owner.scenario -ceq 'mailbox-gear') {
        if ($Receipt.state.status -cne 'ServerStateVerified' -or $initial.state.status -cne 'WaitingForClientAction' -or
            $initial.state.deliveryResult -cne 'delivery-result.json') { throw 'mailRetainedEvidenceMismatch' }
        $request = Read-GasJson $Root 'request.json'
        $result = (Read-GasJson $Root 'delivery-result.json').value
        if (-not (Test-GasHash $request.bytes $initial.state.requestSha256)) { throw 'mailRequestChanged' }
        Assert-GaMailCorrelation $request.value $result $Before.report
        if ($Receipt.state.operationId -ne $request.value.operationId -or
            $Receipt.state.verifiedItemCount -ne @($result.itemUids).Count) { throw 'mailRecordedOutcomeMismatch' }
    } else { throw 'savedStatusUnsupportedForScenario' }
}
