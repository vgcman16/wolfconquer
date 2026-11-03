#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ResultsPath,
    [ValidateRange(1, 100)][int]$Top = 10,
    [switch]$AsJson,
    [switch]$StrictReport
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$resolved = Get-Item -LiteralPath $ResultsPath -Force
if ($resolved.PSProvider.Name -ne 'FileSystem') { throw 'ResultsPath must be a filesystem path.' }
if ($StrictReport -and $resolved.PSIsContainer) { throw 'StrictReport requires a single explicit TRX file.' }
$files = @(if ($resolved.PSIsContainer) {
    Get-ChildItem -LiteralPath $resolved.FullName -Filter '*.trx' -File -Recurse
} elseif ($resolved.Extension -eq '.trx') { $resolved }
else { throw 'ResultsPath must be a TRX file or a directory containing TRX files.' })
if ($files.Count -eq 0) { throw 'No terminal TRX files were found. A running check may not have written its results yet.' }

$rows = [Collections.Generic.List[object]]::new()
$fileReports = [Collections.Generic.List[object]]::new()
$classTotals = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
$outcomes = [Collections.Generic.Dictionary[string, int]]::new([StringComparer]::Ordinal)
$missingDurations = 0
$durationSum = 0.0

function Read-TrxAttribute([Xml.Linq.XElement]$Node, [string]$Name) {
    $attribute = $Node.Attribute($Name)
    if ($null -eq $attribute) { return '' }
    return $attribute.Value
}

foreach ($file in $files) {
    # Reports are input data. Do not resolve external XML entities or allow DTDs.
    $settings = [Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $reader = [Xml.XmlReader]::Create($file.FullName, $settings)
    try { $document = [Xml.Linq.XDocument]::Load($reader) }
    finally { $reader.Dispose() }
    $root = $document.Root
    if ($null -eq $root -or $root.Name.LocalName -ne 'TestRun') { throw "Not a TRX TestRun: $($file.FullName)" }
    $ns = $root.GetDefaultNamespace()
    $definitions = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::OrdinalIgnoreCase)
    $definitionNode = $root.Element($ns.GetName('TestDefinitions'))
    if ($null -ne $definitionNode) {
        foreach ($definition in $definitionNode.Elements($ns.GetName('UnitTest'))) {
            $method = $definition.Element($ns.GetName('TestMethod'))
            $id = (Read-TrxAttribute $definition 'id')
            $className = if ($null -ne $method) { (Read-TrxAttribute $method 'className') } else { '' }
            if ($id -and $className) { $definitions[$id] = $className }
        }
    }
    $summary = $root.Element($ns.GetName('ResultSummary'))
    $fileOutcome = if ($null -ne $summary) { (Read-TrxAttribute $summary 'outcome') } else { 'Unknown' }
    $times = $root.Element($ns.GetName('Times'))
    if ($StrictReport -and @($root.Elements($ns.GetName('Times'))).Count -ne 1) {
        throw 'StrictReport requires exactly one report Times element.'
    }
    $wallSeconds = $null
    if ($null -ne $times) {
        $start = [DateTimeOffset]::MinValue
        $finish = [DateTimeOffset]::MinValue
        $startText = Read-TrxAttribute $times 'start'
        $finishText = Read-TrxAttribute $times 'finish'
        $instantPattern = '^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})$'
        $explicitInstants = $startText -cmatch $instantPattern -and $finishText -cmatch $instantPattern
        if ((-not $StrictReport -or $explicitInstants) -and
            [DateTimeOffset]::TryParse($startText, [Globalization.CultureInfo]::InvariantCulture,
                [Globalization.DateTimeStyles]::None, [ref]$start) -and
            [DateTimeOffset]::TryParse($finishText, [Globalization.CultureInfo]::InvariantCulture,
                [Globalization.DateTimeStyles]::None, [ref]$finish) -and $finish -ge $start) {
            $wallSeconds = [Math]::Round(($finish - $start).TotalSeconds, 3)
        } elseif ($StrictReport) {
            throw 'StrictReport requires valid explicit-offset start/finish instants with finish at or after start.'
        }
    }
    $fileRows = 0
    $resultNode = $root.Element($ns.GetName('Results'))
    if ($null -ne $resultNode) {
        foreach ($result in $resultNode.Elements($ns.GetName('UnitTestResult'))) {
            $name = (Read-TrxAttribute $result 'testName')
            $outcome = (Read-TrxAttribute $result 'outcome')
            if (-not $name -or -not $outcome) { throw "Result lacks a test name or outcome: $($file.FullName)" }
            $id = (Read-TrxAttribute $result 'testId')
            $className = ''
            if (-not $definitions.TryGetValue($id, [ref]$className)) {
                $methodName = $name.Split('(', 2)[0]
                $dot = $methodName.LastIndexOf('.')
                $className = if ($dot -gt 0) { $methodName.Substring(0, $dot) } else { '<unknown>' }
            }
            $rawDuration = (Read-TrxAttribute $result 'duration')
            $duration = [TimeSpan]::Zero
            $seconds = $null
            if ($rawDuration) {
                $validDuration = if ($StrictReport) {
                    [TimeSpan]::TryParseExact($rawDuration, 'c', [Globalization.CultureInfo]::InvariantCulture, [ref]$duration)
                } else {
                    [TimeSpan]::TryParse($rawDuration, [Globalization.CultureInfo]::InvariantCulture, [ref]$duration)
                }
                if (-not $validDuration -or
                    $duration -lt [TimeSpan]::Zero) { throw "Invalid duration for test: $name" }
                $seconds = $duration.TotalSeconds
                $durationSum += $seconds
            } else { $missingDurations++ }
            if (-not $outcomes.ContainsKey($outcome)) { $outcomes[$outcome] = 0 }
            $outcomes[$outcome]++
            if (-not $classTotals.ContainsKey($className)) {
                $classTotals[$className] = [pscustomobject]@{ className = $className; rows = 0; knownDurationRows = 0; rowsWithoutDuration = 0; durationSeconds = 0.0 }
            }
            $group = $classTotals[$className]
            $group.rows++
            if ($null -ne $seconds) { $group.durationSeconds += $seconds; $group.knownDurationRows++ }
            else { $group.rowsWithoutDuration++ }
            $rows.Add([pscustomobject]@{ name = $name; outcome = $outcome; durationSeconds = $seconds; file = $file.FullName })
            $fileRows++
        }
    }
    $fileReports.Add([pscustomobject]@{ path = $file.FullName; outcome = $fileOutcome; rows = $fileRows; wallSeconds = $wallSeconds })
}

