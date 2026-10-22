using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeExchangeShopRuntime
{
    public override string ToString()
    {
        LegacyNativeDbWriteLine writeLine = new LegacyNativeDbWriteLine('/');
        writeLine.Add(Items.Values.Count);
        foreach (Item value in Items.Values)
        {
            writeLine.Add(value.shopid);
            writeLine.Add(value.goods_rec_id);
            writeLine.Add(value.times);
        }
        return writeLine.Close();
    }

    public void Load(string? Line)
    {
        LegacyNativeDbReadLine readLine = new LegacyNativeDbReadLine(Line, '/');
        int num = readLine.Read(0);
        for (int i = 0; i < num; i++)
        {
            Item item = new Item();
            item.shopid = readLine.Read(0u);
            item.goods_rec_id = readLine.Read(0u);
            item.times = readLine.Read(0u);
            Items.Add(item.goods_rec_id, item);
        }
    }
}
