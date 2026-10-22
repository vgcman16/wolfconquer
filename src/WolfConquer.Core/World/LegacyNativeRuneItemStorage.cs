using System.Collections.Generic;
using System.Linq;

namespace WolfConquer.Core.World;

// Native Rune constructor/storage/Add/lookups and complete worn/equipped queries.
// Equip/Unequip and spell mutations remain absent.
// Every stored and cached value is the SAME concrete item, never a UID or DTO wrapper.
internal sealed partial class LegacyNativeRuneItemStorage
{
    public LegacyNativeGameClientRuntime? Owner;
    private Dictionary<uint, LegacyNativeGameItemRuntime> items;
    private LegacyNativeGameItemRuntime[] objects;

    public LegacyNativeGameItemRuntime[] Objects => objects;

    public byte Count
    {
        get
        {
            byte b = 0;
            LegacyNativeGameItemRuntime[] array = objects;
            foreach (LegacyNativeGameItemRuntime value in array)
            {
                if (value != null)
                    b++;
            }
            return b;
        }
    }

    public LegacyNativeRuneItemStorage(LegacyNativeGameClientRuntime? client)
    {
        Owner = client;
        items = new Dictionary<uint, LegacyNativeGameItemRuntime>();
        objects = new LegacyNativeGameItemRuntime[0];
    }

    public bool Add(LegacyNativeGameItemRuntime item)
    {
        if (!items.ContainsKey(item.Uid) && Objects.Where(i => i.Position == 211).Count() < 100)
        {
            items.Add(item.Uid, item);
            objects = items.Values.ToArray();
            return true;
        }
        return false;
    }

    public bool TryGetItem(uint UID, out LegacyNativeGameItemRuntime? item)
    {
        LegacyNativeGameItemRuntime[] array = objects;
        foreach (LegacyNativeGameItemRuntime value in array)
        {
            if (value != null && value.Uid == UID)
            {
                item = value;
                return true;
            }
        }
        item = null;
        return false;
    }

    public LegacyNativeGameItemRuntime? TryGetItem(byte Position)
    {
        return Objects.Where(i => i.Position == Position).FirstOrDefault();
    }

    public LegacyNativeGameItemRuntime? TryGetItem(uint id)
    {
        return Objects.Where(i => i.ITEM_ID == id && i.Position != 211).FirstOrDefault();
    }


    public bool Free(ushort Position)
    {
        return (Position == 211 && Count < 100) || Position == 212 || Objects.Where(i => i.Position == Position).Count() < 1;
    }

    public bool ContainsUID(uint UID)
    {
        return items.ContainsKey(UID);
    }
}