$largestClasses = @($classTotals.Values | Sort-Object durationSeconds -Descending | Select-Object -First $Top |
    ForEach-Object { [pscustomobject]@{ className = $_.className; rows = $_.rows; knownDurationRows = $_.knownDurationRows; rowsWithoutDuration = $_.rowsWithoutDuration; durationSeconds = [Math]::Round($_.durationSeconds, 3) } })
$slowestTests = @($rows | Where-Object { $null -ne $_.durationSeconds } | Sort-Object durationSeconds -Descending |
    Select-Object -First $Top | ForEach-Object {
        [pscustomobject]@{ name = $_.name; outcome = $_.outcome; durationSeconds = [Math]::Round($_.durationSeconds, 3); file = $_.file }
    })
$report = [pscustomobject]@{
    schemaVersion = 1
    strictReport = [bool]$StrictReport
    files = $fileReports.ToArray()
    rowCount = $rows.Count
    outcomes = $outcomes
    rowsWithoutDuration = $missingDurations
    rowsWithDuration = $rows.Count - $missingDurations
    sumTestDurationSeconds = [Math]::Round($durationSum, 3)
    timingLimit = 'TRX snapshot only. Test durations can overlap; known-duration sums and class totals are not wall time or a speedup. Missing row durations are unknown. This reader does not run checks or prove process completion, full CI, coverage, parity, or readiness.'
    slowestTests = $slowestTests
    largestClasses = $largestClasses
}
if ($AsJson) { $report | ConvertTo-Json -Depth 7 }
else { $report }
