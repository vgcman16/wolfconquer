namespace WolfConquer.Core.World;

internal partial class LegacyNativeBinaryFile
{
    public unsafe bool Write(void* Buffer, int Count, int Size)
    {
        return Write(Buffer, unchecked(Count * Size));
    }

    public unsafe bool Write(void* Buffer, int AmountOfBytes)
    {
        if (AmountOfBytes < 0)
        {
            throw new ArgumentException("AmountOfBytes");
        }
        int num = -1;
        if (Success)
        {
            LegacyNativeKernel32File.WriteFile(GetFileHandle(), (byte*)Buffer, AmountOfBytes, &num, IntPtr.Zero);
            return num == AmountOfBytes;
        }
        throw new IOException("File isn't open/failed to open previously");
    }
}
