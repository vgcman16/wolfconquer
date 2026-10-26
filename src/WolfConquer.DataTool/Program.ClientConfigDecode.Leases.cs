using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

internal static partial class Program
{
    private static void RequireConfigDecodeOrdinaryPath(string path)
    {
        // POSIX FIFOs must be rejected before opening a read handle can block.
        if (OperatingSystem.IsWindows()) return;
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("Ordinary read-path identity is not supported on this platform.");
        _ = ReadConfigDecodeLinuxInformation(-100, path, 0x100);
    }

    // Filesystem metadata only, obtained from the already-held read handle.
    // No client DLL, executable, provider, key store or process is invoked.
    private static void RequireConfigDecodeOrdinaryLease(FileStream lease)
    {
        if (OperatingSystem.IsWindows())
        {
            if (!GetConfigDecodeFileInformation(lease.SafeFileHandle, out var information) ||
                information.NumberOfLinks != 1 ||
                (information.Attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.Device)) != 0)
                throw new IOException("Decoder requires a proven ordinary single-link input lease.");
            return;
        }
        if (OperatingSystem.IsLinux())
        {
            _ = ReadConfigDecodeLinuxInformation(lease.SafeFileHandle.DangerousGetHandle().ToInt32(), "", 0x1000);
            return;
        }
        throw new PlatformNotSupportedException("Ordinary read-lease identity is not supported on this platform.");
    }

    private static ConfigDecodeLinuxInformation ReadConfigDecodeLinuxInformation(int descriptor, string path, int flags)
    {
        const uint typeAndLinks = 0x5;
        try
        {
            if (GetConfigDecodeLinuxInformation(descriptor, path, flags, typeAndLinks, out var information) != 0 ||
                (information.Mask & typeAndLinks) != typeAndLinks || information.NumberOfLinks != 1 ||
                (information.Mode & 0xF000) != 0x8000)
                throw new IOException("Decoder requires a proven ordinary single-link input.");
            return information;
        }
        catch (Exception unavailable) when (unavailable is DllNotFoundException or EntryPointNotFoundException)
        {
            throw new PlatformNotSupportedException("Ordinary Linux lease metadata is unavailable.", unavailable);
        }
    }

    [DllImport("kernel32.dll", EntryPoint = "GetFileInformationByHandle", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetConfigDecodeFileInformation(SafeFileHandle file, out ConfigDecodeWindowsInformation information);

    [StructLayout(LayoutKind.Sequential)]
    private struct ConfigDecodeWindowsInformation
    {
        internal FileAttributes Attributes;
        internal System.Runtime.InteropServices.ComTypes.FILETIME CreationTime, AccessTime, WriteTime;
        internal uint VolumeSerialNumber, FileSizeHigh, FileSizeLow, NumberOfLinks, FileIndexHigh, FileIndexLow;
    }

    [DllImport("libc", EntryPoint = "statx", SetLastError = true)]
    private static extern int GetConfigDecodeLinuxInformation(int descriptor, string path, int flags, uint mask,
        out ConfigDecodeLinuxInformation information);

    [StructLayout(LayoutKind.Explicit, Size = 256)]
    private struct ConfigDecodeLinuxInformation
    {
        [FieldOffset(0)] internal uint Mask;
        [FieldOffset(16)] internal uint NumberOfLinks;
        [FieldOffset(28)] internal ushort Mode;
    }
}
