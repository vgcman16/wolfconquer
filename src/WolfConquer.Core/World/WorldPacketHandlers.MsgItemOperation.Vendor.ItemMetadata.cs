using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal static bool TryGetLegacyVendorMetadata(WorldPacketHandlerContext context, ConquerTcpConnection connection,
        WorldSession seller, LegacyItemSaveGraph.Closure closure, out LegacyTradeItemMetadata? metadata)
    {
        metadata = context.WorldData?.TradeItemMetadata;
        if (context.Sessions is not { } sessions) return false;
        if (context.WorldData is not { } catalog) return sessions.IsUnboundTradeItemOwnerCurrent(connection, seller);
        if (metadata is null || !metadata.IsCurrent(catalog)) return false;
        return metadata.HasEmptySource ? sessions.IsUnboundTradeItemOwnerCurrent(connection, seller) :
            closure.AllSourceRows(metadata.Allows);
    }

    private static bool IsLegacyVendorMetadataPairCurrent(WorldPacketHandlerContext context, WorldSession buyer,
        ConquerTcpConnection sellerConnection, WorldSession seller)
    {
        var metadata = context.WorldData?.TradeItemMetadata;
        if (context.WorldData is { } catalog && (metadata is null || !metadata.IsCurrent(catalog))) return false;
        if (metadata is { HasEmptySource: false }) return true;
        return context.Sessions is { } sessions && sessions.IsUnboundTradeItemOwnerCurrent(context.Connection, buyer) &&
            sessions.IsUnboundTradeItemOwnerCurrent(sellerConnection, seller);
    }
}
