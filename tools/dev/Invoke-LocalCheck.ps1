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

$lease = $null
$status = $null
$finalExit = 1
try {
    $root = [IO.Path]::GetFullPath($RepositoryRoot)
    $solution = Join-Path $root 'WolfConquer.sln'
    if (-not (Test-Path -LiteralPath $solution -PathType Leaf)) { throw 'RepositoryRoot must contain WolfConquer.sln.' }
    $dotnet = Resolve-Application $DotNetPath @('dotnet.exe', 'dotnet')
    $pwsh = Resolve-Application $PowerShellPath @('pwsh.exe', 'pwsh')
    $version = [Diagnostics.FileVersionInfo]::GetVersionInfo($pwsh)
    if ($version.ProductMajorPart -ne 7 -or $version.ProductName -notmatch '^PowerShell(?: Core)?$') {
        throw 'The native test probe requires a measured PowerShell 7 executable.'
    }
    $probeEnvironment = [ordered]@{
        WOLFCONQUER_NATIVE_CONSOLE_PROBE_PWSH = $pwsh
        WOLFCONQUER_NATIVE_CONSOLE_PROBE_PWSH_SHA256 = (Get-FileHash -LiteralPath $pwsh -Algorithm SHA256).Hash
    }
    $commands = @(
        [ordered]@{ name = 'restore'; arguments = @('restore', $solution, '--nologo') },
        [ordered]@{ name = 'build'; arguments = @('build', $solution, '--configuration', $Configuration, '--no-restore', '--nologo') },
        [ordered]@{ name = 'test'; arguments = @('test', $solution, '--configuration', $Configuration, '--no-build', '--no-restore', '--nologo') }
    )
    $artifactRoot = Join-Path $root '.artifacts/local-dev'
    $statusPath = Join-Path $artifactRoot 'last-check.json'
    if ($Plan) {
        [ordered]@{ schemaVersion = 1; mode = 'plan'; outcome = 'not-run'; repositoryRoot = $root
            executable = $dotnet; commands = $commands; environment = $probeEnvironment; statusPath = $statusPath } |
            ConvertTo-Json -Depth 8
        exit 0
    }

    Assert-ArtifactPath $artifactRoot
    [IO.Directory]::CreateDirectory($artifactRoot) | Out-Null
    $lockPath = Join-Path $artifactRoot 'check.lock'
    Assert-ArtifactPath $lockPath
    $lease = [IO.File]::Open($lockPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    $runId = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
    $status = [ordered]@{ schemaVersion = 1; repositoryRoot = $root; configuration = $Configuration
        runId = $runId; startedAtUtc = [DateTime]::UtcNow.ToString('O'); finishedAtUtc = $null
        outcome = 'running'; exitCode = $null; steps = @(); error = $null }
    Save-Status
    $finalExit = 0
    foreach ($command in $commands) {
        $prefix = '.artifacts/local-dev/' + $runId + '-' + $command.name
        $stdoutPath = Join-Path $root ($prefix + '.stdout.log')
        $stderrPath = Join-Path $root ($prefix + '.stderr.log')
        $start = [Diagnostics.ProcessStartInfo]::new($dotnet)
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
        try {
            if (-not $process.Start()) { throw 'Could not start the .NET check process.' }
            $stdout = $process.StandardOutput.ReadToEndAsync()
            $stderr = $process.StandardError.ReadToEndAsync()
            $process.WaitForExit()
            $code = $process.ExitCode
            [IO.File]::WriteAllText($stdoutPath, $stdout.GetAwaiter().GetResult())
            [IO.File]::WriteAllText($stderrPath, $stderr.GetAwaiter().GetResult())
        }
        finally { $process.Dispose() }
        $status.steps += [ordered]@{ name = $command.name; exitCode = $code
            outcome = $(if ($code -eq 0) { 'passed' } else { 'failed' })
            stdoutLog = $prefix + '.stdout.log'; stderrLog = $prefix + '.stderr.log' }
        Save-Status
        Write-Host "$($command.name): exit $code; logs: $prefix.*.log"
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
finally { if ($lease) { $lease.Dispose() } }
exit $finalExit
