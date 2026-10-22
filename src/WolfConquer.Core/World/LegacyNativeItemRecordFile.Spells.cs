using Microsoft.Win32.SafeHandles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeItemRecordFile
{
    internal bool ReadSpell(ref LegacyNativeClientSpells.DBSpell buffer)
    {
        int read = -1;
        if (Success)
        {
            _ = ReadSpellBytes(GetReadHandle(), ref buffer,
                Unsafe.SizeOf<LegacyNativeClientSpells.DBSpell>(), ref read, IntPtr.Zero);
            return read == Unsafe.SizeOf<LegacyNativeClientSpells.DBSpell>();
        }
        throw new IOException("File isn't open/failed to open previously");
    }

    internal bool WriteSpell(ref LegacyNativeClientSpells.DBSpell buffer)
    {
        int written = -1;
        if (Success)
        {
            _ = WriteSpellBytes(GetReadHandle(), ref buffer,
                Unsafe.SizeOf<LegacyNativeClientSpells.DBSpell>(), ref written, IntPtr.Zero);
            return written == Unsafe.SizeOf<LegacyNativeClientSpells.DBSpell>();
        }
        throw new IOException("File isn't open/failed to open previously");
    }

    // Same physical file, lazily cached handle and kernel cursor as all other typed I/O.
    // Short reads retain untouched caller bytes, including the native record padding.
    [DllImport("kernel32.dll", EntryPoint = "ReadFile", SetLastError = true)]
    private static extern int ReadSpellBytes(SafeFileHandle handle,
        [In, Out] ref LegacyNativeClientSpells.DBSpell buffer, int amount, ref int read, IntPtr overlapped);

    [DllImport("kernel32.dll", EntryPoint = "WriteFile", SetLastError = true)]
    private static extern int WriteSpellBytes(SafeFileHandle handle,
        [In] ref LegacyNativeClientSpells.DBSpell buffer, int amount, ref int written, IntPtr overlapped);
}
