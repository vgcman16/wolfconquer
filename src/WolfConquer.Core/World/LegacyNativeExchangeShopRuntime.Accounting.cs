#nullable disable
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeExchangeShopRuntime
{
    public bool AddItemEx(uint shopid, uint goods_rec_id, uint times)
    {
        LegacyNativeExchangeShopGoodsEx.item value2;
        if (Items.TryGetValue(goods_rec_id, out var value))
        {
            if (LegacyNativeExchangeShopGoodsEx.exchange_shop_goods_ex.TryGetValue(goods_rec_id, out value2) && (value.times + times <= value2.amount || value2.amount == 0))
            {
                value.times += times;
                return true;
            }
        }
        else
        {
            value = new Item();
            value.goods_rec_id = goods_rec_id;
            value.shopid = shopid;
            value.times = 0u;
            if (LegacyNativeExchangeShopGoodsEx.exchange_shop_goods_ex.TryGetValue(goods_rec_id, out value2) && (value.times + times <= value2.amount || value2.amount == 0))
            {
                value.times = times;
                Items.Add(value.goods_rec_id, value);
                return true;
            }
        }
        return false;
    }

    public bool AddItem(uint shopid, uint goods_rec_id, uint times)
    {
        LegacyNativeExchangeShopGoods.item value2;
        if (Items.TryGetValue(goods_rec_id, out var value))
        {
            if (LegacyNativeExchangeShopGoods.exchange_shop_goods.TryGetValue(goods_rec_id, out value2) && (value.times + times <= value2.amount || value2.amount == 0))
            {
                value.times += times;
                return true;
            }
        }
        else
        {
            value = new Item();
            value.goods_rec_id = goods_rec_id;
            value.shopid = shopid;
            value.times = 0u;
            if (LegacyNativeExchangeShopGoods.exchange_shop_goods.TryGetValue(goods_rec_id, out value2) && (value.times + times <= value2.amount || value2.amount == 0))
            {
                value.times = times;
                Items.Add(value.goods_rec_id, value);
                return true;
            }
        }
        return false;
    }
}
