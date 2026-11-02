Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-FocusedArtifactPath([string]$Root, [string]$Path) {
    $relative = [IO.Path]::GetRelativePath($Root, $Path)
    if ([IO.Path]::IsPathRooted($relative) -or $relative.StartsWith('..')) { throw 'Focused artifact path escaped repository.' }
    $current = $Root
    foreach ($part in @('') + $relative.Split([IO.Path]::DirectorySeparatorChar)) {
        if ($part) { $current = Join-Path $current $part }
        if ((Test-Path -LiteralPath $current) -and ((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'Focused artifacts cannot traverse reparse points.'
        }
    }
}

function Assert-FocusedHostIdle {
    if (-not $IsWindows) { throw 'This runner currently requires Windows host process checks.' }
    $busy = @(Get-CimInstance Win32_Process | Where-Object {
        $_.Name -in @('testhost.exe', 'Runner.Worker.exe', 'vstest.console.exe') -or
        ($_.Name -eq 'dotnet.exe' -and $_.CommandLine -match '(?i)\s(test|build|restore)\s')
    })
    if ($busy.Count) { throw ('A build/test/runner job is already active (PID ' + ($busy.ProcessId -join ', ') + '). Leave it running.') }
}

function Write-FocusedStatus([string]$Root, [string]$Path, $Status) {
    Assert-FocusedArtifactPath $Root $Path
    $temporary = Join-Path ([IO.Path]::GetDirectoryName($Path)) ([guid]::NewGuid().ToString('N') + '.tmp')
    [IO.File]::WriteAllText($temporary, ($Status | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
    [IO.File]::Move($temporary, $Path, $true)
}

function Invoke-FocusedCommand([string]$Executable, [string[]]$Arguments, [string]$Root, [string]$LogPrefix, [hashtable]$Environment) {
    Assert-FocusedArtifactPath $Root ($LogPrefix + '.stdout.log')
    Assert-FocusedArtifactPath $Root ($LogPrefix + '.stderr.log')
    $start = [Diagnostics.ProcessStartInfo]::new($Executable)
    $start.WorkingDirectory = $Root
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($value in $Arguments) { $start.ArgumentList.Add($value) }
    foreach ($entry in $Environment.GetEnumerator()) { $start.Environment[$entry.Key] = [string]$entry.Value }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $job = $null; $started = $false; $stdoutFile = $null; $stderrFile = $null
    try {
        if (-not ('WolfConquer.DevChecks.OwnedProcessJob' -as [type])) {
            Add-Type -LiteralPath (Join-Path $PSScriptRoot 'FocusedCheckProcess.cs')
        }
        $job = [WolfConquer.DevChecks.OwnedProcessJob]::new()
        if (-not $process.Start()) { throw 'Focused command could not start.' }
        $started = $true
        $job.Attach($process)
        $stdoutFile = [IO.File]::Open($LogPrefix + '.stdout.log', [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::Read)
        $stderrFile = [IO.File]::Open($LogPrefix + '.stderr.log', [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::Read)
        $stdout = $process.StandardOutput.BaseStream.CopyToAsync($stdoutFile)
        $stderr = $process.StandardError.BaseStream.CopyToAsync($stderrFile)
        $process.WaitForExit()
        $null = $stdout.GetAwaiter().GetResult()
        $null = $stderr.GetAwaiter().GetResult()
        return [pscustomobject]@{ exitCode = $process.ExitCode; seconds = [Math]::Round($timer.Elapsed.TotalSeconds, 3) }
    }
    finally {
        try {
            if ($started -and -not $process.HasExited) { $process.Kill($true) }
            if ($started) { $process.WaitForExit() }
        }
        finally {
            if ($job) { $job.Dispose() }
            if ($stdoutFile) { $stdoutFile.Dispose() }
            if ($stderrFile) { $stderrFile.Dispose() }
            $process.Dispose()
        }
    }
}

function Read-FocusedTrx([string]$Path, [string[]]$Selectors = @()) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw 'No terminal focused TRX was produced.' }
    $settings = [Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $settings.MaxCharactersInDocument = 128MB
    $reader = [Xml.XmlReader]::Create($Path, $settings)
    try {
        $document = [Xml.XmlDocument]::new()
        $document.XmlResolver = $null
        $document.Load($reader)
    }
    finally { $reader.Dispose() }
    $summary = $document.SelectSingleNode('/*[local-name()="TestRun"]/*[local-name()="ResultSummary"]')
    if (-not $summary -or $summary.GetAttribute('outcome') -notin @('Completed', 'Passed', 'Failed')) {
        throw 'Focused TRX has no completed result summary.'
    }
    $counters = $summary.SelectSingleNode('*[local-name()="Counters"]')
    foreach ($name in @('error', 'timeout', 'aborted', 'inProgress', 'pending', 'passedButRunAborted')) {
        $value = 0L
        if (-not $counters -or -not [long]::TryParse($counters.GetAttribute($name), [ref]$value) -or $value -ne 0) {
            throw ('Focused TRX has an invalid/nonzero ' + $name + ' counter.')
        }
    }
    $counts = [ordered]@{ Passed = 0; Failed = 0; NotExecuted = 0; Other = 0 }
    $rows = @($document.SelectNodes('/*[local-name()="TestRun"]/*[local-name()="Results"]/*[local-name()="UnitTestResult"]'))
    foreach ($row in $rows) {
        $outcome = $row.GetAttribute('outcome')
        if ($outcome -in @('Passed', 'Failed', 'NotExecuted')) { $counts[$outcome]++ }
        else { $counts.Other++ }
    }
    if ($counts.Passed -eq 0) { throw 'Focused selection executed no passing tests; empty/skipped-only selections are not green.' }
    $total = 0L
    if (-not [long]::TryParse($counters.GetAttribute('total'), [ref]$total) -or $total -ne $rows.Count) {
        throw 'Focused TRX total does not match its actual outcome rows.'
    }
    foreach ($name in @('Passed', 'Failed')) {
        $value = 0L
        if (-not [long]::TryParse($counters.GetAttribute($name.ToLowerInvariant()), [ref]$value) -or $value -ne $counts[$name]) {
            throw ('Focused TRX ' + $name + ' counter does not match its actual rows.')
        }
    }
    if ($summary.GetAttribute('outcome') -eq 'Failed' -and $counts.Failed -eq 0) { throw 'Focused TRX reports a failed run.' }
    foreach ($selector in $Selectors) {
        if (-not $selector.StartsWith('FullyQualifiedName~', [StringComparison]::Ordinal)) { throw 'Unsupported built-in selector.' }
        $fragment = $selector.Substring('FullyQualifiedName~'.Length)
        if (-not $fragment -or -not @($rows | Where-Object {
            $_.GetAttribute('outcome') -eq 'Passed' -and $_.GetAttribute('testName').Contains($fragment, [StringComparison]::Ordinal)
        }).Count) { throw ('Built-in selector produced no passing tests: ' + $selector) }
    }
    return [pscustomobject]$counts
}

Export-ModuleMember -Function Assert-FocusedArtifactPath, Assert-FocusedHostIdle, Write-FocusedStatus, Invoke-FocusedCommand, Read-FocusedTrx
