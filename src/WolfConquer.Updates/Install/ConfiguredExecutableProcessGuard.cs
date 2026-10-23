using System.Diagnostics;

namespace WolfConquer.Updates.Install;

// Queries only. Never terminates or attaches to an existing process.
public sealed class ConfiguredExecutableProcessGuard : IUpdateProcessGuard
{
    public Task EnsureStoppedAsync(IReadOnlyList<string> absoluteExecutablePaths, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("The client installer requires Windows process attribution.");
        if (absoluteExecutablePaths.Count == 0)
            throw new InvalidOperationException("Configure the protected client executable paths first.");
        var paths = absoluteExecutablePaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var names = paths.Select(Path.GetFileNameWithoutExtension).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Process[] processes;
        try { processes = Process.GetProcesses(); }
        catch (Exception error) { throw new IOException("Cannot enumerate running clients safely.", error); }
        try
        {
            foreach (var process in processes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (!names.Contains(process.ProcessName)) continue;
                    var image = ConfiguredProcessImageReader.Read(process.Id);
                    if (image is null) continue; // Exact retained process handle is signaled.
                    if (paths.Contains(image))
                        throw new ClientRunningException("Close the configured client before installing updates.");
                }
                catch (ClientRunningException) { throw; }
                catch (Exception error)
                {
                    throw new IOException("A process cannot be safely attributed; update refused.", error);
                }
            }
        }
        finally { foreach (var process in processes) process.Dispose(); }
        return Task.CompletedTask;
    }

    private sealed class ClientRunningException(string message) : IOException(message);
}
