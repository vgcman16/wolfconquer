Set-StrictMode -Version Latest

function Read-GasOwner([string]$Root) {
    $owner = (Read-GasJson $Root 'owner.json').value
    Assert-GasKeys $owner @('schemaVersion','evidenceId','scenario','accountId','configPath','configSha256','dataToolPath','dataToolSha256')
    if (-not (Test-GasUInt $owner.schemaVersion 1) -or $owner.evidenceId -cne [IO.Path]::GetFileName($Root) -or
        $owner.scenario -cnotin @(Get-GasScenarioIds) -or -not (Test-GasUInt $owner.accountId) -or
        $owner.configPath -isnot [string] -or -not [IO.Path]::IsPathFullyQualified($owner.configPath) -or
        $owner.dataToolPath -isnot [string] -or -not [IO.Path]::IsPathFullyQualified($owner.dataToolPath) -or
        $owner.configSha256 -isnot [string] -or $owner.dataToolSha256 -isnot [string] -or
        $owner.configSha256 -cnotmatch '^[0-9A-F]{64}$' -or $owner.dataToolSha256 -cnotmatch '^[0-9A-F]{64}$') {
        throw 'invalidEvidenceOwner'
    }
    if ($owner.scenario -ceq 'equip-unequip') { Assert-GasKeys $owner @('expectedItemUid','expectedItemId','expectedEquipmentPosition') }
    if ($owner.scenario -ceq 'equip-unequip' -and
        (-not (Test-GasUInt $owner.expectedItemUid) -or -not (Test-GasUInt $owner.expectedItemId) -or
         -not (Test-GasUInt $owner.expectedEquipmentPosition 65535))) { throw 'invalidPinnedItemIdentity' }
    if ($owner.scenario -ceq 'login-walk-save' -and $owner.Contains('expectedMap')) {
        Assert-GasKeys $owner @('expectedX','expectedY')
        if (-not (Test-GawInteger $owner.expectedMap 1 ([uint32]::MaxValue)) -or
            -not (Test-GawInteger $owner.expectedX 0 65535) -or -not (Test-GawInteger $owner.expectedY 0 65535)) { throw 'invalidWalkIntent' }
    }
    return $owner # Paths identify retained context only; never open tool/config or run them.
}

function Read-GasInspection([string]$Root, [string]$Leaf, $Owner, $Before = $null) {
    $evidenceRead = Read-GasJson $Root $Leaf
    Assert-GasKeys $evidenceRead.value @('success','report')
    $report = $evidenceRead.value.report
    Assert-GasKeys $report @('schemaVersion','accountId','domain','character','liveStoreSynchronized','items','mails','files','summariesTruncated')
    Assert-GasKeys $report.character @('uid')
    $domain = [guid]::Empty
    if ($evidenceRead.value.success -isnot [bool] -or $evidenceRead.value.success -ne $true -or -not (Test-GasUInt $report.schemaVersion 1) -or
        -not (Test-GasUInt $report.accountId) -or $report.accountId -ne $Owner.accountId -or -not (Test-GasUInt $report.character.uid) -or
        -not [guid]::TryParse([string]$report.domain, [ref]$domain) -or $domain -eq [guid]::Empty -or
        $report.liveStoreSynchronized -isnot [bool] -or $report.liveStoreSynchronized -ne $false -or
        $report.summariesTruncated -isnot [bool] -or $report.items -isnot [array] -or $report.mails -isnot [array] -or
        $report.files -isnot [array] -or $report.files.Count -ne 6 -or $report.items.Count -gt 200 -or $report.mails.Count -gt 200 -or
        @($report.files | Where-Object { $_.bytesAndMetadataUnchanged -isnot [bool] -or $_.bytesAndMetadataUnchanged -ne $true }).Count) { throw 'invalidInspectorIdentityOrStability' }
    $kinds = @('Configuration','Character','Items','Spells','Proficiencies','Mailbox')
    foreach ($kind in $kinds) {
        if (@($report.files | Where-Object { $_.kind -ceq $kind }).Count -ne 1) { throw 'invalidInspectorIdentityOrStability' }
    }
    if ($null -ne $Before -and ([guid]$Before.domain -ne $domain -or $Before.character.uid -ne $report.character.uid)) {
        throw 'inspectorIdentityMismatch'
    }
    return @{ report = $report; bytes = $evidenceRead.bytes }
}

