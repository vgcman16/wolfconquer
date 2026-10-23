using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeRuneTable
{
    // Whole native RuneTable84..113. The two overloads intentionally differ.
    public static uint GetRunePlusPoints(uint value)
    {
        uint Type = value / 1000;
        Type = Convert.ToUInt32(Type.ToString().Substring(0, 3));
        if (RuneLevelEXPList.Values.Where((RuneLevelEXP i) => i.ItemType == Type).Count() > 0)
        {
            return RuneLevelEXPList.Values.Where((RuneLevelEXP i) => i.ItemType == Type).FirstOrDefault()!.RequiredProgress;
        }
        if (value >= 730001 && value <= 730008)
        {
            return LegacyNativeItemTypeRuneMetadata.StonePlusPoints((byte)(value % 10));
        }
        return 0u;
    }

    public static uint GetRunePlusPoints(LegacyNativeGameItemRuntime item)
    {
        uint iTEM_ID = item.ITEM_ID;
        uint Type = iTEM_ID / 1000;
        Type = Convert.ToUInt32(Type.ToString().Substring(0, 3));
        if (item.StackSize == 0)
        {
            item.StackSize = 1;
        }
        if (RuneLevelEXPList.Values.Where((RuneLevelEXP i) => i.ItemType == Type && i.Level == item.ITEM_ID % 100).Count() > 0)
        {
            return RuneLevelEXPList.Values.Where((RuneLevelEXP i) => i.ItemType == Type && i.Level == item.ITEM_ID % 100).FirstOrDefault()!.RequiredProgress * item.StackSize + item.RuneEXP;
        }
        return 0u;
    }
}
