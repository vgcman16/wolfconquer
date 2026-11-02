#requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string]$RepositoryRoot)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'FocusedCheckSupport.psm1') -Force -DisableNameChecking
$runner = Join-Path $PSScriptRoot 'Invoke-FocusedCheck.ps1'
foreach ($file in @($runner, (Join-Path $PSScriptRoot 'FocusedCheckSupport.psm1'), $PSCommandPath)) {
    $errors = $null; $tokens = $null
    $null = [Management.Automation.Language.Parser]::ParseFile($file, [ref]$tokens, [ref]$errors)
    if ($errors.Count) { throw ($errors | Out-String) }
}
foreach ($planProfile in @('Quick', 'Ticker', 'GhostReviver', 'PackageHistory')) {
    $plan = & $runner -RepositoryRoot $RepositoryRoot -Profile $planProfile -Plan | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or $plan.fullGate -or $plan.outcome -ne 'not-run' -or $plan.commands.Count -ne 2) { throw 'Focused plan contract changed.' }
    if ($plan.commands[1].arguments -notcontains '--no-build' -or $plan.commands[1].arguments -notcontains '--filter') { throw 'Focused tests must reuse the build and retain the filter.' }
    if (-not $plan.requiredSelectors.Count) { throw 'Built-in profiles must retain every intended selector.' }
}
$filter = 'FullyQualifiedName~Ghost|FullyQualifiedName~Reviver'
$affected = & $runner -RepositoryRoot $RepositoryRoot -Profile Affected -Filter $filter -Plan | ConvertFrom-Json
if ($affected.filter -ne $filter -or $affected.commands[1].arguments -notcontains $filter) { throw 'Filter was changed or split into shell text.' }
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('wolf-focused-plan-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($temporary) | Out-Null
try {
    $trx = Join-Path $temporary 'sample.trx'
    $summary = '<ResultSummary outcome="Completed"><Counters total="3" passed="1" failed="1" error="0" timeout="0" aborted="0" inProgress="0" pending="0" passedButRunAborted="0"/></ResultSummary>'
    $valid = '<TestRun><Results><UnitTestResult outcome="Passed" testName="Example.Codec.Passes"/><UnitTestResult outcome="Failed"/><UnitTestResult outcome="NotExecuted"/></Results>' + $summary + '</TestRun>'
    [IO.File]::WriteAllText($trx, $valid)
    $counts = Read-FocusedTrx $trx
    if ($counts.Passed -ne 1 -or $counts.Failed -ne 1 -or $counts.NotExecuted -ne 1) { throw 'Raw TRX outcomes were not preserved.' }
    $null = Read-FocusedTrx $trx @('FullyQualifiedName~Example.Codec')
    $missingSelectorRejected = $false
    try { $null = Read-FocusedTrx $trx @('FullyQualifiedName~Example.Codec', 'FullyQualifiedName~Missing.Class') } catch { $missingSelectorRejected = $true }
    if (-not $missingSelectorRejected) { throw 'A partially missing built-in profile was accepted.' }
    foreach ($text in @('<TestRun/>', '<TestRun><Results><UnitTestResult outcome="NotExecuted"/></Results>' + $summary + '</TestRun>',
        '<!DOCTYPE TestRun [<!ENTITY x "x">]><TestRun/>',
        $valid.Replace('outcome="Completed"', 'outcome="Aborted"'),
        $valid.Replace('total="3"', 'total="9"'), $valid.Replace('aborted="0"', 'aborted="1"'),
        $valid.Replace('passed="1"', 'passed="2"'))) {
        [IO.File]::WriteAllText($trx, $text)
        $rejected = $false
        try { $null = Read-FocusedTrx $trx } catch { $rejected = $true }
        if (-not $rejected) { throw 'Empty, skipped-only, partial, malformed or aborted input was accepted.' }
    }
}
finally {
    $resolved = [IO.Path]::GetFullPath($temporary)
    if ([IO.Path]::GetDirectoryName($resolved) -ne [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) -or
        -not [IO.Path]::GetFileName($resolved).StartsWith('wolf-focused-plan-')) { throw 'Unexpected temporary cleanup target.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
Write-Host 'Focused plans/TRX controls passed. No build or test host was started.'
