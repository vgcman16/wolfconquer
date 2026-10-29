Set-StrictMode -Version Latest

function Test-GahType($Type) {
    # Current source classification only; no amount/formula or client evidence is inferred.
    return (Test-GawInteger $Type 1 ([uint32]::MaxValue)) -and
        (($Type -ge 1000000 -and $Type -le 1000040) -or ($Type -ge 1002000 -and $Type -lt 1002030) -or
         $Type -in @(1002050,725065,1003010))
}

function Test-GaHealthReport($Report) {
    $issues = [Collections.Generic.List[string]]::new()
    if ($Report -isnot [Collections.IDictionary] -or $Report.character -isnot [Collections.IDictionary] -or
        $Report.items -isnot [array] -or $Report.mails -isnot [array] -or $Report.files -isnot [array]) { return @('completeSavedProjectionRequired') }
    foreach ($issue in @(Test-GaWalkReport $Report)) { $issues.Add($issue) }
    if ($Report.summariesTruncated -isnot [bool] -or $Report.liveStoreSynchronized -isnot [bool] -or
        @($Report.items).Count + @($Report.mails).Count -gt 200 -or
        -not (Test-GawInteger $Report.schemaVersion 1 1) -or
        -not (Test-GawInteger $Report.accountId 1 ([uint32]::MaxValue)) -or
        -not (Test-GawInteger $Report.character.uid 1 ([uint32]::MaxValue))) { $issues.Add('completeSavedProjectionRequired') }
    foreach ($field in @('hitpoints','minMana','maxMana')) {
        if (-not $Report.character.Contains($field) -or -not (Test-GawInteger $Report.character[$field] 0 ([long]::MaxValue))) {
            $issues.Add('savedVitalRequired:' + $field)
        }
    }
    foreach ($row in @($Report.items)) {
        foreach ($field in @('itemId','warehouseId')) {
            if (-not (Test-GawInteger $row[$field] $(if ($field -ceq 'itemId') { 1 } else { 0 }) ([uint32]::MaxValue))) { $issues.Add('invalidItemField:' + $field) }
        }
        foreach ($field in @('position','stackSize','durability','maximumDurability')) {
            if (-not (Test-GawInteger $row[$field] 0 65535)) { $issues.Add('invalidItemField:' + $field) }
        }
        foreach ($field in @('plus','socketOne','socketTwo','bound','locked')) {
            if (-not (Test-GawInteger $row[$field] 0 255)) { $issues.Add('invalidItemField:' + $field) }
        }
    }
    foreach ($row in @($Report.mails)) {
        foreach ($field in @('attachedItemUid','conquerPoints','packageFlag')) {
            if (-not (Test-GawInteger $row[$field] 0 ([uint32]::MaxValue))) { $issues.Add('invalidMailField:' + $field) }
        }
        if (-not (Test-GawInteger $row.gold 0 ([ulong]::MaxValue)) -or $row.attachmentPresentInInbox -isnot [bool]) { $issues.Add('invalidMailRow') }
    }
    foreach ($kind in @('Configuration','Character','Items','Spells','Proficiencies','Mailbox')) {
        $file=@($Report.files | Where-Object { $_.kind -ceq $kind })
        if ($file.Count -ne 1 -or $file[0].present -isnot [bool] -or $file[0].bytesAndMetadataUnchanged -isnot [bool] -or
            -not (Test-GawInteger $file[0].length 0 ([long]::MaxValue))) { $issues.Add('invalidFileProjection:' + $kind) }
    }
    foreach ($kind in @('Character','Items')) {
        $file = @($Report.files | Where-Object { $_.kind -ceq $kind })
        if ($file.Count -ne 1 -or $file[0].present -ne $true -or $file[0].bytesAndMetadataUnchanged -ne $true -or
            -not $file[0].Contains('contentSha256') -or $file[0].contentSha256 -isnot [string] -or
            $file[0].contentSha256 -cnotmatch '^[0-9A-F]{64}$') { $issues.Add('completeChangedImageRequired:' + $kind) }
    }
    return $issues.ToArray()
}

function Test-GahSelection($Before, $Intent) {
    $issues = @(Test-GaHealthReport $Before)
    if (-not (Test-GahType $Intent.expectedItemId)) { $issues += 'explicitHealthTypeRequired' }
    $item = @($Before.items | Where-Object { $_.uid -eq $Intent.expectedItemUid })
    if ($item.Count -ne 1 -or $item[0].container -cne 'inventory' -or $item[0].position -ne 0 -or
        $item[0].warehouseId -ne 0 -or $item[0].itemId -ne $Intent.expectedItemId) { $issues += 'exactInventoryPotionRequired' }
    if ($Before.character.Contains('hitpoints') -and $Before.character.hitpoints -eq $Intent.expectedHitpoints) { $issues += 'expectedHitpointsMustDiffer' }
    return $issues
}

