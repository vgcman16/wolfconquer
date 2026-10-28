Set-StrictMode -Version Latest

function Get-GaOrdinaryPath([string]$Path, [switch]$Directory) {
    if (-not [IO.Path]::IsPathFullyQualified($Path) -or $Path.StartsWith('\\', [StringComparison]::Ordinal)) { throw 'Explicit absolute local path required.' }
    $full = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($Path))
    $item = Get-Item -LiteralPath $full -Force -ErrorAction Stop
    if ([bool]$item.PSIsContainer -ne [bool]$Directory) { throw 'Path type mismatch.' }
    for ($part = $full; $part; $part = [IO.Path]::GetDirectoryName($part)) {
        if (([IO.File]::GetAttributes($part) -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Reparse path refused.' }
    }
    return $full
}

function Resolve-GaEvidenceRoot([string]$Path, [switch]$Resume) {
    if (-not [IO.Path]::IsPathFullyQualified($Path)) { throw 'Absolute evidence folder required.' }
    $full = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($Path))
    $leaf = [IO.Path]::GetFileName($full)
    if ($leaf -cnotmatch '^wolf-game-acceptance-[0-9a-f]{32}$') { throw 'Owned evidence folder name required.' }
    if ([guid]::ParseExact($leaf.Substring('wolf-game-acceptance-'.Length), 'N') -eq [guid]::Empty) { throw 'Fresh nonempty evidence ID required.' }
    $null = Get-GaOrdinaryPath ([IO.Path]::GetDirectoryName($full)) -Directory
    if ($Resume) { return Get-GaOrdinaryPath $full -Directory }
    if (Test-Path -LiteralPath $full) { throw 'Fresh evidence folder required.' }
    $null = New-Item -ItemType Directory -Path $full -ErrorAction Stop
    return Get-GaOrdinaryPath $full -Directory
}

function Save-GaJson([hashtable]$Context, [string]$Leaf, $Value) {
    if ($Leaf -cnotmatch '^[a-z0-9][a-z0-9.-]*\.json$') { throw 'Evidence leaf invalid.' }
    $null = Get-GaOrdinaryPath $Context.evidenceRoot -Directory
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes(($Value | ConvertTo-Json -Depth 24 -Compress))
    if ($bytes.Length -gt 2MB) { throw 'Evidence size bound exceeded.' }
    $file = [IO.File]::Open((Join-Path $Context.evidenceRoot $Leaf), [IO.FileMode]::CreateNew,
        [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $file.Write($bytes); $file.Flush($true) } finally { $file.Dispose() }
}

function Read-GaJson([hashtable]$Context, [string]$Leaf) {
    if ($Leaf -cnotmatch '^[a-z0-9][a-z0-9.-]*\.json$') { throw 'Evidence leaf invalid.' }
    $file = Get-GaOrdinaryPath (Join-Path $Context.evidenceRoot $Leaf)
    if ((Get-Item -LiteralPath $file).Length -gt 2MB) { throw 'Evidence size bound exceeded.' }
    return (Get-Content -LiteralPath $file -Raw | ConvertFrom-Json -AsHashtable)
}

function Invoke-GaCommand([hashtable]$Context, [string]$Name, [string]$Executable,
    [string[]]$Arguments, [int]$TimeoutSeconds = 30, [switch]$Json) {
    $step = [ordered]@{ name = $Name; startedUtc = [DateTime]::UtcNow.ToString('O');
        completed = $false; exitCode = $null; jsonParsed = $false; stderrPresent = $false;
        processId = $null; processStartUtc = $null; status = 'notStarted' }
    $Context.steps.Add($step)
    $start = [Diagnostics.ProcessStartInfo]::new($Executable)
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    try {
        $step.processId = $process.Id
        $step.processStartUtc = $process.StartTime.ToUniversalTime().ToString('O')
        $step.status = 'running'
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $step.status = 'deadlineExceededProcessNotJoined'
            return @{ completed = $false; exitCode = $null; value = $null }
        }
        $text = $stdout.GetAwaiter().GetResult()
        $errors = $stderr.GetAwaiter().GetResult()
        $step.completed = $true; $step.exitCode = $process.ExitCode
        $step.stderrPresent = -not [string]::IsNullOrWhiteSpace($errors)
        $step.status = 'exited'
        $value = $null
        if ($Json -and $text.Length -le 2MB) {
            try { $value = $text | ConvertFrom-Json -AsHashtable; $step.jsonParsed = $null -ne $value }
            catch { $step.status = 'typedJsonUnavailable' }
        }
        return @{ completed = $true; exitCode = $process.ExitCode; value = $value; stderrPresent = $step.stderrPresent }
    }
    finally { $process.Dispose() }
}

