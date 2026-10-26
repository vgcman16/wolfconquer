using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace WolfConquer.Tests;

public sealed partial class DataToolClientNativeLaunchProbeTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(2500)]
    public async Task ReadinessKeepsRealConnectedChildAlivePastOld1500BoundaryThenCapturesFullWindow(int startupDelayMilliseconds)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var tempRoot = CreateTempRoot();
        var listener = new TcpListener(IPAddress.Loopback, 0);
        TcpClient? accepted = null;
        Task<ConsoleRunResult>? running = null;
        var elapsed = new Stopwatch();
        try
        {
            listener.Start();
            var accept = listener.AcceptTcpClientAsync();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var log = Path.Combine(tempRoot, "runtime.log");
            var ready = Path.Combine(tempRoot, "ready");
            var script = Path.Combine(tempRoot, "delayed-ready.ps1");
            await File.WriteAllTextAsync(log, "seed");
            await File.WriteAllTextAsync(script,
                """
                $ErrorActionPreference = 'Stop'
                $client = New-Object Net.Sockets.TcpClient
                try {
                    [Threading.Thread]::Sleep([int]$args[3])
                    $client.Connect('127.0.0.1', [int]$args[0])
                    [IO.File]::AppendAllText($args[1], "connected" + [Environment]::NewLine)
                    if ($client.GetStream().ReadByte() -ne 71) { throw 'Release token missing' }
                    [IO.File]::WriteAllText($args[2], "pid=$PID`nready`n", [Text.UTF8Encoding]::new($false))
                    $null = $client.GetStream().ReadByte()
                } finally {
                    $client.Dispose()
                }
                """);

            elapsed.Start();
            running = RunDataToolAsync(
                "client-native-launch-probe", GetWindowsPowerShellPath(),
                "--workdir", tempRoot, "--timeout-ms", "20000", "--kill-after-ms", "1500",
                "--debug-log", log, "--top-threads", "8", "--ready-file", ready,
                "--args", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script,
                port.ToString(CultureInfo.InvariantCulture), log, ready, startupDelayMilliseconds.ToString(CultureInfo.InvariantCulture));
            accepted = await accept.WaitAsync(TimeSpan.FromSeconds(10));
            // Intentional adversarial control, not a production wait/retry fix:
            // a real connected child withholds its marker beyond the old kill bound.
            await Task.Delay(1700);
            Assert.False(running.IsCompleted, "Probe terminated before the real readiness release.");
            await accepted.GetStream().WriteAsync(new byte[] { 71 });
            var result = await running;

            Assert.Equal(2, result.ExitCode);
            Assert.True(elapsed.ElapsedMilliseconds >= 3200 + startupDelayMilliseconds, "The unchanged post-ready1500 interval was not preserved.");
            Assert.Contains("ReadinessObserved: True", result.StandardOutput);
            Assert.Contains("ReadinessError: <none>", result.StandardOutput);
            Assert.Contains("TimedOut: False", result.StandardOutput);
            Assert.Contains("RuntimeSnapshot: reason=before-kill-after-bound", result.StandardOutput);
            Assert.Contains("KillAttempted: True", result.StandardOutput);
            Assert.Contains("Killed: True", result.StandardOutput);
            Assert.Contains("Tcp: pid=", result.StandardOutput);
            Assert.Contains("remote=127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture), result.StandardOutput);
            Assert.Matches(@"DebugLog: path=.*delta=[1-9]\d*", result.StandardOutput);
            Assert.Matches(@"Modules: pid=\d+ count=[1-9]\d*", result.StandardOutput);
            Assert.Contains("Thread: pid=", result.StandardOutput);
            Assert.Empty(result.StandardError);
        }
        catch (Exception failure) when (running is not null)
        {
            throw await AddReadinessProbeDiagnosticsAsync(running, elapsed.ElapsedMilliseconds, failure);
        }
        finally
        {
            accepted?.Dispose();
            listener.Stop();
            if (running is not null)
            {
                try
                {
                    await running;
                }
                catch
                {
                    // Preserve the original assertion/failure; RunDataToolAsync owns
                    // its bounded process-tree cleanup before temp inputs are removed.
                }
            }
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task MissingReadinessTimesOutAndKillsOwnedTargetWithoutGrantingExtraWindow()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var tempRoot = CreateTempRoot();
        try
        {
            var ready = Path.Combine(tempRoot, "never-created");
            var result = await RunDataToolAsync(
                "client-native-launch-probe", GetWindowsPowerShellPath(),
                "--workdir", tempRoot, "--timeout-ms", "5000", "--kill-after-ms", "1500",
                "--ready-file", ready, "--top-threads", "0",
                "--args", "-NoProfile", "-NonInteractive", "-Command",
                "[Threading.ManualResetEvent]::new($false).WaitOne() | Out-Null");

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("ReadinessRequested: True", result.StandardOutput);
            Assert.Contains("ReadinessObserved: False", result.StandardOutput);
            Assert.Contains("Startup budget exhausted.", result.StandardOutput);
            Assert.Contains("TimedOut: True", result.StandardOutput);
            Assert.Contains("KillAttempted: True", result.StandardOutput);
            Assert.Contains("Killed: True", result.StandardOutput);
            Assert.Contains("RuntimeSnapshot: reason=readiness-before-kill", result.StandardOutput);
            Assert.False(File.Exists(ready));
            Assert.Empty(result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task EarlyExitWithoutReadinessIsNonzeroAndNotRelabeledAsTimeout()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var tempRoot = CreateTempRoot();
        try
        {
            var result = await RunDataToolAsync(
                "client-native-launch-probe", GetWindowsPowerShellPath(),
                "--workdir", tempRoot, "--timeout-ms", "5000", "--kill-after-ms", "1500",
                "--ready-file", Path.Combine(tempRoot, "not-created"), "--top-threads", "0",
                "--args", "-NoProfile", "-NonInteractive", "-Command", "exit 23");
            Assert.Equal(1, result.ExitCode);
            Assert.Contains("ReadinessObserved: False", result.StandardOutput);
            Assert.Contains("Target exited before readiness was observed.", result.StandardOutput);
            Assert.Contains("Exited: True", result.StandardOutput);
            Assert.Contains("ExitCode: 23", result.StandardOutput);
            Assert.Contains("TimedOut: False", result.StandardOutput);
            Assert.Contains("KillAttempted: False", result.StandardOutput);
            Assert.Contains("RuntimeSnapshot: reason=after-exit", result.StandardOutput);
            Assert.Empty(result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task PreexistingReadinessMarkerIsRejectedBeforeAnyTargetLaunch()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var tempRoot = CreateTempRoot();
        try
        {
            var ready = Path.Combine(tempRoot, "stale");
            const string old = "pid=1\nready\n";
            await File.WriteAllTextAsync(ready, old);
            var result = await RunDataToolAsync(
                "client-native-launch-probe", GetWindowsPowerShellPath(), "--ready-file", ready);
            Assert.Equal(1, result.ExitCode);
            Assert.Contains("StartSucceeded: False", result.StandardOutput);
            Assert.Contains("ProcessId: <none>", result.StandardOutput);
            Assert.Contains("fresh and absent before launch", result.StandardOutput);
            Assert.Contains("KillAttempted: False", result.StandardOutput);
            Assert.Equal(old, await File.ReadAllTextAsync(ready));
            Assert.Empty(result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }
}
