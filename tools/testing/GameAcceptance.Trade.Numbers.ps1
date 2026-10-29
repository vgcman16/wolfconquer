Set-StrictMode -Version Latest

function Test-GatInteger($Value, [decimal]$Minimum, [decimal]$Maximum) {
    # ConvertFrom-Json preserves integer tokens as integral CLR values. A
    # floating/exponent token may already have rounded before any cast/check.
    if ($Value -isnot [sbyte] -and $Value -isnot [byte] -and $Value -isnot [int16] -and
        $Value -isnot [uint16] -and $Value -isnot [int32] -and $Value -isnot [uint32] -and
        $Value -isnot [int64] -and $Value -isnot [uint64] -and $Value -isnot [System.Numerics.BigInteger]) { return $false }
    try { return [decimal]$Value -ge $Minimum -and [decimal]$Value -le $Maximum }
    catch { return $false }
}

function Test-GatReportIntegers($Report) {
    $issues=@()
    foreach ($field in @('schemaVersion','accountId','inventoryCount','equipmentCount','warehouseCount','physicalItemCount','spellCount','proficiencyCount','activeMailCount')) {
        if (-not (Test-GatInteger $Report[$field] 0 ([long]::MaxValue))) { $issues += 'integerJsonRequired:' + $field }
    }
    foreach ($field in @('uid','map','x','y','body','class','level','gold','conquerPoints','hitpoints','minMana','maxMana')) {
        if (-not (Test-GatInteger $Report.character[$field] 0 ([long]::MaxValue))) { $issues += 'integerJsonRequired:character.' + $field }
    }
    foreach ($row in @($Report.items)) {
        foreach ($field in @('uid','itemId','position','warehouseId','plus','stackSize','durability','maximumDurability','socketOne','socketTwo','bound','locked')) {
            if (-not (Test-GatInteger $row[$field] 0 ([uint32]::MaxValue))) { $issues += 'integerJsonRequired:item.' + $field }
        }
    }
    foreach ($row in @($Report.mails)) {
        foreach ($field in @('mailId','attachedItemUid','conquerPoints','packageFlag','gold')) {
            if (-not (Test-GatInteger $row[$field] 0 ([ulong]::MaxValue))) { $issues += 'integerJsonRequired:mail.' + $field }
        }
    }
    foreach ($file in @($Report.files)) {
        if (-not (Test-GatInteger $file.length 0 ([long]::MaxValue))) { $issues += 'integerJsonRequired:file.length' }
    }
    return $issues
}
