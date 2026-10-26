namespace WolfConquer.Core.Data;

internal partial class LegacyNativeCombatGearTable
{
    internal static object MailboxLoadGate { get; } = new();

    // One aggregate publication owns all metadata families and loader readiness.
    // Runtime rank entries retain their own source gates after adoption.
    private sealed record MailboxGraph(Dictionary<uint, Dictionary<uint, CQ>>? Gear,
        Dictionary<uint, LegacyNativeDaoqiDictTypeTable.CQ>? Jade,
        Dictionary<uint, LegacyNativeDictLotteryTable.CQ>? Lottery,
        Dictionary<uint, LegacyNativeDictCollectionTable.CQ>? Collection,
        Dictionary<LegacyNativeCombatGearRankTable.Type, LegacyNativeCombatGearRankTable.Rank>? Ranks,
        string Location, bool RawGate, bool CompleteReady, MailboxLoadProof? Proof);

    private static MailboxGraph mailboxGraph = new(null, null, null, null, new(), "", false, false, null);
    private static MailboxGraph ReadMailboxGraph() => Volatile.Read(ref mailboxGraph);

    // Legacy direct assignment remains source compatible, but cannot create a
    // complete load witness/readiness. In-place mutable aliases are separately
    // checked by the concrete metadata capture before recovery adoption.
    private static void SetMailboxCompatibility(Func<MailboxGraph, MailboxGraph> change)
    {
        lock (MailboxLoadGate)
        {
            var updated = change(ReadMailboxGraph()) with { CompleteReady = false, Proof = null };
            Volatile.Write(ref mailboxGraph, updated);
        }
    }

    internal static Dictionary<uint, LegacyNativeDaoqiDictTypeTable.CQ> MailboxJade
    {
        get => ReadMailboxGraph().Jade!;
        set => SetMailboxCompatibility(graph => graph with { Jade = value });
    }
    internal static Dictionary<uint, LegacyNativeDictLotteryTable.CQ> MailboxLottery
    {
        get => ReadMailboxGraph().Lottery!;
        set => SetMailboxCompatibility(graph => graph with { Lottery = value });
    }
    internal static Dictionary<uint, LegacyNativeDictCollectionTable.CQ> MailboxCollection
    {
        get => ReadMailboxGraph().Collection!;
        set => SetMailboxCompatibility(graph => graph with { Collection = value });
    }
    internal static Dictionary<LegacyNativeCombatGearRankTable.Type, LegacyNativeCombatGearRankTable.Rank> MailboxRanks
    {
        get => ReadMailboxGraph().Ranks!;
        set => SetMailboxCompatibility(graph => graph with { Ranks = value });
    }
}
