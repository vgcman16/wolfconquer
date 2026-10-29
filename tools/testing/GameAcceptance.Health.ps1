Set-StrictMode -Version Latest

function Read-GaHealthIntent([string]$Root) {
    $read=Read-GasJson $Root 'health-intent.json'; $intent=$read.value
    $keys=@('schemaVersion','evidenceId','domain','accountId','characterUid','expectedItemUid','expectedItemId','expectedHitpoints','beforeSha256')
    Assert-GasKeys $intent $keys
    if ($intent.Count -ne $keys.Count -or @($intent.Keys | Where-Object { $_ -cnotin $keys }).Count -ne 0 -or
        -not (Test-GawInteger $intent.schemaVersion 1 1) -or $intent.evidenceId -cne [IO.Path]::GetFileName($Root) -or
        $intent.domain -isnot [string] -or $intent.domain -cnotmatch '^[0-9a-f]{32}$' -or [guid]::ParseExact($intent.domain,'N') -eq [guid]::Empty -or
        -not (Test-GawInteger $intent.accountId 1 ([uint32]::MaxValue)) -or -not (Test-GawInteger $intent.characterUid 1 ([uint32]::MaxValue)) -or
        -not (Test-GawInteger $intent.expectedItemUid 1 ([uint32]::MaxValue)) -or -not (Test-GahType $intent.expectedItemId) -or
        -not (Test-GawInteger $intent.expectedHitpoints 0 ([long]::MaxValue)) -or
        $intent.beforeSha256 -isnot [string] -or $intent.beforeSha256 -cnotmatch '^[0-9A-F]{64}$') { throw 'invalidHealthIntent' }
    return $read
}

function New-GaHealthState([string]$Status, [string[]]$Issues) {
    return @{status=$Status; assertions=[string[]]@($Issues | Where-Object { $null -ne $_ -and $_.Length -gt 0 });
        liveClientVisualStatus='LiveClientVisualPending'; liveStoreSynchronized=$false; wireStatus='WirePending'; sourceGateStatus='SourceGatePending';
        verificationBound='Selected saved HP and one-unit summarized potion transition only; no live Store, wire, client, atomicity or authenticated attestation.'}
}

function Set-GahPins($State, $Intent) {
    foreach ($key in @('expectedItemUid','expectedItemId','expectedHitpoints')) { $State[$key]=$Intent[$key] }
    $State.intent='health-intent.json'; $State.beforeSha256=$Intent.beforeSha256
}

function Start-GaHealth($Context, $Before, [uint32]$Uid, [uint32]$Type, [long]$Hitpoints) {
    $intent=@{schemaVersion=1; evidenceId=[IO.Path]::GetFileName($Context.evidenceRoot); domain=([guid]$Before.domain).ToString('N');
        accountId=$Before.accountId; characterUid=$Before.character.uid; expectedItemUid=$Uid; expectedItemId=$Type; expectedHitpoints=$Hitpoints;
        beforeSha256=(Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $Context.evidenceRoot 'before.json')).Hash}
    $issues=@(Test-GahSelection $Before $intent)
    if ($issues.Count) { return New-GaHealthState 'PrerequisiteMissing' $issues }
    Save-GaJson $Context 'health-intent.json' $intent
    $null=Read-GaHealthIntent $Context.evidenceRoot
    $state=New-GaHealthState 'WaitingForSavedHealthConsumption' @(); Set-GahPins $state $intent
    $state.intentSha256=(Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $Context.evidenceRoot 'health-intent.json')).Hash
    $state.guidedActions=@('Use exactly one retained health potion in the actual test client and complete its real save lifecycle.',
        'Resume with the same UID/type/expected HP; this tool only reads saved files.')
    return $state
}

function Resume-GaHealth($Context, $Initial, $Before, [string]$AfterLeaf, [uint32]$Uid, [uint32]$Type, [long]$Hitpoints) {
    if ($Initial.state.status -cne 'WaitingForSavedHealthConsumption' -or $Initial.state.intent -cne 'health-intent.json') { throw 'initialHealthIntentRequired' }
    $read=Read-GaHealthIntent $Context.evidenceRoot; $intent=$read.value
    if (-not (Test-GasHash $read.bytes $Initial.state.intentSha256) -or
        (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $Context.evidenceRoot 'before.json')).Hash -cne $Initial.state.beforeSha256 -or
        $intent.beforeSha256 -cne $Initial.state.beforeSha256 -or $intent.accountId -ne $Context.accountId -or
        [guid]$intent.domain -ne [guid]$Before.domain -or $intent.characterUid -ne $Before.character.uid -or
        $intent.expectedItemUid -ne $Uid -or $intent.expectedItemId -ne $Type -or $intent.expectedHitpoints -ne $Hitpoints -or
        $Initial.state.expectedItemUid -ne $Uid -or $Initial.state.expectedItemId -ne $Type -or $Initial.state.expectedHitpoints -ne $Hitpoints) { throw 'healthIntentIdentityMismatch' }
    $after=Get-GaInspection $Context $AfterLeaf # The sole child command on Resume.
    $issues=@(Test-GaHealthTransition $Before $after $intent)
    $state=New-GaHealthState $(if ($issues.Count) { 'ClientActionPendingOrStateMismatch' } else { 'SavedHealthConsumptionObserved' }) $issues
    Set-GahPins $state $intent; $state.intentSha256=$Initial.state.intentSha256
    $state.afterSha256=(Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $Context.evidenceRoot $AfterLeaf)).Hash
    return $state
}
