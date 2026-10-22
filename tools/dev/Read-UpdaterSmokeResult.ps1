#requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string]$ResultsDirectory)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$directory = Get-Item -LiteralPath $ResultsDirectory -Force
if (-not $directory.PSIsContainer -or ($directory.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
    throw 'Updater test results must be an ordinary results directory.'
}
for ($ancestor = $directory.FullName; $ancestor; $ancestor = [IO.Path]::GetDirectoryName($ancestor)) {
    if ([IO.File]::GetAttributes($ancestor) -band [IO.FileAttributes]::ReparsePoint) { throw 'TRX ancestors cannot be reparse paths.' }
}
# Accept flat legacy fixtures and the isolated project/target-framework layout.
# Never use an unbounded recursive scan or follow a link into unrelated data.
$files = [Collections.Generic.List[object]]::new()
$pending = [Collections.Generic.Queue[object]]::new()
$pending.Enqueue(@{ path = $directory.FullName; depth = 0 })
$directoryCount = 0; $entryCount = 0
while ($pending.Count -gt 0) {
    $current = $pending.Dequeue()
    if (++$directoryCount -gt 64) { throw 'TRX directory count exceeds the bounded results layout.' }
    foreach ($path in [IO.Directory]::EnumerateFileSystemEntries($current.path)) {
        if (++$entryCount -gt 512) { throw 'TRX entry count exceeds the bounded results layout.' }
        $item = Get-Item -LiteralPath $path -Force
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'TRX results cannot contain reparse entries.' }
        if ($item.PSIsContainer) {
            if ($current.depth -ge 4) { throw 'TRX directory depth exceeds the bounded results layout.' }
            $pending.Enqueue(@{ path = $item.FullName; depth = $current.depth + 1 })
        } elseif ($item.Extension.Equals('.trx', [StringComparison]::OrdinalIgnoreCase)) {
            $files.Add($item)
            if ($files.Count -gt 64) { throw 'Expected a bounded set of fresh TRX results.' }
        }
    }
}
if ($files.Count -eq 0) { throw 'Expected a bounded set of fresh TRX results.' }
$receipt = $null
foreach ($file in $files) {
    if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -or $file.Length -gt 64MB) {
        throw 'Unsafe or oversized TRX result.'
    }
    $settings = [Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $settings.MaxCharactersInDocument = 64MB
    $reader = [Xml.XmlReader]::Create($file.FullName, $settings)
    $document = [Xml.XmlDocument]::new()
    $document.XmlResolver = $null
    try { $document.Load($reader) } finally { $reader.Dispose() }
    $ns = [Xml.XmlNamespaceManager]::new($document.NameTable)
    $ns.AddNamespace('t', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
    $assemblies = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($test in $document.SelectNodes('/t:TestRun/t:TestDefinitions/t:UnitTest', $ns)) {
        $storage = $test.GetAttribute('storage')
        if (-not $storage) {
            $method = $test.SelectSingleNode('t:TestMethod', $ns)
            if ($null -ne $method) { $storage = $method.GetAttribute('codeBase') }
        }
        if (-not $storage) { throw 'TRX test definition is missing its assembly identity.' }
        $null = $assemblies.Add([IO.Path]::GetFileName($storage.Replace('\', '/')))
    }
    if (-not $assemblies.Contains('WolfConquer.Updates.Tests.dll')) { continue }
    if ($assemblies.Count -ne 1 -or $null -ne $receipt) { throw 'Updater assembly TRX identity is mixed or ambiguous.' }
    $summary = $document.SelectSingleNode('/t:TestRun/t:ResultSummary', $ns)
    $times = $document.SelectSingleNode('/t:TestRun/t:Times', $ns)
    if ($null -eq $summary -or $null -eq $times -or $summary.GetAttribute('outcome') -cnotin @('Completed', 'Passed', 'Failed')) {
        throw 'Updater TRX has no terminal result.'
    }
    $counters = $summary.SelectSingleNode('t:Counters', $ns)
    if ($null -eq $counters) { throw 'Updater TRX has no counters.' }
    $counts = @{}
    foreach ($name in @('total', 'executed', 'passed', 'failed', 'notExecuted')) {
        $text = $counters.GetAttribute($name)
        if ($text -cnotmatch '^\d{1,10}$') { throw 'Invalid updater TRX counter.' }
        $counts[$name] = [int]::Parse($text, [Globalization.CultureInfo]::InvariantCulture)
    }
    foreach ($name in @('error', 'timeout', 'aborted', 'inconclusive', 'passedButRunAborted', 'notRunnable', 'disconnected', 'inProgress', 'pending')) {
        $text = $counters.GetAttribute($name)
        if ($text -and $text -cne '0') { throw 'Updater TRX contains incomplete or unsupported outcomes.' }
    }
    # xUnit can emit RunInfo/Error for an ordinary failed test. Only complete
    # nonzero failed-test counters permit that diagnostic; never a green receipt.
    if ($counts.total -le 0 -or $counts.total -ne ($counts.passed + $counts.failed + $counts.notExecuted) -or
        $counts.executed -ne ($counts.passed + $counts.failed) -or ($counts.passed + $counts.failed) -eq 0 -or
        ($summary.GetAttribute('outcome') -eq 'Failed' -and $counts.failed -eq 0) -or
        ($counts.failed -eq 0 -and $summary.SelectNodes('t:RunInfos/t:RunInfo[@outcome="Error"]', $ns).Count -ne 0)) {
        throw 'Updater TRX counters do not describe a complete test run.'
    }
    $finish = [DateTimeOffset]::MinValue
    $text = $times.GetAttribute('finish')
    if ($text -cnotmatch '(?:Z|[+-]\d{2}:\d{2})$' -or -not [DateTimeOffset]::TryParse($text,
        [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::None, [ref]$finish) -or
        $finish -gt [DateTimeOffset]::UtcNow.AddMinutes(1)) { throw 'Updater TRX finish time is invalid.' }
    $receipt = [ordered]@{ schemaVersion = 1; outcome = $(if ($counts.failed -gt 0) { 'failed' } else { 'passed' })
        finishedAtUtc = $finish.UtcDateTime.ToString('O'); passed = $counts.passed; failed = $counts.failed; skipped = $counts.notExecuted }
}
if ($null -eq $receipt) { throw 'No terminal WolfConquer.Updates.Tests assembly result was found.' }
$receipt
