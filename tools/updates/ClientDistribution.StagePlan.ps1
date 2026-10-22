Set-StrictMode -Version Latest

function Read-CdsPlan([string]$Path) {
    if (-not $Path.EndsWith('.json', [StringComparison]::OrdinalIgnoreCase) -or
        -not [IO.File]::Exists($Path)) { throw 'PlanPath must be an existing ordinary JSON plan.' }
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try {
        if ($stream.Length -le 0 -or $stream.Length -gt 64MB) { throw 'Publication plan must be nonempty and at most 64 MiB.' }
        $bytes = [byte[]]::new([int]$stream.Length)
        $stream.ReadExactly($bytes, 0, $bytes.Length)
        if ($stream.ReadByte() -ne -1) { throw 'Publication plan changed while being read.' }
    } finally { $stream.Dispose() }
    $json = [Text.UTF8Encoding]::new($false, $true).GetString($bytes)
    $options = [Text.Json.JsonDocumentOptions]::new(); $options.MaxDepth = 16
    $document = [Text.Json.JsonDocument]::Parse($json, $options)
    $utcStrings = @{}
    try {
        Assert-CdJsonKeys $document.RootElement
        if ($document.RootElement.ValueKind -ne [Text.Json.JsonValueKind]::Object) { throw 'Publication plan must be an object.' }
        foreach ($property in $document.RootElement.EnumerateObject()) {
            if ($property.Name -cin @('createdUtc','expiresUtc')) {
                if ($property.Value.ValueKind -ne [Text.Json.JsonValueKind]::String) { throw 'Plan timestamps must be JSON strings.' }
                $utcStrings[$property.Name] = $property.Value.GetString()
            }
        }
    } finally { $document.Dispose() }
    $plan = $json | ConvertFrom-Json -AsHashtable -Depth 16
    Assert-CdKeys $plan @('schemaVersion','sourceRoot','publishRoot','productId','channel','sequence',
        'contentBaseUrl','createdUtc','expiresUtc','keyId','files','includeBundle','allowLoopbackHttp')
    if (($plan.schemaVersion -isnot [int] -and $plan.schemaVersion -isnot [long]) -or $plan.schemaVersion -ne 1) {
        throw 'Unsupported publication-plan schema.'
    }
    foreach ($name in $utcStrings.Keys) { $plan[$name] = $utcStrings[$name] }
    foreach ($name in @('sourceRoot','publishRoot','productId','channel','contentBaseUrl','createdUtc','expiresUtc','keyId')) {
        if ($plan[$name] -isnot [string] -or [string]::IsNullOrWhiteSpace($plan[$name])) { throw 'Plan strings must be nonempty.' }
    }
    foreach ($name in @('productId','channel','keyId')) {
        if ($plan[$name] -cnotmatch '^[A-Za-z0-9._-]{1,128}$') { throw 'Invalid publication identifier.' }
    }
    if ($plan.productId.Length -gt 122 -or ($plan.sequence -isnot [int] -and $plan.sequence -isnot [long]) -or $plan.sequence -le 0) {
        throw 'Invalid publication sequence or product index identifier.'
    }
    foreach ($name in @('includeBundle','allowLoopbackHttp')) {
        if ($plan[$name] -isnot [bool]) { throw 'Plan switches must be JSON booleans.' }
    }
    $uri = $null
    if (-not [uri]::TryCreate($plan.contentBaseUrl, [UriKind]::Absolute, [ref]$uri) -or
        -not ($uri.Scheme -eq 'https' -or ($plan.allowLoopbackHttp -and $uri.Scheme -eq 'http' -and $uri.IsLoopback)) -or
        [string]::IsNullOrEmpty($uri.Host) -or $uri.UserInfo -or $uri.Query -or $uri.Fragment -or
        -not $plan.contentBaseUrl.EndsWith('/') -or $plan.contentBaseUrl -match '[\s\\]') {
        throw 'Plan content base must be a credential-free HTTPS directory URL (or explicit loopback HTTP).'
    }
    $times = @{}
    foreach ($name in @('createdUtc','expiresUtc')) {
        $value = [DateTimeOffset]::MinValue
        if ($plan[$name] -cnotmatch '(Z|\+00:00)$' -or -not [DateTimeOffset]::TryParse($plan[$name],
            [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind, [ref]$value) -or
            $value.Offset -ne [TimeSpan]::Zero) { throw 'Plan timestamps must be explicit UTC.' }
        $times[$name] = $value
    }
    if ($times.expiresUtc -le $times.createdUtc) { throw 'Plan expiry must follow creation.' }
    if ($plan.files -isnot [Array] -or $plan.files.Count -eq 0 -or $plan.files.Count -gt 200000) {
        throw 'A successful plan must contain 1 through 200000 explicit files.'
    }
    $paths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($file in $plan.files) {
        if ($file -isnot [Collections.IDictionary]) { throw 'Plan entries must be objects.' }
        Assert-CdKeys $file @('path','baseSha256')
        if ($file.path -isnot [string]) { throw 'Plan paths must be strings.' }
        Assert-CdRelative $file.path
        if (Get-CdRisk $file.path) { throw 'Plan selects a quarantined private, local-data, or control path.' }
        if (-not $paths.Add($file.path)) { throw 'Plan paths contain duplicates or Windows case aliases.' }
        if ($null -ne $file.baseSha256 -and ($file.baseSha256 -isnot [string] -or $file.baseSha256 -cnotmatch '^[A-Fa-f0-9]{64}$')) {
            throw 'BaseSha256 must be null or a 64-digit hexadecimal prior-install hash.'
        }
    }
    foreach ($path in $paths) {
        $parent = $path
        while ($parent.Contains('/')) {
            $parent = $parent.Substring(0, $parent.LastIndexOf('/'))
            if ($paths.Contains($parent)) { throw 'A plan path is both a file and a directory prefix.' }
        }
    }
    return $plan
}

function Get-CdsSourceFiles([string]$Source, [object[]]$Files) {
    # Cache each visited directory's name index, not its contents. This avoids
    # rescanning large asset directories once per selected file.
    $indices = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::OrdinalIgnoreCase)
    $observed = [Collections.Generic.List[object]]::new()
    [long]$total = 0
    foreach ($file in $Files) {
        $current = $Source
        foreach ($segment in $file.path.Split('/')) {
            if (-not $indices.ContainsKey($current)) {
                $index = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::OrdinalIgnoreCase)
                foreach ($child in @(Get-CdChildren $current)) {
                    $name = [IO.Path]::GetFileName($child)
                    if (-not $index.TryAdd($name,$child)) { throw 'Source names contain a Windows case alias.' }
                }
                $indices.Add($current,$index)
            }
            $index = $indices[$current]
            if (-not $index.ContainsKey($segment) -or
                -not [IO.Path]::GetFileName($index[$segment]).Equals($segment, [StringComparison]::Ordinal)) {
                throw 'An approved file is missing or differs from its exact reviewed path spelling.'
            }
            $current = $index[$segment]
            Assert-CdNoReparse $current
        }
        if (-not [IO.File]::Exists($current)) { throw 'An approved plan path is not an ordinary file.' }
        $info = [IO.FileInfo]::new($current)
        if ($info.Length -gt 4GB) { throw 'An approved file exceeds the 4 GiB publisher bound.' }
        $total += $info.Length
        if ($total -gt 32GB) { throw 'Approved file bytes exceed the 32 GiB publisher bound.' }
        $observed.Add([pscustomobject]@{ Relative=$file.path; Source=$current; Length=$info.Length; WriteUtc=$info.LastWriteTimeUtc })
    }
    return [pscustomobject]@{ Files=$observed.ToArray(); Bytes=$total }
}
