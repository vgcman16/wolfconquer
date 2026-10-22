using Microsoft.Win32.SafeHandles;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeBinaryFile
{
    // The original Framework getter preserves the advancing kernel pointer;
    // .NET 9's repeated getter resynchronizes it to a stale managed position.
    // Acquire only at the original first-access point, sharing the actual
    // FileStream-owned handle across Position, Read and Write. After Close,
    // re-enter the genuine getter to retain its disposed-stream exception.
    private SafeFileHandle GetFileHandle()
    {
        if (fileHandle is null || fileHandle.IsClosed)
            fileHandle = pFile!.SafeFileHandle;
        return fileHandle;
    }
}
