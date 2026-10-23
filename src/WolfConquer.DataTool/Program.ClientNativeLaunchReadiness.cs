using System.Diagnostics;
using System.Globalization;
using System.Text;

internal static partial class Program
{
    // Opt-in: require a fresh file, this target PID, and a completed marker after
    // actual target setup. This check never removes a pre-existing path.
    private static void ValidateClientNativeLaunchReadinessPath(ClientNativeLaunchProbeOptions options)
    {
        if (options.ReadyFilePath is not string path)
        {
            return;
        }

        if (File.Exists(path) || Directory.Exists(path))
        {
            throw new InvalidOperationException("Readiness path must be fresh and absent before launch: " + path);
        }

        var directory = Path.GetDirectoryName(path);
        if (directory is null || !Directory.Exists(directory))
        {
            throw new InvalidOperationException("Readiness directory must already exist: " + path);
        }
    }

    private static ClientNativeLaunchReadiness PendingClientNativeLaunchReadiness(
        ClientNativeLaunchProbeOptions options) =>
        new(options.ReadyFilePath is not null, false, false, string.Empty, options.KillAfterMs ?? options.TimeoutMs);

    private static ClientNativeLaunchReadiness WaitForClientNativeLaunchReadiness(
        ClientNativeLaunchProbeOptions options,
        Process process,
        Stopwatch stopwatch)
    {
        if (options.ReadyFilePath is not string path)
        {
            return PendingClientNativeLaunchReadiness(options);
        }

        // All new waits share the original stopwatch budget. Reserve the full
        // unchanged steady-state interval; fail explicitly if it no longer fits.
        var reserveMilliseconds = options.KillAfterMs ?? 0;
        var readinessDeadline = options.TimeoutMs - reserveMilliseconds;
        var latestMarkerError = "Readiness marker was not observed.";
        string? watcherError = null;
        ClientNativeLaunchReadiness? result = null;
        FileSystemWatcher? watcher = null;
        using var changed = new ManualResetEventSlim(false);
        void Signal()
        {
            try
            {
                changed.Set();
            }
            catch (ObjectDisposedException)
            {
                // A callback already dispatched before unsubscribe may arrive late.
            }
        }

        void OnChanged(object sender, FileSystemEventArgs args) => Signal();
        void OnRenamed(object sender, RenamedEventArgs args) => Signal();
        void OnExited(object? sender, EventArgs args) => Signal();
        void OnWatcherError(object sender, ErrorEventArgs args)
        {
            Volatile.Write(ref watcherError, "Readiness watcher error: " + args.GetException().GetType().Name);
            Signal();
        }

        try
        {
            watcher = new FileSystemWatcher(Path.GetDirectoryName(path)!, Path.GetFileName(path))
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite | NotifyFilters.Size,
                IncludeSubdirectories = false,
            };
            watcher.Created += OnChanged;
            watcher.Changed += OnChanged;
            watcher.Renamed += OnRenamed;
            watcher.Error += OnWatcherError;
            process.Exited += OnExited;
            process.EnableRaisingEvents = true;
            watcher.EnableRaisingEvents = true;
            var processStartUtc = process.StartTime.ToUniversalTime();
            while (result is null)
            {
                // Reset before state checks so an event during them is retained.
                // The initial check also covers creation before watcher activation.
                changed.Reset();
                if (ClientNativeRuntimeHasExited(process))
                {
                    result = new(true, false, false, "Target exited before readiness was observed.", 0);
                    break;
                }

                if (Volatile.Read(ref watcherError) is string failure)
                {
                    result = new(true, false, false, failure, 0);
                    break;
                }

                var remaining = readinessDeadline - stopwatch.ElapsedMilliseconds;
                if (remaining <= 0)
                {
                    result = new(true, false, true, latestMarkerError + " Startup budget exhausted.", 0);
                    break;
                }

                if (TryReadClientNativeLaunchReadyMarker(path, process.Id, processStartUtc, out latestMarkerError))
                {
                    result = ClientNativeRuntimeHasExited(process)
                        ? new(true, false, false, "Target exited before readiness was observed.", 0)
                        : ResolveClientNativeLaunchReadyWindow(options, stopwatch.ElapsedMilliseconds);
                    break;
                }

                changed.Wait((int)Math.Min(remaining, int.MaxValue));
            }
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            result = new(true, false, false, "Readiness failed: " + FormatClientNativeLaunchProbeException(ex), 0);
        }
        finally
        {
            process.Exited -= OnExited;
            if (watcher is not null)
            {
                watcher.Created -= OnChanged;
                watcher.Changed -= OnChanged;
                watcher.Renamed -= OnRenamed;
                watcher.Error -= OnWatcherError;
                try
                {
                    watcher.EnableRaisingEvents = false;
                }
                catch (Exception ex) when (
                    ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    result = new(true, result?.Observed ?? false, result?.TimedOut ?? false,
                        "Readiness watcher cleanup failed: " + FormatClientNativeLaunchProbeException(ex), 0);
                }

                try
                {
                    watcher.Dispose();
                }
                catch (Exception ex) when (
                    ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    result = new(true, result?.Observed ?? false, result?.TimedOut ?? false,
                        "Readiness watcher disposal failed: " + FormatClientNativeLaunchProbeException(ex), 0);
                }
            }
        }

        return result ?? new(true, false, false, "Readiness ended without an observation.", 0);
    }

    internal static ClientNativeLaunchReadiness ResolveClientNativeLaunchReadyWindow(
        ClientNativeLaunchProbeOptions options,
        long elapsedMilliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(elapsedMilliseconds);
        var available = options.TimeoutMs - elapsedMilliseconds;
        if (available <= 0 || (options.KillAfterMs is int interval && available < interval))
        {
            return new(true, true, true, "Insufficient original timeout budget for the unchanged kill interval.", 0);
        }

        return new(true, true, false, string.Empty, options.KillAfterMs ?? (int)available);
    }

    internal static bool TryReadClientNativeLaunchReadyMarker(
        string path,
        int processId,
        DateTime processStartUtc,
        out string error)
    {
        error = "Readiness marker was not observed.";
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists)
            {
                return false;
            }

            if (file.CreationTimeUtc < processStartUtc)
            {
                error = "Readiness marker predates the target process.";
                return false;
            }

            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var bytes = new byte[65];
            var count = 0;
            while (count < bytes.Length)
            {
                var read = input.Read(bytes, count, bytes.Length - count);
                if (read == 0)
                {
                    break;
                }

                count += read;
            }

            if (count > 64)
            {
                error = "Readiness marker exceeds 64 bytes.";
                return false;
            }

            var marker = Encoding.UTF8.GetString(bytes, 0, count);
            var expected = "pid=" + processId.ToString(CultureInfo.InvariantCulture) + "\nready\n";
            if (!string.Equals(marker, expected, StringComparison.Ordinal))
            {
                error = "Readiness marker is incomplete or does not identify the target PID.";
                return false;
            }

            error = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = "Readiness marker not readable: " + ex.GetType().Name;
            return false;
        }
    }
}

internal sealed record ClientNativeLaunchReadiness(
    bool Requested,
    bool Observed,
    bool TimedOut,
    string Error,
    int WaitMilliseconds);
