Set-StrictMode -Version Latest

function Read-GaResourceIntent([string]$Root, [string]$Scenario) {
    $profile=Get-GarProfile $Scenario; $read=Read-GasJson $Root $profile.intent; $intent=$read.value
    $keys=@('schemaVersion','evidenceId','domain','accountId','characterUid','expectedItemUid','expectedItemId') + $profile.expectedKeys + @('beforeSha256')
    Assert-GasKeys $intent $keys
    $invalid=$intent.Count -ne $keys.Count -or @($intent.Keys | Where-Object { $_ -cnotin $keys }).Count -ne 0 -or
        -not (Test-GawInteger $intent.schemaVersion 1 1) -or $intent.evidenceId -cne [IO.Path]::GetFileName($Root) -or
        $intent.domain -isnot [string] -or $intent.domain -cnotmatch '^[0-9a-f]{32}$' -or [guid]::ParseExact($intent.domain,'N') -eq [guid]::Empty -or
        -not (Test-GawInteger $intent.accountId 1 ([uint32]::MaxValue)) -or -not (Test-GawInteger $intent.characterUid 1 ([uint32]::MaxValue)) -or
        -not (Test-GawInteger $intent.expectedItemUid 1 ([uint32]::MaxValue)) -or -not (Test-GarType $intent.expectedItemId $Scenario) -or
        $intent.beforeSha256 -isnot [string] -or $intent.beforeSha256 -cnotmatch '^[0-9A-F]{64}$'
    foreach ($key in $profile.expectedKeys) { if (-not (Test-GawInteger $intent[$key] 0 ([long]::MaxValue))) { $invalid=$true } }
    if ($invalid) { throw $(if ($Scenario -ceq 'consume-health') { 'invalidHealthIntent' } else { 'invalidSavedResourceIntent' }) }
    return $read
}

function New-GaResourceState([string]$Status, [string[]]$Issues, [string]$Scenario) {
    return @{status=$Status; assertions=[string[]]@($Issues | Where-Object { $null -ne $_ -and $_.Length -gt 0 });
        liveClientVisualStatus='LiveClientVisualPending'; liveStoreSynchronized=$false; wireStatus='WirePending'; sourceGateStatus='SourceGatePending';
        verificationBound=(Get-GarProfile $Scenario).bound}
}

function Set-GarPins($State, $Intent, [string]$Scenario) {
    $profile=Get-GarProfile $Scenario
    foreach ($key in @('expectedItemUid','expectedItemId') + $profile.expectedKeys) { $State[$key]=$Intent[$key] }
    $State.intent=$profile.intent; $State.beforeSha256=$Intent.beforeSha256
}

function Assert-GarInspectorRecord($Record) {
    Assert-GasKeys $Record @('steps','sourceChecks')
    if ($Record.sourceChecks.status -cne 'NotRequested' -or $Record.steps -isnot [array] -or $Record.steps.Count -ne 1) { throw 'resourceInspectorStepRequired' }
    $step=$Record.steps[0]
    Assert-GasKeys $step @('name','completed','exitCode','jsonParsed','stderrPresent','status')
    if ($step.name -cne 'inspect-owned-player' -or $step.completed -isnot [bool] -or $step.completed -ne $true -or
        -not (Test-GawInteger $step.exitCode 0 0) -or $step.jsonParsed -isnot [bool] -or $step.jsonParsed -ne $true -or
        $step.stderrPresent -isnot [bool] -or $step.stderrPresent -ne $false -or $step.status -cne 'exited') { throw 'resourceInspectorStepRequired' }
}

