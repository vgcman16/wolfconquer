#nullable disable
using System.Collections.Generic;
using System.Linq;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeRuneItemStorage
{
    // Complete native Rune123..138; exclusions and current-equipment reads are not normalized.
    public LegacyNativeGameItemRuntime[] WornObjects
    {
        get
        {
            List<LegacyNativeGameItemRuntime> list = new List<LegacyNativeGameItemRuntime>();
            LegacyNativeGameItemRuntime[] array = objects;
            foreach (LegacyNativeGameItemRuntime item in array)
            {
                if (item.Position != 211 && item.Position != 0 && item.Position != 212 &&
                    (!Owner.Equipment.Alternante || item.Position >= 120 ||
                        objects.Count((LegacyNativeGameItemRuntime msgGameItem) => msgGameItem.Position == item.Position + 20) <= 0) &&
                    (Owner.Equipment.Alternante || item.Position <= 120))
                {
                    list.Add(item);
                }
            }
            return list.ToArray();
        }
    }

    // Complete native Rune328..365, including current-culture case conversion and lookup order.
    public bool IsEquipped(string runeName, out LegacyNativeGameItemRuntime item)
    {
        item = null;
        if (Owner.Player.Reborn < 2 || Owner.Player.Level < 15)
        {
            return false;
        }
        LegacyNativeGameItemRuntime[] wornObjects = WornObjects;
        foreach (LegacyNativeGameItemRuntime msgGameItem in wornObjects)
        {
            if (msgGameItem != null &&
                LegacyNativeItemTypeRuneMetadata.EquipPassJobReq(LegacyNativePoolItems.ItemsBase[msgGameItem.ITEM_ID], Owner) &&
                LegacyNativeItemTypeRuneMetadata.IsRune(msgGameItem.ITEM_ID) &&
                LegacyNativePoolItems.ItemsBase[msgGameItem.ITEM_ID].Name.ToLower().Contains(runeName.ToLower()))
            {
                item = msgGameItem;
                return true;
            }
        }
        return false;
    }

    public bool IsEquipped(string runeName)
    {
        LegacyNativeGameItemRuntime item;
        return IsEquipped(runeName, out item);
    }

    public bool IsEquipped(string runeName, ref byte itemLevel)
    {
        IsEquipped(runeName, out var item);
        if (item != null)
        {
            itemLevel = (byte)(item.ITEM_ID % 100);
        }
        else
        {
            itemLevel = 0;
        }
        return item != null;
    }
}
