using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool LegacyTradeMetadataAllows(WorldPacketHandlerContext context, LegacyTradeParty party,
        LegacyItemSaveGraph.Closure closure)
    {
        if (context.WorldData is not { } catalog) return IsGenuinelyUnboundLegacyTradePair(context, party);
        if (catalog.TradeItemMetadata is not { } metadata || !metadata.IsCurrent(catalog)) return false;
        // Existing detached managed components can lack a catalog. This exact
        // Store proof cannot accept a current or retired native owned attachment.
        return metadata.HasEmptySource ? IsGenuinelyUnboundLegacyTradePair(context, party) :
            closure.AllSourceRows(metadata.Allows);
    }

    private static bool IsGenuinelyUnboundLegacyTradePair(WorldPacketHandlerContext context, LegacyTradeParty party) =>
        context.Sessions is { } sessions && sessions.TryGet(party.Connection, out var expected) && expected is not null &&
        expected.Character.Uid == party.Uid && sessions.IsUnboundTradeItemOwnerCurrent(party.Connection, expected) &&
        sessions.TryGet(party.Other.Connection, out var other) && other is not null && other.Character.Uid == party.Other.Uid &&
        sessions.IsUnboundTradeItemOwnerCurrent(party.Other.Connection, other);

    private static bool LegacyTradeMetadataOwnerCurrent(WorldPacketHandlerContext context, LegacyTradeParty party,
        LegacyTradeOffer offer, LegacyItemSaveGraph.Closure current) =>
        ReferenceEquals(offer.MetadataOwner, context.WorldData?.TradeItemMetadata) &&
        LegacyTradeMetadataAllows(context, party, current);
}
