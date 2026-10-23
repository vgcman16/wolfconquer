using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeItemRecordFile
{
    // Whole native BinaryFile raw Read/Write contract, specialized to the genuine322-byte
    // detained record. Pointer transport retains the source one-byte bool at offset32;
    // do not marshal the explicit struct's bool as a four-byte Win32 BOOL.
    internal unsafe bool ReadDetained(ref LegacyNativeMsgDetainedItem buffer)
    {
        int read = -1;
        if (Success)
        {
            fixed (LegacyNativeMsgDetainedItem* pointer = &buffer)
                _ = ReadDetainedBytes(GetReadHandle(), pointer, sizeof(LegacyNativeMsgDetainedItem), ref read, IntPtr.Zero);
            return read == sizeof(LegacyNativeMsgDetainedItem);
        }
        throw new IOException("File isn't open/failed to open previously");
    }

    internal unsafe bool WriteDetained(ref LegacyNativeMsgDetainedItem buffer)
    {
        int written = -1;
        if (Success)
        {
            fixed (LegacyNativeMsgDetainedItem* pointer = &buffer)
                _ = WriteDetainedBytes(GetReadHandle(), pointer, sizeof(LegacyNativeMsgDetainedItem), ref written, IntPtr.Zero);
            return written == sizeof(LegacyNativeMsgDetainedItem);
        }
        throw new IOException("File isn't open/failed to open previously");
    }

    [DllImport("kernel32.dll", EntryPoint = "ReadFile", SetLastError = true)]
    private static extern unsafe int ReadDetainedBytes(SafeFileHandle handle, void* buffer, int count, ref int read, IntPtr overlapped);

    [DllImport("kernel32.dll", EntryPoint = "WriteFile", SetLastError = true)]
    private static extern unsafe int WriteDetainedBytes(SafeFileHandle handle, void* buffer, int count, ref int written, IntPtr overlapped);
}
