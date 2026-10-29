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

. (Join-Path $PSScriptRoot 'GameAcceptance.Resources.Assertions.ps1')

function Test-GahSelection($Before, $Intent) { return Test-GarSelection $Before $Intent 'consume-health' }
function Test-GaHealthTransition($Before, $After, $Intent) { return Test-GaResourceTransition $Before $After $Intent 'consume-health' }
