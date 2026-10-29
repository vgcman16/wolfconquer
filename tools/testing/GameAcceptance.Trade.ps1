Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'GameAcceptance.Trade.Compare.ps1')

function Save-GatBytes($Context, [string]$Leaf, [byte[]]$Bytes) {
    if ($Leaf -cne 'trade-plan.json' -or $Bytes.Length -gt 16KB) { throw 'invalidTradePlan' }
    $null=Get-GaOrdinaryPath $Context.evidenceRoot -Directory
    $file=[IO.File]::Open((Join-Path $Context.evidenceRoot $Leaf), [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $file.Write($Bytes); $file.Flush($true) } finally { $file.Dispose() }
}

function Set-GaTradeIdentity($Context, $Identity, [uint32]$OtherAccount, [string]$PlanPath, [switch]$Resume) {
    if (-not [IO.Path]::IsPathFullyQualified($PlanPath) -or $PlanPath.StartsWith('\\', [StringComparison]::Ordinal)) { throw 'absoluteLocalTradePlanRequired' }
    $Identity.counterpartyAccountId=$OtherAccount
    $Identity.tradePlanPath=[IO.Path]::GetFullPath($PlanPath)
    if (-not $Resume) {
        $path=Get-GaOrdinaryPath $PlanPath
        $info=Get-Item -LiteralPath $path
        $stamp=@($info.Length,$info.CreationTimeUtc.Ticks,$info.LastWriteTimeUtc.Ticks)
        if ($stamp[0] -gt 16KB) { throw 'invalidTradePlan' }
        $stream=[IO.File]::Open($path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
        try {
            if ($stream.Length -gt 16KB) { throw 'invalidTradePlan' }
            $bytes=[byte[]]::new([int]$stream.Length); $stream.ReadExactly($bytes)
            if ($stream.ReadByte() -ne -1) { throw 'tradePlanChangedDuringRead' }
        } finally { $stream.Dispose() }
        $after=Get-Item -LiteralPath (Get-GaOrdinaryPath $path)
        if ($stamp[0] -ne $after.Length -or $stamp[1] -ne $after.CreationTimeUtc.Ticks -or
            $stamp[2] -ne $after.LastWriteTimeUtc.Ticks) { throw 'tradePlanChangedDuringRead' }
        $null=Read-GaTradePlan $bytes
        Save-GatBytes $Context 'trade-plan.json' $bytes
    }
    $plan=Read-GasJson $Context.evidenceRoot 'trade-plan.json'
    $value=Read-GaTradePlan $plan.bytes
    if ($value.participants[0].accountId -ne $Context.accountId -or $value.participants[1].accountId -ne $OtherAccount) { throw 'tradePlanParticipantMismatch' }
    $Identity.tradePlanSha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($plan.bytes))
    $Context.counterpartyAccountId=$OtherAccount
}

function Get-GatInspection($Context, [uint32]$Account, [string]$Leaf) {
    $other=@{}; foreach ($key in $Context.Keys) { $other[$key]=$Context[$key] }
    $other.accountId=$Account # Shared steps list records both genuine child invocations.
    return Get-GaInspection $other $Leaf
}

function Assert-GatTools($Context, $Owner) {
    if ((Get-FileHash -Algorithm SHA256 -LiteralPath (Get-GaOrdinaryPath $Context.tool)).Hash -cne $Owner.dataToolSha256 -or
        (Get-FileHash -Algorithm SHA256 -LiteralPath (Get-GaOrdinaryPath $Context.config)).Hash -cne $Owner.configSha256) { throw 'tradeToolOrConfigurationChanged' }
}

function Read-GatInspection([string]$Root, [string]$Leaf, [uint32]$Account, [string]$ConfigHash) {
    $read=Read-GasJson $Root $Leaf
    Assert-GasKeys $read.value @('success','report')
    if ($read.value.success -isnot [bool] -or $read.value.success -ne $true -or
        $read.value.report.accountId -ne $Account -or @(Test-GaHealthReport $read.value.report).Count) { throw 'tradeInspectionIncomplete' }
    $config=@($read.value.report.files | Where-Object { $_.kind -ceq 'Configuration' })
    if ($config.Count -ne 1 -or $config[0].present -ne $true -or $config[0].contentSha256 -cne $ConfigHash) { throw 'tradeConfigurationMismatch' }
    return @{ report=$read.value.report; bytes=$read.bytes }
}

function Assert-GatOwner($Owner) {
    Assert-GasKeys $Owner @('schemaVersion','evidenceId','scenario','accountId','configSha256','dataToolSha256','counterpartyAccountId','tradePlanPath','tradePlanSha256')
    if (-not (Test-GasUInt $Owner.schemaVersion 1) -or -not (Test-GasUInt $Owner.accountId) -or
        $Owner.evidenceId -isnot [string] -or $Owner.evidenceId -cnotmatch '^wolf-game-acceptance-[0-9a-f]{32}$' -or
        $Owner.configSha256 -isnot [string] -or $Owner.configSha256 -cnotmatch '^[0-9A-F]{64}$' -or
        $Owner.dataToolSha256 -isnot [string] -or $Owner.dataToolSha256 -cnotmatch '^[0-9A-F]{64}$' -or
        $Owner.scenario -cne 'trade-two-clients' -or -not (Test-GasUInt $Owner.counterpartyAccountId) -or
        $Owner.accountId -eq $Owner.counterpartyAccountId -or $Owner.tradePlanPath -isnot [string] -or
        -not [IO.Path]::IsPathFullyQualified($Owner.tradePlanPath) -or
        $Owner.tradePlanSha256 -isnot [string] -or $Owner.tradePlanSha256 -cnotmatch '^[0-9A-F]{64}$') { throw 'invalidTradeOwner' }
}

function Assert-GatPlanIntent($Plan, $Intent) {
    if ($Plan.participants.Count -ne 2 -or $Plan.items.Count -ne $Intent.items.Count) { throw 'tradePlanIntentMismatch' }
    for ($i=0; $i -lt 2; $i++) {
        foreach ($key in @('accountId','goldDelta','conquerPointsDelta')) {
            if ($Plan.participants[$i][$key] -ne $Intent.participants[$i][$key]) { throw 'tradePlanIntentMismatch' }
        }
    }
    for ($i=0; $i -lt $Plan.items.Count; $i++) {
        foreach ($key in @('uid','itemId','stackSize','fromAccountId','toAccountId')) {
            if ($Plan.items[$i][$key] -ne $Intent.items[$i][$key]) { throw 'tradePlanIntentMismatch' }
        }
    }
}

function Read-GatRetained([string]$Root, $Owner, $Initial) {
    Assert-GatOwner $Owner
    Assert-GasKeys $Initial @('schemaVersion','scenario','accountId','before','after','state','visualAcceptance')
    if (-not (Test-GasUInt $Initial.schemaVersion 1) -or -not (Test-GasUInt $Initial.accountId) -or
        $Initial.scenario -cne 'trade-two-clients' -or $Initial.accountId -ne $Owner.accountId -or
        $Initial.before -cne 'before.json' -or $null -ne $Initial.after -or $Initial.visualAcceptance -cne 'LiveClientVisualPending' -or
        $Initial.state.status -cne 'WaitingForSavedTrade') { throw 'tradeInitialReceiptMismatch' }
    Assert-GatState $Initial.state
    $plan=Read-GasJson $Root 'trade-plan.json'
    if (-not (Test-GasHash $plan.bytes $Owner.tradePlanSha256)) { throw 'tradePlanChanged' }
    $parsed=Read-GaTradePlan $plan.bytes
    $raw=Read-GasJson $Root 'trade-intent.json'
    $intent=Read-GaTradeIntent $raw.bytes $Initial.state.intentSha256 $Owner.evidenceId
    Assert-GatPlanIntent $parsed $intent
    $a=Read-GatInspection $Root 'before.json' $Owner.accountId $Owner.configSha256
    $b=Read-GatInspection $Root 'before-counterparty.json' $Owner.counterpartyAccountId $Owner.configSha256
    for ($i=0; $i -lt 2; $i++) {
        $record=@($a,$b)[$i]
        if ($intent.participants[$i].accountId -ne @($Owner.accountId,$Owner.counterpartyAccountId)[$i] -or
            $intent.beforeSha256[$i] -cne $Initial.state.beforeSha256[$i] -or
            -not (Test-GasHash $record.bytes $intent.beforeSha256[$i])) { throw 'tradeBeforeEvidenceChanged' }
    }
    if (@(Test-GaTradeSelection $a.report $b.report $intent).Count) { throw 'tradeSelectionInvalid' }
    return @{ intent=$intent; a=$a; b=$b }
}

function Assert-GatState($State) {
    Assert-GasKeys $State @('intent','intentSha256','beforeSha256','beforeCounterparty','liveClientVisualStatus','liveStoreSynchronized','wireStatus','sourceGateStatus','atomicityStatus')
    if ($State.intent -cne 'trade-intent.json' -or $State.beforeCounterparty -cne 'before-counterparty.json' -or
        $State.beforeSha256 -isnot [array] -or $State.beforeSha256.Count -ne 2 -or
        $State.liveStoreSynchronized -isnot [bool] -or $State.liveStoreSynchronized -ne $false -or
        $State.liveClientVisualStatus -cne 'LiveClientVisualPending' -or
        $State.wireStatus -cne 'WirePending' -or $State.sourceGateStatus -cne 'SourceGatePending' -or
        $State.atomicityStatus -cne 'AtomicityPending') { throw 'tradeRetainedFlagsInvalid' }
}

function Start-GaTrade($Context, $Owner) {
    Assert-GatTools $Context $Owner
    $null=Get-GatInspection $Context $Owner.counterpartyAccountId 'before-counterparty.json'
    Assert-GatTools $Context $Owner
    $a=Read-GatInspection $Context.evidenceRoot 'before.json' $Owner.accountId $Owner.configSha256
    $b=Read-GatInspection $Context.evidenceRoot 'before-counterparty.json' $Owner.counterpartyAccountId $Owner.configSha256
    $planRead=Read-GasJson $Context.evidenceRoot 'trade-plan.json'
    if (-not (Test-GasHash $planRead.bytes $Owner.tradePlanSha256)) { throw 'tradePlanChanged' }
    $plan=Read-GaTradePlan $planRead.bytes
    $participants=@(); $hashes=@()
    for ($i=0; $i -lt 2; $i++) {
        $record=@($a,$b)[$i]
        $participants+=@{ accountId=$plan.participants[$i].accountId; characterUid=$record.report.character.uid;
            goldDelta=$plan.participants[$i].goldDelta; conquerPointsDelta=$plan.participants[$i].conquerPointsDelta }
        $hashes += [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($record.bytes))
    }
    $value=@{ schemaVersion=1; evidenceId=$Owner.evidenceId; domain=([guid]$a.report.domain).ToString('N');
        participants=$participants; items=@($plan.items); beforeSha256=$hashes }
    Save-GaJson $Context 'trade-intent.json' $value
    $raw=Read-GasJson $Context.evidenceRoot 'trade-intent.json'
    $hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($raw.bytes))
    $intent=Read-GaTradeIntent $raw.bytes $hash $Owner.evidenceId
    if (@(Test-GaTradeSelection $a.report $b.report $intent).Count) { throw 'tradeSelectionInvalid' }
    $state=New-GaTradeObservation @()
    $state.status='WaitingForSavedTrade'; $state.intent='trade-intent.json'; $state.intentSha256=$hash
    $state.beforeSha256=$hashes; $state.beforeCounterparty='before-counterparty.json'
    return $state
}

