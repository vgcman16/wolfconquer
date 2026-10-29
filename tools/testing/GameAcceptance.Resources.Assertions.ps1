Set-StrictMode -Version Latest

function Get-GarProfile([string]$Scenario) {
    switch -CaseSensitive ($Scenario) {
        'consume-health' { return @{intent='health-intent.json'; waiting='WaitingForSavedHealthConsumption'; observed='SavedHealthConsumptionObserved';
            expectedKeys=@('expectedHitpoints'); characterFields=@('hitpoints'); typeIssue='explicitHealthTypeRequired';
            bound='Selected saved HP and one-unit summarized potion transition only; no live Store, wire, client, atomicity or authenticated attestation.'} }
        'consume-mana' { return @{intent='mana-intent.json'; waiting='WaitingForSavedManaConsumption'; observed='SavedManaConsumptionObserved';
            expectedKeys=@('expectedMana'); characterFields=@('minMana'); typeIssue='explicitManaTypeRequired';
            bound='Selected saved Mana and one-unit complete reported potion transition only; no full opaque item/Character preservation, live Store, wire, client, atomicity or authenticated attestation.'} }
        'consume-full-restore' { return @{intent='full-restore-intent.json'; waiting='WaitingForSavedFullRestoreConsumption'; observed='SavedFullRestoreConsumptionObserved';
            expectedKeys=@('expectedHitpoints','expectedMana'); characterFields=@('hitpoints','minMana'); typeIssue='explicitFullRestoreTypeRequired';
            bound='Selected saved HP/Mana and one-unit complete reported potion transition only; no HP-trigger/formula proof, full opaque item/Character preservation, live Store, wire, client, atomicity or authenticated attestation.'} }
        default { throw 'unknownSavedResourceScenario' }
    }
}

function Test-GarType($Type, [string]$Scenario) {
    if ($Scenario -ceq 'consume-health') { return Test-GahType $Type }
    if (-not (Test-GawInteger $Type 1 ([uint32]::MaxValue))) { return $false }
    if ($Scenario -ceq 'consume-mana') { return ($Type -ge 1001000 -and $Type -le 1001040) -or $Type -in @(1002030,1002040,725066,1004010) }
    return $Scenario -ceq 'consume-full-restore' -and $Type -eq 723726
}

function Test-GarSelection($Before, $Intent, [string]$Scenario) {
    $profile=Get-GarProfile $Scenario; $issues=@(Test-GaHealthReport $Before)
    if (-not (Test-GarType $Intent.expectedItemId $Scenario)) { $issues += $profile.typeIssue }
    $item=@($Before.items | Where-Object { $_.uid -eq $Intent.expectedItemUid })
    if ($item.Count -ne 1 -or $item[0].container -cne 'inventory' -or $item[0].position -ne 0 -or
        $item[0].warehouseId -ne 0 -or $item[0].itemId -ne $Intent.expectedItemId) { $issues += 'exactInventoryPotionRequired' }
    if ($Scenario -ceq 'consume-health') {
        if ($Before.character.Contains('hitpoints') -and $Before.character.hitpoints -eq $Intent.expectedHitpoints) { $issues += 'expectedHitpointsMustDiffer' }
    } else {
        $improves=$false
        for ($i=0; $i -lt $profile.expectedKeys.Count; $i++) {
            $field=$profile.characterFields[$i]; $key=$profile.expectedKeys[$i]
            if (-not $Before.character.Contains($field)) { continue }
            if ($Intent[$key] -lt $Before.character[$field]) { $issues += 'expectedResourceMustNotDecrease:' + $field }
            if ($Intent[$key] -gt $Before.character[$field]) { $improves=$true }
        }
        if (-not $improves) { $issues += 'expectedResourceMustImprove' }
    }
    return $issues
}

function Test-GaResourceTransition($Before, $After, $Intent, [string]$Scenario) {
    $issues = [Collections.Generic.List[string]]::new()
    foreach ($issue in @(Test-GarSelection $Before $Intent $Scenario) + @(Test-GaHealthReport $After)) { $issues.Add($issue) }
    if ([guid]$Before.domain -ne [guid]$After.domain -or $Before.accountId -ne $After.accountId -or
        $Before.character.uid -ne $After.character.uid -or [guid]$Before.domain -ne [guid]$Intent.domain -or
        $Before.accountId -ne $Intent.accountId -or $Before.character.uid -ne $Intent.characterUid) { $issues.Add('sameSavedIdentityRequired') }
    $profile=Get-GarProfile $Scenario
    for ($i=0; $i -lt $profile.expectedKeys.Count; $i++) {
        $field=$profile.characterFields[$i]; $key=$profile.expectedKeys[$i]
        if (-not $After.character.Contains($field) -or $After.character[$field] -ne $Intent[$key]) {
            $issues.Add($(if ($field -ceq 'hitpoints') { 'expectedSavedHitpointsNotObserved' } else { 'expectedSavedManaNotObserved' }))
        }
    }
    foreach ($field in (@('map','x','y','body','class','level','gold','conquerPoints','hitpoints','minMana','maxMana') | Where-Object { $_ -cnotin $profile.characterFields })) {
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
