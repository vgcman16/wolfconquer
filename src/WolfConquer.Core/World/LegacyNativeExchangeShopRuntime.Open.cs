using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeExchangeShopRuntime
{
    public uint GetTime()
    {
        DateTime now = DateTime.Now;
        int hour = now.Hour;
        int minute = now.Minute;
        int second = now.Second;
        int num = 23 - hour;
        int num2 = 60 - minute;
        int num3 = 60 - second;
        TimeSpan timeSpan = new TimeSpan(DateTime.Now.Ticks);
        TimeSpan timeSpan2 = new TimeSpan(DateTime.Now.AddHours(num).AddMinutes(num2).AddSeconds(num3)
            .Ticks);
        return (uint)(timeSpan2.TotalSeconds - timeSpan.TotalSeconds);
    }

    public void Open(LegacyNativePacket stream, uint NpcID, uint ShopID)
    {
        LegacyNativeCMsgExchangeShopGoods cMsgExchangeShopGoods = new LegacyNativeCMsgExchangeShopGoods();
        cMsgExchangeShopGoods.NpcID = NpcID;
        cMsgExchangeShopGoods.ShopID = ShopID;
        cMsgExchangeShopGoods.UID = user.Player.UID;
        cMsgExchangeShopGoods.TimeInSeconds = GetTime();
        Item[] array = Items.Values.Where((Item p) => p.shopid == ShopID).ToArray();
        cMsgExchangeShopGoods.Items = new LegacyNativeCMsgExchangeShopGoods.Item[array.Length];
        int num = 0;
        Item[] array2 = array;
        foreach (Item item in array2)
        {
            cMsgExchangeShopGoods.Items[num] = new LegacyNativeCMsgExchangeShopGoods.Item();
            cMsgExchangeShopGoods.Items[num].Index = item.goods_rec_id;
            cMsgExchangeShopGoods.Items[num].Count = item.times;
            cMsgExchangeShopGoods.Items[num].uk = 0u;
            num++;
        }
        user.Send(stream.CreateExchangeShopGoods(cMsgExchangeShopGoods));
    }
}