function Read-GasReceipt([string]$Root, [string]$Leaf, $Owner) {
    $evidenceRead = Read-GasJson $Root $Leaf
    $receipt = $evidenceRead.value
    Assert-GasKeys $receipt @('schemaVersion','scenario')
    if (-not (Test-GasUInt $receipt.schemaVersion 1) -or $receipt.scenario -cne $Owner.scenario) { throw 'receiptIdentityMismatch' }
    if ($Leaf -cmatch '^failure-[0-9a-f]{32}\.json$') {
        Assert-GasKeys $receipt @('status','reason','errorType','visualAcceptance')
        if ($receipt.status -cne 'PrerequisiteMissingOrOutcomeUnproven' -or $receipt.visualAcceptance -cne 'LiveClientVisualPending') { throw 'invalidFailureReceipt' }
        return @{ evidenceStatus = 'Pending'; savedStateResult = $receipt.status; reason = 'runnerFailureRecorded';
            visualStatus = 'LiveClientVisualPending'; observedUtc = $null; ticks = $evidenceRead.ticks; receipt = $Leaf }
    }
    Assert-GasKeys $receipt @('accountId','before','after','state','steps','sourceChecks','visualAcceptance')
    if (-not (Test-GasUInt $receipt.accountId) -or $receipt.accountId -ne $Owner.accountId -or $receipt.before -cne 'before.json' -or $null -eq $evidenceRead.observed -or
        $receipt.steps -isnot [array] -or $receipt.steps.Count -gt 512 -or $receipt.sourceChecks -isnot [Collections.IDictionary]) { throw 'receiptIdentityOrSchemaMismatch' }
    Assert-GasKeys $receipt.state @('status')
    $allowed = @('GuidedActionsPending','GuidedAssertionsPending','PrerequisiteMissing','RequestPreparedNotSent',
        'OperationOutcomeUnproven','OperationRejected','RecoveryRequired','WaitingForClientAction','WaitingForEquip',
        'ClientActionPendingOrStateMismatch','ServerStateVerified','SavedEquipObserved','SavedEquipUnequipObserved',
        'WaitingForSavedWalk','SavedWalkObserved')
    $status = $receipt.state.status
    if ($status -cnotin $allowed) { throw 'unknownSavedStateResult' }
    if ($receipt.visualAcceptance -cne 'LiveClientVisualPending' -or
        ($receipt.state.Contains('liveClientVisualStatus') -and $receipt.state.liveClientVisualStatus -cne 'LiveClientVisualPending')) {
        throw 'unverifiedVisualPromotionInReceipt'
    }
    $before = Read-GasInspection $Root 'before.json' $Owner
    $after = $null
    if ($null -ne $receipt.after) {
        if ($receipt.after -isnot [string] -or $receipt.after -cnotmatch '^resume-[0-9a-f]{32}-after\.json$') { throw 'invalidAfterReference' }
        $after = Read-GasInspection $Root $receipt.after $Owner $before.report
    } elseif ($Leaf -cne 'receipt.json') { throw 'resumeAfterReferenceMissing' }
    if ($status -cin @('WaitingForSavedWalk','SavedWalkObserved') -and $Owner.scenario -cne 'login-walk-save') { throw 'savedStatusUnsupportedForScenario' }
    $saved = $status -cin @('ServerStateVerified','SavedEquipObserved','SavedEquipUnequipObserved','SavedWalkObserved')
    if ($saved -and ($null -eq $after -or $receipt.state.assertions -isnot [array] -or $receipt.state.assertions.Count -ne 0)) {
        throw 'savedObservationEvidenceIncomplete'
    }
    if ($saved) { Assert-GasSavedReferences $Root $Owner $receipt $before $after }
    $complete = $before.report.summariesTruncated -eq $false -and
        ($null -eq $after -or $after.report.summariesTruncated -eq $false)
    return @{ evidenceStatus = $(if ($saved -and $complete) { 'RecordedSavedObservation' } else { 'Pending' });
        savedStateResult = $status; reason = $(if (-not $complete) { 'inspectorSummaryIncomplete' } elseif ($saved) { 'recordedSavedBoundaryOnly' } else { 'scenarioWorkPending' });
        visualStatus = 'LiveClientVisualPending'; observedUtc = $evidenceRead.observed; ticks = $evidenceRead.ticks; receipt = $Leaf }
}
