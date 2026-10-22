#nullable disable
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

// Complete native cq_exchange_shop_goods_ex row/storage/Load only; no startup caller.
internal class LegacyNativeExchangeShopGoodsEx
{
    public class item
    {
        public uint id;

        public uint itemtypeid;

        public uint shopid;

        public uint amount;

        public uint monopoly;

        public uint cost_type;

        public uint cost_value;

        public uint weight;

        public uint server_type_flag;

        public uint start_date;

        public uint end_date;
    }

    public static Dictionary<uint, item> exchange_shop_goods_ex;

    public static void Load()
    {
        try
        {
            if (!File.Exists(LegacyNativeServerConfig.DbLocation + "\\exchange_shop_goods_ex.txt"))
            {
                Console.WriteLine("!File.Exists: " + LegacyNativeServerConfig.DbLocation + "\\exchange_shop_goods_ex.txt");
                return;
            }
            string[] array = File.ReadAllLines(LegacyNativeServerConfig.DbLocation + "\\exchange_shop_goods_ex.txt");
            exchange_shop_goods_ex = new Dictionary<uint, item>();
            foreach (string text in array)
            {
                string[] array2 = text.Split(new string[1] { "@@" }, StringSplitOptions.RemoveEmptyEntries);
                item item = new item();
                item.id = uint.Parse(array2[0]);
                item.itemtypeid = uint.Parse(array2[1]);
                item.shopid = uint.Parse(array2[2]);
                item.amount = uint.Parse(array2[3]);
                item.monopoly = uint.Parse(array2[4]);
                item.cost_type = uint.Parse(array2[5]);
                item.cost_value = uint.Parse(array2[6]);
                item.weight = uint.Parse(array2[7]);
                item.server_type_flag = uint.Parse(array2[8]);
                item.start_date = uint.Parse(array2[9]);
                item.end_date = uint.Parse(array2[10]);
                exchange_shop_goods_ex.Add(item.id, item);
            }
        }
        catch (Exception value)
        {
            Console.WriteLine(value);
        }
    }
}
