namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    private static readonly ushort[] ManagedNinjaSkills =
    [16310, 16320, 16330, 16340, 16350, 16360, 16370, 16380, 16390, 16400,
     16410, 16420, 16430, 16440, 16450, 16460, 16470, 16480, 16490, 16500,
     16510, 16520, 16530, 16540, 16550];

    private static RankingItem[] SelectLoadingItems(RankingItem[] rows, bool alternate)
    {
        var selected = new List<RankingItem>(8);
        for (var slot = 1u; slot <= 8; slot++)
        {
            var preferred = alternate ? slot + 8 : slot;
            var row = rows.FirstOrDefault(item => item.Position == preferred);
            if (row is null && alternate) row = rows.FirstOrDefault(item => item.Position == slot);
            if (row is not null) selected.Add(row);
        }
        return selected.ToArray();
    }

    private static byte PrepareLoadingBadge(RankingItem[] selected)
    {
        uint levels = 0;
        try { checked { foreach (var row in selected) levels += row.Level; } }
        catch (OverflowException error)
        { throw new InvalidOperationException("Ninja equipped levels exceed their declared UInt32 width.", error); }
        return levels >= 17 ? (byte)9 : levels >= 9 ? (byte)(levels - 8) : (byte)0;
    }
}
