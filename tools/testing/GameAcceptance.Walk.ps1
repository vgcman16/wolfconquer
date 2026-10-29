Set-StrictMode -Version Latest

function Test-GawInteger($Value, [decimal]$Minimum, [decimal]$Maximum) {
    return $null -ne $Value -and $Value -isnot [bool] -and $Value -isnot [string] -and
        $Value -ge $Minimum -and $Value -le $Maximum -and [decimal]$Value -eq [decimal]::Truncate([decimal]$Value)
}

function Read-GaWalkIntent([string]$Root) {
    $read = Read-GasJson $Root 'walk-intent.json' # Existing bounded strict duplicate-key reader.
    $intent = $read.value
    $keys = @('schemaVersion','evidenceId','domain','accountId','characterUid','expectedMap','expectedX','expectedY','beforeSha256')
    Assert-GasKeys $intent $keys
    if ($intent.Count -ne $keys.Count -or @($intent.Keys | Where-Object { $_ -cnotin $keys }).Count -ne 0 -or
        -not (Test-GawInteger $intent.schemaVersion 1 1) -or $intent.evidenceId -cne [IO.Path]::GetFileName($Root) -or
        $intent.domain -isnot [string] -or $intent.domain -cnotmatch '^[0-9a-f]{32}$' -or [guid]::ParseExact($intent.domain,'N') -eq [guid]::Empty -or
        -not (Test-GawInteger $intent.accountId 1 ([uint32]::MaxValue)) -or
        -not (Test-GawInteger $intent.characterUid 1 ([uint32]::MaxValue)) -or
        -not (Test-GawInteger $intent.expectedMap 1 ([uint32]::MaxValue)) -or
        -not (Test-GawInteger $intent.expectedX 0 65535) -or -not (Test-GawInteger $intent.expectedY 0 65535) -or
        $intent.beforeSha256 -isnot [string] -or $intent.beforeSha256 -cnotmatch '^[0-9A-F]{64}$') { throw 'invalidWalkIntent' }
    return $read
}

function Test-GaWalkReport($Report) {
    $issues = [Collections.Generic.List[string]]::new()
    if ($Report.summariesTruncated -ne $false -or $Report.liveStoreSynchronized -ne $false) { $issues.Add('completeSavedProjectionRequired') }
    if (@($Report.files).Count -ne 6 -or @($Report.items).Count -gt 200 -or @($Report.mails).Count -gt 200) { $issues.Add('completeSavedProjectionRequired') }
    foreach ($field in @('map','x','y','body','class','level','gold','conquerPoints')) {
        if (-not (Test-GawInteger $Report.character[$field] 0 ([long]::MaxValue))) { $issues.Add('invalidCharacterProjection:' + $field) }
    }
    foreach ($field in @('inventoryCount','equipmentCount','warehouseCount','physicalItemCount','spellCount','proficiencyCount','activeMailCount')) {
        if (-not (Test-GawInteger $Report[$field] 0 ([int]::MaxValue))) { $issues.Add('invalidCount:' + $field) }
    }
    if (@($Report.items).Count -ne $Report.physicalItemCount -or @($Report.mails).Count -ne $Report.activeMailCount) { $issues.Add('incompleteRows') }
    $containers = @('inventory','equipment','warehouse','relicFuse','mythsoulBag','rune','wardrobe')
    $allowedContainers = @($containers) + @($containers | ForEach-Object { $_ + '.deposited' })
    foreach ($item in @($Report.items)) {
        if ($item.container -isnot [string] -or $item.container -cnotin $allowedContainers) { $issues.Add('unknownItemContainer') }
    }
    foreach ($container in @('inventory','equipment','warehouse')) {
        $count = @($Report.items | Where-Object { $_.container -ceq $container }).Count
        if ($count -ne $Report[($container + 'Count')]) { $issues.Add('containerCountMismatch') }
    }
    foreach ($rows in @(@{ name='item'; rows=@($Report.items); id='uid'; fields=@('container','uid','itemId','position','warehouseId','plus','stackSize','durability','maximumDurability','socketOne','socketTwo','bound','locked') },
                         @{ name='mail'; rows=@($Report.mails); id='mailId'; fields=@('mailId','attachedItemUid','attachmentPresentInInbox','gold','conquerPoints','packageFlag') })) {
        $ids = [Collections.Generic.HashSet[uint32]]::new()
        foreach ($row in $rows.rows) {
            if ($row -isnot [Collections.IDictionary] -or $row.Count -ne $rows.fields.Count -or
                @($row.Keys | Where-Object { $_ -cnotin $rows.fields }).Count -ne 0 -or
                -not (Test-GawInteger $row[$rows.id] 1 ([uint32]::MaxValue)) -or -not $ids.Add([uint32]$row[$rows.id])) {
                $issues.Add('incompleteOrDuplicate' + $rows.name + 'Row')
            }
        }
    }
    foreach ($kind in @('Items','Spells','Proficiencies','Mailbox')) {
        $file = @($Report.files | Where-Object { $_.kind -ceq $kind })
        if ($file.Count -ne 1 -or -not $file[0].Contains('contentSha256') -or $file[0].present -isnot [bool] -or
            $file[0].bytesAndMetadataUnchanged -ne $true -or
            ($file[0].present -and ($file[0].contentSha256 -isnot [string] -or $file[0].contentSha256 -cnotmatch '^[0-9A-F]{64}$')) -or
            (-not $file[0].present -and ($null -ne $file[0].contentSha256 -or $file[0].length -ne 0))) { $issues.Add('completeSidecarDigestRequired:' + $kind) }
    }
    return $issues.ToArray()
}

