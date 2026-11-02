#requires -Version 7.0
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'Windows process ownership control required.' }
Add-Type -LiteralPath (Join-Path $PSScriptRoot 'FocusedCheckProcess.cs')
$job = [WolfConquer.DevChecks.OwnedProcessJob]::new()
$child = [Diagnostics.Process]::new()
$started = $false
try {
    $info = [Diagnostics.ProcessStartInfo]::new((Join-Path $PSHOME 'pwsh.exe'))
    $info.UseShellExecute = $false; $info.CreateNoWindow = $true
    foreach ($argument in @('-NoProfile', '-Command', 'Start-Sleep -Seconds 60')) { $info.ArgumentList.Add($argument) }
    $child.StartInfo = $info
    if (-not $child.Start()) { throw 'Owned control child did not start.' }
    $started = $true
    $job.Attach($child)
    $job.Dispose()
    if (-not $child.WaitForExit(5000)) { throw 'Closing the owned job failed to terminate its child.' }
    Write-Host 'Owned job close terminated and reaped the disposable PowerShell child.'
}
finally {
    try {
        if ($started -and -not $child.HasExited) { $child.Kill($true) }
        if ($started) { $child.WaitForExit() }
    }
    finally { $job.Dispose(); $child.Dispose() }
}
