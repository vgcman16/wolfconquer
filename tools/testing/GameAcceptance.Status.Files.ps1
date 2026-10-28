Set-StrictMode -Version Latest

function Get-GasScenarioIds {
    @('login-walk-save','reconnect','mailbox-gear','equip-unequip','upgrade-socket','combat',
      'npc-shop-rewards','trade-two-clients','persistence-restart','events-social','updater')
}

function Assert-GasJsonKeys([System.Text.Json.JsonElement]$Element, [int]$Depth = 0) {
    if ($Depth -gt 24) { throw 'jsonDepthExceeded' }
    if ($Element.ValueKind -eq [System.Text.Json.JsonValueKind]::Object) {
        $keys = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($property in $Element.EnumerateObject()) {
            if (-not $keys.Add($property.Name)) { throw 'duplicateJsonProperty' }
            Assert-GasJsonKeys $property.Value ($Depth + 1)
        }
    } elseif ($Element.ValueKind -eq [System.Text.Json.JsonValueKind]::Array) {
        foreach ($entry in $Element.EnumerateArray()) { Assert-GasJsonKeys $entry ($Depth + 1) }
    }
}

function Read-GasJson([string]$Root, [string]$Leaf) {
    if ($Leaf -cnotmatch '^[a-z0-9][a-z0-9.-]*\.json$') { throw 'invalidEvidenceLeaf' }
    $path = Get-GaOrdinaryPath (Join-Path $Root $Leaf)
    $before = Get-Item -LiteralPath $path -Force
    if ($before.Length -gt 2MB) { throw 'evidenceFileTooLarge' }
    $stamp = @($before.Length, $before.CreationTimeUtc.Ticks, $before.LastWriteTimeUtc.Ticks)
    $stream = [IO.FileStream]::new($path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try {
        if ($stream.Length -gt 2MB) { throw 'evidenceFileTooLarge' }
        $bytes = [byte[]]::new([int]$stream.Length)
        $stream.ReadExactly($bytes, 0, $bytes.Length)
        if ($stream.ReadByte() -ne -1 -or $stream.Length -ne $bytes.Length) { throw 'evidenceChangedDuringRead' }
    } finally { $stream.Dispose() }
    $after = Get-Item -LiteralPath (Get-GaOrdinaryPath $path) -Force
    if ($stamp[0] -ne $after.Length -or $stamp[1] -ne $after.CreationTimeUtc.Ticks -or
        $stamp[2] -ne $after.LastWriteTimeUtc.Ticks) { throw 'evidenceChangedDuringRead' }
    $text = [Text.UTF8Encoding]::new($false, $true).GetString($bytes)
    $document = [System.Text.Json.JsonDocument]::Parse($text)
    try {
        Assert-GasJsonKeys $document.RootElement
        if ($document.RootElement.ValueKind -ne [System.Text.Json.JsonValueKind]::Object) { throw 'jsonObjectRequired' }
        $observed = $null
        $element = [System.Text.Json.JsonElement]::new()
        if ($document.RootElement.TryGetProperty('observedUtc', [ref]$element)) {
            if ($element.ValueKind -ne [System.Text.Json.JsonValueKind]::String) { throw 'invalidReceiptTimestamp' }
            $observed = $element.GetString() # Never infer DateTime/culture through ConvertFrom-Json.
            $formats = [string[]]@("yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'")
            if ($observed -cnotmatch '^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?Z$') { throw 'invalidReceiptTimestamp' }
            $null = [DateTimeOffset]::ParseExact($observed, $formats, [Globalization.CultureInfo]::InvariantCulture,
                ([Globalization.DateTimeStyles]::AssumeUniversal -bor [Globalization.DateTimeStyles]::AdjustToUniversal))
        }
        return @{ value = ($text | ConvertFrom-Json -AsHashtable); bytes = $bytes; observed = $observed;
            ticks = $stamp[2]; leaf = $Leaf }
    } finally { $document.Dispose() }
}

function Test-GasUInt($Value, [uint64]$Maximum = [uint32]::MaxValue) {
    return $null -ne $Value -and $Value -isnot [bool] -and $Value -isnot [string] -and
        $Value -ge 1 -and $Value -le $Maximum -and [decimal]$Value -eq [decimal]::Truncate([decimal]$Value)
}
function Test-GasHash([byte[]]$Bytes, $Expected) {
    return $Expected -is [string] -and $Expected -cmatch '^[0-9A-F]{64}$' -and
        [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Bytes)) -ceq $Expected
}

function Assert-GasKeys($Value, [string[]]$Keys) {
    if ($Value -isnot [Collections.IDictionary]) { throw 'jsonObjectRequired' }
    foreach ($key in $Keys) { if ($Value.Keys -cnotcontains $key) { throw 'requiredSchemaFieldMissing' } }
}
