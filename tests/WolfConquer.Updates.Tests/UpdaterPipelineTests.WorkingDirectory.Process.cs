using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Xunit;

namespace WolfConquer.Updates.Tests;

public sealed partial class UpdaterPipelineTests
{
    private static async Task WaitForOwnedCommand(int processId, string target)
    {
        Process child;
        try { child = Process.GetProcessById(processId); }
        catch (ArgumentException) { return; } // Already gone; the caller still requires actual cwd output.
        using (child)
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
        {
            bool attributed = false, handleCaptured = false;
            string? observed = null;
            int nativeError = 0;
            try
            {
                // Retain this exact process handle across refreshes; never act on a later PID reuse.
                var handle = child.SafeHandle;
                handleCaptured = true;
                var startup = Stopwatch.StartNew();
                var buffer = new StringBuilder(32768);
                while (startup.Elapsed < TimeSpan.FromSeconds(2))
                {
                    child.Refresh();
                    if (child.HasExited) { Assert.Equal(0, child.ExitCode); return; }
                    buffer.Clear();
                    int length = buffer.Capacity;
                    if (QueryOwnedProcessImage(handle, 0, buffer, ref length))
                    {
                        observed = buffer.ToString(); nativeError = 0;
                        if (string.Equals(target, observed, StringComparison.OrdinalIgnoreCase))
                        { attributed = true; break; }
                    }
                    else { nativeError = Marshal.GetLastWin32Error(); observed = null; }
                    await Task.Delay(25, timeout.Token);
                }
                child.Refresh();
                if (child.HasExited) { Assert.Equal(0, child.ExitCode); return; }
                Assert.True(attributed, $"Owned command identity was not established within two seconds. PID={processId}; " +
                    $"expected='{target}'; observed='{observed ?? "<null>"}'; nativeError={nativeError}. No unattributed PID was terminated.");
                await child.WaitForExitAsync(timeout.Token);
                Assert.Equal(0, child.ExitCode);
            }
            catch (Exception error) when (!handleCaptured && (error is InvalidOperationException or Win32Exception) && child.HasExited)
            {
                // The process disappeared before a handle could be captured. Its owned output is still mandatory.
            }
            finally
            {
                if (attributed && !child.HasExited)
                {
                    // Failure cleanup only: timeout/identity/assertion exceptions remain failures, never success.
                    child.Kill(entireProcessTree: false);
                    using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await child.WaitForExitAsync(cleanup.Token);
                }
            }
        }
    }

    // Query only the process returned by this fixture's launch. Win32-format image identity
    // does not depend on user-mode module enumeration finishing during process startup.
    [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", ExactSpelling = true,
        CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryOwnedProcessImage(SafeProcessHandle process, int flags, StringBuilder path, ref int length);
}