function Test-GaWalkTransition($Before, $After, $Intent) {
    $issues = [Collections.Generic.List[string]]::new()
    foreach ($issue in @(Test-GaWalkReport $Before) + @(Test-GaWalkReport $After)) { $issues.Add($issue) }
    if ([guid]$Before.domain -ne [guid]$After.domain -or $Before.accountId -ne $After.accountId -or
        $Before.character.uid -ne $After.character.uid) { $issues.Add('sameSavedIdentityRequired'); return $issues.ToArray() }
    if ($After.character.map -ne $Intent.expectedMap -or $After.character.x -ne $Intent.expectedX -or $After.character.y -ne $Intent.expectedY) { $issues.Add('expectedSavedDestinationNotObserved') }
    if ($Before.character.map -eq $After.character.map -and $Before.character.x -eq $After.character.x -and $Before.character.y -eq $After.character.y) { $issues.Add('savedPositionUnchanged') }
    foreach ($field in @('body','class','level','gold','conquerPoints')) {
        if ($Before.character[$field] -cne $After.character[$field]) { $issues.Add('characterProjectionChanged:' + $field) }
    }
    foreach ($field in @('inventoryCount','equipmentCount','warehouseCount','physicalItemCount','spellCount','proficiencyCount','activeMailCount')) {
        if ($Before[$field] -cne $After[$field]) { $issues.Add('countChanged:' + $field) }
    }
    foreach ($group in @(@{ name='items'; key='uid' }, @{ name='mails'; key='mailId' })) {
        foreach ($row in @($Before[$group.name])) {
            $matched = @($After[$group.name] | Where-Object { $_[$group.key] -eq $row[$group.key] })
            if ($matched.Count -ne 1) { $issues.Add('rowIdentityChanged:' + $group.name); continue }
            foreach ($field in $row.Keys) { if ($row[$field] -cne $matched[0][$field]) { $issues.Add('rowChanged:' + $group.name + ':' + $field) } }
        }
    }
    foreach ($kind in @('Items','Spells','Proficiencies','Mailbox')) {
        $a = @($Before.files | Where-Object { $_.kind -ceq $kind }); $b = @($After.files | Where-Object { $_.kind -ceq $kind })
        if ($a.Count -ne 1 -or $b.Count -ne 1) { continue }
        foreach ($field in @('present','length','contentSha256')) {
            if (-not $a[0].Contains($field) -or -not $b[0].Contains($field) -or $a[0][$field] -cne $b[0][$field]) { $issues.Add('savedSidecarChanged:' + $kind) }
        }
    }
    return $issues.ToArray()
}

