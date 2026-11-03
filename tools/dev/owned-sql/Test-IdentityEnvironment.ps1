#Requires -Version 7.2
[CmdletBinding()]
param([Parameter(Mandatory)][string] $RepositoryRoot)

# Synthetic inspection/environment controls only. No process, port, DB, SQL Fact,
# compiler or genuine SQL TRX is created or executed by this control script.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Results.ps1')
. (Join-Path $PSScriptRoot 'Environment.ps1')
Import-Module (Join-Path ([IO.Path]::GetFullPath($RepositoryRoot)) 'tools/dev/FocusedCheckSupport.psm1') -Force
function Require-Review([bool] $Condition, [string] $Reason) { if (-not $Condition) { throw $Reason } }

function Assert-ReviewEnvironmentSnapshot($Expected, [string] $Phase) {
    foreach ($entry in @(
        [pscustomobject]@{ Name = 'WOLF_SQL_AUTH_FIXTURE_ENABLED'; Present = $Expected.EnabledPresent; Value = $Expected.Enabled },
        [pscustomobject]@{ Name = 'WOLF_SQL_AUTH_FIXTURE_ADMIN_CONNECTION'; Present = $Expected.ConnectionPresent; Value = $Expected.Connection }
    )) {
        $present = Test-Path -LiteralPath ('Env:' + $entry.Name)
        $actual = [Environment]::GetEnvironmentVariable($entry.Name, [EnvironmentVariableTarget]::Process)
        Require-Review ($present -eq $entry.Present) ('Process environment presence differs at ' + $Phase + '/' + $Expected.Mode + '.')
        if ($entry.Present) {
            Require-Review ($null -ne $actual -and [string]::Equals($actual, $entry.Value, [StringComparison]::Ordinal)) ('Process environment value differs at ' + $Phase + '/' + $Expected.Mode + '.')
        }
        else { Require-Review ($null -eq $actual) ('Absent process environment value differs at ' + $Phase + '/' + $Expected.Mode + '.') }
    }
}

