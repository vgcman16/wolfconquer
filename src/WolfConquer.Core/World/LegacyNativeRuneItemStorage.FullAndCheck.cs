namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeRuneItemStorage
{
    // Complete native Rune376..391: eleven worn entries, not exactly eleven or equipped count.
    public bool Full()
    {
        if (WornObjects.Count() < 11)
        {
            return false;
        }
        LegacyNativeGameItemRuntime[] wornObjects = WornObjects;
        foreach (LegacyNativeGameItemRuntime msgGameItem in wornObjects)
        {
            if (LegacyNativeItemTypeRuneMetadata.MaxRuneLevel(msgGameItem.ITEM_ID) > msgGameItem.ITEM_ID % 100)
            {
                return false;
            }
        }
        return true;
    }

    // Complete native Rune423..438. Do not widen yellow slots to match EquippedCount.
    public bool Check(uint ID, uint Position)
    {
        if ((Position == 101 || Position == 121) && ID / 10000 == 401)
        {
            return true;
        }
        if ((Position == 102 || Position == 122) && ID / 10000 == 402)
        {
            return true;
        }
        if (((Position >= 103 && Position <= 111) || (Position >= 123 && Position <= 131)) && ID / 10000 == 403)
        {
            return true;
        }
        return false;
    }
}
