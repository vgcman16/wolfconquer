Set-StrictMode -Version Latest

function Assert-GatKeys($Value, [string[]]$Keys) {
    Assert-GasKeys $Value $Keys
    if ($Value.Count -ne $Keys.Count -or @($Value.Keys | Where-Object { $_ -cnotin $Keys }).Count) { throw 'invalidTradeSchema' }
}

function Read-GatDocument([byte[]]$Bytes) {
    if ($null -eq $Bytes -or $Bytes.Length -eq 0 -or $Bytes.Length -gt 16KB) { throw 'invalidTradeDocumentSize' }
    $text = [Text.UTF8Encoding]::new($false, $true).GetString($Bytes)
    $document = [System.Text.Json.JsonDocument]::Parse($text)
    try {
        Assert-GasJsonKeys $document.RootElement
        if ($document.RootElement.ValueKind -ne [System.Text.Json.JsonValueKind]::Object) { throw 'invalidTradeDocument' }
        return ($text | ConvertFrom-Json -AsHashtable)
    } finally { $document.Dispose() }
}

function Assert-GatOffer($Value, [bool]$WithCharacterUid) {
    if ($Value.participants -isnot [array] -or $Value.participants.Count -ne 2 -or
        $Value.items -isnot [array] -or $Value.items.Count -gt 40) { throw 'invalidTradeParticipantsOrItems' }
    $accounts = [Collections.Generic.HashSet[uint32]]::new()
    $characters = [Collections.Generic.HashSet[uint32]]::new()
    [decimal]$gold = 0; [decimal]$cp = 0; $changes = $Value.items.Count -gt 0
    foreach ($party in $Value.participants) {
        $keys = @('accountId','goldDelta','conquerPointsDelta')
        if ($WithCharacterUid) { $keys += 'characterUid' }
        Assert-GatKeys $party $keys
        if (-not (Test-GatInteger $party.accountId 1 ([uint32]::MaxValue)) -or -not $accounts.Add([uint32]$party.accountId) -or
            -not (Test-GatInteger $party.goldDelta (-[decimal][long]::MaxValue) ([long]::MaxValue)) -or
            -not (Test-GatInteger $party.conquerPointsDelta (-[decimal][long]::MaxValue) ([long]::MaxValue))) { throw 'invalidTradeParticipant' }
        if ($WithCharacterUid -and (-not (Test-GatInteger $party.characterUid 1 ([uint32]::MaxValue)) -or
            -not $characters.Add([uint32]$party.characterUid))) { throw 'invalidTradeCharacter' }
        $gold += [decimal]$party.goldDelta; $cp += [decimal]$party.conquerPointsDelta
        $changes = $changes -or $party.goldDelta -ne 0 -or $party.conquerPointsDelta -ne 0
    }
    if ($gold -ne 0 -or $cp -ne 0 -or -not $changes) { throw 'tradeExpectedDeltasMustConserveAndChange' }
    $uids = [Collections.Generic.HashSet[uint32]]::new()
    foreach ($item in $Value.items) {
        Assert-GatKeys $item @('uid','itemId','stackSize','fromAccountId','toAccountId')
        foreach ($key in @('uid','itemId','fromAccountId','toAccountId')) {
            if (-not (Test-GatInteger $item[$key] 1 ([uint32]::MaxValue))) { throw 'invalidTradeItem' }
        }
        if (-not (Test-GatInteger $item.stackSize 0 65535) -or -not $uids.Add([uint32]$item.uid) -or
            $item.fromAccountId -eq $item.toAccountId -or -not $accounts.Contains([uint32]$item.fromAccountId) -or
            -not $accounts.Contains([uint32]$item.toAccountId)) { throw 'invalidTradeItemRoute' }
    }
}

function Read-GaTradePlan([byte[]]$Bytes) {
    $plan = Read-GatDocument $Bytes
    Assert-GatKeys $plan @('schemaVersion','participants','items')
    if (-not (Test-GatInteger $plan.schemaVersion 1 1)) { throw 'unsupportedTradePlan' }
    Assert-GatOffer $plan $false
    return $plan
}

function Read-GaTradeIntent([byte[]]$Bytes, [string]$ExpectedSha256, [string]$EvidenceId) {
    if (-not (Test-GasHash $Bytes $ExpectedSha256)) { throw 'tradeIntentChanged' }
    $intent = Read-GatDocument $Bytes
    Assert-GatKeys $intent @('schemaVersion','evidenceId','domain','participants','items','beforeSha256')
    if (-not (Test-GatInteger $intent.schemaVersion 1 1) -or $intent.evidenceId -isnot [string] -or
        $intent.evidenceId -cne $EvidenceId -or $EvidenceId -cnotmatch '^wolf-game-acceptance-[0-9a-f]{32}$' -or
        [guid]::ParseExact($EvidenceId.Substring(21),'N') -eq [guid]::Empty -or
        $intent.domain -isnot [string] -or $intent.domain -cnotmatch '^[0-9a-f]{32}$' -or
        [guid]::ParseExact($intent.domain,'N') -eq [guid]::Empty -or
        $intent.beforeSha256 -isnot [array] -or $intent.beforeSha256.Count -ne 2) { throw 'invalidTradeIntentIdentity' }
    foreach ($hash in $intent.beforeSha256) {
        if ($hash -isnot [string] -or $hash -cnotmatch '^[0-9A-F]{64}$') { throw 'invalidTradeBeforeHash' }
    }
    Assert-GatOffer $intent $true
    return $intent
}
