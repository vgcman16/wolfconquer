Set-StrictMode -Version Latest

function Stop-CheckLogReadPipes([Diagnostics.Process]$Process) {
    $errors = [Collections.Generic.List[Exception]]::new()
    try { $Process.StandardOutput.BaseStream.Dispose() } catch { $errors.Add($_.Exception) }
    try { $Process.StandardError.BaseStream.Dispose() } catch { $errors.Add($_.Exception) }
    if ($errors.Count -eq 1) { throw $errors[0] }
    if ($errors.Count -gt 1) { throw [AggregateException]::new('Closing child log pipes failed.', [Exception[]]$errors.ToArray()) }
}
# The caller owns paths, process lifetime and waiting. Both pipes are copied
# concurrently as raw bytes; FileShare.Read permits inspection while running.
function Start-CheckLogPump([Diagnostics.Process]$Process, [string]$StdoutPath, [string]$StderrPath) {
    $output = $null; $errorOutput = $null; $stdout = $null; $stderr = $null
    try {
        $output = [IO.FileStream]::new($StdoutPath, [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::Read, 1, $true)
        $errorOutput = [IO.FileStream]::new($StderrPath, [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::Read, 1, $true)
        $stdout = $Process.StandardOutput.BaseStream.CopyToAsync($output)
        $stderr = $Process.StandardError.BaseStream.CopyToAsync($errorOutput)
        return [pscustomobject]@{ Output = $output; ErrorOutput = $errorOutput; Stdout = $stdout; Stderr = $stderr }
    }
    catch {
        $errors = [Collections.Generic.List[Exception]]::new(); $errors.Add($_.Exception)
        $exited = $false
        try { if (-not $Process.HasExited) { $Process.Kill($true) }; $Process.WaitForExit(); $exited = $true }
        catch { $errors.Add($_.Exception) }
        if (-not $exited) {
            try { Stop-CheckLogReadPipes $Process } catch { $errors.Add($_.Exception) }
        }
        foreach ($task in @($stdout, $stderr)) {
            if ($task) { try { $null = $task.GetAwaiter().GetResult() } catch { $errors.Add($_.Exception) } }
        }
        foreach ($file in @($output, $errorOutput)) {
            if ($file) { try { $file.Dispose() } catch { $errors.Add($_.Exception) } }
        }
        if ($errors.Count -eq 1) { throw $errors[0] }
        throw [AggregateException]::new('Log pump start and cleanup failed.', [Exception[]]$errors.ToArray())
    }
}
# A failed sink can stop draining a live pipe. Retire only this owned child
# rather than waiting forever; retain both the sink error and any kill failure.
function Wait-CheckLoggedProcess([Diagnostics.Process]$Process, $Pump, [int]$TimeoutMilliseconds = -1) {
    if ($TimeoutMilliseconds -lt -1) { throw 'Invalid log wait timeout.' }
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $exit = $Process.WaitForExitAsync()
    $pending = [Collections.Generic.List[Threading.Tasks.Task]]::new()
    $pending.Add($exit); $pending.Add($Pump.Stdout); $pending.Add($Pump.Stderr)
    while (-not $exit.IsCompleted) {
        $remaining = if ($TimeoutMilliseconds -lt 0) { -1 } else { [Math]::Max(0, $TimeoutMilliseconds - [int]$timer.ElapsedMilliseconds) }
        $index = [Threading.Tasks.Task]::WaitAny($pending.ToArray(), $remaining)
        if ($index -lt 0) { throw [TimeoutException]::new('Logged process did not finish within the control timeout.') }
        $completed = $pending[$index]
        if ($completed.IsFaulted -or $completed.IsCanceled) {
            try { $null = $completed.GetAwaiter().GetResult() }
            catch {
                $errors = [Collections.Generic.List[Exception]]::new(); $errors.Add($_.Exception)
                try { if (-not $Process.HasExited) { $Process.Kill($true) }; $Process.WaitForExit() }
                catch {
                    $errors.Add($_.Exception)
                    try { Stop-CheckLogReadPipes $Process } catch { $errors.Add($_.Exception) }
                }
                if ($errors.Count -eq 1) { throw $errors[0] }
                throw [AggregateException]::new('Log sink failed and child cleanup failed.', [Exception[]]$errors.ToArray())
            }
        }
        $pending.RemoveAt($index)
    }
    $null = $exit.GetAwaiter().GetResult()
}
function Complete-CheckLogPump($Pump) {
    $errors = [Collections.Generic.List[Exception]]::new()
    foreach ($task in @($Pump.Stdout, $Pump.Stderr)) {
        try { $null = $task.GetAwaiter().GetResult() } catch { $errors.Add($_.Exception) }
    }
    foreach ($file in @($Pump.Output, $Pump.ErrorOutput)) {
        try { $file.Dispose() } catch { $errors.Add($_.Exception) }
    }
    if ($errors.Count -eq 1) { throw $errors[0] }
    if ($errors.Count -gt 1) { throw [AggregateException]::new('Log pump completion failed.', [Exception[]]$errors.ToArray()) }
}
Export-ModuleMember -Function Start-CheckLogPump, Wait-CheckLoggedProcess, Stop-CheckLogReadPipes, Complete-CheckLogPump
