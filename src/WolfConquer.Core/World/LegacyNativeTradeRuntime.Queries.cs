namespace WolfConquer.Core.World;

internal partial class LegacyNativeTradeRuntime
{
    // Native31..34: source checks ITEM_ID against escrow keys, not the supplied UID.
    public bool ItemInTrade(LegacyNativeGameItemRuntime Dataitem)
    {
        return Items.ContainsKey(Dataitem.ITEM_ID);
    }

    // Whole native58..68: source checks item-value UIDs, not escrow dictionary keys.
    public bool ValidItems()
    {
        foreach (LegacyNativeGameItemRuntime value in Items.Values)
        {
            if (!Owner.Inventory!.ClientItems.ContainsKey(value.Uid))
                return false;
        }
        return true;
    }
}
