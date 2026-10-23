namespace WolfConquer.Core.World;

internal partial class LegacyNativeVendorRuntime
{
    // Whole native Vendor86..107. Equality-only capacity and Bound==1 are intentional.
    public bool AddItem(LegacyNativeGameItemRuntime DataItem, LegacyNativeMsgItemView.ActionMode CostType, long Amout)
    {
        if (DataItem.Bound == 1 || DataItem.Inscribed == 1 || DataItem.Locked != 0 || DataItem.ITEM_ID == 750000)
            return false;
        if (Items.Count == 20)
            return false;
        if (!Items.ContainsKey(DataItem.Uid))
        {
            VendorItem vendorItem = new VendorItem();
            vendorItem.DataItem = DataItem;
            vendorItem.CostType = CostType;
            vendorItem.AmountCost = Amout;
            Items.TryAdd(DataItem.Uid, vendorItem);
            return true;
        }
        return false;
    }
}
