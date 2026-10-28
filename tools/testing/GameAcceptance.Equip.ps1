Set-StrictMode -Version Latest

# Compare complete inspector projections. This is a saved-file observation, not a
# claim that the connected Store or client rendered/accepted either action.
function Test-GaEquipTransition($Before, $After, [uint32]$Uid, [uint32]$ItemId,
    [uint16]$Position, [bool]$Equipped) {
    $issues = [Collections.Generic.List[string]]::new()
    if ($Before.summariesTruncated -ne $false -or $After.summariesTruncated -ne $false -or
        [guid]$Before.domain -ne [guid]$After.domain -or $Before.accountId -ne $After.accountId -or
        $Before.character.uid -ne $After.character.uid) { $issues.Add('completeMatchingIdentityRequired'); return $issues.ToArray() }
    if ($Before.character.gold -ne $After.character.gold -or $Before.character.conquerPoints -ne $After.character.conquerPoints) {
        $issues.Add('currencyChanged')
    }
    if ($Before.physicalItemCount -ne @($Before.items).Count -or $After.physicalItemCount -ne @($After.items).Count -or
        $Before.physicalItemCount -ne $After.physicalItemCount) { $issues.Add('physicalItemCountChanged') }
    $beforeIds = @($Before.items | ForEach-Object { [uint32]$_.uid })
    $afterIds = @($After.items | ForEach-Object { [uint32]$_.uid })
    if (@($beforeIds | Where-Object { $_ -eq 0 }).Count -or @($afterIds | Where-Object { $_ -eq 0 }).Count -or
        @($beforeIds | Select-Object -Unique).Count -ne $beforeIds.Count -or
        @($afterIds | Select-Object -Unique).Count -ne $afterIds.Count) { $issues.Add('duplicateOrZeroItemUid') }
    foreach ($original in @($Before.items)) {
        $matches = @($After.items | Where-Object { [uint32]$_.uid -eq [uint32]$original.uid })
        if ($matches.Count -ne 1) { $issues.Add('itemMissingOrDuplicated'); continue }
        $fields = @('itemId','warehouseId','plus','stackSize','durability','maximumDurability','socketOne','socketTwo','bound','locked')
        if ([uint32]$original.uid -ne $Uid) { $fields += @('container','position') }
        foreach ($field in $fields) { if ($original[$field] -cne $matches[0][$field]) { $issues.Add('itemFieldChanged:' + $field) } }
    }
    $targetBefore = @($Before.items | Where-Object { [uint32]$_.uid -eq $Uid })
    $targetAfter = @($After.items | Where-Object { [uint32]$_.uid -eq $Uid })
    if ($targetBefore.Count -ne 1 -or $targetAfter.Count -ne 1 -or
        $targetBefore[0].itemId -ne $ItemId -or $targetAfter[0].itemId -ne $ItemId) { $issues.Add('expectedItemUnavailable') }
    elseif ($Equipped) {
        if ($targetBefore[0].container -cne 'inventory' -or $targetBefore[0].position -ne 0 -or $targetBefore[0].warehouseId -ne 0 -or
            $targetAfter[0].container -cne 'equipment' -or $targetAfter[0].position -ne $Position -or $targetAfter[0].warehouseId -ne 0) {
            $issues.Add('inventoryToExpectedEquipmentSlotNotObserved')
        }
    }
    elseif ($targetBefore[0].container -cne 'equipment' -or $targetBefore[0].position -ne $Position -or
        $targetAfter[0].container -cne 'inventory' -or $targetAfter[0].position -ne 0 -or $targetAfter[0].warehouseId -ne 0) {
        $issues.Add('expectedEquipmentSlotToInventoryNotObserved')
    }
    return $issues.ToArray()
}

