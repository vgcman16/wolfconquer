using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool ValidLegacyTradeOffers(WorldPacketHandlerContext context, LegacyTradeParty party,
        LegacyTradeItemGraph graph)
    {
        foreach (var pair in party.Items)
        {
            if (!graph.TryGetInventory(pair.Key, out var current) ||
                !pair.Value.Closure.MatchesSource(current.Source) || !IsLegacyTradeClosureTradable(context, party, current))
                return false;
        }
        return true;
    }

    private static bool IsLegacyTradeClosureTradable(WorldPacketHandlerContext context, LegacyTradeParty party,
        LegacyTradeItemGraph.Closure closure) => closure.IsTradable(lockedPartner: false) ||
        closure.IsTradable(lockedPartner: true) && HasLegacyTradeRelation(context, party.Uid, party.Other.Uid, 3);

    private static bool LegacyTradeHasUidCollision(LegacyTradeItemGraph recipient, LegacyTradeParty sender) =>
        sender.Items.Values.Any(offer => recipient.ContainsAny(offer.Closure));

    private static bool ValidateLegacyTradeExchangeOffers(WorldPacketHandlerContext context, LegacyTradeParty? party,
        LegacyTradeItemGraph firstItems, LegacyTradeItemGraph secondItems, out string reason)
    {
        reason = string.Empty;
        // Refunds do not transfer items and must not require permission to trade them.
        if (party is null || ValidLegacyTradeOffers(context, party, firstItems) &&
            ValidLegacyTradeOffers(context, party.Other, secondItems)) return true;
        reason = "tradeOfferChangedBeforeCommit";
        return false;
    }

    private static bool TryUseLegacyTradeItemGraph(PlayerItemSnapshot items, LegacyTradeItemGraph? expected,
        out LegacyTradeItemGraph graph)
    {
        if (expected is null) return LegacyTradeItemGraph.TryCapture(items, out graph);
        graph = expected;
        return graph.IsCurrent(items);
    }

    private static bool ValidateLegacyTradeItemGraphs(WorldSession first, LegacyTradeItemGraph firstItems,
        WorldSession updatedFirst, LegacyTradeItemGraph updatedFirstItems,
        WorldSession second, LegacyTradeItemGraph secondItems,
        WorldSession updatedSecond, LegacyTradeItemGraph updatedSecondItems, out string reason)
    {
        reason = string.Empty;
        if (firstItems.IsCurrent(first.PlayerItems) && updatedFirstItems.IsCurrent(updatedFirst.PlayerItems) &&
            secondItems.IsCurrent(second.PlayerItems) && updatedSecondItems.IsCurrent(updatedSecond.PlayerItems)) return true;
        reason = "tradeItemGraphChanged";
        return false;
    }
}
