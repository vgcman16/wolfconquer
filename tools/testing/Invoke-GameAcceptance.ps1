#requires -Version 7.4
[CmdletBinding()]
param(
    [switch]$List,
    [string]$Scenario,
    [string]$DataToolDll,
    [string]$OwnedAdmissionConfig,
    [uint32]$AccountId,
    [string]$EvidenceRoot,
    [switch]$Resume,
    [switch]$SendTestKit,
    [ValidateSet('basic-trojan', 'socket-materials')][string]$Kit = 'basic-trojan',
    [string]$DotNetPath,
    [switch]$RunSourceChecks,
    [string]$SourceRepositoryRoot,
    [string]$PowerShellPath
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'GameAcceptance.Common.ps1')
. (Join-Path $PSScriptRoot 'GameAcceptance.Mailbox.ps1')

function Get-GaScenarios {
    @(
        @{ id = 'login-walk-save'; mode = 'guided'; actions = @('Log into the pinned test client through the authorized input workflow.', 'Walk to a different known tile, save through the real lifecycle and inspect again.'); required = 'Correct model/map, movement wire, saved position and live/save ownership.' },
        @{ id = 'reconnect'; mode = 'guided'; actions = @('Record current character/items.', 'Disconnect and reconnect the same disposable account; compare retained state and old-owner retirement.'); required = 'Actual session retirement, no duplicate issuance, correct rehydration.' },
        @{ id = 'mailbox-gear'; mode = 'mailbox'; actions = @('Generate a retained test-kit request.', 'Explicitly dispatch, claim in the real client, then Resume without resending.'); required = 'Exact delivered UIDs once in inventory, requested item types/stacks, cleared mail references, separate client visual proof.' },
        @{ id = 'equip-unequip'; mode = 'guided'; actions = @('Record inventory/equipment and visible stats.', 'Equip and unequip a disposable compatible item; record wire, stats and saved containers.'); required = 'Exact UID relocation, capacity/eligibility rejection and derived-stat coherence.' },
        @{ id = 'upgrade-socket'; mode = 'guided'; actions = @('Record disposable gear and materials.', 'Use the actual forge/socket UI for valid and rejected operations; inspect resources and gear afterward.'); required = 'Catalog-backed costs/results, no rejected debit, socket/gem bytes and real durable transaction.' },
        @{ id = 'combat'; mode = 'guided'; actions = @('Record health/resources, weapon and learned skills.', 'Use controlled valid/rejected attacks against known targets; observe damage/status/XP and saves.'); required = 'Real target authority, formulas, ordering, resource admission, progression and client effects.' },
        @{ id = 'npc-shop-rewards'; mode = 'guided'; actions = @('Record balances, items and quest/reward prerequisites.', 'Use real NPC/shop/reward UI for success, rejection and duplicate submission.'); required = 'Actual catalog/script branch, price/capacity, one reward and durable item/currency closure.' },
        @{ id = 'trade-two-clients'; mode = 'guided'; actions = @('Use two independently logged disposable clients and snapshot both.', 'Trade exact items/currency; test cancellation and disconnect before final acceptance.'); required = 'Both owner identities, atomic swap, duplicate/disconnect safety, both clients and durable files.' },
        @{ id = 'persistence-restart'; mode = 'guided'; actions = @('Record exact saved test state and recovery readiness.', 'Root performs an explicitly owned graceful lifecycle transition, reconnects and compares state.'); required = 'Actual worker/lease drain, correct new owner, recovery, no duplicate/lost items or currency.' },
        @{ id = 'events-social'; mode = 'guided'; actions = @('Select one explicitly implemented social/event lifecycle and document prerequisites.', 'Exercise join/actions/leave/end and rejection paths with required real participants.'); required = 'Specific implemented lifecycle and persistence evidence; unimplemented systems remain pending.' },
        @{ id = 'updater'; mode = 'guided'; actions = @('Use existing tools/dev status and updater test tooling.', 'Test a real isolated feed update, interrupted/invalid update and preservation of local edits.'); required = 'Actual download/hash/apply/restart behavior and real distribution links; service health alone is insufficient.' }
    )
}

$catalog = @(Get-GaScenarios)
if ($List) {
    if ($Resume -or $SendTestKit -or $RunSourceChecks) { throw 'List does not execute scenarios or source checks.' }
    @{ schemaVersion = 1; scenarios = $catalog; defaultState = 'Pending';
        bound = 'Catalog only; no host, client, source-check or saved-state probe executed.' } | ConvertTo-Json -Depth 8
    exit 0
}

