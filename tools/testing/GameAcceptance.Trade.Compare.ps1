Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'GameAcceptance.Trade.Numbers.ps1')
. (Join-Path $PSScriptRoot 'GameAcceptance.Trade.Intent.ps1')
. (Join-Path $PSScriptRoot 'GameAcceptance.Trade.Validation.ps1')

function Test-GatUnchangedRows($Before, $After, [string]$Key, [string]$Label) {
    $issues = @()
    if (@($Before).Count -ne @($After).Count) { $issues += 'unrelatedRowsChanged:' + $Label }
    foreach ($row in @($Before)) {
        $matches = @($After | Where-Object { $_[$Key] -eq $row[$Key] })
        if ($matches.Count -ne 1) { $issues += 'unrelatedRowIdentityChanged:' + $Label; continue }
        foreach ($field in $row.Keys) {
            if ($row[$field] -cne $matches[0][$field]) { $issues += 'unrelatedRowChanged:' + $Label + ':' + $field }
        }
    }
    return $issues
}

function Test-GaTradeTransition($BeforeA, $BeforeB, $AfterA, $AfterB, $Intent) {
    $issues = @(Test-GaTradeSelection $BeforeA $BeforeB $Intent)
    if ($issues.Count) { return $issues }
    $issues = @(Test-GatPair $AfterA $AfterB $Intent)
    if ($issues.Count) { return $issues }
    $before = @($BeforeA,$BeforeB); $after = @($AfterA,$AfterB)
    if (@($BeforeA.items).Count + @($BeforeB.items).Count -ne @($AfterA.items).Count + @($AfterB.items).Count) {
        $issues += 'pairPhysicalItemCountChanged'
    }
    for ($index = 0; $index -lt 2; $index++) {
        $prior = $before[$index]; $next = $after[$index]; $party = $Intent.participants[$index]
        foreach ($field in @('map','x','y','body','class','level','hitpoints','minMana','maxMana')) {
            if ($prior.character[$field] -cne $next.character[$field]) { $issues += 'characterProjectionChanged:' + $field }
        }
        foreach ($change in @(@{field='gold';delta='goldDelta'},@{field='conquerPoints';delta='conquerPointsDelta'})) {
            [decimal]$expected = [decimal]$prior.character[$change.field] + [decimal]$party[$change.delta]
            if ([decimal]$next.character[$change.field] -ne $expected) { $issues += 'expectedCurrencyDeltaNotObserved:' + $change.field }
        }
        $outgoing = @($Intent.items | Where-Object { $_.fromAccountId -eq $prior.accountId }).Count
        $incoming = @($Intent.items | Where-Object { $_.toAccountId -eq $prior.accountId }).Count
        foreach ($field in @('inventoryCount','physicalItemCount','equipmentCount','warehouseCount','spellCount','proficiencyCount','activeMailCount')) {
            $delta = $(if ($field -cin @('inventoryCount','physicalItemCount')) { $incoming - $outgoing } else { 0 })
            if ($next[$field] -ne $prior[$field] + $delta) { $issues += 'unexpectedTradeCount:' + $field }
        }
        foreach ($row in $prior.items) {
            $selection = @($Intent.items | Where-Object { $_.uid -eq $row.uid })
            $destination = $prior.accountId
            if ($selection.Count -eq 1) { $destination = $selection[0].toAccountId }
            $destinationReport = $(if ($AfterA.accountId -eq $destination) { $AfterA } else { $AfterB })
            $matches = @($destinationReport.items | Where-Object { $_.uid -eq $row.uid })
            if ($matches.Count -ne 1) { $issues += 'itemMissingOrWrongSavedOwner'; continue }
            foreach ($field in $row.Keys) {
                if ($row[$field] -cne $matches[0][$field]) { $issues += 'transferredOrUnrelatedItemChanged:' + $field }
            }
        }
        $issues += @(Test-GatUnchangedRows $prior.mails $next.mails 'mailId' 'mails')
        foreach ($kind in @('Configuration','Character','Items','Spells','Proficiencies','Mailbox')) {
            $a = @($prior.files | Where-Object { $_.kind -ceq $kind })[0]
            $b = @($next.files | Where-Object { $_.kind -ceq $kind })[0]
            $changed = ($kind -ceq 'Character' -and ($party.goldDelta -ne 0 -or $party.conquerPointsDelta -ne 0)) -or
                ($kind -ceq 'Items' -and ($incoming + $outgoing) -gt 0)
            if ($changed) {
                if ($a.contentSha256 -ceq $b.contentSha256) { $issues += 'expectedSavedImageChangeNotObserved:' + $kind }
            } else {
                foreach ($field in @('present','length','contentSha256')) {
                    if ($a[$field] -cne $b[$field]) { $issues += 'untouchedSavedImageChanged:' + $kind }
                }
            }
        }
    }
    foreach ($field in @('gold','conquerPoints')) {
        [decimal]$totalBefore = [decimal]$BeforeA.character[$field] + [decimal]$BeforeB.character[$field]
        [decimal]$totalAfter = [decimal]$AfterA.character[$field] + [decimal]$AfterB.character[$field]
        if ($totalBefore -ne $totalAfter) { $issues += 'pairCurrencyNotConserved:' + $field }
    }
    return $issues
}

function New-GaTradeObservation([string[]]$Issues) {
    $actual = [string[]]@($Issues | Where-Object { $null -ne $_ -and $_.Length -gt 0 } | Select-Object -Unique)
    return @{ status=$(if ($actual.Count) { 'ClientActionPendingOrStateMismatch' } else { 'SavedTradeObserved' });
        assertions=$actual; liveClientVisualStatus='LiveClientVisualPending'; liveStoreSynchronized=$false;
        wireStatus='WirePending'; atomicityStatus='AtomicityPending'; sourceGateStatus='SourceGatePending';
        verificationBound='Two saved projections, explicit complete-row transfers and conserved expected wallets only; touched opaque bytes, live Store, client/wire, cross-player atomic snapshot, transaction safety and attestation remain unproved.' }
}
