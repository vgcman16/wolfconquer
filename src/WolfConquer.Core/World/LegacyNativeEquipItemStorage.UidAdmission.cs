namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeEquipItemStorage
{
    // Whole native Equip262..280, including post-admission riding-crop Bless repair.
    // This is not the held Equip.Add/Inventory.Update or a transport substitute.
    private void CheakUp(LegacyNativeGameItemRuntime ItemDat)
    {
        if (JoinedItemGraph is not null) throw new InvalidOperationException("Joined equipment requires canonical Store publication.");
        if (ItemDat.Uid == 0)
        {
            ItemDat.Uid = LegacyNativePoolItems.ItemCounter.Next;
        }
        if (!ClientItems.TryAdd(ItemDat.Uid, ItemDat))
        {
            do
            {
                ItemDat.Uid = LegacyNativePoolItems.ItemCounter.Next;
            }
            while (!ClientItems.TryAdd(ItemDat.Uid, ItemDat));
        }
        if (ItemDat.ITEM_ID / 1000 == 203 && ItemDat.Bless > 1)
        {
            ItemDat.Bless = 0;
        }
    }
}
