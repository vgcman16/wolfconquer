#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot '../..'),
    [Alias('Profile')][ValidateSet('Quick', 'Ticker', 'GhostReviver', 'PackageHistory', 'Affected')][string]$CheckProfile = 'Quick',
    [string]$Filter,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [ValidateRange(0, 3600)][int]$HangSeconds = 0,
    [switch]$PhaseTiming,
    [Alias('DryRun')][switch]$Plan
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'FocusedCheckSupport.psm1') -Force -DisableNameChecking
$lease = $null; $hostLease = $null; $ownsHostLease = $false; $status = $null; $exitCode = 1
try {
    $root = [IO.Path]::GetFullPath($RepositoryRoot)
    $project = Join-Path $root 'tests/WolfConquer.Tests/WolfConquer.Tests.csproj'
    if (-not (Test-Path -LiteralPath $project -PathType Leaf)) { throw 'RepositoryRoot must contain the Core test project.' }
    $profiles = Import-PowerShellDataFile (Join-Path $PSScriptRoot 'FocusedCheckProfiles.psd1')
    $selectors = @()
    if ($CheckProfile -eq 'Affected') {
        if ([string]::IsNullOrWhiteSpace($Filter)) { throw 'Affected requires an explicit reviewed -Filter.' }
        $description = 'Explicit affected filter; review dependency neighbors separately.'
        if ($HangSeconds -eq 0) { $HangSeconds = 180 }
    }
    else {
        if ($Filter) { throw 'Use -Profile Affected when supplying -Filter.' }
        $Filter = $profiles[$CheckProfile].Filter
        $selectors = @($Filter.Split('|'))
        $description = $profiles[$CheckProfile].Description
        if ($HangSeconds -eq 0) { $HangSeconds = $profiles[$CheckProfile].HangSeconds }
    }
    $runId = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
    $artifacts = Join-Path $root '.artifacts/local-focused'
    $runRoot = Join-Path $artifacts $runId
    $results = Join-Path $runRoot 'results'
    $commands = @(
        [ordered]@{ name = 'build'; arguments = @('build', $project, '-c', $Configuration, '--nologo', '-m:1', '-nodeReuse:false', '/p:UseSharedCompilation=false') },
        [ordered]@{ name = 'test'; arguments = @('test', $project, '-c', $Configuration, '--no-build', '--no-restore', '--nologo', '--filter', $Filter,
            '--logger', 'trx;LogFileName=focused.trx', '--results-directory', $results, '--blame-hang-timeout', ($HangSeconds.ToString() + 's'), '--blame-hang-dump-type', 'none') }
    )
    $phaseEnabled = $PhaseTiming.IsPresent -or [Environment]::GetEnvironmentVariable('WOLF_TEST_PHASE_TIMING') -eq '1'
    $status = [ordered]@{ schemaVersion = 1; suite = 'windows-focused-v1'; fullGate = $false; outcome = 'not-run'; profile = $CheckProfile
        description = $description; filter = $Filter; requiredSelectors = $selectors; phaseTiming = $phaseEnabled; repositoryRoot = $root; runId = $runId
        commands = $commands; steps = @(); counts = $null; startedAtUtc = $null; finishedAtUtc = $null; error = $null }
    if ($Plan) { $status | ConvertTo-Json -Depth 8; exit 0 }

    if (-not $IsWindows) { throw 'This runner requires Windows.' }
    $hostLease = [Threading.Mutex]::new($false, 'Global\WolfConquer.FocusedChecks')
    try { $ownsHostLease = $hostLease.WaitOne(0) }
    catch [Threading.AbandonedMutexException] { $ownsHostLease = $true }
    if (-not $ownsHostLease) { throw 'Another focused check owns the host lease. Leave it running.' }
    Assert-FocusedHostIdle
    $shared = Join-Path $root '.artifacts/local-dev'
    Assert-FocusedArtifactPath $root $shared
    Assert-FocusedArtifactPath $root $runRoot
    [IO.Directory]::CreateDirectory($shared) | Out-Null
    $lease = [IO.File]::Open((Join-Path $shared 'check.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    Assert-FocusedHostIdle
    [IO.Directory]::CreateDirectory($results) | Out-Null
    $statusPath = Join-Path $artifacts 'last-focused-check.json'
    $dotnet = (Get-Command dotnet -CommandType Application -ErrorAction Stop).Source
    $engine = Join-Path $PSHOME 'pwsh.exe'
    $version = [Diagnostics.FileVersionInfo]::GetVersionInfo($engine)
    if ($version.ProductMajorPart -ne 7 -or $version.ProductName -notmatch '^PowerShell(?: Core)?$') { throw 'A measured PowerShell 7 engine is required.' }
    $environment = @{ DOTNET_CLI_TELEMETRY_OPTOUT = '1'; DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
        WOLFCONQUER_NATIVE_CONSOLE_PROBE_PWSH = $engine
        WOLFCONQUER_NATIVE_CONSOLE_PROBE_PWSH_SHA256 = (Get-FileHash -LiteralPath $engine -Algorithm SHA256).Hash }
    if ($PhaseTiming) { $environment.WOLF_TEST_PHASE_TIMING = '1' }
    $status.outcome = 'running'; $status.startedAtUtc = [DateTime]::UtcNow.ToString('O')
    Write-FocusedStatus $root $statusPath $status
    $exitCode = 0
    foreach ($command in $commands) {
        $prefix = Join-Path $runRoot $command.name
        $result = Invoke-FocusedCommand $dotnet $command.arguments $root $prefix $environment
        $status.steps += [ordered]@{ name = $command.name; exitCode = $result.exitCode; seconds = $result.seconds; logPrefix = $prefix }
        Write-FocusedStatus $root $statusPath $status
        Write-Host ($command.name + ': exit ' + $result.exitCode + '; ' + $result.seconds + 's; ' + $prefix)
        if ($result.exitCode -ne 0) { $exitCode = $result.exitCode; break }
    }
    if ($exitCode -eq 0) {
        $status.counts = Read-FocusedTrx (Join-Path $results 'focused.trx') $selectors
        if ($status.counts.Failed -gt 0 -or $status.counts.Other -gt 0) { $exitCode = 1 }
    }
    $status.outcome = $(if ($exitCode -eq 0) { 'passed' } else { 'failed' })
    $status.finishedAtUtc = [DateTime]::UtcNow.ToString('O')
    Write-FocusedStatus $root $statusPath $status
    Write-Host ('FOCUSED ONLY: ' + $status.outcome + '. Full publication gate: tools/dev/Invoke-LocalCheck.ps1')
}
catch {
    $exitCode = 1
    [Console]::Error.WriteLine($_.Exception.Message)
    if ($lease -and $status -and (Test-Path -LiteralPath $runRoot)) {
        $status.outcome = 'failed'; $status.error = $_.Exception.Message; $status.finishedAtUtc = [DateTime]::UtcNow.ToString('O')
        Write-FocusedStatus $root (Join-Path $artifacts 'last-focused-check.json') $status
    }
}
finally {
    if ($lease) { $lease.Dispose() }
    if ($ownsHostLease) { $hostLease.ReleaseMutex() }
    if ($hostLease) { $hostLease.Dispose() }
}
exit $exitCode
