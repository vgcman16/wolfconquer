using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

// Concrete native Warehouse storage/constructor and non-wire item methods.
// Show is an explicit partial; send/removal/Inventory.Update and full host remain held.
internal sealed partial class LegacyNativeWarehouseRuntime
{
    public const byte Max_Count = 60;
    public ConcurrentDictionary<uint, ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>> ClientItems;
    public List<uint> IsShow = new List<uint>();
    public LegacyNativeGameClientRuntime? User;

    public byte WHMaxSpace()
    {
        return 60;
    }

    public void RemoveInscribedItems()
    {
        foreach (ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> value in ClientItems.Values)
        {
            foreach (LegacyNativeGameItemRuntime value2 in value.Values)
            {
                if (value2.Inscribed == 1)
                {
                    value2.Inscribed = 0;
                }
            }
        }
    }

    public bool HaveItemsInBanks()
    {
        foreach (ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> value in ClientItems.Values)
        {
            if (value.Count > 0)
            {
                return true;
            }
        }
        return false;
    }

    public bool TryGetItem(uint WH_ID, uint UID, out LegacyNativeGameItemRuntime? item)
    {
        if (ClientItems.TryGetValue(WH_ID, out var value))
        {
            return value.TryGetValue(UID, out item);
        }
        item = null;
        return false;
    }

    public LegacyNativeWarehouseRuntime(LegacyNativeGameClientRuntime? client)
    {
        ClientItems = new ConcurrentDictionary<uint, ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>>();
        User = client;
    }

    public bool AddItem(LegacyNativeGameItemRuntime DataItem, uint NpcID)
    {
        if (!ClientItems.ContainsKey(NpcID))
        {
            ClientItems.TryAdd(NpcID, new ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>());
        }
        if (ClientItems[NpcID].TryAdd(DataItem.Uid, DataItem))
        {
            DataItem.WH_ID = NpcID;
            return true;
        }
        return false;
    }
}