$context = $null
try {
    if (-not $IsWindows -or $AccountId -eq 0 -or -not $Scenario -or
        ($Resume -and ($SendTestKit -or $RunSourceChecks)) -or ($SendTestKit -and $Scenario -cne 'mailbox-gear')) {
        throw 'Explicit Windows scenario arguments required; Resume never dispatches or runs source checks.'
    }
    $selected = @($catalog | Where-Object { $_.id -ceq $Scenario })
    if ($selected.Count -ne 1) { throw 'Known scenario ID required.' }
    $tool = Get-GaOrdinaryPath $DataToolDll
    if ([IO.Path]::GetFileName($tool) -cne 'WolfConquer.DataTool.dll') { throw 'Actual DataTool DLL required.' }
    foreach ($leaf in @('WolfConquer.Core.dll', 'WolfConquer.DataTool.deps.json', 'WolfConquer.DataTool.runtimeconfig.json')) {
        $null = Get-GaOrdinaryPath (Join-Path ([IO.Path]::GetDirectoryName($tool)) $leaf)
    }
    $config = Get-GaOrdinaryPath $OwnedAdmissionConfig
    if (-not $DotNetPath) { $DotNetPath = (Get-Command dotnet.exe -CommandType Application -ErrorAction Stop).Source }
    $dotnet = Get-GaOrdinaryPath $DotNetPath
    if ([IO.Path]::GetFileName($dotnet) -ine 'dotnet.exe') { throw 'Actual dotnet.exe required.' }
    $root = Resolve-GaEvidenceRoot $EvidenceRoot -Resume:$Resume
    $context = @{ evidenceRoot = $root; tool = $tool; config = $config; dotnet = $dotnet; accountId = $AccountId;
        steps = [Collections.Generic.List[object]]::new() }
    $identity = @{ schemaVersion = 1; evidenceId = [IO.Path]::GetFileName($root); scenario = $Scenario;
        accountId = $AccountId; configPath = $config; configSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $config).Hash;
        dataToolPath = $tool; dataToolSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $tool).Hash }
    $sourceChecks = @{ status = 'NotRequested'; bound = 'No full suite automatically runs for acceptance.' }
    if ($Resume) {
        $retained = Read-GaJson $context 'owner.json'
        foreach ($key in @('schemaVersion', 'evidenceId', 'scenario', 'accountId', 'configPath', 'configSha256', 'dataToolPath', 'dataToolSha256')) {
            if ($retained[$key] -cne $identity[$key]) { throw 'Retained evidence owner/configuration/tool identity mismatch.' }
        }
    }
    else {
        if ($RunSourceChecks) {
            if (-not $SourceRepositoryRoot -or -not $PowerShellPath) { throw 'Explicit actual CI checkout and PowerShell path required.' }
            $sourceChecks = Invoke-GaSourceChecks $context $SourceRepositoryRoot $PowerShellPath
            $identity.dataToolSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $tool).Hash
        }
        Save-GaJson $context 'owner.json' $identity
    }
    $afterLeaf = 'resume-' + [guid]::NewGuid().ToString('N') + '-after.json'
    if ($Resume) {
        $initial = Read-GaJson $context 'receipt.json'
        $before = (Read-GaJson $context 'before.json').report
        if ($Scenario -ceq 'mailbox-gear') { $state = Resume-GaMailbox $context $initial $before $afterLeaf }
        else {
            $null = Get-GaInspection $context $afterLeaf
            $state = @{ status = 'GuidedAssertionsPending'; reason = 'NoExecutableAssertionForThisScenarioYet';
                guidedActions = $selected[0].actions; requiredEvidence = $selected[0].required;
                liveClientVisualStatus = 'LiveClientVisualPending' }
        }
    }
    else {
        $before = Get-GaInspection $context 'before.json'
        if ($SendTestKit -and $RunSourceChecks -and $sourceChecks.status -cne 'SourceChecksVerified') {
            $state = @{ status = 'PrerequisiteMissing'; reason = 'requestedSourceGateNotVerifiedNoGrantDispatched';
                liveClientVisualStatus = 'LiveClientVisualPending' }
        }
        elseif ($Scenario -ceq 'mailbox-gear') { $state = Start-GaMailbox $context $before $Kit -SendTestKit:$SendTestKit }
        else {
            $state = @{ status = 'GuidedActionsPending'; reason = 'NoExecutableAssertionForThisScenarioYet';
                guidedActions = $selected[0].actions; requiredEvidence = $selected[0].required;
                liveClientVisualStatus = 'LiveClientVisualPending'; snapshotTruncated = $before.summariesTruncated }
        }
    }
    $receipt = @{ schemaVersion = 1; scenario = $Scenario; accountId = $AccountId; observedUtc = [DateTime]::UtcNow.ToString('O');
        state = $state; steps = @($context.steps.ToArray()); sourceChecks = $sourceChecks;
        before = 'before.json'; after = $(if ($Resume) { $afterLeaf } else { $null });
        visualAcceptance = 'LiveClientVisualPending';
        bound = 'Actual saved-file inspections and typed command outcomes only; not full parity or live-store/rendering proof.' }
    $receiptLeaf = $(if ($Resume) { 'resume-' + [guid]::NewGuid().ToString('N') + '-receipt.json' } else { 'receipt.json' })
    Save-GaJson $context $receiptLeaf $receipt
    @{ scenario = $Scenario; status = $state.status; state = $state; evidenceRoot = $root;
        receipt = $receiptLeaf; visualAcceptance = 'LiveClientVisualPending' } | ConvertTo-Json -Depth 12
    if ($state.status -ceq 'ServerStateVerified') { exit 0 }
    if ($state.status -in @('OperationOutcomeUnproven', 'RecoveryRequired', 'OperationRejected')) { exit 1 }
    exit 2 # Guided, client-action and prerequisite states are explicitly pending.
}
catch {
    $failure = @{ schemaVersion = 1; scenario = $Scenario; status = 'PrerequisiteMissingOrOutcomeUnproven';
        reason = 'Acceptance stopped; retained evidence must be inspected. No automatic retry or cleanup.';
        errorType = $_.Exception.GetType().Name; visualAcceptance = 'LiveClientVisualPending' }
    if ($null -ne $context) {
        $failure.steps = @($context.steps.ToArray())
        try { Save-GaJson $context ('failure-' + [guid]::NewGuid().ToString('N') + '.json') $failure }
        catch { $failure.evidenceWrite = 'unavailableOriginalEvidenceRetained' }
    }
    $failure | ConvertTo-Json -Depth 12
    exit 1
}
