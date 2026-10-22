#nullable disable
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

// Complete native cq_exchange_shop_goods row/storage/Load only; no startup caller.
internal class LegacyNativeExchangeShopGoods
{
    public class item
    {
        public uint id;

        public uint itemtypeid;

        public uint shopid;

        public uint amount;

        public uint monopoly;

        public uint material1;

        public uint material_amount1;

        public uint material2;

        public uint material_amount2;

        public uint material3;

        public uint material_amount3;

        public uint material4;

        public uint material_amount4;
    }

    public static Dictionary<uint, item> exchange_shop_goods;

    public static void Load()
    {
        try
        {
            if (!File.Exists(LegacyNativeServerConfig.DbLocation + "\\exchange_shop_goods.txt"))
            {
                Console.WriteLine("!File.Exists: " + LegacyNativeServerConfig.DbLocation + "\\exchange_shop_goods.txt");
                return;
            }
            string[] array = File.ReadAllLines(LegacyNativeServerConfig.DbLocation + "\\exchange_shop_goods.txt");
            exchange_shop_goods = new Dictionary<uint, item>();
            foreach (string text in array)
            {
                string[] array2 = text.Split(new string[1] { "@@" }, StringSplitOptions.RemoveEmptyEntries);
                item item = new item();
                item.id = uint.Parse(array2[0]);
                item.itemtypeid = uint.Parse(array2[1]);
                item.shopid = uint.Parse(array2[2]);
                item.amount = uint.Parse(array2[3]);
                item.monopoly = uint.Parse(array2[4]);
                item.material1 = uint.Parse(array2[5]);
                item.material_amount1 = uint.Parse(array2[6]);
                item.material2 = uint.Parse(array2[7]);
                item.material_amount2 = uint.Parse(array2[8]);
                item.material3 = uint.Parse(array2[9]);
                item.material_amount3 = uint.Parse(array2[10]);
                item.material4 = uint.Parse(array2[11]);
                item.material_amount4 = uint.Parse(array2[12]);
                exchange_shop_goods.Add(item.id, item);
            }
        }
        catch (Exception value)
        {
            Console.WriteLine(value);
        }
    }
}
