namespace WolfConquer.Core.World;

internal partial class LegacyNativeBinaryFile
{
    public bool Open(string FileName, FileMode Mode)
    {
        try
        {
            pFile = new FileStream(FileName, Mode);
        }
        catch
        {
            pFile = null;
        }
        fileHandle = null; // Every Open outcome begins a new lazy handle lifetime.
        return Success;
    }

    public void Close()
    {
        if (Success)
        {
            pFile!.Close();
            return;
        }
        throw new IOException("File isn't open/failed to open previously");
    }

    public bool Reopen(string FileName, FileMode Mode)
    {
        Close();
        return Open(FileName, Mode);
    }
}
