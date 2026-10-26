using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageStackPlan
{
    internal Materialization Materialize(LegacyItemUidReservationReceipt? receipt)
    {
        if (!IsCurrent(source) || NewCount == 0 && receipt is not null || NewCount != 0 &&
            (receipt is null || receipt.Count != NewCount || receipt.First == 0 ||
             (ulong)receipt.First + (uint)NewCount - 1 > uint.MaxValue))
            throw new InvalidOperationException("Package stack receipt or captured graph is invalid.");
        var current = new Dictionary<uint, PlayerItemSnapshotItem>(physical);
        var created = new Dictionary<int, PlayerItemSnapshotItem>();
        var events = new List<MaterializedEvent>();
        foreach (var change in changes)
        {
            var grant = grants[change.GrantIndex];
            PlayerItemSnapshotItem next;
            if (change.IsNew)
            {
                next = CreateTemplate(grant, checked(receipt!.First + (uint)change.AllocationIndex), change.QuantityAfter, change.Color);
                if (current.ContainsKey(next.Uid) || !created.TryAdd(change.AllocationIndex, next))
                    throw new InvalidOperationException("Package UID overlaps a physical or generated row.");
            }
            else if (change.ExistingUid != 0)
                next = current[change.ExistingUid].WithStackSize(change.QuantityAfter);
            else
                next = created[change.AllocationIndex].WithStackSize(change.QuantityAfter);
            if (change.ExistingUid != 0) current[change.ExistingUid] = next;
            else created[change.AllocationIndex] = next;
            events.Add(new(change.GrantIndex, next, change.IsNew ? LegacyGameItemPacket.AddItemMode : LegacyGameItemPacket.UpdateItemMode));
        }
        if (created.Count != NewCount) throw new InvalidOperationException("Package allocation was not completely materialized.");
        var inventory = finalRows.Select(row => row.ExistingUid != 0 ? current[row.ExistingUid] : created[row.AllocationIndex]).ToArray();
        var items = source with
        {
            InventoryItems = inventory,
            WarehouseItems = source.WarehouseItems.Where(item => !attachments.ContainsKey(item.Uid)).ToArray(),
        };
        if (!LegacyItemSaveGraph.TryCapture(items, out _) || !IsCurrent(source))
            throw new InvalidOperationException("Package stack output is not a complete unique save graph.");
        return new(items, events.ToArray());
    }
}
