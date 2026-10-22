namespace WolfConquer.Core.World;

internal partial class LegacyNativeAnimaSkinRuntime
{
    public void AddSkin(uint SkinID, bool isEquip = false)
    {
        if (SkinID != 0 && !Items.ContainsKey(SkinID))
        {
            Item item = new Item();
            item.isEquip = isEquip;
            item.ID = SkinID;
            Items.Add(item.ID, item);
        }
    }

    public void RemoveSkin(uint SkinID)
    {
        if (Items.ContainsKey(SkinID))
        {
            Items.Remove(SkinID);
            uint num = 0u;
            if (isEquip != 0)
            {
            }
        }
    }
}
