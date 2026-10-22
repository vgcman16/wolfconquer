namespace WolfConquer.Core.World;

// Complete native ExchangeShop physical storage and constructor. No snapshot
// projection, automatic member birth, metadata loader or host caller is installed.
internal partial class LegacyNativeExchangeShopRuntime
{
    public class Item
    {
        public uint shopid;

        public uint goods_rec_id;

        public uint times;
    }

    public Dictionary<uint, Item> Items = new Dictionary<uint, Item>();

    private LegacyNativeGameClientRuntime user;

    public LegacyNativeExchangeShopRuntime(LegacyNativeGameClientRuntime _user)
    {
        user = _user;
    }
}
