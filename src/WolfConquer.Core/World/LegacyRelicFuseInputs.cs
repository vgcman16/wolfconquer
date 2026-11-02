using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

/// <summary>Complete managed graph witness and two actual inventory roots; no UID, random draw or transaction is performed.</summary>
internal sealed partial class LegacyRelicFuseInputs
{
    internal const int MaximumRows = 65_535;
    internal const int MaximumGraphDepth = 32;
    private readonly PlayerItemSnapshot owner;
    private readonly Graph graph;

    private LegacyRelicFuseInputs(PlayerItemSnapshot owner, Graph graph, PlayerItemSnapshotItem first, PlayerItemSnapshotItem second)
    {
        this.owner = owner; this.graph = graph;
        First = first; Second = second;
        ResultBound = first.Bound >= second.Bound ? first.Bound : second.Bound;
    }

    internal PlayerItemSnapshotItem First { get; }
    internal PlayerItemSnapshotItem Second { get; }
    internal byte ResultBound { get; }
    internal uint MaximumUid => graph.MaximumUid;
    internal int MaximumDepth => graph.MaximumDepth;

    internal static bool TryCapture(PlayerItemSnapshot items, uint firstUid, uint secondUid,
        out LegacyRelicFuseInputs? inputs, out string reason)
    {
        inputs = null;
        reason = "relicFuseOwnerRequired";
        if (items is null) return false;
        if (firstUid == 0 || secondUid == 0 || firstUid == secondUid)
        { reason = "relicFuseDistinctInputsRequired"; return false; }
        if (!TryGraph(items, firstUid, secondUid, out var captured))
        { reason = "relicFuseItemGraphInvalid"; return false; }
        if (captured!.RootCounts[1] != 0)
        { reason = "relicFuseHeldStateNotEmpty"; return false; }
        if (!Eligible(captured.First) || !Eligible(captured.Second))
        { reason = "relicFuseInputNotEligible"; return false; }
        var prepared = new LegacyRelicFuseInputs(items, captured, captured.First!, captured.Second!);
        if (!prepared.IsCurrent(items))
        { reason = "relicFuseItemGraphChanged"; return false; }
        inputs = prepared;
        reason = string.Empty;
        return true;
    }

    internal bool IsCurrent(PlayerItemSnapshot items)
    {
        if (!ReferenceEquals(owner, items) || !TryGraph(items, First.Uid, Second.Uid, out var current)) return false;
        if (current!.RootCounts[1] != 0 || !Eligible(current.First) || !Eligible(current.Second) ||
            !ReferenceEquals(First, current.First) || !ReferenceEquals(Second, current.Second)) return false;
        return graph.Matches(current);
    }

    private static bool Eligible(PlayerItemSnapshotItem? row) => row is not null && row.Position == 0 && row.WarehouseId == 0 &&
        row.StackSize <= 1 && row.DepositedItems.Count == 0 && row.Locked == 0 && row.Suspicious == 0 && row.Bound <= 1;

    private static IReadOnlyList<PlayerItemSnapshotItem>[] Stores(PlayerItemSnapshot items) =>
        [items.InventoryItems, items.RelicFuseItems, items.MythsoulBagItems, items.EquipmentItems,
            items.RuneItems, items.WardrobeItems, items.WarehouseItems];

    private sealed class Pending(IReadOnlyList<PlayerItemSnapshotItem> source, int count, int group, int parent, int depth)
    {
        internal IReadOnlyList<PlayerItemSnapshotItem> Source { get; } = source;
        internal int Count { get; } = count;
        internal int Group { get; } = group;
        internal int Parent { get; } = parent;
        internal int Depth { get; } = depth;
        internal int Index { get; set; }
    }

    private static bool TryGraph(PlayerItemSnapshot items, uint firstUid, uint secondUid, out Graph? graph)
    {
        graph = null;
        try
        {
            var roots = Stores(items); var counts = new int[roots.Length];
            for (var group = 0; group < roots.Length; group++)
            {
                if (roots[group] is null) return false;
                counts[group] = roots[group].Count;
                if (counts[group] is < 0 or > MaximumRows) return false;
            }
            HashSet<uint> identities = [];
            HashSet<PlayerItemSnapshotItem> physical = new(ReferenceEqualityComparer.Instance);
            List<Row> rows = [];
            uint maximumUid = 0;
            var maximumDepth = 0;
            PlayerItemSnapshotItem? first = null, second = null;
            for (var group = 0; group < roots.Length; group++)
            {
                var pending = new Stack<Pending>();
                pending.Push(new(roots[group], counts[group], group, -1, 0));
                while (pending.TryPeek(out var collection))
                {
                    if (collection.Index == collection.Count)
                    {
                        if (collection.Source.Count != collection.Count) return false;
                        pending.Pop();
                        continue;
                    }
                    if (rows.Count == MaximumRows) return false;
                    var index = collection.Index++;
                    var item = collection.Source[index];
                    if (item is null || item.Uid == 0 || item.ItemId == 0 || item.DepositedItems is null ||
                        item.OwnerName is null || item.Signature is null || !identities.Add(item.Uid) || !physical.Add(item)) return false;
                    var deposits = item.DepositedItems; var depositCount = deposits.Count;
                    if (depositCount < 0 || depositCount > MaximumRows - rows.Count - 1) return false;
                    var rowIndex = rows.Count;
                    rows.Add(new(item, collection.Source, index, collection.Group, collection.Parent, collection.Depth,
                        deposits, depositCount, Values.Capture(item)));
                    if (item.Uid > maximumUid) maximumUid = item.Uid;
                    if (collection.Depth > maximumDepth) maximumDepth = collection.Depth;
                    if (collection.Group == 0 && collection.Parent == -1)
                    {
                        if (item.Uid == firstUid) first = item;
                        if (item.Uid == secondUid) second = item;
                    }
                    if (depositCount == 0) continue;
                    if (collection.Depth == MaximumGraphDepth) return false;
                    pending.Push(new(deposits, depositCount, collection.Group, rowIndex, collection.Depth + 1));
                }
            }
            for (var group = 0; group < roots.Length; group++)
                if (roots[group].Count != counts[group]) return false;
            graph = new(roots, counts, rows.ToArray(), first, second, maximumUid, maximumDepth);
            return true;
        }
        catch (Exception error) when (error is ArgumentException or IndexOutOfRangeException or InvalidOperationException or OverflowException)
        { return false; }
    }
}
