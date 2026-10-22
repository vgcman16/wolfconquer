using Microsoft.Win32.SafeHandles;

namespace WolfConquer.Core.World;

// Complete native BinaryFile owner. The lazy handle bridge is limited to the
// proven Framework/.NET 9 SafeFileHandle getter position difference.
internal partial class LegacyNativeBinaryFile
{
    private FileStream? pFile;
    private SafeFileHandle? fileHandle;

    public bool Success => pFile != null;

    public LegacyNativeBinaryFile(string FileName, FileMode Mode)
    {
        Open(FileName, Mode);
    }

    public LegacyNativeBinaryFile()
    {
        pFile = null;
    }
}