function New-ReviewTrx([string] $Scenario) {
    $idA = 'aaaaaaaa-0000-0000-0000-000000000001'; $idB = 'bbbbbbbb-0000-0000-0000-000000000002'
    $execA = 'cccccccc-0000-0000-0000-000000000001'; $execB = 'dddddddd-0000-0000-0000-000000000002'
    $other = 'eeeeeeee-0000-0000-0000-000000000003'
    $classA = 'WolfConquer.Tests.ModernSqlAuthFileRoundtripTests'; $nameA = 'SqlFactA'
    if ($Scenario -eq 'ClassCase') { $classA = $classA.ToLowerInvariant() }
    if ($Scenario -eq 'MethodCase') { $nameA = $nameA.ToLowerInvariant() }
    $definitionExecution = if ($Scenario -eq 'MissingDefinitionExecution') { '' } else { "<Execution id=`"$execA`" />" }
    $definitionA = "<UnitTest id=`"$idA`">$definitionExecution<TestMethod className=`"$classA`" name=`"$nameA`" /></UnitTest>"
    $definitions = $definitionA + "<UnitTest id=`"$idB`"><Execution id=`"$execB`" /><TestMethod className=`"WolfConquer.Tests.ModernSqlAuthFileRoundtripTests`" name=`"SqlFactB`" /></UnitTest>"
    if ($Scenario -eq 'DuplicateDefinition') { $definitions += $definitionA }
    $entryExecA = if ($Scenario -eq 'WrongEntryExecution') { $other } else { $execA }
    $entries = "<TestEntry testId=`"$idA`" executionId=`"$entryExecA`" />"
    if ($Scenario -ne 'MissingEntry') { $entries += "<TestEntry testId=`"$idB`" executionId=`"$execB`" />" }
    $resultExecA = if ($Scenario -eq 'WrongResultExecution') { $other } else { $execA }
    $resultIdA = if ($Scenario -eq 'UndefinedResult') { $other } else { $idA }
    if ($Scenario -eq 'GuidCase') { $resultIdA = $resultIdA.ToUpperInvariant(); $resultExecA = $resultExecA.ToUpperInvariant() }
    $outcomeB = if ($Scenario -eq 'Skipped') { 'NotExecuted' } else { 'Passed' }
    $results = "<UnitTestResult testId=`"$resultIdA`" executionId=`"$resultExecA`" outcome=`"Passed`" />"
    $total = 1; $passed = 1
    if ($Scenario -ne 'MissingFact') {
        $results += "<UnitTestResult testId=`"$idB`" executionId=`"$execB`" outcome=`"$outcomeB`" />"; $total++
        if ($outcomeB -eq 'Passed') { $passed++ }
    }
    return @"
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
<Times finish="2026-11-02T00:00:01.0000000Z" />
<Results>$results</Results><TestDefinitions>$definitions</TestDefinitions><TestEntries>$entries</TestEntries>
<ResultSummary outcome="Completed"><Counters total="$total" executed="$total" passed="$passed" failed="0" error="0" timeout="0" aborted="0" inProgress="0" pending="0" passedButRunAborted="0" /></ResultSummary>
</TestRun>
"@
}

$controlNonce = [Guid]::NewGuid().ToString('N')
$controlRoot = Join-Path ([IO.Path]::GetTempPath()) ('wolf-sql-review-' + $controlNonce)
if (Test-Path -LiteralPath $controlRoot) { throw 'The synthetic review directory must be fresh.' }
[void][IO.Directory]::CreateDirectory($controlRoot)
$files = [Collections.Generic.List[string]]::new(); $passedControls = 0
$outerEnabled = [Environment]::GetEnvironmentVariable('WOLF_SQL_AUTH_FIXTURE_ENABLED')
$outerConnection = [Environment]::GetEnvironmentVariable('WOLF_SQL_AUTH_FIXTURE_ADMIN_CONNECTION')
$outerExitVariable = Get-Variable -Name LASTEXITCODE -Scope Global -ErrorAction SilentlyContinue
$outerHadExit = $null -ne $outerExitVariable
$outerExitValue = if ($outerHadExit) { $outerExitVariable.Value } else { $null }
try {
    foreach ($scenario in @('Valid', 'GuidCase', 'ClassCase', 'MethodCase', 'DuplicateDefinition', 'MissingDefinitionExecution',
        'WrongEntryExecution', 'MissingEntry', 'WrongResultExecution', 'UndefinedResult', 'MissingFact', 'Skipped')) {
        $path = Join-Path $controlRoot ($scenario + '.trx'); $files.Add($path)
        [IO.File]::WriteAllText($path, (New-ReviewTrx $scenario), [Text.UTF8Encoding]::new($false))
        $accepted = $false; $count = 0
        try { $count = Assert-OwnedSqlTrx $path @('SqlFactA', 'SqlFactB'); $accepted = $true } catch { }
        $expected = $scenario -in @('Valid', 'GuidCase')
        Require-Review ($accepted -eq $expected) 'Synthetic SQL TRX identity acceptance differed from its expected result.'
        if ($expected) { Require-Review ($count -eq 2) 'Synthetic inspector count differs.' }
        $passedControls++
    }

    foreach ($scenario in @('Absent', 'Success', 'Throw', 'Nonzero', 'PresentNull', 'PresentEmpty')) {
        # Detached expected data stays in this caller and is never passed to the
        # callback. The callback branches only on its explicit context.Mode.
        $expectedEnvironment = [pscustomobject]@{
            Mode = $scenario; EnabledPresent = ($scenario -ne 'Absent'); ConnectionPresent = ($scenario -ne 'Absent')
            Enabled = $(if ($scenario -eq 'Absent') { $null } elseif ($scenario -eq 'PresentEmpty') { '' } else { 'previous-synthetic-setting' })
            Connection = $(if ($scenario -eq 'Absent') { $null } elseif ($scenario -eq 'PresentEmpty') { '' } else { 'previous-synthetic-connection' })
            ExitPresent = ($scenario -ne 'Absent'); ExitValue = $(if ($scenario -eq 'PresentNull') { $null } else { 137 })
        }
        Set-OwnedSqlFixtureProcessValue 'WOLF_SQL_AUTH_FIXTURE_ENABLED' $expectedEnvironment.Enabled
        Set-OwnedSqlFixtureProcessValue 'WOLF_SQL_AUTH_FIXTURE_ADMIN_CONNECTION' $expectedEnvironment.Connection
        Assert-ReviewEnvironmentSnapshot $expectedEnvironment 'setup'
        if (-not $expectedEnvironment.ExitPresent) { Remove-Variable -Name LASTEXITCODE -Scope Global -ErrorAction SilentlyContinue }
        else { $global:LASTEXITCODE = $expectedEnvironment.ExitValue }
        $cleanup = [Collections.Generic.List[Exception]]::new(); $state = [pscustomobject]@{ Called = $false; Mode = $scenario }
        $threw = $false
        try {
            Invoke-OwnedSqlQualificationCallback {
                param($context)
                Require-Review ([Environment]::GetEnvironmentVariable('WOLF_SQL_AUTH_FIXTURE_ENABLED') -ceq '1') 'Callback opt-in is missing.'
                Require-Review ([Environment]::GetEnvironmentVariable('WOLF_SQL_AUTH_FIXTURE_ADMIN_CONNECTION') -ceq 'synthetic-review-only') 'Callback connection differs.'
                Require-Review ($global:LASTEXITCODE -eq 0) 'Callback exit state was not initialized.'
                $context.Called = $true
                if ($context.Mode -ceq 'Throw') { throw [InvalidOperationException]::new('Synthetic callback failure.') }
                if ($context.Mode -ceq 'Nonzero') { $global:LASTEXITCODE = 9 }
            } $state 'synthetic-review-only' $cleanup
        }
        catch { $threw = $true }
        Require-Review ($state.Called -and $threw -eq ($expectedEnvironment.Mode -in @('Throw', 'Nonzero'))) ('Callback success/failure propagation differed at ' + $expectedEnvironment.Mode + '.')
        Require-Review ($cleanup.Count -eq 0) 'Ordinary callback restoration produced cleanup failures.'
        # These are checked immediately after return/throw, before any later phase.
        Assert-ReviewEnvironmentSnapshot $expectedEnvironment 'immediate-return'
        $afterExit = Get-Variable -Name LASTEXITCODE -Scope Global -ErrorAction SilentlyContinue
        if (-not $expectedEnvironment.ExitPresent) { Require-Review ($null -eq $afterExit) 'Absent LASTEXITCODE was created.' }
        else {
            Require-Review ($null -ne $afterExit) 'Existing LASTEXITCODE was removed.'
            Require-Review ($afterExit.Value -eq $expectedEnvironment.ExitValue) 'Previous LASTEXITCODE value changed.'
        }
        $passedControls++
    }
}
finally {
    try { Set-OwnedSqlFixtureProcessValue 'WOLF_SQL_AUTH_FIXTURE_ENABLED' $outerEnabled }
    finally {
        try { Set-OwnedSqlFixtureProcessValue 'WOLF_SQL_AUTH_FIXTURE_ADMIN_CONNECTION' $outerConnection }
        finally {
            try {
                if ($outerHadExit) { $global:LASTEXITCODE = $outerExitValue }
                elseif (Get-Variable -Name LASTEXITCODE -Scope Global -ErrorAction SilentlyContinue) { Remove-Variable -Name LASTEXITCODE -Scope Global -ErrorAction Stop }
            }
            finally {
                $full = [IO.Path]::GetFullPath($controlRoot)
                if ([IO.Path]::GetDirectoryName($full) -cne [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath([IO.Path]::GetTempPath())) -or
                    [IO.Path]::GetFileName($full) -cne ('wolf-sql-review-' + $controlNonce) -or
                    ((Get-Item -LiteralPath $full -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Synthetic cleanup root changed; retained.' }
                foreach ($file in $files) {
                    if ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($file)) -cne $full -or
                        ((Get-Item -LiteralPath $file -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Synthetic cleanup path changed; retained.' }
                    [IO.File]::Delete($file)
                }
                # No recursive delete: any unexpected extra entry retains the root.
                [IO.Directory]::Delete($full, $false)
            }
        }
    }
}
Write-Host ('Synthetic SQL identity/environment controls passed: {0}. No SQL/DB execution is proved.' -f $passedControls)
