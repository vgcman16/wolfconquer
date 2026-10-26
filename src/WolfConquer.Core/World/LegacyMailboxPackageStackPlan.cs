using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageStackPlan
{
    internal sealed record Change(int GrantIndex, uint ExistingUid, int AllocationIndex,
        ushort QuantityAfter, uint Color, bool IsNew);
    internal sealed record MaterializedEvent(int GrantIndex, PlayerItemSnapshotItem Item, ushort Mode);
    internal sealed record Materialization(PlayerItemSnapshot Items, MaterializedEvent[] Events);
    private sealed record VirtualRow(LegacyMailboxPackageItemRecipe Template, uint ExistingUid,
        int AllocationIndex, ushort Quantity, uint Color);

    private readonly LegacyItemSaveGraph sourceGraph;
    private readonly PlayerItemSnapshot source;
    private readonly LegacyMailboxPackageItemRecipe[] grants;
    private readonly Change[] changes;
    private readonly VirtualRow[] finalRows;
    private readonly Dictionary<uint, PlayerItemSnapshotItem> physical;
    private readonly Dictionary<uint, PlayerItemSnapshotItem> attachments;
    private readonly byte[] seed;
    internal int NewCount { get; }
    internal Change[] CopyChanges() => changes.ToArray();
    internal byte[] CopySeed() => seed.ToArray();
    internal uint[] CopyAttachmentUids() => attachments.Keys.OrderBy(uid => uid).ToArray();
    internal bool IsCurrent(PlayerItemSnapshot items) => ReferenceEquals(items, source) && sourceGraph.IsCurrent(items);

    private LegacyMailboxPackageStackPlan(LegacyItemSaveGraph graph, PlayerItemSnapshot source,
        LegacyMailboxPackageItemRecipe[] grants, Change[] changes, VirtualRow[] rows,
        Dictionary<uint, PlayerItemSnapshotItem> physical, Dictionary<uint, PlayerItemSnapshotItem> attachments,
        int count, byte[] seed)
    {
        sourceGraph = graph; this.source = source; this.grants = grants; this.changes = changes; finalRows = rows;
        this.physical = physical; this.attachments = attachments; NewCount = count; this.seed = seed;
    }

    internal static bool TryCreate(PlayerItemSnapshot source, LegacyMailboxPackageItemRecipe[] grants,
        IReadOnlyDictionary<uint, LegacyMailboxPackageCatalog> metadata, LegacyMailboxEntry[] effectiveMails,
        bool claimAll, byte[] seed, out LegacyMailboxPackageStackPlan? plan, out string reason)
    {
        plan = null;
        reason = "mailboxClaimPackageStackInvalid";
        if (source is null || grants is null || grants.Length is < 1 or > 256 || seed is not { Length: 32 } ||
            metadata is null || effectiveMails is null || !LegacyItemSaveGraph.TryCapture(source, out var graph) ||
            source.InventoryItems.Count > 40) return false;
        var builder = new StackBuilder(source, graph, grants.ToArray(), metadata, seed.ToArray());
        if (!builder.TryPlan(effectiveMails, claimAll, out reason) || !graph.IsCurrent(source)) return false;
        plan = builder.Finish();
        return true;
    }

    private static LegacyMailboxPackageItemRecipe RowTemplate(PlayerItemSnapshotItem item) =>
        new(0, 0, item.ItemId, item.StackSize, item.Durability, item.Plus, item.Bound, item.SocketProgress, string.Empty);

    private static bool SameTemplate(LegacyMailboxPackageItemRecipe left, LegacyMailboxPackageItemRecipe right) =>
        left.ItemId == right.ItemId && left.Durability == right.Durability && left.Plus == right.Plus &&
        left.Bound == right.Bound && left.SocketProgress == right.SocketProgress;

    internal PlayerItemSnapshotItem CopyAttachmentForMove(uint uid) =>
        attachments.TryGetValue(uid, out var item) ? item.WithStackSize(item.StackSize) :
            throw new InvalidOperationException("Package attachment was not admitted.");
}
