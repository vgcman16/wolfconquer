Set-StrictMode -Version Latest

function Assert-GaMailCorrelation($Request, $Result, $Before) {
    if ([guid]$Request.operationId -eq [guid]::Empty -or [guid]$Request.operationId -ne [guid]$Result.operationId -or
        [guid]$Request.expectedDomain -ne [guid]$Before.domain -or $Request.accountId -ne $Before.accountId -or
        $Request.characterUid -ne $Before.character.uid -or $Before.summariesTruncated -ne $false -or
        $Result.status -notin @('CommittedNotificationCompleted', 'CommittedNotificationStopped') -or
        $null -eq $Result.itemUids -or $null -eq $Result.mailIds -or
        @($Request.items).Count -ne @($Result.itemUids).Count -or @($Request.items).Count -ne @($Result.mailIds).Count -or
        @($Result.itemUids).Count -lt 1 -or @($Result.itemUids).Count -gt 32) { throw 'Exact committed mail correlation unavailable.' }
    foreach ($key in @('itemUids', 'mailIds')) {
        $ids = @($Result[$key] | ForEach-Object { [uint32]$_ })
        if (@($ids | Where-Object { $_ -eq 0 }).Count -or @($ids | Select-Object -Unique).Count -ne $ids.Count) {
            throw 'Committed UID lists invalid.'
        }
    }
    if (@($Before.items | Where-Object { [uint32]$_.uid -in @($Result.itemUids) }).Count -or
        @($Before.mails | Where-Object { [uint32]$_.mailId -in @($Result.mailIds) }).Count) {
        throw 'Delivered identifiers were already present before dispatch.'
    }
}

function Start-GaMailbox([hashtable]$Context, $Before, [string]$Kit, [switch]$SendTestKit) {
    if ($Before.summariesTruncated -ne $false) { return @{ status = 'PrerequisiteMissing'; reason = 'completeBeforeSnapshotRequired' } }
    $requestPath = Join-Path $Context.evidenceRoot 'request.json'
    $domain = ([guid]$Before.domain).ToString('N')
    $create = Invoke-GaCommand $Context 'create-operator-test-kit' $Context.dotnet @($Context.tool,
        'create-operator-test-kit', $requestPath, $domain, [string]$Context.accountId,
        [string]$Before.character.uid, $Kit) -Json
    if (-not $create.completed -or $create.exitCode -ne 0 -or $create.stderrPresent -or $null -eq $create.value -or
        $create.value.requestCreated -ne $true -or $create.value.sent -ne $false) { throw 'Real kit request generation unavailable.' }
    $request = Read-GaJson $Context 'request.json'
    if ([guid]$request.operationId -ne [guid]$create.value.operationId -or [guid]$request.expectedDomain -ne [guid]$Before.domain -or
        $request.accountId -ne $Context.accountId -or $request.characterUid -ne $Before.character.uid) {
        throw 'Generated request identity mismatch.'
    }
    $state = @{ status = 'RequestPreparedNotSent'; operationId = [string]$request.operationId; kit = $Kit;
        requestSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $requestPath).Hash;
        deliveryResult = $null; liveClientVisualStatus = 'LiveClientVisualPending';
        actions = @('Dispatch requires -SendTestKit on the initial run. Resume never sends.',
            'Do not manually regenerate an operation after uncertain delivery.') }
    if (-not $SendTestKit) { return $state }
    $state.status = 'OperationOutcomeUnproven'
    $send = Invoke-GaCommand $Context 'send-operator-gear-mail' $Context.dotnet @($Context.tool,
        'send-operator-gear-mail', ('wolfconquer-operator-mail-' + $domain), $requestPath) -Json
    if (-not $send.completed -or $null -eq $send.value -or $send.stderrPresent) {
        $state.reason = 'dispatchReceiptUnavailableNeverResendAutomatically'
        return $state
    }
    $result = $send.value
    if ([guid]$result.operationId -ne [guid]$request.operationId) { throw 'Actual delivery operation mismatch.' }
    Save-GaJson $Context 'delivery-result.json' $result
    $state.deliveryResult = 'delivery-result.json'
    $state.actualStatus = $result.status
    if ($send.exitCode -eq 0 -and $result.status -in @('CommittedNotificationCompleted', 'CommittedNotificationStopped')) {
        Assert-GaMailCorrelation $request $result $Before
        $state.status = 'WaitingForClientAction'
        $state.actions = @('Open the client mailbox and claim only this operation''s delivered test items.',
            'Leave the client open; use -Resume with this same evidence root. Never resend/regenerate.',
            'Record actual visual acceptance separately through Root''s computer-use session.')
        try {
            $after = Get-GaInspection $Context 'after-delivery.json'
            $state.afterDeliverySnapshot = 'after-delivery.json'
            $state.afterDeliveryTruncated = $after.summariesTruncated
        }
        catch { $state.afterDeliverySnapshot = 'unavailableOrUnstable'; }
    }
    elseif ($send.exitCode -eq 2 -and $result.status -eq 'Rejected') { $state.status = 'OperationRejected' }
    elseif ($send.exitCode -eq 3 -and $result.status -eq 'PreviouslySubmittedOutcomeUnproven') { $state.status = 'OperationOutcomeUnproven' }
    elseif ($send.exitCode -eq 4 -and $result.status -eq 'RecoveryRequired') { $state.status = 'RecoveryRequired' }
    else { $state.reason = 'exitCodeAndTypedStatusDoNotAgree' }
    return $state
}