function Resume-GaTrade($Context, $Owner, [string]$AfterLeaf) {
    $initial=(Read-GasJson $Context.evidenceRoot 'receipt.json').value
    $retained=Read-GatRetained $Context.evidenceRoot $Owner $initial
    $otherLeaf=$AfterLeaf.Replace('-after.json','-counterparty-after.json')
    Assert-GatTools $Context $Owner
    $null=Get-GatInspection $Context $Owner.accountId $AfterLeaf
    Assert-GatTools $Context $Owner
    $null=Get-GatInspection $Context $Owner.counterpartyAccountId $otherLeaf
    Assert-GatTools $Context $Owner
    $a=Read-GatInspection $Context.evidenceRoot $AfterLeaf $Owner.accountId $Owner.configSha256
    $b=Read-GatInspection $Context.evidenceRoot $otherLeaf $Owner.counterpartyAccountId $Owner.configSha256
    $state=New-GaTradeObservation @(Test-GaTradeTransition $retained.a.report $retained.b.report $a.report $b.report $retained.intent)
    foreach ($key in @('intent','intentSha256','beforeSha256','beforeCounterparty')) { $state[$key]=$initial.state[$key] }
    $state.afterCounterparty=$otherLeaf
    $state.afterSha256=@($a,$b) | ForEach-Object { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($_.bytes)) }
    return $state
}
