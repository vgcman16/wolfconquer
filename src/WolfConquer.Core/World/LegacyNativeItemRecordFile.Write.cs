using Microsoft.Win32.SafeHandles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeItemRecordFile
{
    internal bool WriteCount(ref int buffer)
    {
        int written = -1;
        if (Success)
        {
            _ = WriteCountBytes(GetReadHandle(), ref buffer, sizeof(int), ref written, IntPtr.Zero);
            return written == sizeof(int);
        }
        throw new IOException("File isn't open/failed to open previously");
    }

    internal bool WriteRecord(ref LegacyNativeClientItemRecord buffer)
    {
        int written = -1;
        if (Success)
        {
            _ = WriteRecordBytes(GetReadHandle(), ref buffer,
                Unsafe.SizeOf<LegacyNativeClientItemRecord>(), ref written, IntPtr.Zero);
            return written == Unsafe.SizeOf<LegacyNativeClientItemRecord>();
        }
        throw new IOException("File isn't open/failed to open previously");
    }

    internal bool WritePerfection(ref LegacyNativeClientItemPerfectionRecord buffer)
    {
        int written = -1;
        if (Success)
        {
            _ = WritePerfectionBytes(GetReadHandle(), ref buffer,
                Unsafe.SizeOf<LegacyNativeClientItemPerfectionRecord>(), ref written, IntPtr.Zero);
            return written == Unsafe.SizeOf<LegacyNativeClientItemPerfectionRecord>();
        }
        throw new IOException("File isn't open/failed to open previously");
    }

    // BinaryFile.Write ignores WriteFile's BOOL and compares only the count.
    // Blittable caller-owned buffers, including padding, are passed by reference.
    [DllImport("kernel32.dll", EntryPoint = "WriteFile", SetLastError = true)]
    private static extern int WriteCountBytes(SafeFileHandle handle,
        [In] ref int buffer, int amount, ref int written, IntPtr overlapped);

    [DllImport("kernel32.dll", EntryPoint = "WriteFile", SetLastError = true)]
    private static extern int WriteRecordBytes(SafeFileHandle handle,
        [In] ref LegacyNativeClientItemRecord buffer, int amount, ref int written, IntPtr overlapped);

    [DllImport("kernel32.dll", EntryPoint = "WriteFile", SetLastError = true)]
    private static extern int WritePerfectionBytes(SafeFileHandle handle,
        [In] ref LegacyNativeClientItemPerfectionRecord buffer, int amount, ref int written, IntPtr overlapped);
}
