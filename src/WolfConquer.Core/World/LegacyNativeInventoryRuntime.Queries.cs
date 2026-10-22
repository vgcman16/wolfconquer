using System.Collections.Generic;

namespace WolfConquer.Core.World;

// Native Inventory22..49,95..137,139..199 over the actual replaceable ClientItems owner.
// No snapshots, owner bootstrap, mutation/send route or full Inventory.Update implementation.
internal sealed partial class LegacyNativeInventoryRuntime
{
    internal int GetCountItem(uint ItemID)
    {
        int num = 0;
        foreach (LegacyNativeGameItemRuntime value in ClientItems.Values)
        {
            if (value.ITEM_ID == ItemID)
            {
                num += ((value.StackSize <= 1) ? 1 : value.StackSize);
            }
        }
        return num;
    }

    internal bool VerifiedUpdateItem(List<uint> ItemsUIDS, uint ID, byte count,
        out Queue<LegacyNativeGameItemRuntime> Items)
    {
        Queue<LegacyNativeGameItemRuntime> queue = new Queue<LegacyNativeGameItemRuntime>();
        foreach (LegacyNativeGameItemRuntime value in ClientItems.Values)
        {
            if (value.ITEM_ID == ID && ItemsUIDS.Contains(value.Uid))
            {
                count--;
                ItemsUIDS.Remove(value.Uid);
                queue.Enqueue(value);
            }
        }
        Items = queue;
        return ItemsUIDS.Count == 0 && count == 0;
    }

    internal bool HaveSpace(byte count)
    {
        return ClientItems.Count + count <= 40;
    }

    internal bool SearchItemByID(uint ID, out LegacyNativeGameItemRuntime? item)
    {
        foreach (LegacyNativeGameItemRuntime value in ClientItems.Values)
        {
            if (value.ITEM_ID == ID)
            {
                item = value;
                return true;
            }
        }
        item = null;
        return false;
    }

    internal bool SearchItemByID(uint ID, byte count, out List<LegacyNativeGameItemRuntime>? Items)
    {
        byte b = 0;
        Items = new List<LegacyNativeGameItemRuntime>();
        foreach (LegacyNativeGameItemRuntime value in ClientItems.Values)
        {
            if (value.ITEM_ID == ID)
            {
                Items.Add(value);
                b++;
                if (b == count)
                {
                    return true;
                }
            }
        }
        Items = null;
        return false;
    }

    internal bool Contain(uint ID, uint Amount, byte bound = 0)
    {
        if (ID == 1088001 || ID == 1088002)
        {
            uint num = 0u;
            foreach (LegacyNativeGameItemRuntime value in ClientItems.Values)
            {
                if ((value.ITEM_ID == 1088001 || value.ITEM_ID == 1088002) &&
                    ((bound > 0 && value.Bound > 0) || bound == 0))
                {
                    num += value.StackSize;
                    if (num >= Amount)
                    {
                        return true;
                    }
                }
            }
        }
        else if (ID == 721080 || ID == 723087)
        {
            uint num2 = 0u;
            foreach (LegacyNativeGameItemRuntime value2 in ClientItems.Values)
            {
                if (value2.ITEM_ID == ID && ((bound > 0 && value2.Bound > 0) || bound == 0))
                {
                    num2 += value2.StackSize;
                    if (num2 >= Amount)
                    {
                        return true;
                    }
                }
            }
        }
        else
        {
            uint num3 = 0u;
            foreach (LegacyNativeGameItemRuntime value3 in ClientItems.Values)
            {
                if (value3.ITEM_ID == ID && (value3.Bound == bound || (bound > 0 && value3.Bound > 0)))
                {
                    num3 += value3.StackSize;
                    if (num3 >= Amount)
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    internal bool HaveItemsInSash()
    {
        foreach (LegacyNativeGameItemRuntime value in ClientItems.Values)
        {
            if (LegacyNativeItemTypeRuneMetadata.IsSash(value.ITEM_ID) && value.Deposite.Count > 0)
            {
                return true;
            }
        }
        return false;
    }
}
