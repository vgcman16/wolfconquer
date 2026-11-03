#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot '../..'),
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [string]$PowerShellPath,
    [string]$DotNetPath,
    [Alias('DryRun')][switch]$Plan
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-Application([string]$ExplicitPath, [string[]]$Names) {
    if ($ExplicitPath) { $path = [IO.Path]::GetFullPath($ExplicitPath) }
    else {
        $command = $Names | ForEach-Object { Get-Command $_ -CommandType Application -ErrorAction SilentlyContinue } | Select-Object -First 1
        if (-not $command) { throw "Required application not found: $($Names -join ', ')" }
        $path = $command.Source
    }
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Application file not found: $path" }
    return $path
}

function Assert-ArtifactPath([string]$Path) {
    $relative = [IO.Path]::GetRelativePath($root, $Path)
    if ([IO.Path]::IsPathRooted($relative) -or $relative.StartsWith('..')) { throw 'Artifact path escaped repository.' }
    if ((Get-Item -LiteralPath $root -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Repository root cannot be a reparse point.' }
    $current = $root
    foreach ($segment in $relative.Split([IO.Path]::DirectorySeparatorChar)) {
        $current = Join-Path $current $segment
        if ((Test-Path -LiteralPath $current) -and
            ((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'Local-check artifacts cannot traverse a reparse point.'
        }
    }
}

function Save-Status {
    Assert-ArtifactPath $statusPath
    $temporary = Join-Path $artifactRoot ('status-' + [guid]::NewGuid().ToString('N') + '.tmp')
    [IO.File]::WriteAllText($temporary, ($status | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
    [IO.File]::Move($temporary, $statusPath, $true)
}

function Save-UpdaterSmoke {
    # This consumes the solution test's terminal TRX; it never reruns Updates.Tests.
    $receipt = & (Join-Path $PSScriptRoot 'Read-UpdaterSmokeResult.ps1') -ResultsDirectory $solutionResults
    $path = Join-Path $artifactRoot 'last-updater-smoke.json'
    Assert-ArtifactPath $path
    $temporary = Join-Path $artifactRoot ('smoke-' + [guid]::NewGuid().ToString('N') + '.tmp')
    [IO.File]::WriteAllText($temporary, ($receipt | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
    [IO.File]::Move($temporary, $path, $true)
}

$lease = $null
$hostLease = $null
$ownsHostLease = $false
$status = $null
$finalExit = 1
try {
    $root = [IO.Path]::GetFullPath($RepositoryRoot)
    $solution = Join-Path $root 'WolfConquer.sln'
    if (-not (Test-Path -LiteralPath $solution -PathType Leaf)) { throw 'RepositoryRoot must contain WolfConquer.sln.' }
    $launcher = Join-Path $root 'tests/WolfConquer.Launcher.Tests/WolfConquer.Launcher.Tests.csproj'
    if (-not (Test-Path -LiteralPath $launcher -PathType Leaf)) { throw 'Windows checks require the separate launcher test project.' }
    $dotnet = Resolve-Application $DotNetPath @('dotnet.exe', 'dotnet')
    # Resolve the actual engine hosting this script, not a PATH app-execution
    # alias such as WindowsApps\pwsh.exe (whose file has no engine metadata).
    $engine = Join-Path $PSHOME $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })
    $pwsh = Resolve-Application $(if ($PowerShellPath) { $PowerShellPath } else { $engine }) @()
    $version = [Diagnostics.FileVersionInfo]::GetVersionInfo($pwsh)
    if ($version.ProductMajorPart -ne 7 -or $version.ProductName -notmatch '^PowerShell(?: Core)?$') {
        throw 'The native test probe requires a measured PowerShell 7 executable.'
    }
    $probeEnvironment = [ordered]@{
        WOLFCONQUER_NATIVE_CONSOLE_PROBE_PWSH = $pwsh
        WOLFCONQUER_NATIVE_CONSOLE_PROBE_PWSH_SHA256 = (Get-FileHash -LiteralPath $pwsh -Algorithm SHA256).Hash
    }
    $artifactRoot = Join-Path $root '.artifacts/local-dev'
    $statusPath = Join-Path $artifactRoot 'last-check.json'
    $runId = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
    $solutionResults = Join-Path $artifactRoot ($runId + '-solution-results')
    $launcherResults = Join-Path $artifactRoot ($runId + '-launcher-results')
    $commands = @(
        [ordered]@{ name = 'restore'; executable = $dotnet; arguments = @('restore', $solution, '--nologo') },
        [ordered]@{ name = 'build'; executable = $dotnet; arguments = @('build', $solution, '--configuration', $Configuration, '--no-restore', '--nologo') },
        [ordered]@{ name = 'test'; executable = $dotnet; arguments = @('test', $solution, '-m:1', '-p:TestTfmsInParallel=false', '--configuration', $Configuration, '--no-build', '--no-restore', '--nologo', '--logger', 'trx', "-p:WolfConquerLocalResultsId=$runId") },
        [ordered]@{ name = 'launcher-restore'; executable = $dotnet; arguments = @('restore', $launcher, '--nologo') },
        [ordered]@{ name = 'launcher-build'; executable = $dotnet; arguments = @('build', $launcher, '--configuration', $Configuration, '--no-restore', '--nologo') },
        [ordered]@{ name = 'launcher-test'; executable = $dotnet; arguments = @('test', $launcher, '--configuration', $Configuration, '--no-build', '--no-restore', '--nologo', '--logger', 'trx', '--results-directory', $launcherResults) },
        [ordered]@{ name = 'updater-status-test'; executable = $pwsh; arguments = @('-NoProfile', '-File', (Join-Path $PSScriptRoot 'Test-UpdaterStatus.ps1')) },
        [ordered]@{ name = 'updater-smoke-parser-test'; executable = $pwsh; arguments = @('-NoProfile', '-File', (Join-Path $PSScriptRoot 'Test-UpdaterSmokeResult.ps1')) }
    )
    if ($Plan) {
        [ordered]@{ schemaVersion = 1; suite = 'windows-local-v2'; mode = 'plan'; outcome = 'not-run'; repositoryRoot = $root
            executable = $dotnet; commands = $commands; environment = $probeEnvironment; statusPath = $statusPath
            updaterSmokeSource = $solutionResults; updaterSmokeAssembly = 'WolfConquer.Updates.Tests.dll'
            solutionResultsLayout = 'project/target-framework' } |
            ConvertTo-Json -Depth 8
        exit 0
    }

    # Plan is read-only and never starts a process; only a real run needs the
    # output pump. Refuse a missing module before creating any owned artifacts.
    Import-Module (Join-Path $PSScriptRoot 'CheckLogPump.psm1') -Force

    # Without this opt-in import, omitting --results-directory would fall back to
    # project-local TestResults outside the owned run's artifact directory.
    if (-not (Test-Path -LiteralPath (Join-Path $root 'Directory.Build.targets') -PathType Leaf)) {
        throw 'Local checks require the repository Directory.Build.targets results-isolation import.'
    }
    # Share the focused/owned-SQL exclusion across separate checkouts too.
    $hostLease = [Threading.Mutex]::new($false, 'Global\WolfConquer.FocusedChecks')
    try { $ownsHostLease = $hostLease.WaitOne(0) }
    catch [Threading.AbandonedMutexException] { $ownsHostLease = $true }
    if (-not $ownsHostLease) { throw 'Another local, focused or SQL check owns the host lease. Leave it running.' }
    Assert-ArtifactPath $artifactRoot
    [IO.Directory]::CreateDirectory($artifactRoot) | Out-Null
    $lockPath = Join-Path $artifactRoot 'check.lock'
    Assert-ArtifactPath $lockPath
    $lease = [IO.File]::Open($lockPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    foreach ($results in @($solutionResults, $launcherResults)) {
        Assert-ArtifactPath $results
        if (Test-Path -LiteralPath $results) { throw 'Fresh local-check results directory already exists.' }
        [IO.Directory]::CreateDirectory($results) | Out-Null
    }
    $status = [ordered]@{ schemaVersion = 1; suite = 'windows-local-v2'; repositoryRoot = $root; configuration = $Configuration
        runId = $runId; startedAtUtc = [DateTime]::UtcNow.ToString('O'); finishedAtUtc = $null
        outcome = 'running'; exitCode = $null; steps = @(); error = $null }
    Save-Status
    $finalExit = 0
    foreach ($command in $commands) {
        $prefix = '.artifacts/local-dev/' + $runId + '-' + $command.name
        $stdoutPath = Join-Path $root ($prefix + '.stdout.log')
        $stderrPath = Join-Path $root ($prefix + '.stderr.log')
        Assert-ArtifactPath $stdoutPath
        Assert-ArtifactPath $stderrPath
        $start = [Diagnostics.ProcessStartInfo]::new($command.executable)
        $start.WorkingDirectory = $root
        $start.UseShellExecute = $false
        $start.CreateNoWindow = $true
        $start.RedirectStandardOutput = $true
        $start.RedirectStandardError = $true
        foreach ($argument in $command.arguments) { $start.ArgumentList.Add($argument) }
        foreach ($entry in $probeEnvironment.GetEnumerator()) { $start.Environment[$entry.Key] = $entry.Value }
        $start.Environment['DOTNET_CLI_TELEMETRY_OPTOUT'] = '1'
        $start.Environment['DOTNET_SKIP_FIRST_TIME_EXPERIENCE'] = '1'
        $process = [Diagnostics.Process]::new()
        $process.StartInfo = $start
        $started = $false
        $processErrors = [Collections.Generic.List[Exception]]::new()
        try {
            if (-not $process.Start()) { throw 'Could not start the local-check process.' }
            $started = $true
            $pump = Start-CheckLogPump $process $stdoutPath $stderrPath
            $pumpErrors = [Collections.Generic.List[Exception]]::new()
            try { Wait-CheckLoggedProcess $process $pump; $code = $process.ExitCode }
            catch {
                $pumpErrors.Add($_.Exception)
                $mustClose = $true
                try { $mustClose = -not $process.HasExited } catch { $pumpErrors.Add($_.Exception) }
                if ($mustClose) { try { Stop-CheckLogReadPipes $process } catch { $pumpErrors.Add($_.Exception) } }
            }
            try { Complete-CheckLogPump $pump } catch { $pumpErrors.Add($_.Exception) }
            if ($pumpErrors.Count -eq 1) { throw $pumpErrors[0] }
            if ($pumpErrors.Count -gt 1) { throw [AggregateException]::new('Process wait and log completion failed.', [Exception[]]$pumpErrors.ToArray()) }
        }
        catch {
            $processErrors.Add($_.Exception)
            try { if ($started -and -not $process.HasExited) { $process.Kill($true); $process.WaitForExit() } }
            catch { $processErrors.Add($_.Exception) }
        }
        finally { try { $process.Dispose() } catch { $processErrors.Add($_.Exception) } }
        if ($processErrors.Count -eq 1) { throw $processErrors[0] }
        if ($processErrors.Count -gt 1) { throw [AggregateException]::new('Local check and child cleanup failed.', [Exception[]]$processErrors.ToArray()) }
        $status.steps += [ordered]@{ name = $command.name; exitCode = $code
            outcome = $(if ($code -eq 0) { 'passed' } else { 'failed' })
            stdoutLog = $prefix + '.stdout.log'; stderrLog = $prefix + '.stderr.log' }
        Save-Status
        Write-Host "$($command.name): exit $code; logs: $prefix.*.log"
        if ($command.name -eq 'test') {
            try { Save-UpdaterSmoke }
            catch {
                [Console]::Error.WriteLine('No complete updater TRX receipt could be saved; previous receipt was not replaced.')
                if ($code -eq 0) { throw }
            }
        }
        if ($code -ne 0) { $finalExit = $code; break }
    }
    $status.outcome = $(if ($finalExit -eq 0) { 'passed' } else { 'failed' })
    $status.exitCode = $finalExit
    $status.finishedAtUtc = [DateTime]::UtcNow.ToString('O')
    Save-Status
}
catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    if ($lease -and $status) {
        $status.outcome = 'failed'; $status.exitCode = 1; $status.finishedAtUtc = [DateTime]::UtcNow.ToString('O')
        $status.error = 'Local check could not finish; inspect the local terminal and logs.'
        try { Save-Status } catch { [Console]::Error.WriteLine('Could not persist local-check status.') }
    }
    $finalExit = 1
}
finally {
    try { if ($lease) { $lease.Dispose() } }
    finally {
        try { if ($ownsHostLease) { $hostLease.ReleaseMutex() } }
        finally { if ($hostLease) { $hostLease.Dispose() } }
    }
}
exit $finalExit
