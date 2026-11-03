namespace WolfConquer.Core.Data;

public sealed partial class WorldDataCatalog
{
    private LegacyTradeItemMetadata? tradeItemMetadata;
    internal LegacyTradeItemMetadata? TradeItemMetadata => tradeItemMetadata;

    private void InitializeLegacyTradeItemMetadata()
    {
        try { tradeItemMetadata = LegacyTradeItemMetadata.Capture(this); }
        catch (Exception error) when (error is ArgumentException or IndexOutOfRangeException or
            InvalidOperationException or OverflowException)
        { tradeItemMetadata = null; }
    }
}
