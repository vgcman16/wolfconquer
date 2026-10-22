using Microsoft.Win32.SafeHandles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace WolfConquer.Core.World;

// BinaryFile Open/Success/Close + count, item, perfection, spell and proficiency I/O.
// Caller owns every buffer. False reads do not clear or repair caller storage.
// Native Perfection is fresh per postlude iteration; reused count lifetime is separate.
// Not the loader loop, route owner, serializer or full BinaryFile API.
internal sealed partial class LegacyNativeItemRecordFile
{
    private FileStream? file;
    private SafeFileHandle? readHandle;

    internal LegacyNativeItemRecordFile() => file = null;

    internal LegacyNativeItemRecordFile(string fileName, FileMode mode)
    {
        Open(fileName, mode);
    }

    internal bool Success => file != null;

    internal bool Open(string fileName, FileMode mode)
    {
        try
        {
            file = new FileStream(fileName, mode);
        }
        catch
        {
            file = null;
        }
        readHandle = null; // Every Open outcome starts a different handle-cache lifetime.
        return Success;
    }

    internal void Close()
    {
        if (Success)
        {
            file!.Close();
            return;
        }
        throw new IOException("File isn't open/failed to open previously");
    }

    internal bool ReadCount(ref int buffer)
    {
        int read = -1;
        if (Success)
        {
            _ = ReadCountBytes(GetReadHandle(), ref buffer, sizeof(int), ref read, IntPtr.Zero);
            return read == sizeof(int);
        }
        throw new IOException("File isn't open/failed to open previously");
    }

    internal bool ReadRecord(ref LegacyNativeClientItemRecord buffer)
    {
        int read = -1;
        if (Success)
        {
            _ = ReadRecordBytes(GetReadHandle(), ref buffer,
                Unsafe.SizeOf<LegacyNativeClientItemRecord>(), ref read, IntPtr.Zero);
            return read == Unsafe.SizeOf<LegacyNativeClientItemRecord>();
        }
        throw new IOException("File isn't open/failed to open previously");
    }

    // Caller storage is not initialized here. Native postlude creates a fresh
    // Perfection inside each iteration; its reused count has a separate lifetime.
    internal bool ReadPerfection(ref LegacyNativeClientItemPerfectionRecord buffer)
    {
        int read = -1;
        if (Success)
        {
            _ = ReadPerfectionBytes(GetReadHandle(), ref buffer,
                Unsafe.SizeOf<LegacyNativeClientItemPerfectionRecord>(), ref read, IntPtr.Zero);
            return read == Unsafe.SizeOf<LegacyNativeClientItemPerfectionRecord>();
        }
        throw new IOException("File isn't open/failed to open previously");
    }

    // .NET 9's SafeFileHandle getter can seek to its stale managed position.
    // Capture it lazily once per Open, at the original first-I/O fault point.
    // Typed writes share this same cache and advancing kernel file position.
    // After Close, re-enter that getter only to preserve its disposed-stream fault.
    private SafeFileHandle GetReadHandle()
    {
        if (readHandle is null || readHandle.IsClosed)
            readHandle = file!.SafeFileHandle;
        return readHandle;
    }

    // These existing buffers are blittable and passed by reference, never through an out
    // record, fresh byte array, retrying Stream.ReadExactly or DTO conversion.
    // ReadFile's int return is ignored, exactly as BinaryFile.Read ignores it.
    [DllImport("kernel32.dll", EntryPoint = "ReadFile", SetLastError = true)]
    private static extern int ReadCountBytes(SafeFileHandle handle,
        [In, Out] ref int buffer, int amount, ref int read, IntPtr overlapped);

    [DllImport("kernel32.dll", EntryPoint = "ReadFile", SetLastError = true)]
    private static extern int ReadRecordBytes(SafeFileHandle handle,
        [In, Out] ref LegacyNativeClientItemRecord buffer, int amount, ref int read, IntPtr overlapped);

    [DllImport("kernel32.dll", EntryPoint = "ReadFile", SetLastError = true)]
    private static extern int ReadPerfectionBytes(SafeFileHandle handle,
        [In, Out] ref LegacyNativeClientItemPerfectionRecord buffer, int amount, ref int read, IntPtr overlapped);
}
