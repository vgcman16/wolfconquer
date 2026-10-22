Set-StrictMode -Version Latest

function Assert-CdJsonKeys([Text.Json.JsonElement]$Element) {
    if ($Element.ValueKind -eq [Text.Json.JsonValueKind]::Object) {
        $keys = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($property in $Element.EnumerateObject()) {
            if (-not $keys.Add($property.Name)) { throw 'Duplicate JSON policy property.' }
            Assert-CdJsonKeys $property.Value
        }
    } elseif ($Element.ValueKind -eq [Text.Json.JsonValueKind]::Array) {
        foreach ($value in $Element.EnumerateArray()) { Assert-CdJsonKeys $value }
    }
}

function Assert-CdKeys([Collections.IDictionary]$Object, [string[]]$Required, [string[]]$Optional = @()) {
    foreach ($key in $Required) { if (-not $Object.Contains($key)) { throw 'A required policy property is missing.' } }
    foreach ($key in $Object.Keys) { if ($key -cnotin $Required -and $key -cnotin $Optional) { throw 'Unknown policy property.' } }
}

function Get-CdPathSet([object]$Values, [switch]$TopLevelOnly) {
    if ($Values -isnot [Array]) { throw 'Policy path lists must be JSON arrays.' }
    $set = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($path in $Values) {
        if ($path -isnot [string]) { throw 'Policy paths must be strings.' }
        Assert-CdRelative $path
        if ($TopLevelOnly -and $path.Contains('/')) { throw 'Asset directories must be exact top-level names.' }
        if (-not $set.Add($path)) { throw 'Policy paths contain a duplicate or Windows case alias.' }
    }
    return ,$set
}

