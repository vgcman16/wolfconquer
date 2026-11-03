#Requires -Version 7.2
[CmdletBinding()]
param(
    [string] $RepositoryRoot = (Join-Path $PSScriptRoot '../..'),
    [Parameter(Mandatory)][ValidateRange(1024, 65535)][int] $Port,
    [string] $MariaDbHome = 'C:\Program Files\MariaDB 12.2',
    [ValidateSet('Debug', 'Release')][string] $Configuration = 'Release',
    [AllowEmptyString()][string] $OnlyFact,
    [switch] $Plan
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($Port -eq 3306) { throw 'Port 3306 is forbidden; select an explicit separate owned SQL port.' }

function Assert-SqlGameplayOrdinaryPath([string] $Path, [bool] $Directory = $false) {
    $full = [IO.Path]::GetFullPath($Path)
    $item = Get-Item -LiteralPath $full -Force -ErrorAction Stop
    if ([bool]$item.PSIsContainer -ne $Directory) { throw 'A required SQL gameplay project/support path has the wrong type.' }
    $current = $full
    while ($current) {
        $entry = Get-Item -LiteralPath $current -Force -ErrorAction Stop
        if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'SQL gameplay inputs cannot traverse reparse points.' }
        $current = [IO.Path]::GetDirectoryName($current)
    }
    return $full
}

$root = Assert-SqlGameplayOrdinaryPath $RepositoryRoot $true
$project = Assert-SqlGameplayOrdinaryPath (Join-Path $root 'tests/WolfConquer.Tests/WolfConquer.Tests.csproj')
$support = Assert-SqlGameplayOrdinaryPath (Join-Path $root 'tools/dev/FocusedCheckSupport.psm1')
$lifecycle = Assert-SqlGameplayOrdinaryPath (Join-Path $root 'tools/dev/Invoke-OwnedSqlAuthFixture.ps1')
$filter = 'FullyQualifiedName~WolfConquer.Tests.ModernSqlAuthFileRoundtripTests'
$selectedFactNames = @()
$fullProviderFamily = -not $PSBoundParameters.ContainsKey('OnlyFact')
if (-not $fullProviderFamily) {
    if ([string]::IsNullOrEmpty($OnlyFact) -or $OnlyFact -cnotmatch '^[A-Za-z_][A-Za-z0-9_]*$') {
        throw 'OnlyFact must be one exact SQL Fact method name.'
    }
    # Plan remains read-only: inspect source text, never import or run lifecycle
    # support. This mirrors the lifecycle's complete bounded discovery rule.
    $directory = Assert-SqlGameplayOrdinaryPath (Join-Path $root 'tests/WolfConquer.Tests') $true
    $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $attributeCount = 0
    $pattern = '\[ModernSqlAuthFixtureFact(?:Attribute)?\]\s*public\s+(?:async\s+)?Task\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\('
    foreach ($file in Get-ChildItem -LiteralPath $directory -Filter 'ModernSqlAuthFileRoundtripTests*.cs' -File) {
        $path = Assert-SqlGameplayOrdinaryPath $file.FullName
        $source = [IO.File]::ReadAllText($path)
        $attributeCount += [regex]::Matches($source, '\[ModernSqlAuthFixtureFact(?:Attribute)?\]').Count
        foreach ($match in [regex]::Matches($source, $pattern)) {
            if (-not $names.Add($match.Groups['name'].Value)) { throw 'Duplicate SQL fixture Fact method name.' }
        }
    }
    if ($names.Count -eq 0 -or $attributeCount -ne $names.Count) { throw 'Cannot discover every required SQL fixture Fact.' }
    if (-not $names.Contains($OnlyFact)) { throw 'OnlyFact does not match a discovered SQL Fact.' }
    $selectedFactNames = @($OnlyFact)
    $filter = 'FullyQualifiedName=WolfConquer.Tests.ModernSqlAuthFileRoundtripTests.' + $OnlyFact
}
$testArguments = @('test', $project, '-c', $Configuration, '--nologo', '-m:1', '-nodeReuse:false', '/p:UseSharedCompilation=false',
    '--filter', $filter, '--results-directory', '<owned-sql-root>', '--logger', 'trx;LogFileName=qualification.trx',
    '--blame-hang-timeout', '180s', '--blame-hang-dump-type', 'none')
