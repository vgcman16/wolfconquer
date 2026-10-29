Set-StrictMode -Version Latest

function New-GasPendingRow([string]$Scenario, $Account) {
    return [ordered]@{ scenario = $Scenario; accountId = $Account; counterpartyAccountId = $null; evidenceId = $null; receipt = $null;
        receiptObservedUtc = $null; savedStateResult = $null; evidenceStatus = 'Pending'; reason = 'noMatchingEvidence';
        visualStatus = 'LiveClientVisualPending'; liveGameplayProven = $false }
}

function Get-GasReason($ErrorRecord) {
    $allowed = @('jsonDepthExceeded','duplicateJsonProperty','invalidEvidenceLeaf','evidenceFileTooLarge',
        'evidenceChangedDuringRead','jsonObjectRequired','invalidReceiptTimestamp','invalidEvidenceOwner',
        'invalidPinnedItemIdentity','invalidInspectorIdentityOrStability','inspectorIdentityMismatch',
        'receiptIdentityMismatch','invalidFailureReceipt','receiptIdentityOrSchemaMismatch','unknownSavedStateResult',
        'unverifiedVisualPromotionInReceipt','invalidAfterReference','resumeAfterReferenceMissing',
        'savedObservationEvidenceIncomplete','initialReceiptIdentityMismatch','equipRetainedEvidenceMismatch',
        'equipPinnedIdentityMismatch','mailRetainedEvidenceMismatch','mailRequestChanged','mailRecordedOutcomeMismatch',
        'savedStatusUnsupportedForScenario','requiredSchemaFieldMissing','invalidEvidenceFolderName',
        'evidenceDirectoryEntryLimit','receiptCountLimit')
    if ($ErrorRecord.Exception.Message -cin $allowed) { return $ErrorRecord.Exception.Message }
    return 'evidenceUnreadableOrSchemaInvalid' # Never echo arbitrary content, paths or exception data.
}

function Get-GasEvidence([string]$Root, [int]$MaximumReceipts) {
    $owner = Read-GasOwner $Root
    $files = [Collections.Generic.List[object]]::new()
    $scanned = 0
    foreach ($path in [IO.Directory]::EnumerateFileSystemEntries($Root)) {
        $scanned++; if ($scanned -gt 1024) { throw 'evidenceDirectoryEntryLimit' }
        $leaf = [IO.Path]::GetFileName($path)
        if ($leaf -cne 'receipt.json' -and $leaf -cnotmatch '^resume-[0-9a-f]{32}-receipt\.json$' -and
            $leaf -cnotmatch '^failure-[0-9a-f]{32}\.json$') { continue }
        if ($files.Count -ge $MaximumReceipts) { throw 'receiptCountLimit' }
        try {
            $actual = Get-GaOrdinaryPath $path
            $ticks = (Get-Item -LiteralPath $actual -Force).LastWriteTimeUtc.Ticks
            $files.Add(@{ leaf = $leaf; ticks = $ticks; ordinary = $true })
        } catch { $files.Add(@{ leaf = $leaf; ticks = [long]::MaxValue; ordinary = $false }) }
    }
    $base = @{ scenario = $owner.scenario; accountId = [uint32]$owner.accountId; evidenceId = $owner.evidenceId;
        receipt = $null; receiptObservedUtc = $null; savedStateResult = $null; evidenceStatus = 'Pending';
        reason = 'receiptMissing'; visualStatus = 'LiveClientVisualPending'; liveGameplayProven = $false; ticks = 0L }
    if ($owner.Contains('counterpartyAccountId')) { $base.counterpartyAccountId=[uint32]$owner.counterpartyAccountId }
    if ($files.Count -eq 0) { $base.ticks = (Get-Item -LiteralPath $Root -Force).LastWriteTimeUtc.Ticks; return $base }
    $ordered = @($files | Sort-Object -Property @{ Expression = 'ticks'; Descending = $true })
    $latest = $ordered[0]; $base.ticks = $latest.ticks; $base.receipt = $latest.leaf
    if ($ordered.Count -gt 1 -and $ordered[1].ticks -eq $latest.ticks) {
        $base.evidenceStatus = 'Invalid'; $base.reason = 'latestReceiptAmbiguous'; return $base
    }
    try {
        if (-not $latest.ordinary) { throw 'invalidEvidenceLeaf' }
        $record = Read-GasReceipt $Root $latest.leaf $owner
        if ($record.ticks -ne $latest.ticks) { throw 'evidenceChangedDuringRead' }
        foreach ($key in @('receipt','savedStateResult','evidenceStatus','reason','visualStatus')) { $base[$key] = $record[$key] }
        $base.receiptObservedUtc = $record.observedUtc
    } catch { $base.evidenceStatus = 'Invalid'; $base.reason = Get-GasReason $_ }
    return $base
}