function Read-CdPolicy([string]$Path, [string]$Source) {
    if ((Test-CdWithin $Source $Path) -or -not $Path.EndsWith('.json', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The reviewed JSON policy must be outside the client source tree.'
    }
    if (Get-CdRisk ([IO.Path]::GetFileName($Path))) { throw 'A private/local-data filename is not an inspectable policy input.' }
    $policyStream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try {
        if ($policyStream.Length -gt 1MB) { throw 'Policy exceeds the 1 MiB bound.' }
        $bytes = [byte[]]::new([int]$policyStream.Length); $policyStream.ReadExactly($bytes, 0, $bytes.Length)
    } finally { $policyStream.Dispose() }
    $json = [Text.UTF8Encoding]::new($false, $true).GetString($bytes)
    $document = [Text.Json.JsonDocument]::Parse($json)
    $utcStrings = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
    try {
        Assert-CdJsonKeys $document.RootElement
        # ConvertFrom-Json date handling differs by PowerShell version. Read the
        # original JSON string tokens, never stringify a coerced date/number.
        if ($document.RootElement.ValueKind -eq [Text.Json.JsonValueKind]::Object) {
            foreach ($property in $document.RootElement.EnumerateObject()) {
                if ($property.Name -ceq 'publication' -and $property.Value.ValueKind -eq [Text.Json.JsonValueKind]::Object) {
                    foreach ($field in $property.Value.EnumerateObject()) {
                        if ($field.Name -cin @('createdUtc', 'expiresUtc')) {
                            if ($field.Value.ValueKind -ne [Text.Json.JsonValueKind]::String) { throw 'Publication timestamps must be JSON strings.' }
                            $utcStrings.Add($field.Name, $field.Value.GetString())
                        }
                    }
                }
            }
        }
    } finally { $document.Dispose() }
    $policy = $json | ConvertFrom-Json -AsHashtable -Depth 16
    if ($policy -isnot [Collections.IDictionary]) { throw 'Policy must be a JSON object.' }
    Assert-CdKeys $policy @('schemaVersion', 'assetDirectories', 'files', 'publication') @('excludePaths')
    if (($policy.schemaVersion -isnot [long] -and $policy.schemaVersion -isnot [int]) -or $policy.schemaVersion -ne 1) { throw 'Unsupported distribution-policy schema.' }
    $directories = Get-CdPathSet $policy.assetDirectories -TopLevelOnly
    $files = Get-CdPathSet $policy.files
    $excludeList = @()
    if ($policy.Contains('excludePaths')) { $excludeList = $policy.excludePaths }
    $excludes = Get-CdPathSet $excludeList
    if ($policy.publication -isnot [Collections.IDictionary]) { throw 'Publication metadata must be an object.' }
    $p = $policy.publication
    foreach ($name in $utcStrings.Keys) { $p[$name] = $utcStrings[$name] }
    Assert-CdKeys $p @('publishRoot', 'productId', 'channel', 'sequence', 'contentBaseUrl', 'createdUtc', 'expiresUtc', 'keyId') @('includeBundle', 'allowLoopbackHttp')
    foreach ($name in @('publishRoot', 'productId', 'channel', 'contentBaseUrl', 'createdUtc', 'expiresUtc', 'keyId')) {
        if ($p[$name] -isnot [string] -or [string]::IsNullOrWhiteSpace($p[$name])) { throw 'Publication strings must be nonempty.' }
    }
    foreach ($name in @('productId', 'channel', 'keyId')) {
        if ($p[$name] -cnotmatch '^[A-Za-z0-9._-]{1,128}$') { throw 'Publication identifiers must use the publisher identifier grammar.' }
    }
    if ($p.productId.Length -gt 122 -or $p.sequence -isnot [long] -and $p.sequence -isnot [int] -or $p.sequence -le 0) { throw 'Invalid product index identifier or sequence.' }
    foreach ($name in @('includeBundle', 'allowLoopbackHttp')) {
        if ($p.Contains($name) -and $p[$name] -isnot [bool]) { throw 'Publication switches must be JSON booleans.' }
    }
    $loopback = $p.Contains('allowLoopbackHttp') -and $p.allowLoopbackHttp
    $uri = $null
    if (-not [uri]::TryCreate($p.contentBaseUrl, [UriKind]::Absolute, [ref]$uri) -or
        -not ($uri.Scheme -eq 'https' -or ($loopback -and $uri.Scheme -eq 'http' -and $uri.IsLoopback)) -or
        [string]::IsNullOrEmpty($uri.Host) -or $uri.UserInfo -or $uri.Query -or $uri.Fragment -or -not $p.contentBaseUrl.EndsWith('/') -or $p.contentBaseUrl -match '[\s\\]') {
        throw 'Content base must be a credential-free HTTPS directory URL (or explicitly allowed loopback HTTP).'
    }
    $created = [DateTimeOffset]::MinValue; $expires = [DateTimeOffset]::MinValue
    foreach ($name in @('createdUtc', 'expiresUtc')) {
        $parsed = [DateTimeOffset]::MinValue
        if ($p[$name] -cnotmatch '(Z|\+00:00)$' -or -not [DateTimeOffset]::TryParse($p[$name], [Globalization.CultureInfo]::InvariantCulture,
            [Globalization.DateTimeStyles]::RoundtripKind, [ref]$parsed) -or $parsed.Offset -ne [TimeSpan]::Zero) { throw 'Publication timestamps must be explicit UTC.' }
        if ($name -eq 'createdUtc') { $created = $parsed } else { $expires = $parsed }
    }
    if ($expires -le $created) { throw 'Expiry must follow creation.' }
    $publish = Resolve-CdAbsolute $p.publishRoot
    if ([IO.File]::Exists($publish)) { throw 'Publication root cannot name an existing file.' }
    if ((Test-CdWithin $Source $publish) -or (Test-CdWithin $publish $Source)) { throw 'Source and publication roots must not overlap.' }
    foreach ($relative in ([string[]]$directories + [string[]]$files + [string[]]$excludes)) {
        $actual = Assert-CdExactExistingPath $Source $relative
        if ($directories.Contains($relative) -and -not [IO.Directory]::Exists($actual)) { throw 'An asset directory is not a directory.' }
        if ($files.Contains($relative) -and -not [IO.File]::Exists($actual)) { throw 'An exact file is not a file.' }
    }
    foreach ($file in $files) {
        foreach ($exclude in $excludes) {
            if ($file.Equals($exclude, [StringComparison]::OrdinalIgnoreCase) -or $file.StartsWith($exclude + '/', [StringComparison]::OrdinalIgnoreCase)) { throw 'Exact-file approval conflicts with an exclusion.' }
        }
    }
    return [pscustomobject]@{ Publication = $p; PublishRoot = $publish; Directories = $directories; Files = $files; Excludes = $excludes
        Sha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)) }
}
