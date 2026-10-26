using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static void AppendLegacyWarehouseShowExtras(List<byte[]> frames,
        IReadOnlyList<PlayerItemSnapshotItem> rows, DateTime now)
    {
        var purifications = new List<LegacyItemExtraPurification>();
        var refineries = new List<LegacyItemExtraRefinery>();
        foreach (var row in rows)
        {
            if (!LegacyItemExtraPacket.TryCreate(row, now, out var extra)) continue;
            purifications.AddRange(extra.Purifications);
            refineries.AddRange(extra.Refineries);
        }
        if (purifications.Count != 0 || refineries.Count != 0)
            frames.Add(new LegacyItemExtraPacket(purifications, refineries).ToFrameBytes());
    }
}