function Resume-GaMailbox([hashtable]$Context, $Initial, $Before, [string]$AfterLeaf) {
    if ($Initial.state.status -ne 'WaitingForClientAction' -or $Initial.state.deliveryResult -cne 'delivery-result.json') {
        return @{ status = 'PrerequisiteMissing'; reason = 'originalCommittedDeliveryReceiptRequiredNeverResend';
            liveClientVisualStatus = 'LiveClientVisualPending' }
    }
    $request = Read-GaJson $Context 'request.json'
    if ((Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $Context.evidenceRoot 'request.json')).Hash -cne $Initial.state.requestSha256) {
        throw 'Retained request changed.'
    }
    $result = Read-GaJson $Context 'delivery-result.json'
    Assert-GaMailCorrelation $request $result $Before
    $after = Get-GaInspection $Context $AfterLeaf
    if ($after.summariesTruncated -ne $false -or [guid]$after.domain -ne [guid]$Before.domain -or
        $after.character.uid -ne $Before.character.uid) {
        return @{ status = 'PrerequisiteMissing'; reason = 'completeMatchingAfterSnapshotRequired';
            liveClientVisualStatus = 'LiveClientVisualPending' }
    }
    $issues = [Collections.Generic.List[object]]::new()
    if ($after.character.gold -ne $Before.character.gold -or
        $after.character.conquerPoints -ne $Before.character.conquerPoints) {
        $issues.Add(@{ assertion = 'freeGrantAndClaimPreserveGoldAndConquerPoints'; status = 'notVerified' })
    }
    if ($after.physicalItemCount -ne $Before.physicalItemCount + @($result.itemUids).Count) {
        $issues.Add(@{ assertion = 'onlyDeliveredPhysicalRowsAdded'; status = 'notVerified' })
    }
    foreach ($original in @($Before.items)) {
        $retained = @($after.items | Where-Object { [uint32]$_.uid -eq [uint32]$original.uid })
        if ($retained.Count -ne 1) {
            $issues.Add(@{ itemUid = $original.uid; assertion = 'existingPhysicalItemRetainedOnce'; status = 'notVerified' })
            continue
        }
        foreach ($field in @('container','itemId','position','warehouseId','plus','stackSize','durability',
            'maximumDurability','socketOne','socketTwo','bound','locked')) {
            if ($retained[0][$field] -cne $original[$field]) {
                $issues.Add(@{ itemUid = $original.uid; assertion = 'existingItemPreserved'; field = $field; status = 'notVerified' })
            }
        }
    }
    for ($index = 0; $index -lt @($result.itemUids).Count; $index++) {
        $uid = [uint32]$result.itemUids[$index]
        $mailId = [uint32]$result.mailIds[$index]
        $physical = @($after.items | Where-Object { [uint32]$_.uid -eq $uid })
        if ($physical.Count -ne 1 -or $physical[0].container -cne 'inventory' -or
            $physical[0].itemId -ne $request.items[$index].itemId -or
            $physical[0].stackSize -ne $request.items[$index].stackSize) {
            $issues.Add(@{ itemUid = $uid; assertion = 'exactRequestedItemOnceInInventory'; status = 'notVerified' })
        }
        $mail = @($after.mails | Where-Object { [uint32]$_.mailId -eq $mailId })
        if ($mail.Count -gt 1 -or ($mail.Count -eq 1 -and $mail[0].attachedItemUid -ne 0) -or
            @($after.mails | Where-Object { [uint32]$_.attachedItemUid -eq $uid }).Count) {
            $issues.Add(@{ mailId = $mailId; assertion = 'claimedMailAttachmentClearedOrMailAbsent'; status = 'notVerified' })
        }
    }
    return @{ status = $(if ($issues.Count -eq 0) { 'ServerStateVerified' } else { 'ClientActionPendingOrStateMismatch' });
        operationId = [string]$request.operationId; assertions = @($issues.ToArray());
        verifiedItemCount = $(if ($issues.Count -eq 0) { @($result.itemUids).Count } else { 0 });
        liveClientVisualStatus = 'LiveClientVisualPending'; liveStoreSynchronized = $after.liveStoreSynchronized;
        verificationBound = 'Saved-file boundary inspection only. Live memory, restore ABA, rendering and client usability are not proven.' }
}