function Test-GaHealthTransition($Before, $After, $Intent) {
    $issues = [Collections.Generic.List[string]]::new()
    foreach ($issue in @(Test-GahSelection $Before $Intent) + @(Test-GaHealthReport $After)) { $issues.Add($issue) }
    if ([guid]$Before.domain -ne [guid]$After.domain -or $Before.accountId -ne $After.accountId -or
        $Before.character.uid -ne $After.character.uid -or [guid]$Before.domain -ne [guid]$Intent.domain -or
        $Before.accountId -ne $Intent.accountId -or $Before.character.uid -ne $Intent.characterUid) { $issues.Add('sameSavedIdentityRequired') }
    if (-not $After.character.Contains('hitpoints') -or $After.character.hitpoints -ne $Intent.expectedHitpoints) { $issues.Add('expectedSavedHitpointsNotObserved') }
    foreach ($field in @('map','x','y','body','class','level','gold','conquerPoints','minMana','maxMana')) {
        if (-not $Before.character.Contains($field) -or -not $After.character.Contains($field) -or
            $Before.character[$field] -cne $After.character[$field]) { $issues.Add('characterProjectionChanged:' + $field) }
    }
    $selected = @($Before.items | Where-Object { $_.uid -eq $Intent.expectedItemUid })
    if ($selected.Count -ne 1) { return $issues.ToArray() }
    $last = $selected[0].stackSize -le 1
    $consumed = @($After.items | Where-Object { $_.uid -eq $Intent.expectedItemUid })
    if ($last) {
        if ($consumed.Count -ne 0) { $issues.Add('lastPotionUidStillPresent') }
    } elseif ($consumed.Count -ne 1) { $issues.Add('stackedPotionUidMissing') }
    else {
        foreach ($field in $selected[0].Keys) {
            $expected = $(if ($field -ceq 'stackSize') { $selected[0].stackSize - 1 } else { $selected[0][$field] })
            if ($consumed[0][$field] -cne $expected) { $issues.Add('selectedPotionChanged:' + $field) }
        }
    }
    foreach ($field in @('inventoryCount','physicalItemCount','equipmentCount','warehouseCount','spellCount','proficiencyCount','activeMailCount')) {
        $expected = $Before[$field] - $(if ($last -and $field -cin @('inventoryCount','physicalItemCount')) { 1 } else { 0 })
        if ($After[$field] -ne $expected) { $issues.Add('unexpectedCount:' + $field) }
    }
    foreach ($group in @(@{name='items';key='uid'},@{name='mails';key='mailId'})) {
        $prior = @($Before[$group.name] | Where-Object { $group.name -cne 'items' -or $_.uid -ne $Intent.expectedItemUid })
        $next = @($After[$group.name] | Where-Object { $group.name -cne 'items' -or $_.uid -ne $Intent.expectedItemUid })
        if ($prior.Count -ne $next.Count) { $issues.Add('unrelatedRowsChanged:' + $group.name) }
        foreach ($row in $prior) {
            $match = @($next | Where-Object { $_[$group.key] -eq $row[$group.key] })
            if ($match.Count -ne 1) { $issues.Add('unrelatedRowIdentityChanged:' + $group.name); continue }
            foreach ($field in $row.Keys) { if ($row[$field] -cne $match[0][$field]) { $issues.Add('unrelatedRowChanged:' + $group.name + ':' + $field) } }
        }
    }
    foreach ($kind in @('Spells','Proficiencies','Mailbox','Character','Items')) {
        $a=@($Before.files | Where-Object { $_.kind -ceq $kind }); $b=@($After.files | Where-Object { $_.kind -ceq $kind })
        if ($a.Count -ne 1 -or $b.Count -ne 1) { continue }
        if ($kind -cin @('Character','Items')) {
            if ($a[0].contentSha256 -ceq $b[0].contentSha256) { $issues.Add('savedImageUnchanged:' + $kind) }
        } else {
            foreach ($field in @('present','length','contentSha256')) {
                if ($a[0][$field] -cne $b[0][$field]) { $issues.Add('savedSidecarChanged:' + $kind) }
            }
        }
    }
    return $issues.ToArray()
}
