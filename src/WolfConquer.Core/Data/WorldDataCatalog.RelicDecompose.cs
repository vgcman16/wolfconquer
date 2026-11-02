namespace WolfConquer.Core.Data;

public sealed partial class WorldDataCatalog
{
    private LegacyRelicDecomposeMetadata? relicDecomposeMetadata;
    internal LegacyRelicDecomposeMetadata? RelicDecomposeMetadata => relicDecomposeMetadata;

    private void InitializeRelicDecomposeMetadata()
    {
        try { relicDecomposeMetadata = LegacyRelicDecomposeMetadata.Capture(this); }
        catch (Exception error) when (error is ArgumentException or IndexOutOfRangeException or
            InvalidOperationException or OverflowException)
        { relicDecomposeMetadata = null; }
    }
}