if ($Plan) {
    [ordered]@{ schemaVersion = 1; suite = 'owned-sql-gameplay'; outcome = 'not-run'; fullGate = $false; mode = 'plan'
        repositoryRoot = $root; project = $project; configuration = $Configuration; port = $Port; mariaDbHome = $MariaDbHome
        filter = $filter; fullProviderFamily = $fullProviderFamily; selectedFactNames = @($selectedFactNames)
        ownedLifecycle = $lifecycle; command = [ordered]@{ executable = 'dotnet'; arguments = $testArguments }
    } | ConvertTo-Json -Depth 5
    return
}

if (-not $IsWindows -or -not [Environment]::Is64BitProcess) { throw 'The SQL gameplay check requires 64-bit PowerShell on Windows.' }
foreach ($relative in @('tools/dev/FocusedCheckProcess.cs', 'tools/dev/owned-sql/Paths.ps1',
    'tools/dev/owned-sql/Server.ps1', 'tools/dev/owned-sql/Results.ps1', 'tools/dev/owned-sql/Environment.ps1',
    'tools/dev/owned-sql/OwnedSqlNative.cs', 'tools/dev/owned-sql/OwnedSqlChild.cs')) {
    [void](Assert-SqlGameplayOrdinaryPath (Join-Path $root $relative))
}
Import-Module $support -Force -DisableNameChecking -ErrorAction Stop
$commandOwner = Get-Command Invoke-FocusedCommand -CommandType Function -ErrorAction Stop
$lease = $null; $hostLease = $null; $ownsHostLease = $false; $primaryFailure = $null
try {
    $hostLease = [Threading.Mutex]::new($false, 'Global\WolfConquer.FocusedChecks')
    try { $ownsHostLease = $hostLease.WaitOne(0) }
    catch [Threading.AbandonedMutexException] { $ownsHostLease = $true }
    if (-not $ownsHostLease) { throw 'Another check owns the host lease. Leave it running.' }
    Assert-FocusedHostIdle
    $shared = Join-Path $root '.artifacts/local-dev'
    Assert-FocusedArtifactPath $root $shared
    [IO.Directory]::CreateDirectory($shared) | Out-Null
    $lease = [IO.File]::Open((Join-Path $shared 'check.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    Assert-FocusedHostIdle
    $dotnet = Assert-SqlGameplayOrdinaryPath ((Get-Command dotnet -CommandType Application -ErrorAction Stop).Source)
    # No preliminary command: the lifecycle compiles its shared process owner
    # before readiness. Only its private-environment callback may start dotnet.
    $run = {
        param($fixture)
        $arguments = @('test', $project, '-c', $Configuration, '--nologo', '-m:1', '-nodeReuse:false', '/p:UseSharedCompilation=false',
            '--filter', $filter, '--results-directory', $fixture.ArtifactRoot, '--logger', 'trx;LogFileName=qualification.trx',
            '--blame-hang-timeout', '180s', '--blame-hang-dump-type', 'none')
        $environment = @{ DOTNET_CLI_TELEMETRY_OPTOUT = '1'; DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1' }
        $result = & $commandOwner $dotnet $arguments $root (Join-Path $fixture.ArtifactRoot 'sql-gameplay') $environment
        if ($result.exitCode -ne 0) { throw 'The owned SQL gameplay test command failed; inspect its private logs.' }
    }.GetNewClosure()
    $lifecycleArguments = @{ RepositoryRoot = $root; Port = $Port; MariaDbHome = $MariaDbHome; Run = $run }
    if (-not $fullProviderFamily) { $lifecycleArguments.OnlyFact = $OnlyFact }
    & $lifecycle @lifecycleArguments
}
catch { $primaryFailure = $_.Exception; throw }
finally {
    $cleanupFailures = [Collections.Generic.List[Exception]]::new()
    if ($lease) { try { $lease.Dispose() } catch { $cleanupFailures.Add($_.Exception) } }
    if ($ownsHostLease) { try { $hostLease.ReleaseMutex() } catch { $cleanupFailures.Add($_.Exception) } }
    if ($hostLease) { try { $hostLease.Dispose() } catch { $cleanupFailures.Add($_.Exception) } }
    if ($cleanupFailures.Count) {
        if ($primaryFailure) {
            $primaryFailure.Data['SqlGameplayLeaseCleanupFailures'] = $cleanupFailures.ToArray()
            [Console]::Error.WriteLine('SQL gameplay check also failed to retire a held check lease; primary failure retained.')
        }
        else {
            $failure = [InvalidOperationException]::new('SQL gameplay check could not retire its held check leases.')
            $failure.Data['SqlGameplayLeaseCleanupFailures'] = $cleanupFailures.ToArray()
            throw $failure
        }
    }
}
