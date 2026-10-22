#requires -Version 7.0
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('wolf-updater-status-' + [guid]::NewGuid().ToString('N'))
$receiptDirectory = Join-Path $fixtureRoot '.artifacts/local-dev'
$statusScript = Join-Path $PSScriptRoot 'Get-LocalStatus.ps1'
$cases = 0
function Assert-Equal($Expected, $Actual, [string]$Because) {
    if ($Expected -cne $Actual) { throw "Assertion failed: $Because (expected $Expected; got $Actual)." }
}
function New-CheckReceipt {
    return [ordered]@{ schemaVersion = 1; outcome = 'passed'; checkedAtUtc = [DateTime]::UtcNow.ToString('O')
        productId = 'wolfconquer.client'; channel = 'stable'; sequence = 7; indexValid = $true
        manifestValid = $true; downloadsReachable = $true; reason = 'verified' }
}
function Save-Receipt([string]$Name, $Value) {
    [IO.File]::WriteAllText((Join-Path $receiptDirectory $Name), ($Value | ConvertTo-Json -Depth 4), [Text.UTF8Encoding]::new($false))
}
function Read-Status {
    return ((& $statusScript -RepositoryRoot $fixtureRoot -SkipServiceProbe -SkipUpdaterProbe) | ConvertFrom-Json)
}
try {
    $null = [IO.Directory]::CreateDirectory($receiptDirectory)
    $status = Read-Status
    Assert-Equal 'unknown' $status.updater.health.state 'skip never infers online'
    Assert-Equal 'unknown' $status.updater.lastCheck.state 'missing signed receipt'
    Assert-Equal 'unknown' $status.updater.lastSmokeTest.state 'missing smoke receipt'; $cases++

    Save-Receipt 'last-updater-check.json' (New-CheckReceipt)
    Save-Receipt 'last-updater-smoke.json' ([ordered]@{ outcome = 'passed'; finishedAtUtc = [DateTime]::UtcNow.ToString('O'); passed = 7; failed = 0; skipped = 1 })
    $status = Read-Status
    Assert-Equal 'passed' $status.updater.lastCheck.state 'fresh actual check receipt'
    Assert-Equal 'passed' $status.updater.lastSmokeTest.state 'separate smoke receipt'
    Assert-Equal 'unknown' $status.updater.health.state 'receipts do not imply online'
    Assert-Equal $true $status.updater.lastCheck.downloadsReachable 'endpoint reachability retained'; $cases++

    $receipt = New-CheckReceipt; $receipt.outcome = 'failed'; $receipt.downloadsReachable = $false; $receipt.reason = 'downloads-unreachable'
    Save-Receipt 'last-updater-check.json' $receipt
    $status = Read-Status
    Assert-Equal 'failed' $status.updater.lastCheck.state 'failed endpoint check'
    Assert-Equal 'passed' $status.updater.lastSmokeTest.state 'smoke result is independent'; $cases++

    $receipt = New-CheckReceipt; $receipt.checkedAtUtc = [DateTime]::UtcNow.AddMinutes(-11).ToString('O')
    Save-Receipt 'last-updater-check.json' $receipt
    Save-Receipt 'last-updater-smoke.json' ([ordered]@{ schemaVersion = 1; outcome = 'passed'; finishedAtUtc = [DateTime]::UtcNow.AddMinutes(-12).ToString('O'); passed = 7; failed = 0; skipped = 0 })
    $status = Read-Status
    Assert-Equal 'stale' $status.updater.lastCheck.state 'old signed receipt'
    Assert-Equal 'passed' $status.updater.lastCheck.outcome 'stale preserves historical outcome'
    Assert-Equal 'stale' $status.updater.lastSmokeTest.state 'old smoke receipt'; $cases++

    $receipt = New-CheckReceipt; $receipt.checkedAtUtc = [DateTime]::UtcNow.AddMinutes(3).ToString('O')
    Save-Receipt 'last-updater-check.json' $receipt
    Assert-Equal 'unknown' (Read-Status).updater.lastCheck.state 'future time cannot remain fresh forever'; $cases++

    foreach ($mutation in @('contradictory', 'string-bool', 'string-schema', 'fractional-sequence', 'unsafe-reason', 'unsafe-product')) {
        $receipt = New-CheckReceipt
        switch ($mutation) {
            contradictory { $receipt.downloadsReachable = $false }
            string-bool { $receipt.indexValid = 'true' }
            string-schema { $receipt.schemaVersion = '1' }
            fractional-sequence { $receipt.sequence = 1.5 }
            unsafe-reason { $receipt.reason = 'C:\private\secret-key?https://invalid.example/' }
            unsafe-product { $receipt.productId = 'C:\private\client' }
        }
        Save-Receipt 'last-updater-check.json' $receipt
        $status = Read-Status
        Assert-Equal 'unknown' $status.updater.lastCheck.state $mutation
        $serialized = $status.updater | ConvertTo-Json -Depth 6
        if ($serialized -match 'secret-key|invalid\.example|C:\\private') { throw 'Unsafe input leaked into updater status.' }
        $cases++
    }

    $receipt = New-CheckReceipt; $receipt.outcome = 'failed'; $receipt.reason = 'index-invalid'; $receipt.sequence = $null
    $receipt.indexValid = $false; $receipt.manifestValid = $false; $receipt.downloadsReachable = $false
    Save-Receipt 'last-updater-check.json' $receipt
    Assert-Equal 'failed' (Read-Status).updater.lastCheck.state 'failed before sequence discovery is valid'; $cases++

    foreach ($invalidJson in @('{broken', ((New-CheckReceipt | ConvertTo-Json -Compress).TrimEnd('}') + ',"reason":"verified"}'), (' ' * 32769))) {
        [IO.File]::WriteAllText((Join-Path $receiptDirectory 'last-updater-check.json'), $invalidJson)
        Assert-Equal 'unknown' (Read-Status).updater.lastCheck.state 'malformed, duplicate or oversized receipt'; $cases++
    }
    Save-Receipt 'last-updater-smoke.json' ([ordered]@{ outcome = 'passed'; finishedAtUtc = [DateTime]::UtcNow.ToString('O'); passed = 7; failed = 1; skipped = 0 })
    Assert-Equal 'unknown' (Read-Status).updater.lastSmokeTest.state 'failed tests cannot be labelled passed'; $cases++

    $rejected = $false
    try { $null = & $statusScript -RepositoryRoot $fixtureRoot -SkipServiceProbe -SkipUpdaterProbe -UpdaterHealthUri 'http://192.0.2.1/health' }
    catch { $rejected = $true }
    Assert-Equal $true $rejected 'nonloopback configuration refuses even when probing is skipped'; $cases++

    $steps = @('restore', 'build', 'test', 'launcher-restore', 'launcher-build', 'launcher-test', 'updater-status-test', 'updater-smoke-parser-test')
    $check = [ordered]@{ schemaVersion = 1; suite = 'windows-local-v2'; repositoryRoot = $fixtureRoot; outcome = 'passed'
        exitCode = 0; startedAtUtc = [DateTime]::UtcNow.AddSeconds(-2).ToString('O'); finishedAtUtc = [DateTime]::UtcNow.ToString('O')
        steps = @($steps | ForEach-Object { [ordered]@{ name = $_; outcome = 'passed'; exitCode = 0
            stdoutLog = ".artifacts/local-dev/fixture-$_.stdout.log"; stderrLog = ".artifacts/local-dev/fixture-$_.stderr.log" } }) }
    Save-Receipt 'last-check.json' $check
    Assert-Equal 'passed' (Read-Status).lastCheck.state 'complete Windows suite is recognized'; $cases++
    $check.steps = @($check.steps | Select-Object -First 3)
    Save-Receipt 'last-check.json' $check
    Assert-Equal 'unknown' (Read-Status).lastCheck.state 'v2 cannot pass without launcher and parser steps'; $cases++
    $check.Remove('suite')
    Save-Receipt 'last-check.json' $check
    Assert-Equal 'passed' (Read-Status).lastCheck.state 'historical three-step receipts remain readable'; $cases++
    [ordered]@{ outcome = 'passed'; cases = $cases; networkProbes = 0; scope = 'Synthetic receipt parsing only; HTTP and service probes skipped.' } | ConvertTo-Json
} finally {
    $resolvedFixture = [IO.Path]::GetFullPath($fixtureRoot)
    $tempPrefix = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolvedFixture.StartsWith($tempPrefix, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolvedFixture) -notmatch '^wolf-updater-status-[0-9a-f]{32}$') { throw 'Refusing unsafe fixture cleanup.' }
    if (Test-Path -LiteralPath $resolvedFixture) { Remove-Item -LiteralPath $resolvedFixture -Recurse -Force }
}
