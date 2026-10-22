using Microsoft.Win32.SafeHandles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeItemRecordFile
{
    internal bool ReadProf(ref LegacyNativeClientProficiency.DBProf buffer)
    {
        int read = -1;
        if (Success)
        {
            _ = ReadProfBytes(GetReadHandle(), ref buffer,
                Unsafe.SizeOf<LegacyNativeClientProficiency.DBProf>(), ref read, IntPtr.Zero);
            return read == Unsafe.SizeOf<LegacyNativeClientProficiency.DBProf>();
        }
        throw new IOException("File isn't open/failed to open previously");
    }

    internal bool WriteProf(ref LegacyNativeClientProficiency.DBProf buffer)
    {
        int written = -1;
        if (Success)
        {
            _ = WriteProfBytes(GetReadHandle(), ref buffer,
                Unsafe.SizeOf<LegacyNativeClientProficiency.DBProf>(), ref written, IntPtr.Zero);
            return written == Unsafe.SizeOf<LegacyNativeClientProficiency.DBProf>();
        }
        throw new IOException("File isn't open/failed to open previously");
    }

    // Same real file and lazy handle/kernel cursor as count/item/spell I/O.
    // Native reads/writes include padding and never repair short caller storage.
    [DllImport("kernel32.dll", EntryPoint = "ReadFile", SetLastError = true)]
    private static extern int ReadProfBytes(SafeFileHandle handle,
        [In, Out] ref LegacyNativeClientProficiency.DBProf buffer, int amount, ref int read, IntPtr overlapped);

    [DllImport("kernel32.dll", EntryPoint = "WriteFile", SetLastError = true)]
    private static extern int WriteProfBytes(SafeFileHandle handle,
        [In] ref LegacyNativeClientProficiency.DBProf buffer, int amount, ref int written, IntPtr overlapped);
}
