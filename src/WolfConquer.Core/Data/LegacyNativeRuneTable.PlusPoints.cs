using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeRuneTable
{
    // These are quotes, not mutations or grants. The two valid overload rules remain distinct.
    public static uint GetRunePlusPoints(uint value)
    {
        if (!TryPlusPointsFamily(value, out var type) || !TryPlusPointsRow(type, null, out var row)) return 0;
        if (row is not null) return row.RequiredProgress;
        return value >= 730001 && value <= 730008
            ? LegacyNativeItemTypeRuneMetadata.StonePlusPoints((byte)(value % 10)) : 0u;
    }

    public static uint GetRunePlusPoints(LegacyNativeGameItemRuntime item)
    {
        if (item is null) return 0;
        uint id = item.ITEM_ID;
        if (!TryPlusPointsFamily(id, out var type) ||
            !TryPlusPointsRow(type, (byte)(id % 100), out var row) || row is null) return 0;
        uint stack = item.StackSize == 0 ? 1u : item.StackSize;
        ulong points = (ulong)row.RequiredProgress * stack + item.RuneEXP;
        return points <= uint.MaxValue ? (uint)points : 0u;
    }

    private static bool TryPlusPointsFamily(uint id, out uint family)
    {
        family = id / 1000;
        if (family < 100) return false;
        while (family >= 1000) family /= 10;
        return true;
    }

    private static bool TryPlusPointsRow(uint family, byte? level, out RuneLevelEXP? match)
    {
        match = null;
        var table = RuneLevelEXPList;
        if (table is null) return false;
        RuneLevelEXP? first = null;
        foreach (var row in table.Values)
        {
            // Validate the entire physical catalog, not just a successful prefix.
            if (row is null) return false;
            if (first is null && row.ItemType == family && (level is null || row.Level == level))
                first = row;
        }
        match = first;
        return true;
    }
}
