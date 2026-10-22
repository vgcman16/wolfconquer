#requires -Version 7.0
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('wolf-updater-trx-' + [guid]::NewGuid().ToString('N'))
$parser = Join-Path $PSScriptRoot 'Read-UpdaterSmokeResult.ps1'
$finish = [DateTimeOffset]::UtcNow.AddMinutes(-1).ToString('O')
$cases = 0
function Write-Trx([string]$Name, [string]$Assembly = 'WolfConquer.Updates.Tests.dll', [string]$Outcome = 'Completed',
    [int]$Passed = 7, [int]$Failed = 0, [int]$Skipped = 2, [string]$ExtraCounter = '', [string]$RunInfo = '') {
    $total = $Passed + $Failed + $Skipped
    $executed = $Passed + $Failed
    $xml = @"
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <Times finish="$finish" />
  <TestDefinitions><UnitTest storage="C:\synthetic\$Assembly"><TestMethod codeBase="C:\synthetic\$Assembly" /></UnitTest></TestDefinitions>
  <ResultSummary outcome="$Outcome"><Counters total="$total" executed="$executed" passed="$Passed" failed="$Failed" notExecuted="$Skipped" $ExtraCounter />$RunInfo</ResultSummary>
</TestRun>
"@
    [IO.File]::WriteAllText((Join-Path $fixture $Name), $xml)
}
function Assert-Rejected([string]$Because) {
    $rejected = $false
    try { $null = & $parser -ResultsDirectory $fixture } catch { $rejected = $true }
    if (-not $rejected) { throw "Expected parser refusal: $Because" }
    $script:cases++
}
try {
    $null = [IO.Directory]::CreateDirectory($fixture)
    Write-Trx 'updates.trx'
    Write-Trx 'other.trx' -Assembly 'WolfConquer.Tests.dll' -Passed 1234
    $result = & $parser -ResultsDirectory $fixture
    if ($result.outcome -cne 'passed' -or $result.passed -ne 7 -or $result.failed -ne 0 -or $result.skipped -ne 2 -or
        $result.finishedAtUtc -cne ([DateTimeOffset]::Parse($finish).UtcDateTime.ToString('O'))) { throw 'Assembly-specific counts/time were not retained.' }
    $cases++
    Write-Trx 'updates.trx' -Outcome 'Failed' -Passed 6 -Failed 1
    $result = & $parser -ResultsDirectory $fixture
    if ($result.outcome -cne 'failed' -or $result.failed -ne 1 -or $result.passed -ne 6) { throw 'Failed tests were not retained.' }; $cases++
    $diagnostic = '<RunInfos><RunInfo outcome="Error"><Text>Synthetic xUnit [FAIL] diagnostic.</Text></RunInfo></RunInfos>'
    Write-Trx 'updates.trx' -Outcome 'Failed' -Passed 6 -Failed 1 -RunInfo $diagnostic
    $result = & $parser -ResultsDirectory $fixture
    if ($result.outcome -cne 'failed' -or $result.failed -ne 1 -or $result.passed -ne 6) { throw 'A complete failed run with a diagnostic was not retained.' }; $cases++
    Write-Trx 'updates.trx' -RunInfo $diagnostic; Assert-Rejected 'error diagnostic without a failed test'
    Write-Trx 'updates.trx' -Outcome 'Running'; Assert-Rejected 'nonterminal outcome'
    Write-Trx 'updates.trx' -ExtraCounter 'aborted="1"'; Assert-Rejected 'aborted run'
    Write-Trx 'updates.trx' -Passed 0 -Skipped 2; Assert-Rejected 'all skipped'
    Write-Trx 'updates.trx' -Outcome 'Failed'; Assert-Rejected 'failure without failed counter'
    Write-Trx 'updates.trx'; Write-Trx 'duplicate.trx'; Assert-Rejected 'ambiguous assembly results'
    Remove-Item -LiteralPath (Join-Path $fixture 'duplicate.trx')
    [IO.File]::WriteAllText((Join-Path $fixture 'updates.trx'), '<broken'); Assert-Rejected 'malformed XML'
    [IO.File]::WriteAllText((Join-Path $fixture 'updates.trx'), '<!DOCTYPE x [<!ENTITY x "bad">]><x>&x;</x>'); Assert-Rejected 'DTD'
    Write-Trx 'updates.trx' -Assembly 'Unrelated.Tests.dll'; Assert-Rejected 'missing updater identity'
    # Identical SDK-generated filenames are safe in separate project/framework directories.
    $null = [IO.Directory]::CreateDirectory((Join-Path $fixture 'WolfConquer.Tests/net9.0'))
    $null = [IO.Directory]::CreateDirectory((Join-Path $fixture 'WolfConquer.Updates.Tests/net9.0'))
    Write-Trx 'WolfConquer.Tests/net9.0/same-second.trx' -Assembly 'WolfConquer.Tests.dll' -Passed 1234
    Write-Trx 'WolfConquer.Updates.Tests/net9.0/same-second.trx'
    $result = & $parser -ResultsDirectory $fixture
    if ($result.outcome -cne 'passed' -or $result.passed -ne 7 -or $result.failed -ne 0 -or $result.skipped -ne 2) {
        throw 'Project-isolated same-name TRX files were not read independently.'
    }; $cases++
    [ordered]@{ outcome = 'passed'; cases = $cases; scope = 'Synthetic TRX parser fixtures only; no tests or network were run.' } | ConvertTo-Json
} finally {
    $resolved = [IO.Path]::GetFullPath($fixture)
    $prefix = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolved) -notmatch '^wolf-updater-trx-[0-9a-f]{32}$') { throw 'Unsafe fixture cleanup refused.' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
