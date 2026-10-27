using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace WolfConquer.Tests;

public sealed partial class DataToolClientNativeLaunchProbeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeProbeCleanupCannotTerminateAnUnrelatedObservedPidEvenWithTheSameImage(bool rootAlreadyExited)
    {
        if (!OperatingSystem.IsWindows()) return;
        var directory = CreateTempRoot();
        Process? root = null, unrelated = null;
        var rootOutput = new List<string>();
        var unrelatedOutput = new List<string>();
        ExceptionDispatchInfo? primary = null;
        var cleanupFailures = new List<Exception>();
        try
        {
            root = StartOwnedCleanupCommand(directory);
            _ = root.SafeHandle;
            await RequireOwnedCleanupReadyAsync(root, "root", rootOutput);
            unrelated = StartOwnedCleanupCommand(directory);
            _ = unrelated.SafeHandle;
            await RequireOwnedCleanupReadyAsync(unrelated, "unrelated sibling", unrelatedOutput);
            if (rootAlreadyExited)
            {
                await root.StandardInput.WriteLineAsync("exit 0");
                await root.StandardInput.FlushAsync();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await root.WaitForExitAsync(timeout.Token);
                Assert.Equal(0, root.ExitCode);
            }
            var method = typeof(global::Program).GetMethod("TryKillClientNativeRuntimeProcessTree",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            // Both commands are genuine independently created owned test actors.
            // The second is a sibling, not a descendant of the probe root. This
            // integer simulates an old observation now referring to a foreign
            // process; no actual PID recycling or user process is needed.
            object?[] arguments = [root, new HashSet<int> { root.Id, unrelated.Id }, false, null, null];
            if (!rootAlreadyExited) await RequireOwnedCleanupAliveAsync(root, "root immediately before cleanup", rootOutput);
            else Assert.True(root.HasExited, "The intentional exited-root control must retain the actual exit witness.");
            await RequireOwnedCleanupAliveAsync(unrelated, "unrelated sibling immediately before cleanup", unrelatedOutput);
            method.Invoke(null, arguments);

            await RequireOwnedCleanupAliveAsync(unrelated,
                "unrelated sibling immediately after cleanup; killError=" + arguments[4], unrelatedOutput);
            if (!root.HasExited)
                Assert.Fail("Owned root did not exit after cleanup. " + await DescribeOwnedCleanupActorAsync(root, rootOutput));
            Assert.Equal(!rootAlreadyExited, Assert.IsType<bool>(arguments[2]));
            Assert.Contains($"Unproven historical descendant pid={unrelated.Id}; not terminated.",
                Assert.IsType<string>(arguments[4]), StringComparison.Ordinal);
        }
        catch (Exception error) { primary = ExceptionDispatchInfo.Capture(error); }
        finally
        {
            var allKnownStopped = true;
            foreach (var (label, process) in new[] { ("root", root), ("unrelated sibling", unrelated) })
            {
                if (process is null) continue;
                try { StopOwnedCleanupCommand(process); }
                catch (Exception error)
                { cleanupFailures.Add(new IOException($"Owned {label} stop failed.", error)); }

                var knownStopped = false;
                try
                {
                    knownStopped = process.HasExited;
                    if (!knownStopped) cleanupFailures.Add(new IOException($"Owned {label} remains active."));
                }
                catch (Exception error)
                { cleanupFailures.Add(new IOException($"Owned {label} exit status is unproved.", error)); }
                if (!knownStopped) allKnownStopped = false;

                // Status and every disposal are attempted independently even if
                // this actor's stop, or the other actor's cleanup, failed.
                try { process.Dispose(); }
                catch (Exception error)
                { cleanupFailures.Add(new IOException($"Owned {label} handle disposal failed.", error)); }
            }
            if (allKnownStopped && cleanupFailures.Count == 0)
            {
                try { DeleteTempRoot(directory); }
                catch (Exception error)
                { cleanupFailures.Add(new IOException("Owned fixture deletion failed; retained path: " + directory, error)); }
            }
            else cleanupFailures.Add(new IOException("Owned actor cleanup is uncertain or failed; retained fixture: " + directory));
        }

        if (primary is not null && cleanupFailures.Count == 0) primary.Throw();
        if (primary is not null)
            throw new AggregateException("Native cleanup regression and owned actor cleanup failed.",
                new[] { primary.SourceException }.Concat(cleanupFailures));
        if (cleanupFailures.Count == 1) ExceptionDispatchInfo.Capture(cleanupFailures[0]).Throw();
        if (cleanupFailures.Count > 1) throw new AggregateException("Owned actor cleanup failed.", cleanupFailures);
    }

    private static async Task RequireOwnedCleanupReadyAsync(Process process, string label, List<string> observed)
    {
        var marker = "WOLF-CLEANUP-READY-" + Guid.NewGuid().ToString("N");
        var ready = false;
        var error = string.Empty;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            // Real command processing through its redirected stdin/stdout, not
            // a file/PID existence surrogate. The /q actor disables input echo;
            // set a whitespace prompt, flush an empty line, then require the
            // exact nonce line rather than accepting an echoed command.
            await process.StandardInput.WriteLineAsync("prompt $S");
            await process.StandardInput.WriteLineAsync("echo.");
            await process.StandardInput.WriteLineAsync("echo " + marker);
            await process.StandardInput.FlushAsync(timeout.Token);
            for (var lineNumber = 0; lineNumber < 16; lineNumber++)
            {
                var line = await process.StandardOutput.ReadLineAsync(timeout.Token);
                if (line is null) break;
                observed.Add(line.Length <= 2048 ? line : line[..2048]);
                if (line.Trim() == marker) { ready = true; break; }
            }
        }
        catch (Exception failure) when (failure is OperationCanceledException or IOException or InvalidOperationException)
        { error = failure.GetType().Name + ": " + failure.Message; }
        if (!ready)
            Assert.Fail($"Owned {label} did not produce its real READY line: {error}. " +
                await DescribeOwnedCleanupActorAsync(process, observed));
        await RequireOwnedCleanupAliveAsync(process, label + " after READY", observed);
    }

    private static async Task RequireOwnedCleanupAliveAsync(Process process, string label, List<string> observed)
    {
        try
        {
            if (!process.HasExited) return;
        }
        catch (Exception error)
        {
            Assert.Fail($"Owned {label} status is unproved: {error.GetType().Name}. " +
                await DescribeOwnedCleanupActorAsync(process, observed));
        }
        Assert.Fail($"Owned {label} unexpectedly exited. " + await DescribeOwnedCleanupActorAsync(process, observed));
    }

    private static async Task<string> DescribeOwnedCleanupActorAsync(Process process, List<string> observed)
    {
        var details = new List<string> { "observedStdout=" + string.Join(" | ", observed) };
        try
        {
            var exited = process.HasExited;
            details.Add("exited=" + exited);
            details.Add("exitCode=" + (exited ? process.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture) : "<live>"));
        }
        catch (Exception error) { details.Add("exitStatus=" + error.GetType().Name); }
        try { details.Add("stdoutTail=" + await ReadOwnedCleanupDiagnosticAsync(process.StandardOutput)); }
        catch (Exception error) { details.Add("stdout=" + error.GetType().Name); }
        try { details.Add("stderr=" + await ReadOwnedCleanupDiagnosticAsync(process.StandardError)); }
        catch (Exception error) { details.Add("stderr=" + error.GetType().Name); }
        return string.Join("; ", details);
    }

    private static async Task<string> ReadOwnedCleanupDiagnosticAsync(StreamReader reader)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try
        {
            var text = await reader.ReadToEndAsync(timeout.Token);
            return text.Length <= 2048 ? text : text[..2048] + "<truncated>";
        }
        catch (OperationCanceledException) { return "<bounded read timed out>"; }
    }

    private static Process StartOwnedCleanupCommand(string directory)
    {
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"))
        {
            UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = directory,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        start.ArgumentList.Add("/d"); start.ArgumentList.Add("/q"); start.ArgumentList.Add("/k");
        return Assert.IsType<Process>(Process.Start(start));
    }

    private static void StopOwnedCleanupCommand(Process process)
    {
        if (process.HasExited) return;
        process.Kill(entireProcessTree: false);
        Assert.True(process.WaitForExit(5000), "Owned command did not terminate; its fixture must be retained.");
    }
}
