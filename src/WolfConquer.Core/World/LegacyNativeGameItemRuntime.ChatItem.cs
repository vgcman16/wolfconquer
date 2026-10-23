namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeGameItemRuntime
{
    // Whole native MsgGameItem386..394. Genuine item construction and ITEM_ID
    // setter; receiver/counter/catalog are not used unless that setter needs them.
    public LegacyNativeGameItemRuntime FakeChatItem(uint ItemID, ushort Durability)
    {
        LegacyNativeGameItemRuntime msgGameItem = new LegacyNativeGameItemRuntime();
        msgGameItem.Uid = 1u;
        msgGameItem.Durability = (msgGameItem.MaximDurability = Durability);
        msgGameItem.ITEM_ID = ItemID;
        msgGameItem.Mode = LegacyNativeGameItemFlags.ItemMode.ChatItem;
        return msgGameItem;
    }
}
