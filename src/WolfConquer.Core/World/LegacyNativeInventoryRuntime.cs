using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

// Concrete Inventory UID-admission owner and retained-item queries, NOT full Inventory.Update.
// No account lookup/log/position/mode/send, raw-load bridge or live wiring.
internal sealed partial class LegacyNativeInventoryRuntime
{
    // Native ClientItems is a replaceable field, not a normalized key == value.Uid map.
    internal ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> ClientItems = new();

    // Native Inventory constructor stores the supplied owner without a null guard.
    internal LegacyNativeInventoryRuntime(LegacyNativeGameClientRuntime? owner)
    {
        Owner = owner;
    }

    internal LegacyNativeGameClientRuntime? Owner { get; }

    // Native Inventory100..103: key lookup only; no item.Uid normalization/copy/fallback.
    internal bool TryGetItem(uint UID, out LegacyNativeGameItemRuntime? item)
    {
        return ClientItems.TryGetValue(UID, out item);
    }

    internal void AddDBItem(LegacyNativeGameItemRuntime item)
    {
        if (JoinedItemGraph is not null) throw new InvalidOperationException("Joined inventory requires canonical Store publication.");
        ClientItems.TryAdd(item.Uid, item);
    }

    // Native spelling/order retained. No copied item, cached counter, lock or retry bound.
    internal void CheakUp(LegacyNativeGameItemRuntime item)
    {
        if (JoinedItemGraph is not null) throw new InvalidOperationException("Joined inventory requires canonical Store publication.");
        if (item.Uid == 0)
        {
            item.Uid = LegacyNativePoolItems.ItemCounter.Next;
        }
        if (!ClientItems.TryAdd(item.Uid, item))
        {
            do
            {
                item.Uid = LegacyNativePoolItems.ItemCounter.Next;
            }
            while (!ClientItems.TryAdd(item.Uid, item));
        }
    }
}
