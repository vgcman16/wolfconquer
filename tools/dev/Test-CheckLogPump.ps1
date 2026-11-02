#requires -Version 7.0
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'CheckLogPump.psm1') -Force
$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$directory = [IO.Path]::GetFullPath((Join-Path $tempRoot ('wolf-check-log-' + [guid]::NewGuid().ToString('N'))))
$relative = [IO.Path]::GetRelativePath($tempRoot, $directory)
if ([IO.Path]::IsPathRooted($relative) -or $relative.Contains([IO.Path]::DirectorySeparatorChar) -or
    $relative -notmatch '^wolf-check-log-[0-9a-f]{32}$') { throw 'Invalid owned temporary directory.' }
[IO.Directory]::CreateDirectory($directory) | Out-Null
$child = [Diagnostics.Process]::new(); $started = $false; $pump = $null
$errors = [Collections.Generic.List[Exception]]::new()
function Read-LiveLog([string]$Path) {
    $file = [IO.FileStream]::new($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
    try { $reader = [IO.StreamReader]::new($file); try { return $reader.ReadToEnd() } finally { $reader.Dispose() } }
    finally { $file.Dispose() }
}
function Has-SinkFailure([Exception]$Failure) {
    if ($Failure -is [IO.IOException] -or $Failure -is [ObjectDisposedException]) { return $true }
    if ($Failure -is [AggregateException]) {
        foreach ($inner in $Failure.InnerExceptions) { if (Has-SinkFailure $inner) { return $true } }
    }
    if ($Failure.InnerException) { return (Has-SinkFailure $Failure.InnerException) }
    return $false
}
function Has-MissingDirectory([Exception]$Failure) {
    if ($Failure -is [IO.DirectoryNotFoundException]) { return $true }
    if ($Failure -is [AggregateException]) {
        foreach ($inner in $Failure.InnerExceptions) { if (Has-MissingDirectory $inner) { return $true } }
    }
    if ($Failure.InnerException) { return (Has-MissingDirectory $Failure.InnerException) }
    return $false
}
try {
    $release = Join-Path $directory 'release'; $out = Join-Path $directory 'out'; $err = Join-Path $directory 'err'
    $childFile = Join-Path $directory 'child.ps1'
    [IO.File]::WriteAllText($childFile, @'
param($Release)
[Console]::Out.Write('before-out'); [Console]::Out.Flush()
[Console]::Error.Write('before-err'); [Console]::Error.Flush()
while (-not [IO.File]::Exists($Release)) { Start-Sleep -Milliseconds 10 }
[Console]::Out.Write(('x' * 131072) + 'after-out')
[Console]::Error.Write(('y' * 131072) + 'after-err')
exit 7
'@)
    $info = [Diagnostics.ProcessStartInfo]::new((Join-Path $PSHOME $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })))
    $info.UseShellExecute = $false; $info.CreateNoWindow = $true; $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
    foreach ($argument in @('-NoProfile', '-File', $childFile, $release)) { $info.ArgumentList.Add($argument) }
    $child.StartInfo = $info
    if (-not $child.Start()) { throw 'Control child failed to start.' }; $started = $true
    $pump = Start-CheckLogPump $child $out $err
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while ($timer.Elapsed -lt [TimeSpan]::FromSeconds(5)) {
        if ((Read-LiveLog $out) -eq 'before-out' -and (Read-LiveLog $err) -eq 'before-err') { break }
        if ($child.HasExited) { throw 'Control exited before both live logs appeared.' }
        Start-Sleep -Milliseconds 10
    }
    if ((Read-LiveLog $out) -ne 'before-out' -or (Read-LiveLog $err) -ne 'before-err' -or $child.HasExited) { throw 'Live logs not observed while the child was running.' }
    [IO.File]::WriteAllText($release, 'go')
    if (-not $child.WaitForExit(10000)) { throw 'Control child failed to exit.' }
    $complete = $pump; $pump = $null; Complete-CheckLogPump $complete
    if ($child.ExitCode -ne 7 -or [IO.File]::ReadAllText($out) -ne ('before-out' + ('x' * 131072) + 'after-out') -or
        [IO.File]::ReadAllText($err) -ne ('before-err' + ('y' * 131072) + 'after-err')) { throw 'Control output or exit changed.' }
    $child.Dispose(); $child = [Diagnostics.Process]::new(); $started = $false
    $release2 = Join-Path $directory 'release2'
    $info2 = [Diagnostics.ProcessStartInfo]::new($info.FileName)
    $info2.UseShellExecute = $false; $info2.CreateNoWindow = $true; $info2.RedirectStandardOutput = $true; $info2.RedirectStandardError = $true
    foreach ($argument in @('-NoProfile', '-File', $childFile, $release2)) { $info2.ArgumentList.Add($argument) }
    $child.StartInfo = $info2
    if (-not $child.Start()) { throw 'Failure control child failed to start.' }; $started = $true
    $pump = Start-CheckLogPump $child (Join-Path $directory 'out2') (Join-Path $directory 'err2')
    $timer.Restart()
    while ((Read-LiveLog (Join-Path $directory 'out2')) -ne 'before-out' -and $timer.Elapsed -lt [TimeSpan]::FromSeconds(5)) { Start-Sleep -Milliseconds 10 }
    if ($timer.Elapsed -ge [TimeSpan]::FromSeconds(5) -or $child.HasExited) { throw 'Failure control did not reach its live boundary.' }
    $pump.Output.Dispose() # Real pipe remains active; its next real sink write must fail.
    [IO.File]::WriteAllText($release2, 'go')
    $refused = $false
    try { Wait-CheckLoggedProcess $child $pump 5000 } catch { if (Has-SinkFailure $_.Exception) { $refused = $true } else { throw } }
    $failedPump = $pump; $pump = $null
    try { Complete-CheckLogPump $failedPump } catch { if (Has-SinkFailure $_.Exception) { $refused = $true } else { throw } }
    if (-not $refused -or -not $child.HasExited) { throw 'Failed sink did not terminate and reap its actual child.' }
    $child.Dispose(); $child = [Diagnostics.Process]::new(); $started = $false
    $release3 = Join-Path $directory 'release3'
    $info3 = [Diagnostics.ProcessStartInfo]::new($info.FileName)
    $info3.UseShellExecute = $false; $info3.CreateNoWindow = $true; $info3.RedirectStandardOutput = $true; $info3.RedirectStandardError = $true
    foreach ($argument in @('-NoProfile', '-File', $childFile, $release3)) { $info3.ArgumentList.Add($argument) }
    $child.StartInfo = $info3
    if (-not $child.Start()) { throw 'Start failure control child failed to start.' }; $started = $true
    $firstSink = Join-Path $directory 'first-start-sink'
    $startFailure = $null
    try { $pump = Start-CheckLogPump $child $firstSink (Join-Path $directory 'missing-parent/second') }
    catch { $startFailure = $_.Exception }
    if (-not $startFailure -or -not (Has-MissingDirectory $startFailure) -or -not $child.HasExited) { throw 'Second log open did not preserve its directory failure and reap the child.' }
    $exclusive = [IO.File]::Open($firstSink, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    $exclusive.Dispose()
}
catch { $errors.Add($_.Exception) }
finally {
    try { if ($started -and -not $child.HasExited) { $child.Kill($true); $child.WaitForExit() } } catch { $errors.Add($_.Exception) }
    if ($pump) {
        try {
            if (-not $child.HasExited) {
                foreach ($stream in @($child.StandardOutput.BaseStream, $child.StandardError.BaseStream)) {
                    try { $stream.Dispose() } catch { $errors.Add($_.Exception) }
                }
            }
        } catch { $errors.Add($_.Exception) }
    }
    if ($pump) { try { Complete-CheckLogPump $pump } catch { $errors.Add($_.Exception) } }
    try { $child.Dispose() } catch { $errors.Add($_.Exception) }
    try { [IO.Directory]::Delete($directory, $true) } catch { $errors.Add($_.Exception) }
}
if ($errors.Count -eq 1) { throw $errors[0] }
if ($errors.Count -gt 1) { throw [AggregateException]::new('Log control and cleanup failed.', [Exception[]]$errors.ToArray()) }
