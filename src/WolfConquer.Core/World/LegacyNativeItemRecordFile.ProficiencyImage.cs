namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeItemRecordFile
{
    // The opened proficiency writer owns a complete dedicated file, not a
    // substream. A failed write can still leave this file partially replaced.
    internal void WriteCompleteProficiencyImage(ReadOnlySpan<byte> image)
    {
        if (file is null) throw new IOException("File isn't open/failed to open previously");
        file.Seek(0, SeekOrigin.Begin);
        file.Write(image);
        file.SetLength(image.Length);
        file.Flush(flushToDisk: true);
    }
}
