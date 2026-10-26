using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool CanShowLegacyWarehouse(LegacyWarehouseShowOwner owner, LegacyWarehousePacket request) =>
        request.Action == LegacyWarehouseAction.ShashShow
            ? owner.Items.TryGetInventory(request.NpcId, out var sash) && ItemType.GetLegacySashCapacity(sash.Source.ItemId) > 0
            : LegacyWarehousePacket.IsShowAction(request.Action) &&
                CanUseLegacyWarehouse(owner.Session, request.NpcId, request.Action);

    private static bool TryPrepareLegacyWarehouseShow(LegacyWarehouseShowOwner owner, LegacyWarehousePacket request,
        out byte[][] frames, out int count, DateTime? preparedAt = null,
        IReadOnlyDictionary<uint, PlayerItemSnapshotItem>? unlocked = null)
    {
        frames = [];
        count = 0;
        if (!CanShowLegacyWarehouse(owner, request)) return false;
        try
        {
            var prepared = new List<byte[]>();
            var now = preparedAt ?? DateTime.Now;
            if (request.Action == LegacyWarehouseAction.ShashShow)
            {
                if (!owner.Items.TryGetInventory(request.NpcId, out var sash)) return false;
                var rows = sash.CopyForPersistence().DepositedItems.OrderBy(item => item.Uid).ToArray();
                count = rows.Length;
                prepared.Add(new LegacyWarehousePacket(request.NpcId, request.Action, 0,
                    ItemType.GetLegacySashCapacity(sash.Source.ItemId), rows).ToFrameBytes());
                AppendLegacyWarehouseShowExtras(prepared, rows, now);
            }
            else
            {
                if (!TryReadLegacyWarehouseShowCapacity(owner, request.NpcId, out var capacity)) return false;
                var rows = owner.Items.CopyWarehouseRowsForPresentation(request.NpcId);
                count = rows.Length;
                // Existing overfull stores can still be inspected and withdrawn.
                foreach (var chunk in rows.Chunk(4))
                {
                    prepared.Add(new LegacyWarehousePacket(request.NpcId, request.Action, 0, capacity, chunk).ToFrameBytes());
                    foreach (var row in chunk)
                    {
                        if (unlocked is not null && unlocked.TryGetValue(row.Uid, out var final))
                            prepared.Add(LegacyGameItemPacket.FromSnapshotItem(final, mode: LegacyGameItemPacket.UpdateItemMode).ToFrameBytes());
                        else if (LegacyItemLockPacket.TryCreateUnlockDate(row, now, out var itemLock))
                            prepared.Add(itemLock.ToFrameBytes());
                    }
                    AppendLegacyWarehouseShowExtras(prepared, chunk, now);
                }
            }
            // Validate the complete tail and all wire bounds before the first send.
            if (!owner.Items.IsCurrent(owner.Session.PlayerItems)) return false;
            frames = prepared.ToArray();
            return true;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        { return false; }
    }

    private static bool TryReadLegacyWarehouseShowCapacity(LegacyWarehouseShowOwner owner, uint npcId, out ushort capacity)
    {
        capacity = LegacyWarehousePacket.DefaultCapacity;
        if (npcId != (uint)owner.Session.Character.Uid) return true;
        var fields = owner.Fields.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        if (!LegacyInventorySashUpgradePersistence.TryReadCount(fields, out var purchased) || purchased > 300) return false;
        capacity = (ushort)purchased;
        return true;
    }
}
