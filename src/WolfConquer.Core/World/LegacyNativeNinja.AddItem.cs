namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    public void AddItem(uint ID, uint Level, uint Position, uint Points, uint UK)
    {
        if (Level == 0)
        {
            Level = 1u;
        }
        if (Level > 9)
        {
            Level = 9u;
        }
        Item item = new Item();
        item.ItemID = ID;
        item.Level = Level;
        item.Position = Position;
        item.Exp = Points;
        item.UK = UK;
        item.UPDATE();
        LegacyNativeMsgNinja.MsgNinjaItem.Create(user, item, LegacyNativeMsgNinja.MsgNinjaItem.TypeID.Add);
        if (!Items.ContainsKey(ID))
        {
            Items.Add(ID, item);
        }
        else
        {
            Items[ID] = item;
        }
        UpdateRank();
    }
}