function Start-GaEquip($Before, [uint32]$Uid, [uint32]$ItemId, [uint16]$Position) {
    if ($Uid -eq 0 -or $ItemId -eq 0 -or $Position -eq 0 -or $Before.summariesTruncated -ne $false -or
        @($Before.items | Where-Object { [uint32]$_.uid -eq $Uid -and [uint32]$_.itemId -eq $ItemId -and
            $_.container -ceq 'inventory' -and $_.position -eq 0 -and $_.warehouseId -eq 0 }).Count -ne 1 -or
        @($Before.items | Where-Object { [uint32]$_.uid -eq $Uid }).Count -ne 1) {
        return @{ status = 'PrerequisiteMissing'; reason = 'expectedInventoryItemAndCompleteSnapshotRequired'; liveClientVisualStatus = 'LiveClientVisualPending' }
    }
    return @{ status = 'WaitingForEquip'; expectedItemUid = $Uid; expectedItemId = $ItemId; expectedPosition = $Position;
        liveClientVisualStatus = 'LiveClientVisualPending'; verificationBound = 'Caller-supplied UID/type; delivery provenance is separate.' }
}

function Resume-GaEquip($Context, $Initial, $Before, [string]$AfterLeaf, [string]$EquippedReceipt, [uint32]$ExpectedUid, [uint32]$ExpectedItem, [uint16]$ExpectedPosition) {
    if ($Initial.state.status -cne 'WaitingForEquip') { throw 'Valid initial equip evidence required.' }
    if ((Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $Context.evidenceRoot 'before.json')).Hash -cne $Initial.state.beforeSha256) { throw 'Initial equip snapshot changed.' }
    $uid = [uint32]$Initial.state.expectedItemUid; $item = [uint32]$Initial.state.expectedItemId
    $position = [uint16]$Initial.state.expectedPosition
    if ($uid -eq 0 -or $item -eq 0 -or $position -eq 0 -or $uid -ne $ExpectedUid -or $item -ne $ExpectedItem -or $position -ne $ExpectedPosition) { throw 'Pinned item identity unavailable.' }
    $baseline = $Before
    $equipped = $true
    if ($EquippedReceipt) {
        if ($EquippedReceipt -cnotmatch '^resume-[0-9a-f]{32}-receipt\.json$') { throw 'Retained equip receipt leaf required.' }
        $prior = Read-GaJson $Context $EquippedReceipt
        if ($prior.scenario -cne 'equip-unequip' -or $prior.accountId -ne $Context.accountId -or
            $prior.before -cne 'before.json' -or $prior.state.status -cne 'SavedEquipObserved' -or
            $prior.state.expectedItemUid -ne $uid -or $prior.state.expectedItemId -ne $item -or
            $prior.state.expectedPosition -ne $position -or $prior.after -cnotmatch '^resume-[0-9a-f]{32}-after\.json$') {
            throw 'Matching retained equip receipt required.'
        }
        if ((Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $Context.evidenceRoot $prior.after)).Hash -cne $prior.state.afterSha256) { throw 'Retained equip snapshot changed.' }
        $baseline = (Read-GaJson $Context $prior.after).report
        if (@(Test-GaEquipTransition $Before $baseline $uid $item $position $true).Count -ne 0) { throw 'Retained equip observation no longer verifies.' }
        $equipped = $false
    }
    $after = Get-GaInspection $Context $AfterLeaf
    $issues = @(Test-GaEquipTransition $baseline $after $uid $item $position $equipped)
    return @{ status = $(if ($issues.Count) { 'ClientActionPendingOrStateMismatch' } elseif ($equipped) { 'SavedEquipObserved' } else { 'SavedEquipUnequipObserved' });
        expectedItemUid = $uid; expectedItemId = $item; expectedPosition = $position; assertions = $issues;
        afterSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $Context.evidenceRoot $AfterLeaf)).Hash;
        liveClientVisualStatus = 'LiveClientVisualPending'; liveStoreSynchronized = $after.liveStoreSynchronized;
        verificationBound = 'Saved file projections only; caller-supplied identity, runtime Store, wire, derived stats and client visuals remain unproved.' }
}
