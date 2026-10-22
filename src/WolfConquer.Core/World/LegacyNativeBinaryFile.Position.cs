namespace WolfConquer.Core.World;

internal partial class LegacyNativeBinaryFile
{
    public unsafe int Position
    {
        get
        {
            if (Success)
            {
                return LegacyNativeKernel32File.SetFilePointer(GetFileHandle(), 0, null, SeekOrigin.Current);
            }
            throw new IOException("File isn't open/failed to open previously");
        }
        set
        {
            if (Success)
            {
                int lo = unchecked(value - Position);
                LegacyNativeKernel32File.SetFilePointer(GetFileHandle(), lo, null, SeekOrigin.Current);
                return;
            }
            throw new IOException("File isn't open/failed to open previously");
        }
    }
}
