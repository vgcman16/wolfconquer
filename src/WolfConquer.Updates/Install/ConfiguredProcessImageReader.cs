using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace WolfConquer.Updates.Install;

// A read-only query lease, not update authorization. The public guard always opens
// the real Windows handle; deterministic tests supply only these observed reads.
internal interface IConfiguredProcessImageLease
{
    bool HasExited();
    string ReadImage();
}

internal static class ConfiguredProcessImageReader
{
    internal static string? Read(int processId)
    {
        using var lease = WindowsProcessImageLease.Open(processId);
        return Read(lease);
    }

    internal static string? Read(IConfiguredProcessImageLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        if (lease.HasExited()) return null;
        try
        {
            var image = lease.ReadImage();
            if (string.IsNullOrWhiteSpace(image) || !Path.IsPathFullyQualified(image))
                throw new IOException("Client image attribution unavailable or invalid.");
            return Path.GetFullPath(image);
        }
        catch (Exception error) when (error is Win32Exception or IOException or InvalidOperationException or ArgumentException)
        {
            // Check the SAME retained handle, never a newly opened process with a reused PID.
            // Unknown/denied exit status throws; an alive unattributable process still refuses.
            if (lease.HasExited()) return null;
            throw;
        }
    }

    private sealed class WindowsProcessImageLease(SafeProcessHandle handle) : IConfiguredProcessImageLease, IDisposable
    {
        private const uint QueryLimitedInformation = 0x1000;
        private const uint Synchronize = 0x00100000;
        private const int MaximumPathCharacters = 32768;

        internal static WindowsProcessImageLease Open(int processId)
        {
            if (processId <= 0) throw new ArgumentOutOfRangeException(nameof(processId));
            var opened = OpenProcess(QueryLimitedInformation | Synchronize, false, processId);
            if (!opened.IsInvalid) return new(opened);
            int error = Marshal.GetLastWin32Error();
            opened.Dispose();
            // No retained handle means no exact-process exit proof: stay fail-closed,
            // even when disappearance before OpenProcess is a plausible explanation.
            throw new Win32Exception(error, "Cannot open a read-only client attribution handle.");
        }

        public bool HasExited()
        {
            uint result = WaitForSingleObject(handle, 0);
            return result switch
            {
                0 => true,
                258 => false,
                uint.MaxValue => throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot establish client exit state."),
                _ => throw new IOException("Unexpected client process wait result: " + result),
            };
        }

        public string ReadImage()
        {
            var path = new StringBuilder(MaximumPathCharacters);
            int length = path.Capacity;
            if (!QueryFullProcessImageName(handle, 0, path, ref length))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot read the client executable image.");
            if (length <= 0 || length >= MaximumPathCharacters || path.Length != length)
                throw new IOException("Client image attribution returned an invalid length.");
            return path.ToString();
        }

        public void Dispose() => handle.Dispose();

        [DllImport("kernel32.dll", EntryPoint = "OpenProcess", ExactSpelling = true, SetLastError = true)]
        private static extern SafeProcessHandle OpenProcess(uint access,
            [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

        [DllImport("kernel32.dll", EntryPoint = "WaitForSingleObject", ExactSpelling = true, SetLastError = true)]
        private static extern uint WaitForSingleObject(SafeProcessHandle process, uint milliseconds);

        [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", ExactSpelling = true,
            CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryFullProcessImageName(SafeProcessHandle process, int flags,
            StringBuilder path, ref int characters);
    }
}
