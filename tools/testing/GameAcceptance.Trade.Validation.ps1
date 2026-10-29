Set-StrictMode -Version Latest

function Test-GatReport($Report) {
    try {
        Assert-GasKeys $Report @('schemaVersion','domain','accountId','character','items','mails','files','summariesTruncated',
            'liveStoreSynchronized','inventoryCount','equipmentCount','warehouseCount','physicalItemCount','spellCount','proficiencyCount','activeMailCount')
        Assert-GasKeys $Report.character @('uid','map','x','y','body','class','level','gold','conquerPoints')
        foreach ($field in @('hitpoints','minMana','maxMana')) {
            if (-not $Report.character.Contains($field)) { return @('savedVitalRequired:' + $field) }
        }
        Assert-GatKeys $Report.character @('uid','map','x','y','body','class','level','gold','conquerPoints','hitpoints','minMana','maxMana')
        $domain = [guid]::Empty
        if ($Report.domain -isnot [string] -or -not [guid]::TryParse($Report.domain, [ref]$domain)) {
            return @('invalidSavedDomain')
        }
        if ([guid]$Report.domain -eq [guid]::Empty) { return @('invalidSavedDomain') }
        $issues = @(Test-GaHealthReport $Report) + @(Test-GatReportIntegers $Report)
        foreach ($kind in @('Configuration','Character','Items','Spells','Proficiencies','Mailbox')) {
            $file = @($Report.files | Where-Object { $_.kind -ceq $kind })
            if ($file.Count -ne 1 -or -not $file[0].Contains('contentSha256') -or
                $file[0].bytesAndMetadataUnchanged -ne $true -or
                ($file[0].present -and ($file[0].contentSha256 -isnot [string] -or $file[0].contentSha256 -cnotmatch '^[0-9A-F]{64}$')) -or
                (-not $file[0].present -and ($null -ne $file[0].contentSha256 -or $file[0].length -ne 0))) { $issues += 'completeTradeFileRequired:' + $kind }
            if ($kind -ceq 'Configuration' -and ($file.Count -ne 1 -or -not $file[0].present)) { $issues += 'configurationImageRequired' }
        }
        return $issues
    } catch { return @('malformedSavedTradeReport') }
}

function Test-GatPair($First, $Second, $Intent) {
    $issues = @(Test-GatReport $First) + @(Test-GatReport $Second)
    if ($issues.Count) { return $issues }
    $reports = @($First,$Second)
    $uids = [Collections.Generic.HashSet[uint32]]::new()
    for ($index = 0; $index -lt 2; $index++) {
        $report = $reports[$index]; $party = $Intent.participants[$index]
        if ([guid]$report.domain -ne [guid]$Intent.domain -or $report.accountId -ne $party.accountId -or
            $report.character.uid -ne $party.characterUid) { $issues += 'sameExplicitSavedIdentityRequired' }
        foreach ($row in $report.items) {
            if (-not $uids.Add([uint32]$row.uid)) { $issues += 'duplicateUidAcrossPlayers' }
        }
    }
    $firstConfig = @($First.files | Where-Object { $_.kind -ceq 'Configuration' })[0]
    $secondConfig = @($Second.files | Where-Object { $_.kind -ceq 'Configuration' })[0]
    foreach ($field in @('present','length','contentSha256')) {
        if ($firstConfig[$field] -cne $secondConfig[$field]) { $issues += 'sameOwnedConfigurationRequired' }
    }
    return $issues
}

function Test-GaTradeSelection($BeforeA, $BeforeB, $Intent) {
    try { Assert-GatOffer $Intent $true } catch { return @('invalidTradeIntent') }
    $issues = @(Test-GatPair $BeforeA $BeforeB $Intent)
    if ($issues.Count) { return $issues }
    foreach ($selection in $Intent.items) {
        $source = $(if ($BeforeA.accountId -eq $selection.fromAccountId) { $BeforeA } else { $BeforeB })
        $row = @($source.items | Where-Object { $_.uid -eq $selection.uid })
        if ($row.Count -ne 1 -or $row[0].container -cne 'inventory' -or $row[0].position -ne 0 -or $row[0].warehouseId -ne 0 -or
            $row[0].itemId -ne $selection.itemId -or $row[0].stackSize -ne $selection.stackSize) { $issues += 'exactSourceInventoryRowRequired' }
        if (@($source.items | Where-Object { $_.container -ceq 'inventory.deposited' }).Count -or
            @(@($BeforeA.mails) + @($BeforeB.mails) | Where-Object { $_.attachedItemUid -eq $selection.uid }).Count) {
            $issues += 'selectedOwnershipAmbiguous'
        }
    }
    for ($index = 0; $index -lt 2; $index++) {
        $report = @($BeforeA,$BeforeB)[$index]; $party = $Intent.participants[$index]
        foreach ($change in @(@{field='gold';delta='goldDelta'},@{field='conquerPoints';delta='conquerPointsDelta'})) {
            [decimal]$next = [decimal]$report.character[$change.field] + [decimal]$party[$change.delta]
            if ($next -lt 0 -or $next -gt [long]::MaxValue) { $issues += 'expectedCurrencyOutOfReportRange:' + $change.field }
        }
    }
    return $issues
}