function Start-GaResource($Context, $Before, [string]$Scenario, [uint32]$Uid, [uint32]$Type, $Expected) {
    $profile=Get-GarProfile $Scenario
    $intent=@{schemaVersion=1; evidenceId=[IO.Path]::GetFileName($Context.evidenceRoot); domain=([guid]$Before.domain).ToString('N');
        accountId=$Before.accountId; characterUid=$Before.character.uid; expectedItemUid=$Uid; expectedItemId=$Type;
        beforeSha256=(Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $Context.evidenceRoot 'before.json')).Hash}
    foreach ($key in $profile.expectedKeys) { $intent[$key]=$Expected[$key] }
    $issues=@(Test-GarSelection $Before $intent $Scenario)
    if ($issues.Count) { return New-GaResourceState 'PrerequisiteMissing' $issues $Scenario }
    Save-GaJson $Context $profile.intent $intent; $null=Read-GaResourceIntent $Context.evidenceRoot $Scenario
    $state=New-GaResourceState $profile.waiting @() $Scenario; Set-GarPins $state $intent $Scenario
    $state.intentSha256=(Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $Context.evidenceRoot $profile.intent)).Hash
    $state.guidedActions=@('Use exactly one retained potion in the actual test client and complete its real save lifecycle.',
        'Resume with the identical UID/type and expected saved resources; this tool only reads saved files.')
    return $state
}

function Resume-GaResource($Context, $Initial, $Before, [string]$AfterLeaf, [string]$Scenario, [uint32]$Uid, [uint32]$Type, $Expected) {
    $profile=Get-GarProfile $Scenario
    if ($Scenario -cne 'consume-health') {
        # New resource evidence uses the existing strict bounded reader before
        # any child. Valid historical health schema/validation stays compatible.
        $Initial=(Read-GasJson $Context.evidenceRoot 'receipt.json').value
        $Before=(Read-GasJson $Context.evidenceRoot 'before.json').value.report
        Assert-GasKeys $Initial @('scenario','accountId','before','after','state','sourceChecks','visualAcceptance')
        Assert-GasKeys $Initial.state @('status','liveStoreSynchronized','wireStatus','sourceGateStatus','liveClientVisualStatus')
        if ($Initial.scenario -cne $Scenario -or $Initial.accountId -ne $Context.accountId -or $Initial.before -cne 'before.json' -or
            $null -ne $Initial.after -or $Initial.sourceChecks.status -cne 'NotRequested' -or
            $Initial.visualAcceptance -cne 'LiveClientVisualPending' -or $Initial.state.liveClientVisualStatus -cne 'LiveClientVisualPending' -or
            $Initial.state.liveStoreSynchronized -isnot [bool] -or $Initial.state.liveStoreSynchronized -ne $false -or
            $Initial.state.wireStatus -cne 'WirePending' -or $Initial.state.sourceGateStatus -cne 'SourceGatePending') { throw 'resourceInitialEvidenceMismatch' }
        Assert-GarInspectorRecord $Initial
    }
    if ($Initial.state.status -cne $profile.waiting -or $Initial.state.intent -cne $profile.intent) { throw 'initialResourceIntentRequired' }
    $read=Read-GaResourceIntent $Context.evidenceRoot $Scenario; $intent=$read.value
    if (-not (Test-GasHash $read.bytes $Initial.state.intentSha256) -or
        (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $Context.evidenceRoot 'before.json')).Hash -cne $Initial.state.beforeSha256 -or
        $intent.beforeSha256 -cne $Initial.state.beforeSha256 -or $intent.accountId -ne $Context.accountId -or
        [guid]$intent.domain -ne [guid]$Before.domain -or $intent.characterUid -ne $Before.character.uid -or
        $intent.expectedItemUid -ne $Uid -or $intent.expectedItemId -ne $Type -or
        $Initial.state.expectedItemUid -ne $Uid -or $Initial.state.expectedItemId -ne $Type) { throw 'resourceIntentIdentityMismatch' }
    foreach ($key in $profile.expectedKeys) {
        if ($intent[$key] -cne $Expected[$key] -or $Initial.state[$key] -cne $Expected[$key]) { throw 'resourceIntentIdentityMismatch' }
    }
    $after=Get-GaInspection $Context $AfterLeaf # Sole child on every resource Resume.
    $issues=@(Test-GaResourceTransition $Before $after $intent $Scenario)
    $state=New-GaResourceState $(if ($issues.Count) { 'ClientActionPendingOrStateMismatch' } else { $profile.observed }) $issues $Scenario
    Set-GarPins $state $intent $Scenario; $state.intentSha256=$Initial.state.intentSha256
    $state.afterSha256=(Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $Context.evidenceRoot $AfterLeaf)).Hash
    return $state
}
