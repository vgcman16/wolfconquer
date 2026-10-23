namespace WolfConquer.Core.World;

// Genuine native book/counter records. Public raw fields remain outside the
// cooperating method gate; no global persistence/economy transaction is claimed.
internal partial class LegacyNativeExchangeShopRuntime
{
    public class Item
    {
        public uint shopid;
        public uint goods_rec_id;
        public uint times;
    }

    public Dictionary<uint, Item> Items = new();
    private LegacyNativeGameClientRuntime user;
    private readonly object bookSyncRoot = new();

    public LegacyNativeExchangeShopRuntime(LegacyNativeGameClientRuntime _user) => user = _user;
}
