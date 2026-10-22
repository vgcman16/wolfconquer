#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot '../..'),
    [string]$ServiceRoot,
    [switch]$SkipServiceProbe,
    [switch]$SkipUpdaterProbe,
    [uri]$UpdaterHealthUri = 'http://127.0.0.1:48237/health'
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($RepositoryRoot)
if ($ServiceRoot -and -not [IO.Path]::IsPathFullyQualified($ServiceRoot)) {
    throw 'An explicit ServiceRoot must be an absolute path.'
}
$serviceRootPath = $(if ($ServiceRoot) { [IO.Path]::GetFullPath($ServiceRoot) } else { $root })
$comparison = $(if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal })
$rootPrefix = $serviceRootPath.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$check = [ordered]@{ state = 'unknown'; reason = 'No saved local check.'; startedAtUtc = $null
    finishedAtUtc = $null; exitCode = $null; steps = @() }
$statusPath = Join-Path $root '.artifacts/local-dev/last-check.json'
try {
    if (Test-Path -LiteralPath $statusPath -PathType Leaf) {
        if ((Get-Item -LiteralPath $statusPath).Length -gt 1MB) { throw 'Status is too large.' }
        $saved = Get-Content -LiteralPath $statusPath -Raw | ConvertFrom-Json
        if ($saved.schemaVersion -ne 1 -or -not $root.Equals($saved.repositoryRoot, $comparison) -or
            $saved.outcome -notin @('running', 'passed', 'failed')) { throw 'Invalid status identity.' }
        $requiredSteps = @('restore', 'build', 'test')
        $suiteProperty = $saved.PSObject.Properties['suite']
        if ($null -ne $suiteProperty) {
            if ($suiteProperty.Value -cne 'windows-local-v2') { throw 'Unknown local-check suite.' }
            $requiredSteps += @('launcher-restore', 'launcher-build', 'launcher-test', 'updater-status-test', 'updater-smoke-parser-test')
        }
        if ($saved.outcome -eq 'passed' -and ($saved.exitCode -ne 0 -or $saved.steps.Count -ne $requiredSteps.Count -or
            (($saved.steps.name -join ',') -cne ($requiredSteps -join ',')) -or
            @($saved.steps | Where-Object { $_.outcome -ne 'passed' -or $_.exitCode -ne 0 }).Count -ne 0)) {
            throw 'Incomplete checks cannot be reported as passed.'
        }
        if ($saved.outcome -eq 'failed' -and ($null -eq $saved.exitCode -or $saved.exitCode -eq 0)) {
            throw 'Failed checks require a nonzero exit code.'
        }
        $steps = @($saved.steps | ForEach-Object {
            foreach ($log in @($_.stdoutLog, $_.stderrLog)) {
                if ($log -notmatch '^\.artifacts/local-dev/[^/\\]+\.(stdout|stderr)\.log$') { throw 'Invalid log reference.' }
            }
            [ordered]@{ name = $_.name; outcome = $_.outcome; exitCode = $_.exitCode
                stdoutLog = $_.stdoutLog; stderrLog = $_.stderrLog }
        })
        $check = [ordered]@{ state = $saved.outcome; reason = 'Saved check only; not deployment or live-client proof.'
            startedAtUtc = $saved.startedAtUtc; finishedAtUtc = $saved.finishedAtUtc
            exitCode = $saved.exitCode; steps = $steps }
    }
}
catch { $check.reason = 'Saved check is unreadable, malformed, or belongs to another repository.' }

$services = [ordered]@{ state = 'unknown'; reason = 'Service discovery is Windows-only.'; items = @() }
if ($SkipServiceProbe) { $services.reason = 'Service discovery was explicitly skipped.' }
elseif (-not [IO.Directory]::Exists($serviceRootPath)) { $services.reason = 'Selected service root is unavailable.' }
elseif ($IsWindows) {
    try {
        $names = @('WolfConquer.AccountServer', 'WolfConquer.Gateway', 'WolfConquer.LoginWeb', 'WolfConquer.WorldServer')
        $owned = @()
        foreach ($process in @(Get-CimInstance -ClassName Win32_Process -ErrorAction Stop)) {
            $name = [IO.Path]::GetFileNameWithoutExtension([string]$process.Name)
            $service = $null
            if ($name -in $names -and $process.ExecutablePath) {
                $path = [IO.Path]::GetFullPath($process.ExecutablePath)
                if ($path.StartsWith($rootPrefix, $comparison)) { $service = $name }
            }
            elseif ($name -eq 'dotnet' -and $process.CommandLine) {
                $tokens = @([regex]::Matches($process.CommandLine, '"[^"]*"|[^\s"]+') | ForEach-Object { $_.Value.Trim('"') })
                $index = 1
                if ($tokens.Count -gt 1 -and $tokens[1] -eq 'exec') { $index = 2 }
                if ($tokens.Count -gt $index -and [IO.Path]::IsPathFullyQualified($tokens[$index])) {
                    $path = [IO.Path]::GetFullPath($tokens[$index])
                    $candidate = [IO.Path]::GetFileNameWithoutExtension($path)
                    if ($candidate -in $names -and [IO.Path]::GetExtension($path) -eq '.dll' -and
                        $path.StartsWith($rootPrefix, $comparison)) { $service = $candidate }
                }
            }
            if ($service) { $owned += [ordered]@{ service = $service; processId = [int]$process.ProcessId; listeners = @() } }
        }
        $tcpReason = $null
        try {
            $listeners = @(Get-NetTCPConnection -State Listen -ErrorAction Stop)
            foreach ($entry in $owned) {
                $entry.listeners = @($listeners | Where-Object { $_.OwningProcess -eq $entry.processId } |
                    ForEach-Object { [ordered]@{ address = $_.LocalAddress; port = [int]$_.LocalPort } })
            }
        }
        catch { $tcpReason = 'TCP listener details unavailable; process identity only.' }
        $services = [ordered]@{ state = 'available'; reason = $tcpReason; items = $owned }
    }
    catch { $services.reason = 'Process discovery unavailable; no service health was inferred.' }
}
$updater = & (Join-Path $PSScriptRoot 'Get-UpdaterStatus.ps1') -RepositoryRoot $root -SkipUpdaterProbe:$SkipUpdaterProbe -UpdaterHealthUri $UpdaterHealthUri
[ordered]@{ schemaVersion = 1; observedAtUtc = [DateTime]::UtcNow.ToString('O'); repositoryRoot = $root; serviceRoot = $serviceRootPath
    lastCheck = $check; services = $services; updater = $updater } | ConvertTo-Json -Depth 8
