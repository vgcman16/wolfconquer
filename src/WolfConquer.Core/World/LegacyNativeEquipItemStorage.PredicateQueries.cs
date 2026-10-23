namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeEquipItemStorage
{
    // All three native predicate query methods282..322, not normalized LINQ.
    public bool Exist(Func<LegacyNativeGameItemRuntime, bool> predicate)
    {
        bool result = false;
        LegacyNativeGameItemRuntime[] curentEquip = CurentEquip;
        foreach (LegacyNativeGameItemRuntime arg in curentEquip)
        {
            if (predicate(arg))
            {
                result = true;
                break;
            }
        }
        return result;
    }

    public void Have(Func<LegacyNativeGameItemRuntime, bool> predicate, out int count)
    {
        count = 0;
        LegacyNativeGameItemRuntime[] curentEquip = CurentEquip;
        foreach (LegacyNativeGameItemRuntime arg in curentEquip)
        {
            if (predicate(arg)) count++;
        }
    }

    public bool Exist(Func<LegacyNativeGameItemRuntime, bool> predicate, int count)
    {
        int num = 0;
        LegacyNativeGameItemRuntime[] curentEquip = CurentEquip;
        foreach (LegacyNativeGameItemRuntime arg in curentEquip)
        {
            if (predicate(arg)) num++;
        }
        return num >= count;
    }

    // Whole native Equip2457..2460. Dictionary key, not value.Uid.
    public bool Contains(uint UID)
    {
        return ClientItems.ContainsKey(UID);
    }
}
