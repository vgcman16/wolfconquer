using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // CURRENT main creator policy only. The canonical registry variant is a
    // separate composition owner, never a hidden max+1 compatibility fallback.
    private static bool TryCreateLegacyDragonSkinRetrievedItem(WorldPacketHandlerContext context,
        WorldSession session, uint itemId, out PlayerItemSnapshotItem row, out string reason)
    {
        row = null!;
        reason = "dragonSkinItemMetadataInvalid";
        ushort durability = 0;
        if (context.WorldData is { } data)
        {
            if (data.ItemsById is null) return false;
            if (data.ItemsById.TryGetValue((int)itemId, out var rows))
            {
                if (rows is null) return false;
                if (rows.Count > 0)
                {
                    var itemType = rows[0];
                    if (itemType is null || itemType.Id != itemId || !itemType.TryGetLegacyDurability(out durability)) return false;
                }
            }
        }
        var uid = GetNextSessionItemUid(session.PlayerItems);
        row = new() { Uid = uid, ItemId = itemId, Durability = durability,
            MaximumDurability = durability, StackSize = 1 };
        reason = string.Empty;
        return true;
    }
}