function Get-GaInspection([hashtable]$Context, [string]$Leaf) {
    $read = Invoke-GaCommand $Context 'inspect-owned-player' $Context.dotnet @($Context.tool,
        'inspect-owned-player', $Context.config, [string]$Context.accountId) -Json
    if ($read.completed -and $null -ne $read.value) { Save-GaJson $Context $Leaf $read.value }
    if (-not $read.completed -or $read.exitCode -ne 0 -or $read.stderrPresent -or
        $null -eq $read.value -or $read.value.success -ne $true -or $null -eq $read.value.report) {
        throw 'Genuine inspector result unavailable.'
    }
    $report = $read.value.report
    $domain = [guid]::Empty
    if ($report.schemaVersion -ne 1 -or $report.accountId -ne $Context.accountId -or
        -not [guid]::TryParse([string]$report.domain, [ref]$domain) -or $domain -eq [guid]::Empty -or
        $null -eq $report.character -or $report.character.uid -le 0 -or $report.character.uid -gt [uint32]::MaxValue -or
        $null -eq $report.items -or $null -eq $report.mails -or $null -eq $report.files -or
        @($report.files | Where-Object { $_.bytesAndMetadataUnchanged -ne $true }).Count) {
        throw 'Inspector identity or file-stability proof unavailable.'
    }
    return $report
}

function Invoke-GaSourceChecks([hashtable]$Context, [string]$SourceRepositoryRoot, [string]$PowerShellPath) {
    $root = Get-GaOrdinaryPath $SourceRepositoryRoot -Directory
    $check = Get-GaOrdinaryPath (Join-Path $root 'tools/dev/Invoke-LocalCheck.ps1')
    $status = Get-GaOrdinaryPath (Join-Path $root 'tools/dev/Get-LocalStatus.ps1')
    $null = Get-GaOrdinaryPath (Join-Path $root 'WolfConquer.sln')
    $pwsh = Get-GaOrdinaryPath $PowerShellPath
    $mutex = [Threading.Mutex]::new($false, 'Local\WolfConquer.GameAcceptance.SourceChecks')
    $owned = $false
    try {
        $owned = $mutex.WaitOne(0)
        if (-not $owned) { return @{ status = 'Deferred'; reason = 'acceptanceSourceChecksAlreadyActive' } }
        $snapshot = Invoke-GaCommand $Context 'saved-local-status' $pwsh @('-NoProfile', '-File', $status,
            '-RepositoryRoot', $root, '-SkipServiceProbe', '-SkipUpdaterProbe') -Json
        if (-not $snapshot.completed -or $snapshot.exitCode -ne 0 -or $null -eq $snapshot.value -or
            $snapshot.value.lastCheck.state -eq 'running') {
            return @{ status = 'Deferred'; reason = 'actualCiCheckoutStatusUnavailableOrRunning' }
        }
        $lockPath = Join-Path $root '.artifacts/local-dev/check.lock'
        if (-not (Test-Path -LiteralPath $lockPath -PathType Leaf)) {
            return @{ status = 'Deferred'; reason = 'actualCiCheckoutLockUnavailable' }
        }
        try {
            $lease = [IO.File]::Open((Get-GaOrdinaryPath $lockPath), [IO.FileMode]::Open,
                [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
            $lease.Dispose() # The existing checker acquires this same real lock itself.
        }
        catch { return @{ status = 'Deferred'; reason = 'actualCiCheckoutLockHeld' } }
        $run = Invoke-GaCommand $Context 'existing-full-source-checks' $pwsh @('-NoProfile', '-File', $check,
            '-RepositoryRoot', $root, '-Configuration', 'Release', '-DotNetPath', $Context.dotnet,
            '-PowerShellPath', $pwsh) -TimeoutSeconds 7200
        if (-not $run.completed) {
            return @{ status = 'SourceChecksNotVerified'; reason = 'sourceProcessNotJoinedNoFollowOnSourceCommand';
                exitCode = $null; bound = 'Actual source process may still be active; no drain or success inferred.' }
        }
        $after = Invoke-GaCommand $Context 'saved-local-status-after' $pwsh @('-NoProfile', '-File', $status,
            '-RepositoryRoot', $root, '-SkipServiceProbe', '-SkipUpdaterProbe') -Json
        $verified = $run.completed -and $run.exitCode -eq 0 -and $after.completed -and $after.exitCode -eq 0 -and
            $null -ne $after.value -and $after.value.lastCheck.state -ceq 'passed'
        return @{ status = $(if ($verified) { 'SourceChecksVerified' } else { 'SourceChecksNotVerified' });
            exitCode = $run.exitCode; savedStatus = $after.value;
            bound = 'Existing local-check suite only; no deployment or game acceptance proof.' }
    }
    finally { if ($owned) { $mutex.ReleaseMutex() }; $mutex.Dispose() }
}
