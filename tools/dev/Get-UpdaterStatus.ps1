#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$RepositoryRoot,
    [switch]$SkipUpdaterProbe,
    [uri]$UpdaterHealthUri = 'http://127.0.0.1:48237/health'
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$receiptRoot = [IO.Path]::GetFullPath($RepositoryRoot)
$now = [DateTimeOffset]::UtcNow
$address = $null
if (-not $UpdaterHealthUri.IsAbsoluteUri -or $UpdaterHealthUri.Scheme -notin @('http', 'https') -or
    $UpdaterHealthUri.UserInfo -or $UpdaterHealthUri.Query -or $UpdaterHealthUri.Fragment -or
    -not [Net.IPAddress]::TryParse($UpdaterHealthUri.DnsSafeHost, [ref]$address) -or
    -not [Net.IPAddress]::IsLoopback($address)) {
    throw 'Updater health URI must be a credential-free literal loopback HTTP(S) address.'
}

function Read-Receipt([string]$Name, [string[]]$Required, [string[]]$Optional = @()) {
    $path = $receiptRoot
    foreach ($segment in @('.artifacts', 'local-dev', $Name)) {
        if (Test-Path -LiteralPath $path) {
            if ((Get-Item -LiteralPath $path -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Unsafe receipt path.' }
        }
        $path = Join-Path $path $segment
    }
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return $null }
    if ((Get-Item -LiteralPath $path -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Unsafe receipt file.' }
    $stream = [IO.File]::Open($path, [IO.FileMode]::Open, [IO.FileAccess]::Read,
        [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete)
    try {
        $buffer = [byte[]]::new(32769)
        $length = 0
        while ($length -lt $buffer.Length) {
            $read = $stream.Read($buffer, $length, $buffer.Length - $length)
            if ($read -eq 0) { break }
            $length += $read
        }
        if ($length -gt 32768) { throw 'Receipt exceeds bound.' }
        $json = [Text.UTF8Encoding]::new($false, $true).GetString($buffer, 0, $length)
    } finally { $stream.Dispose() }
    $document = [Text.Json.JsonDocument]::Parse($json)
    try {
        if ($document.RootElement.ValueKind -ne [Text.Json.JsonValueKind]::Object) { throw 'Receipt must be an object.' }
        $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        $record = @{}
        foreach ($property in $document.RootElement.EnumerateObject()) {
            if (-not $names.Add($property.Name) -or $property.Name -cnotin ($Required + $Optional)) { throw 'Unknown or duplicate receipt field.' }
            $value = $property.Value
            switch ($value.ValueKind) {
                String { $record[$property.Name] = $value.GetString() }
                Number { $record[$property.Name] = $value.GetInt64() }
                True { $record[$property.Name] = $true }
                False { $record[$property.Name] = $false }
                Null { $record[$property.Name] = $null }
                default { throw 'Receipt fields must be scalars.' }
            }
        }
        foreach ($requiredName in $Required) { if (-not $names.Contains($requiredName)) { throw 'Missing receipt field.' } }
        return $record
    } finally { $document.Dispose() }
}

function Read-Utc([object]$Value) {
    if ($Value -isnot [string] -or $Value -cnotmatch '^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|\+00:00)$') { throw 'UTC timestamp required.' }
    $parsed = [DateTimeOffset]::MinValue
    if (-not [DateTimeOffset]::TryParse($Value, [Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]::None, [ref]$parsed) -or $parsed -gt $now.AddMinutes(1)) { throw 'Invalid receipt time.' }
    return $parsed
}
function Get-ReceiptState([string]$Outcome, [DateTimeOffset]$Time) {
    if (($now - $Time).TotalMinutes -gt 10) { return 'stale' }
    return $Outcome
}

$health = [ordered]@{ state = 'unknown'; observedAtUtc = $now.UtcDateTime.ToString('O'); reason = 'HTTP health was not observed.' }
if ($SkipUpdaterProbe) { $health.reason = 'HTTP health probe was explicitly skipped.' }
else {
    $handler = [Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = $false; $handler.UseProxy = $false; $handler.UseCookies = $false
    $handler.UseDefaultCredentials = $false; $handler.MaxResponseHeadersLength = 16
    $client = [Net.Http.HttpClient]::new($handler)
    $client.Timeout = [TimeSpan]::FromSeconds(2)
    $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Get, $UpdaterHealthUri)
    $request.Headers.ConnectionClose = $true
    $response = $null
    try {
        $response = $client.SendAsync($request, [Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
        if ([int]$response.StatusCode -eq 200 -and $response.RequestMessage.RequestUri -eq $UpdaterHealthUri) {
            $health.state = 'online'; $health.reason = 'Loopback HTTP health returned 200; this is not a signed update check.'
        } else { $health.state = 'offline'; $health.reason = 'HTTP health did not return a direct 200 response.' }
    } catch { $health.state = 'offline'; $health.reason = 'HTTP health could not be reached within the bounded probe.' }
    finally { if ($response) { $response.Dispose() }; $request.Dispose(); $client.Dispose() }
}

$check = [ordered]@{ state = 'unknown'; outcome = $null; checkedAtUtc = $null; productId = $null; channel = $null; sequence = $null
    indexValid = $null; manifestValid = $null; downloadsReachable = $null; reason = 'No signed updater check receipt.' }
try {
    $saved = Read-Receipt 'last-updater-check.json' @('schemaVersion', 'outcome', 'checkedAtUtc', 'productId', 'channel',
        'sequence', 'indexValid', 'manifestValid', 'downloadsReachable', 'reason')
    if ($null -ne $saved) {
        if ($saved.schemaVersion -isnot [long] -or $saved.schemaVersion -ne 1 -or $saved.outcome -cnotin @('passed', 'failed') -or
            $saved.productId -isnot [string] -or $saved.productId -cnotmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$' -or
            $saved.channel -isnot [string] -or $saved.channel -cnotmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$' -or
            $saved.indexValid -isnot [bool] -or $saved.manifestValid -isnot [bool] -or $saved.downloadsReachable -isnot [bool] -or
            ($null -ne $saved.sequence -and ($saved.sequence -isnot [long] -or $saved.sequence -lt 1)) -or
            $saved.reason -cnotin @('verified', 'index-invalid', 'manifest-invalid', 'downloads-unreachable', 'check-failed')) { throw 'Invalid updater receipt.' }
        if ($saved.outcome -eq 'passed' -and (-not $saved.indexValid -or -not $saved.manifestValid -or
            -not $saved.downloadsReachable -or $null -eq $saved.sequence -or $saved.reason -ne 'verified')) { throw 'Contradictory passed receipt.' }
        if ($saved.outcome -eq 'failed' -and $saved.reason -eq 'verified') { throw 'Contradictory failed receipt.' }
        $time = Read-Utc $saved.checkedAtUtc
        $check = [ordered]@{ state = (Get-ReceiptState $saved.outcome $time); outcome = $saved.outcome
            checkedAtUtc = $time.UtcDateTime.ToString('O'); productId = $saved.productId; channel = $saved.channel; sequence = $saved.sequence
            indexValid = $saved.indexValid; manifestValid = $saved.manifestValid; downloadsReachable = $saved.downloadsReachable
            reason = $(if ($saved.outcome -eq 'passed') { 'Signed metadata and bundle/launcher endpoints checked; payload integrity was not tested.' }
                else { 'Updater check failed; signed metadata or bundle/launcher endpoints were not confirmed.' }) }
    }
} catch { $check.reason = 'Updater check receipt is unreadable, malformed, or unsafe; no result inferred.' }

$smoke = [ordered]@{ state = 'unknown'; outcome = $null; finishedAtUtc = $null; passed = $null; failed = $null; skipped = $null
    reason = 'No updater smoke-test receipt.' }
try {
    $savedSmoke = Read-Receipt 'last-updater-smoke.json' @('outcome', 'finishedAtUtc', 'passed', 'failed', 'skipped') @('schemaVersion')
    if ($null -ne $savedSmoke) {
        if (($savedSmoke.ContainsKey('schemaVersion') -and ($savedSmoke.schemaVersion -isnot [long] -or $savedSmoke.schemaVersion -ne 1)) -or
            $savedSmoke.outcome -cnotin @('passed', 'failed')) { throw 'Invalid smoke receipt.' }
        foreach ($name in @('passed', 'failed', 'skipped')) {
            if ($savedSmoke[$name] -isnot [long] -or $savedSmoke[$name] -lt 0 -or $savedSmoke[$name] -gt [int]::MaxValue) { throw 'Invalid test count.' }
        }
        if (($savedSmoke.outcome -eq 'passed' -and ($savedSmoke.failed -ne 0 -or $savedSmoke.passed -eq 0)) -or
            ($savedSmoke.outcome -eq 'failed' -and $savedSmoke.failed -eq 0)) { throw 'Contradictory test receipt.' }
        $time = Read-Utc $savedSmoke.finishedAtUtc
        $smoke = [ordered]@{ state = (Get-ReceiptState $savedSmoke.outcome $time); outcome = $savedSmoke.outcome
            finishedAtUtc = $time.UtcDateTime.ToString('O'); passed = $savedSmoke.passed; failed = $savedSmoke.failed; skipped = $savedSmoke.skipped
            reason = 'Saved smoke tests only; separate from HTTP health, signed checks, and live-client proof.' }
    }
} catch { $smoke.reason = 'Updater smoke-test receipt is unreadable, malformed, or unsafe; no result inferred.' }
[ordered]@{ health = $health; lastCheck = $check; lastSmokeTest = $smoke }
