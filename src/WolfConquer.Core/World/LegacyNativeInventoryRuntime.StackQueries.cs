using System.Collections.Generic;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeInventoryRuntime
{
    // Complete native Inventory890..897: lookup by dictionary key, not ITEM_ID
    // or the retained object's possibly different Uid. A zero stack counts as one
    // ONLY for Count==1; the native >= comparison also accepts Count==0.
    internal bool ContainItemWithStack(uint UID, ushort Count)
    {
        if (ClientItems.TryGetValue(UID, out var value))
        {
            return value.StackSize >= Count || (Count == 1 && value.StackSize == 0);
        }
        return false;
    }

    // Complete native Inventory1259..1270. Selects all bound states, returns
    // the same objects and uses item.Uid as the result key (first duplicate wins).
    private Dictionary<uint, LegacyNativeGameItemRuntime> GetMyMetscrolls()
    {
        Dictionary<uint, LegacyNativeGameItemRuntime> dictionary = new Dictionary<uint, LegacyNativeGameItemRuntime>();
        foreach (LegacyNativeGameItemRuntime value in ClientItems.Values)
        {
            if (value.ITEM_ID == 720027 && !dictionary.ContainsKey(value.Uid))
            {
                dictionary.Add(value.Uid, value);
            }
        }
        return dictionary;
    }

    // Complete native Inventory1272..1283, including both meteor IDs and the
    // per-result UID dedup. It does not count, split, normalize or sort stacks.
    private Dictionary<uint, LegacyNativeGameItemRuntime> GetMyMeteors()
    {
        Dictionary<uint, LegacyNativeGameItemRuntime> dictionary = new Dictionary<uint, LegacyNativeGameItemRuntime>();
        foreach (LegacyNativeGameItemRuntime value in ClientItems.Values)
        {
            if ((value.ITEM_ID == 1088001 || value.ITEM_ID == 1088002) && !dictionary.ContainsKey(value.Uid))
            {
                dictionary.Add(value.Uid, value);
            }
        }
        return dictionary;
    }
}
