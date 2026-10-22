using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeExchangeShopRuntime
{
    public void GetRandomEx(uint shopid, uint Count)
    {
        List<uint> list = new List<uint>();
        foreach (LegacyNativeExchangeShopGoodsEx.item item in LegacyNativeExchangeShopGoodsEx.exchange_shop_goods_ex.Values.Where((LegacyNativeExchangeShopGoodsEx.item p) => p.shopid == shopid))
        {
            if (Items.ContainsKey(item.id))
            {
                Items.Remove(item.id);
            }
            list.Add(item.id);
        }
        int num = 0;
        while (num < Count)
        {
            uint num2 = list[LegacyNativePoolRandom.GetRandom.Next(0, list.Count)];
            if (!Items.ContainsKey(num2) && AddItemEx(shopid, num2, 0u))
            {
                num++;
            }
        }
    }

    public void Reset()
    {
        Items.Clear();
        GetRandomEx(168u, 8u);
    }

    public void Loading()
    {
        if (Items.Values.Count((Item p) => p.goods_rec_id == 168) == 0)
        {
            GetRandomEx(168u, 8u);
        }
    }
}
