using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool ValidLegacyTradeOffers(WorldPacketHandlerContext context, LegacyTradeParty party,
        WorldSession expected, LegacyItemSaveGraph graph)
    {
        foreach (var pair in party.Items)
        {
            if (!graph.TryGetInventory(pair.Key, out var current) ||
                !IsLegacyTradeClosureTradable(context, party, current) ||
                !LegacyTradeOfferMatchesCurrent(context, party, expected, pair.Value, current.Source))
                return false;
        }
        return true;
    }

    private static bool BoundLegacyTradeOffersCurrent(WorldPacketHandlerContext context, LegacyTradeParty party,
        WorldSession expected, LegacyItemSaveGraph graph)
    {
        foreach (var pair in party.Items)
            if (pair.Value.Lineage is { } lineage &&
                (!graph.TryGetInventory(pair.Key, out var current) || context.Sessions is not { } sessions ||
                 !lineage.MatchesCurrent(sessions, party.Connection, expected, current.Source))) return false;
        return true;
    }

    private static bool LegacyTradeOfferMatchesCurrent(WorldPacketHandlerContext context, LegacyTradeParty party,
        WorldSession expected, LegacyTradeOffer offer, PlayerItemSnapshotItem current) => offer.Lineage is { } lineage
        ? context.Sessions is { } sessions && lineage.MatchesCurrent(sessions, party.Connection, expected, current)
        : context.Sessions is { } unbound && unbound.IsUnboundTradeItemOwnerCurrent(party.Connection, expected) &&
          offer.Closure.MatchesSource(current);

    private static bool IsLegacyTradeClosureTradable(WorldPacketHandlerContext context, LegacyTradeParty party,
        LegacyItemSaveGraph.Closure closure) => LegacyTradeClosureAllows(closure, lockedPartner: false) ||
        LegacyTradeClosureAllows(closure, lockedPartner: true) && HasLegacyTradeRelation(context, party.Uid, party.Other.Uid, 3);

    private static bool LegacyTradeClosureAllows(LegacyItemSaveGraph.Closure closure, bool lockedPartner) =>
        closure.AllSourceRows(row => row.Bound == 0 && row.Inscribed != 1 &&
            !LegacyNativeItemRestrictions.IsUnableTradeItem(row.ItemId) && (row.Locked == 0 || lockedPartner));

    private static bool LegacyTradeHasUidCollision(LegacyItemSaveGraph recipient, LegacyTradeParty sender) =>
        sender.Items.Values.Any(offer => recipient.ContainsAny(offer.Closure));

    private static bool ValidateLegacyTradeExchangeOffers(WorldPacketHandlerContext context, LegacyTradeParty? party,
        WorldSession first, LegacyItemSaveGraph firstItems, WorldSession second, LegacyItemSaveGraph secondItems,
        out string reason)
    {
        reason = string.Empty;
        // Refunds do not transfer items and must not require permission to trade them.
        if (party is null) return true;
        // Complete all callback-bearing tradability/relation reads first, then
        // finish both real bound owners with a pure pass. A later peer lookup
        // cannot leave the earlier owner's lineage unchecked before the WAL.
        if (ValidLegacyTradeOffers(context, party, first, firstItems) &&
            ValidLegacyTradeOffers(context, party.Other, second, secondItems) &&
            BoundLegacyTradeOffersCurrent(context, party, first, firstItems) &&
            BoundLegacyTradeOffersCurrent(context, party.Other, second, secondItems)) return true;
        reason = "tradeOfferChangedBeforeCommit";
        return false;
    }

    private static bool TryUseLegacyTradeItemGraph(PlayerItemSnapshot items, LegacyItemSaveGraph? expected,
        out LegacyItemSaveGraph graph)
    {
        if (expected is null) return LegacyItemSaveGraph.TryCapture(items, out graph);
        graph = expected;
        return graph.IsCurrent(items);
    }

    private static bool ValidateLegacyTradeItemGraphs(WorldSession first, LegacyItemSaveGraph firstItems,
        WorldSession updatedFirst, LegacyItemSaveGraph updatedFirstItems,
        WorldSession second, LegacyItemSaveGraph secondItems,
        WorldSession updatedSecond, LegacyItemSaveGraph updatedSecondItems, out string reason)
    {
        reason = string.Empty;
        if (firstItems.IsCurrent(first.PlayerItems) && updatedFirstItems.IsCurrent(updatedFirst.PlayerItems) &&
            secondItems.IsCurrent(second.PlayerItems) && updatedSecondItems.IsCurrent(updatedSecond.PlayerItems)) return true;
        reason = "tradeItemGraphChanged";
        return false;
    }
}
