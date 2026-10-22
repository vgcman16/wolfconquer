using System.Diagnostics;

namespace WolfConquer.Updates.Tests;

public sealed partial class ClientAssetLayoutTests
{
    private sealed class OwnedGuardCommand : IAsyncDisposable
    {
        private readonly Process process;
        private readonly Task<string> output;
        private readonly Task<string> error;
        internal string Executable { get; }
        internal bool HasExited => process.HasExited;

        private OwnedGuardCommand(string executable, Process child)
        {
            Executable = executable; process = child;
            _ = process.SafeHandle; // Keep the exact Process.Start handle; never reopen or clean up by PID.
            output = process.StandardOutput.ReadToEndAsync();
            error = process.StandardError.ReadToEndAsync();
        }

        internal static async Task<OwnedGuardCommand> StartAsync(string root, string name)
        {
            string owned = Path.GetFullPath(root);
            string temporaryRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
            if (!owned.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(owned).StartsWith("wolf-layout-", StringComparison.Ordinal) ||
                Path.GetFileName(name) != name || !name.EndsWith(".exe", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing command fixture outside its owned temporary root.");
            string directory = Directory.CreateDirectory(Path.Combine(owned, "owned-guard-command")).FullName;
            string executable = Path.Combine(directory, name);
            string systemCommand = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            Assert.True(File.Exists(systemCommand)); File.Copy(systemCommand, executable, overwrite: false);
            var start = new ProcessStartInfo(executable)
            {
                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                WorkingDirectory = directory
            };
            start.ArgumentList.Add("/d"); start.ArgumentList.Add("/q"); start.ArgumentList.Add("/k");
            var command = new OwnedGuardCommand(executable, Process.Start(start)!);
            try
            {
                // Establish a settled real module before testing exact-path attribution. A startup
                // null module must not be treated as successful guard evidence or a reason to retry it.
                var elapsed = Stopwatch.StartNew();
                while (elapsed.Elapsed < TimeSpan.FromSeconds(5))
                {
                    command.process.Refresh(); Assert.False(command.HasExited, "Owned command exited before identity was established.");
                    string? image = command.process.MainModule?.FileName;
                    if (!string.IsNullOrEmpty(image))
                    {
                        Assert.Equal(executable, Path.GetFullPath(image), StringComparer.OrdinalIgnoreCase);
                        return command;
                    }
                    await Task.Delay(25);
                }
                throw new TimeoutException("Owned command did not publish its exact module identity within five seconds.");
            }
            catch (Exception failure)
            {
                try { await command.DisposeAsync(); }
                catch (Exception cleanup) { throw new AggregateException("Owned command startup and bounded cleanup both failed.", failure, cleanup); }
                throw;
            }
        }

        internal async Task StopAsync()
        {
            if (!process.HasExited)
            {
                await process.StandardInput.WriteLineAsync("exit 0");
                await process.StandardInput.FlushAsync(); process.StandardInput.Close();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await process.WaitForExitAsync(timeout.Token);
            }
            Assert.Equal(0, process.ExitCode);
            await Task.WhenAll(output, error).WaitAsync(TimeSpan.FromSeconds(5));
        }

        public async ValueTask DisposeAsync()
        {
            try { await StopAsync(); }
            catch (Exception failure)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: false); // Only the retained, originally returned handle.
                        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        await process.WaitForExitAsync(timeout.Token);
                    }
                }
                catch (Exception cleanup) { throw new AggregateException("Owned command stop and bounded cleanup both failed.", failure, cleanup); }
                throw; // Cleanup cannot turn a failed graceful stop into test success.
            }
            finally { process.Dispose(); }
        }
    }
}
