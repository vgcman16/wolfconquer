using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Preserve the health-only pure helper contract; no owner or writer is created.
    internal static byte[] PrepareHealthConsumableItemImage(byte[] raw, PlayerItemSnapshot before,
        PlayerItemSnapshot after, uint uid) =>
        PrepareOwnedConsumableItemImage(raw, before, after, uid, LegacyConsumableItemKind.Hitpoints);
}