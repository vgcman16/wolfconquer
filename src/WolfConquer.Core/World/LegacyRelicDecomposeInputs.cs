using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal readonly record struct LegacyRelicDecomposeSelection(uint Uid, uint ItemId, byte Bound);

// A complete save-graph observation and immutable batch facts, never an issuer.
internal sealed class LegacyRelicDecomposeInputs
{
    private readonly PlayerItemSnapshot owner;
    private readonly LegacyItemSaveGraph graph;
    private readonly byte[] image;
    private readonly LegacyRelicDecomposeSelection[] selections;

    private LegacyRelicDecomposeInputs(PlayerItemSnapshot owner, LegacyItemSaveGraph graph, byte[] image,
        LegacyRelicDecomposeSelection[] selections, uint maximumUid)
    { this.owner = owner; this.graph = graph; this.image = image; this.selections = selections; MaximumUid = maximumUid; }

    internal uint MaximumUid { get; }
    internal ReadOnlySpan<LegacyRelicDecomposeSelection> Selections => selections;
    internal byte[] CopyBeforeImage() => image.ToArray();
    internal bool IsCurrent(PlayerItemSnapshot actual) => ReferenceEquals(owner, actual) && graph.IsCurrent(actual);

    internal static bool TryCapture(PlayerItemSnapshot items, LegacyRelicDecomposeRequest request,
        out LegacyRelicDecomposeInputs? inputs, out string reason)
    {
        inputs = null; reason = "relicDecomposeItemGraphInvalid";
        if (items is null || request is null) return false;
        try
        {
            if (!LegacyItemSaveGraph.TryCapture(items, out var graph)) return false;
            var selected = new LegacyRelicDecomposeSelection[request.SelectedUids.Length];
            long removedBytes = 0;
            for (var index = 0; index < selected.Length; index++)
            {
                var uid = request.SelectedUids[index];
                if (!graph.TryGetInventory(uid, out var closure) || !Eligible(closure.Source))
                { reason = "relicDecomposeInputNotEligible"; return false; }
                var row = closure.Source;
                selected[index] = new(row.Uid, row.ItemId, row.Bound);
                removedBytes += PlayerItemSnapshotLoader.LegacyDbItemSize;
                if (row.PerfectionLevel != 0 || row.PerfectionProgress != 0 || row.OwnerUid != 0 ||
                    row.OwnerName.Length != 0 || row.Signature.Length != 0)
                    removedBytes += PlayerItemSnapshotLoader.LegacyPerfectionSize;
            }
            int rewards = checked(selected.Length * LegacyRelicDecomposeRecipe.RewardsPerInput);
            if (checked(items.InventoryItems.Count - selected.Length + rewards) > 40)
            { reason = "relicDecomposeInventoryCapacity"; return false; }
            var beforeImage = PlayerItemSnapshotLoader.ToLegacyBytes(items);
            long afterLength = checked(beforeImage.LongLength - removedBytes + (long)rewards * PlayerItemSnapshotLoader.LegacyDbItemSize);
            if (afterLength > CharacterSnapshotFileBaseline.MaximumBytes || !graph.HasBytes(beforeImage) || !graph.IsCurrent(items))
            { reason = "relicDecomposeImageNotPersistable"; return false; }
            var detached = PlayerItemSnapshotLoader.Parse(beforeImage);
            if (!PlayerItemSnapshotLoader.ToLegacyBytes(detached).AsSpan().SequenceEqual(beforeImage))
            { reason = "relicDecomposeImageNotPersistable"; return false; }
            var rows = WorldPacketHandlers.IndexRuneBagPhysicalItems(detached);
            var maximumUid = rows.Keys.DefaultIfEmpty(0U).Max();
            if (!graph.IsCurrent(items)) { reason = "relicDecomposeInputsChanged"; return false; }
            inputs = new(items, graph, beforeImage, selected, maximumUid);
            reason = string.Empty; return true;
        }
        catch (Exception error) when (error is ArgumentException or InvalidDataException or IndexOutOfRangeException or
            InvalidOperationException or OverflowException)
        { return false; }
    }

    private static bool Eligible(PlayerItemSnapshotItem item) => item.ItemId is >= 4_100_001 and <= 4_100_005 &&
        item.Position == 0 && item.WarehouseId == 0 && item.StackSize <= 1 && item.Bound <= 1 &&
        item.Locked == 0 && item.Suspicious == 0 && item.DepositedItems.Count == 0;
}
