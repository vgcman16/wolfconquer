function Get-OwnedSqlRequiredFacts([string] $Repository) {
    $directory = Join-Path $Repository 'tests\WolfConquer.Tests'
    $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $attributeCount = 0
    $pattern = '\[ModernSqlAuthFixtureFact(?:Attribute)?\]\s*public\s+(?:async\s+)?Task\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\('
    foreach ($file in Get-ChildItem -LiteralPath $directory -Filter 'ModernSqlAuthFileRoundtripTests*.cs' -File) {
        $source = [IO.File]::ReadAllText($file.FullName)
        $attributeCount += [regex]::Matches($source, '\[ModernSqlAuthFixtureFact(?:Attribute)?\]').Count
        foreach ($match in [regex]::Matches($source, $pattern)) {
            if (-not $names.Add($match.Groups['name'].Value)) { throw 'Duplicate SQL fixture Fact method name.' }
        }
    }
    if ($names.Count -eq 0 -or $attributeCount -ne $names.Count) {
        throw 'Cannot discover every required SQL fixture Fact from this checkout; update the bounded discovery rule.'
    }
    return ,@($names | Sort-Object)
}

function Read-OwnedSqlTrxIdentity([string] $Text) {
    $identity = [Guid]::Empty
    if (-not [Guid]::TryParse($Text, [ref]$identity) -or $identity -eq [Guid]::Empty) {
        throw 'SQL TRX has a missing or malformed test/execution identity.'
    }
    return $identity.ToString('D')
}

function Assert-OwnedSqlTrx([string] $TrxPath, [string[]] $RequiredFacts) {
    $required = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($name in $RequiredFacts) {
        if ($name -notmatch '^[A-Za-z_][A-Za-z0-9_]*$' -or -not $required.Add($name)) { throw 'The required SQL Fact inventory is invalid.' }
    }
    if ($required.Count -eq 0) { throw 'The required SQL Fact inventory is empty.' }
    if (-not (Test-Path -LiteralPath $TrxPath -PathType Leaf)) { throw 'The requested SQL run produced no terminal TRX.' }
    if ((Get-Item -LiteralPath $TrxPath).Length -gt 33554432) { throw 'SQL TRX exceeded its inspection bound.' }
    $counts = Read-FocusedTrx -Path $TrxPath
    if ($counts.Failed -ne 0 -or $counts.Other -ne 0) { throw 'The requested terminal run contains failures or unknown outcomes.' }
    $settings = [Xml.XmlReaderSettings]::new(); $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit; $settings.XmlResolver = $null
    $reader = [Xml.XmlReader]::Create($TrxPath, $settings)
    $document = [Xml.XmlDocument]::new(); $document.XmlResolver = $null
    try { $document.Load($reader) } finally { $reader.Dispose() }
    $ns = [Xml.XmlNamespaceManager]::new($document.NameTable)
    $ns.AddNamespace('t', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
    $summary = $document.SelectSingleNode('/t:TestRun/t:ResultSummary', $ns)
    $times = $document.SelectSingleNode('/t:TestRun/t:Times', $ns)
    $counters = $document.SelectSingleNode('/t:TestRun/t:ResultSummary/t:Counters', $ns)
    if (-not $summary -or -not $times -or -not $times.GetAttribute('finish') -or -not $counters -or
        $summary.GetAttribute('outcome') -notin @('Completed', 'Passed')) { throw 'SQL TRX is incomplete or unsuccessful.' }
    foreach ($field in @('failed', 'error', 'timeout', 'aborted')) {
        if ($counters.GetAttribute($field) -notmatch '^0$') { throw 'The terminal SQL test run contains a failure.' }
    }
    $definitionIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $definitions = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
    $definedNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $definedExecutions = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($test in $document.SelectNodes('/t:TestRun/t:TestDefinitions/t:UnitTest', $ns)) {
        $testId = Read-OwnedSqlTrxIdentity ($test.GetAttribute('id'))
        if (-not $definitionIds.Add($testId)) { throw 'SQL TRX contains duplicate test definitions.' }
        $method = $test.SelectSingleNode('t:TestMethod', $ns)
        if ($method -and $method.GetAttribute('className') -ceq 'WolfConquer.Tests.ModernSqlAuthFileRoundtripTests') {
            $name = $method.GetAttribute('name')
            if (-not $required.Contains($name) -or -not $definedNames.Add($name)) { throw 'SQL TRX definitions do not match exact required Fact names.' }
            $executions = @($test.SelectNodes('t:Execution', $ns))
            if ($executions.Count -ne 1) { throw 'A required SQL Fact has no unambiguous execution definition.' }
            $execution = Read-OwnedSqlTrxIdentity ($executions[0].GetAttribute('id'))
            if (-not $definedExecutions.Add($execution)) { throw 'Required SQL Fact definitions share an execution identity.' }
            $definitions.Add($testId, [pscustomobject]@{ Name = $name; Execution = $execution })
        }
    }
    if ($definitions.Count -ne $required.Count) { throw 'A required SQL Fact definition is missing.' }
    $entries = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
    foreach ($entry in $document.SelectNodes('/t:TestRun/t:TestEntries/t:TestEntry', $ns)) {
        $testId = Read-OwnedSqlTrxIdentity ($entry.GetAttribute('testId'))
        if (-not $definitions.ContainsKey($testId)) { continue }
        $execution = Read-OwnedSqlTrxIdentity ($entry.GetAttribute('executionId'))
        if ($entries.ContainsKey($testId) -or $execution -cne $definitions[$testId].Execution) {
            throw 'A required SQL Fact has duplicate or inconsistent execution entries.'
        }
        $entries.Add($testId, $execution)
    }
    if ($entries.Count -ne $required.Count) { throw 'A required SQL Fact execution entry is missing.' }
    $observed = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $resultExecutions = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $rows = 0
    foreach ($result in $document.SelectNodes('/t:TestRun/t:Results/t:UnitTestResult', $ns)) {
        $testId = Read-OwnedSqlTrxIdentity ($result.GetAttribute('testId'))
        $execution = Read-OwnedSqlTrxIdentity ($result.GetAttribute('executionId'))
        if (-not $definitionIds.Contains($testId) -or -not $resultExecutions.Add($execution)) {
            throw 'SQL TRX contains undefined test results or duplicate execution results.'
        }
        if (-not $definitions.ContainsKey($testId)) { continue }
        $definition = $definitions[$testId]; $rows++
        if ($execution -cne $definition.Execution -or $execution -cne $entries[$testId] -or
            -not $observed.Add($definition.Name) -or $result.GetAttribute('outcome') -cne 'Passed') {
            throw 'An opt-in SQL Fact was duplicated, skipped, not executed, or failed.'
        }
    }
    $executed = 0L
    if ($rows -eq 0 -or -not [long]::TryParse($counters.GetAttribute('executed'), [ref]$executed) -or
        $executed -lt $rows -or $executed -gt $resultExecutions.Count) { throw 'No consistent real SQL fixture Fact execution count exists.' }
    foreach ($name in $required) {
        if (-not $observed.Contains($name)) { throw 'A required SQL Fact is missing from the actual TRX.' }
    }
    if ($rows -ne $required.Count) { throw 'SQL TRX does not match the selected checkout Fact inventory.' }
    return $rows
}