function Get-GasSummary([string]$Parent, [uint32]$Account, [int]$MaximumFolders, [int]$MaximumReceipts) {
    $root = Get-GaOrdinaryPath $Parent -Directory
    $records = [Collections.Generic.List[object]]::new()
    $issues = [Collections.Generic.List[object]]::new()
    $scanned = 0; $folders = 0
    foreach ($path in [IO.Directory]::EnumerateFileSystemEntries($root)) {
        $scanned++; if ($scanned -gt 4096) { throw 'evidenceParentEntryLimit' }
        $leaf = [IO.Path]::GetFileName($path)
        if (-not $leaf.StartsWith('wolf-game-acceptance-', [StringComparison]::Ordinal)) { continue }
        $folders++; if ($folders -gt $MaximumFolders) { throw 'evidenceFolderCountLimit' }
        try {
            if ($leaf -cnotmatch '^wolf-game-acceptance-[0-9a-f]{32}$' -or
                [guid]::ParseExact($leaf.Substring(21), 'N') -eq [guid]::Empty) { throw 'invalidEvidenceFolderName' }
            $folder = Get-GaOrdinaryPath $path -Directory
            $record = Get-GasEvidence $folder $MaximumReceipts
            if ($Account -ne 0 -and $record.accountId -ne $Account -and
                (-not $record.ContainsKey('counterpartyAccountId') -or $record.counterpartyAccountId -ne $Account)) { continue }
            $records.Add($record)
        } catch { $issues.Add(@{ evidenceId = $(if ($leaf -cmatch '^wolf-game-acceptance-[0-9a-f]{32}$') { $leaf } else { $null }); reason = Get-GasReason $_ }) }
    }
    $rows = [Collections.Generic.List[object]]::new()
    foreach ($scenario in Get-GasScenarioIds) {
        $matching = @($records | Where-Object { $_.scenario -ceq $scenario })
        if ($matching.Count -eq 0) { $rows.Add((New-GasPendingRow $scenario $(if ($Account) { $Account } else { $null }))); continue }
        foreach ($group in @($matching | Group-Object -Property { [string]$_.accountId + ':' +
            $(if ($_.ContainsKey('counterpartyAccountId')) { [string]$_.counterpartyAccountId } else { '' }) })) {
            $ordered = @($group.Group | Sort-Object -Property @{ Expression = 'ticks'; Descending = $true })
            $selected = $ordered[0]
            if ($ordered.Count -gt 1 -and $ordered[1].ticks -eq $selected.ticks) {
                $selected.evidenceStatus = 'Invalid'; $selected.reason = 'latestEvidenceAmbiguous'
            }
            $row = New-GasPendingRow $scenario $selected.accountId
            foreach ($key in @($row.Keys)) { if ($selected.ContainsKey($key)) { $row[$key] = $selected[$key] } }
            $rows.Add($row)
        }
    }
    return @{ schemaVersion = 1; readOnly = $true; sourceGateAccepted = $false; liveGameplayProven = $false;
        status = $(if ($issues.Count -or @($rows | Where-Object { $_.evidenceStatus -ceq 'Invalid' }).Count) { 'InvalidEvidence' } else { 'EvidenceSummary' });
        rows = @($rows.ToArray()); diagnostics = @($issues.ToArray());
        selectionBound = 'Latest local receipt-file mtime per scenario/account or ordered trade pair; editable metadata, ties invalid; not authenticated event chronology.';
        trustBound = 'Recorded saved-schema/identity references; saved walk/resource/trade consistency is rechecked from retained JSON only. No opaque selected-item/Character, source checks, inspector, live Store, client, process or atomicity proof. Local JSON is editable, not an attestation.' }
}
