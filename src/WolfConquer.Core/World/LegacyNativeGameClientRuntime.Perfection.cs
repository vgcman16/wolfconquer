using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    // Native GameClient1623..1649. Deferred live owner reads, no sorting/dedup
    // or construction of missing owners; Wardrobe/Rune/the two bags are excluded.
    internal IEnumerable<LegacyNativeGameItemRuntime> AllPerfectionItems()
    {
        foreach (LegacyNativeGameItemRuntime item in Inventory!.ClientItems.Values)
        {
            if ((item.PerfectionProgress != 0 || item.PerfectionLevel != 0) && item.OwnerUID != 0 && item.IsEquip)
            {
                yield return item;
            }
        }
        foreach (LegacyNativeGameItemRuntime item2 in Equipment!.ClientItems.Values)
        {
            if ((item2.PerfectionProgress != 0 || item2.PerfectionLevel != 0) && item2.OwnerUID != 0 && item2.IsEquip)
            {
                yield return item2;
            }
        }
        foreach (ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> Wh in Warehouse!.ClientItems.Values)
        {
            foreach (LegacyNativeGameItemRuntime item3 in Wh.Values)
            {
                if ((item3.PerfectionProgress != 0 || item3.PerfectionLevel != 0) && item3.OwnerUID != 0 && item3.IsEquip)
                {
                    yield return item3;
                }
            }
        }
    }

    // Native GameClient1651..1661: fresh predicate counts, not storage counts.
    internal int GetPerfectionItemsCount()
    {
        int num = 0;
        num += Inventory!.ClientItems.Values.Where(p => (p.PerfectionProgress != 0 || p.PerfectionLevel != 0) && p.OwnerUID != 0 && p.IsEquip).Count();
        num += Equipment!.ClientItems.Values.Where(p => (p.PerfectionProgress != 0 || p.PerfectionLevel != 0) && p.OwnerUID != 0 && p.IsEquip).Count();
        foreach (ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> value in Warehouse!.ClientItems.Values)
        {
            num += value.Values.Where(p => (p.PerfectionProgress != 0 || p.PerfectionLevel != 0) && p.OwnerUID != 0 && p.IsEquip).Count();
        }
        return num;
    }
}
