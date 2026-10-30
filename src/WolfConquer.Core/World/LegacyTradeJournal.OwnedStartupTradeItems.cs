using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    // Same receiving-space rule as ConfirmLegacyTrade: outgoing removals do
    // not create space for incoming roots in the legacy 40-slot inventory.
    private const int OwnedTradeInventorySlots = 40;
    private sealed record OwnedTradeItemClosure(long Owner, bool Inventory, bool Transferable, byte[] Bytes);

    private static void ValidateOwnedTradeItemClosures(LegacyTradeJournalChange[] items, bool exchange)
    {
        var before = CaptureOwnedTradeItemClosures(items, after: false);
        var after = CaptureOwnedTradeItemClosures(items, after: true);
        if (before.Count != after.Count)
            throw new InvalidDataException("Owned trade cannot replace physical item closures.");
        foreach (var (uid, previous) in before)
        {
            if (!after.TryGetValue(uid, out var current) || !previous.Bytes.AsSpan().SequenceEqual(current.Bytes) ||
                previous.Owner != current.Owner && (!exchange || !previous.Inventory || !current.Inventory || !previous.Transferable))
                throw new InvalidDataException("Owned trade changes an item closure or transfers an unauthorized item.");
        }
        var moved = before.Where(pair => pair.Value.Owner != after[pair.Key].Owner).ToArray();
        if (moved.Length == 0) return; // A refund does not require transfer capacity or permission.
        foreach (var owner in items.Select(item => item.FileId))
        {
            var occupied = before.Values.Count(item => item.Owner == owner && item.Inventory);
            var incoming = moved.Count(pair => after[pair.Key].Owner == owner);
            if (occupied + incoming > OwnedTradeInventorySlots)
                throw new InvalidDataException("Owned trade exceeds receiving capacity before outgoing removals.");
        }
    }

    private static Dictionary<uint, OwnedTradeItemClosure> CaptureOwnedTradeItemClosures(
        LegacyTradeJournalChange[] changes, bool after)
    {
        var result = new Dictionary<uint, OwnedTradeItemClosure>();
        foreach (var change in changes)
        {
            var bytes = after ? change.After : change.Before!;
            var items = PlayerItemSnapshotLoader.Parse(bytes);
            if (!LegacyItemSaveGraph.TryCapture(items, out var graph) || !graph.HasBytes(bytes))
                throw new InvalidDataException("Owned trade Items image is not lossless through the actual trade save graph.");
            var other = items.RelicFuseItems.Concat(items.MythsoulBagItems).Concat(items.EquipmentItems)
                .Concat(items.RuneItems).Concat(items.WardrobeItems).Concat(items.WarehouseItems);
            void Add(PlayerItemSnapshotItem item, bool inventory)
            {
                // The complete root closure includes its deposited children and
                // perfection data. A transfer cannot reparent or edit either.
                var image = PlayerItemSnapshotLoader.ToLegacyBytes(new([item], [], [], [], []));
                // The live producer already validated mutable partner relations
                // before publishing durable intent. Recovery preserves Locked
                // bytes and settles that intent rather than reauthorizing it.
                var transferable = new[] { item }.Concat(item.DepositedItems).All(row =>
                    row.Bound == 0 && row.Inscribed != 1 &&
                    !LegacyNativeItemRestrictions.IsUnableTradeItem(row.ItemId));
                if (!result.TryAdd(item.Uid, new(change.FileId, inventory, transferable, image)))
                    throw new InvalidDataException("Owned trade contains duplicate physical item roots.");
            }
            foreach (var item in items.InventoryItems) Add(item, inventory: true);
            foreach (var item in other) Add(item, inventory: false);
        }
        return result;
    }
}
