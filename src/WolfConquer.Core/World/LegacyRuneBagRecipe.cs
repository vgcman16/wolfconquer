using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed record LegacyRuneBagProjection(PlayerItemSnapshot Items, PlayerItemSnapshotItem ResultItem);

// An immutable historical data recipe, never a live producer or allocation
// permission. The actual evidence/receipt owner supplies the optional fresh UID.
internal sealed class LegacyRuneBagRecipe
{
    private readonly byte[] beforeImage;
    private readonly uint selectedUid;
    private readonly bool intoBag;
    private readonly LegacyRuneBagMetadata metadata;

    private LegacyRuneBagRecipe(byte[] beforeImage, uint selectedUid, bool intoBag, LegacyRuneBagMetadata metadata)
    { this.beforeImage = beforeImage; this.selectedUid = selectedUid; this.intoBag = intoBag; this.metadata = metadata; }

    // Rebuild on a private parsed copy: mutable perfection DTOs returned to a
    // caller cannot change the recipe or its subsequent projection.
    internal LegacyRuneBagPlan Plan => WorldPacketHandlers.PlanRecordedRuneBag(ReadBefore(), selectedUid, intoBag, metadata);

    internal static LegacyRuneBagRecipe Prepare(PlayerItemSnapshot before, uint selectedUid,
        bool intoBag, LegacyRuneBagMetadata metadata)
    {
        _ = WorldPacketHandlers.PlanRecordedRuneBag(before, selectedUid, intoBag, metadata);
        var image = PlayerItemSnapshotLoader.ToLegacyBytes(before);
        var copied = PlayerItemSnapshotLoader.Parse(image);
        var originalRows = WorldPacketHandlers.IndexRuneBagPhysicalItems(before);
        var copiedRows = WorldPacketHandlers.IndexRuneBagPhysicalItems(copied);
        if (!PlayerItemSnapshotLoader.ToLegacyBytes(copied).AsSpan().SequenceEqual(image) ||
            originalRows.Count != copiedRows.Count || originalRows.Any(pair =>
                !copiedRows.TryGetValue(pair.Key, out var row) ||
                !string.Equals(pair.Value.OwnerName, row.OwnerName, StringComparison.Ordinal) ||
                !string.Equals(pair.Value.Signature, row.Signature, StringComparison.Ordinal)))
            throw new InvalidDataException("Rune bag before graph is not losslessly representable.");
        return new(image, selectedUid, intoBag, metadata);
    }

    internal LegacyRuneBagProjection Project(uint? newUid)
    {
        var before = ReadBefore();
        var plan = WorldPacketHandlers.PlanRecordedRuneBag(before, selectedUid, intoBag, metadata);
        if ((plan.NewUidCount == 1) != newUid.HasValue || newUid == 0 ||
            newUid is { } issued && WorldPacketHandlers.IndexRuneBagPhysicalItems(before).ContainsKey(issued))
            throw new InvalidDataException("Rune bag projection has an invalid fresh UID shape.");

        PlayerItemSnapshotItem result;
        PlayerItemSnapshot items;
        if (plan.IntoBag)
        {
            result = plan.Target.WithPosition(211);
            items = before with
            {
                InventoryItems = before.InventoryItems.Where(row => row.Uid != selectedUid).ToArray(),
                RuneItems = OrderRunes(before.RuneItems.Append(result)),
            };
        }
        else if (plan.MergeUid is { } receivingUid)
        {
            result = before.InventoryItems.Single(row => row.Uid == receivingUid).WithStackSize(plan.MergedStackSize!.Value);
            items = before with
            {
                InventoryItems = before.InventoryItems.Select(row => row.Uid == receivingUid ? result : row).ToArray(),
                RuneItems = before.RuneItems.Where(row => row.Uid != selectedUid).ToArray(),
            };
        }
        else
        {
            result = plan.Target.WithUid(newUid!.Value).WithPosition(0);
            items = before with
            {
                InventoryItems = before.InventoryItems.Append(result).ToArray(),
                RuneItems = before.RuneItems.Where(row => row.Uid != selectedUid).ToArray(),
            };
        }
        return new(items, result);
    }

    private PlayerItemSnapshot ReadBefore() => PlayerItemSnapshotLoader.Parse(beforeImage);

    private static PlayerItemSnapshotItem[] OrderRunes(IEnumerable<PlayerItemSnapshotItem> rows) =>
        rows.OrderBy(row => row.Position).ThenBy(row => row.Position is 211 or 212 ? 0U : row.Uid).ToArray();
}
