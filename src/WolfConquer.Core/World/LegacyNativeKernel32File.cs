using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace WolfConquer.Core.World;

// Only the three native BinaryFile dependencies, with their actual signatures.
internal class LegacyNativeKernel32File
{
    [DllImport("kernel32.dll")]
    public unsafe static extern int SetFilePointer(SafeFileHandle handle, int lo, int* hi, SeekOrigin origin);

    [DllImport("kernel32.dll", SetLastError = true)]
    public unsafe static extern int WriteFile(SafeFileHandle handle, byte* bytes, int numBytesToWrite, int* numBytesWritten, IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    public unsafe static extern int ReadFile(SafeFileHandle handle, byte* bytes, int numBytesToRead, int* numBytesRead, IntPtr lpOverlapped);
}
