namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeGameItemRuntime
{
    // Native MsgGameItem108: item classification, not its current storage slot.
    public bool IsEquip => LegacyNativeItemPosition.ItemPosition(ITEM_ID) != 0;
}