function New-GaWalkState([string]$Status, [string[]]$Issues) {
    return @{ status=$Status; assertions=[string[]]@($Issues | Where-Object { $null -ne $_ -and $_.Length -gt 0 }); liveClientVisualStatus='LiveClientVisualPending';
        liveStoreSynchronized=$false; wireStatus='WirePending'; sourceGateStatus='SourceGatePending';
        verificationBound='Selected saved position/projections and sidecar image equality only; no movement path, client, live Store or authenticated attestation.' }
}

function Start-GaWalk($Context, $Before, [uint32]$Map, [uint16]$X, [uint16]$Y) {
    $issues = @(Test-GaWalkReport $Before)
    if ($Before.character.map -eq $Map -and $Before.character.x -eq $X -and $Before.character.y -eq $Y) { $issues += 'expectedDestinationMustDiffer' }
    if ($issues.Count) { return New-GaWalkState 'PrerequisiteMissing' $issues }
    $beforeHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $Context.evidenceRoot 'before.json')).Hash
    $intent = @{ schemaVersion=1; evidenceId=[IO.Path]::GetFileName($Context.evidenceRoot); domain=([guid]$Before.domain).ToString('N');
        accountId=$Before.accountId; characterUid=$Before.character.uid; expectedMap=$Map; expectedX=$X; expectedY=$Y; beforeSha256=$beforeHash }
    Save-GaJson $Context 'walk-intent.json' $intent
    $null = Read-GaWalkIntent $Context.evidenceRoot
    $state = New-GaWalkState 'WaitingForSavedWalk' @()
    $state.intent='walk-intent.json'; $state.beforeSha256=$beforeHash
    $state.intentSha256=(Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $Context.evidenceRoot 'walk-intent.json')).Hash
    $state.expectedMap=$Map; $state.expectedX=$X; $state.expectedY=$Y
    $state.guidedActions=@('Move the actual test client to the retained destination and complete its real save lifecycle.', 'Resume with the same destination/identity; this tool never moves or saves the client.')
    return $state
}

function Resume-GaWalk($Context, $Initial, $Before, [string]$AfterLeaf, [uint32]$Map, [uint16]$X, [uint16]$Y) {
    if ($Initial.state.status -cne 'WaitingForSavedWalk' -or $Initial.state.intent -cne 'walk-intent.json') { throw 'initialWalkIntentRequired' }
    $read = Read-GaWalkIntent $Context.evidenceRoot; $intent=$read.value
    if (-not (Test-GasHash $read.bytes $Initial.state.intentSha256) -or
        (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $Context.evidenceRoot 'before.json')).Hash -cne $Initial.state.beforeSha256 -or
        $intent.beforeSha256 -cne $Initial.state.beforeSha256 -or $intent.accountId -ne $Context.accountId -or
        [guid]$intent.domain -ne [guid]$Before.domain -or $intent.characterUid -ne $Before.character.uid -or
        $intent.expectedMap -ne $Map -or $intent.expectedX -ne $X -or $intent.expectedY -ne $Y -or
        $Initial.state.expectedMap -ne $Map -or $Initial.state.expectedX -ne $X -or $Initial.state.expectedY -ne $Y) { throw 'walkIntentIdentityMismatch' }
    $after=Get-GaInspection $Context $AfterLeaf # Sole child command on Resume.
    $issues=@(Test-GaWalkTransition $Before $after $intent)
    $state=New-GaWalkState $(if ($issues.Count) { 'ClientActionPendingOrStateMismatch' } else { 'SavedWalkObserved' }) $issues
    $state.expectedMap=$Map; $state.expectedX=$X; $state.expectedY=$Y
    $state.intent='walk-intent.json'; $state.intentSha256=$Initial.state.intentSha256
    $state.beforeSha256=$Initial.state.beforeSha256
    $state.afterSha256=(Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $Context.evidenceRoot $AfterLeaf)).Hash
    return $state
}
