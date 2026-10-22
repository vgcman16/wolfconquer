#nullable disable

namespace WolfConquer.Core.Data;

// Complete native CQ_DICT_LOTTERY row, mutable static cache and loader only.
internal class LegacyNativeDictLotteryTable
{
    public class CQ
    {
        public uint Id;
        public uint Type;
        public uint Value1;
        public uint Value2;
        public uint Value3;
        public uint rate;
    }

    public static Dictionary<uint, CQ> Info;

    public static void Load(string DBLocation)
    {
        try
        {
            if (!LegacyNativeCombatGearTable.m_Load)
            {
                return;
            }
            if (!File.Exists(DBLocation + "dict_lottery.txt"))
            {
                Console.WriteLine("!File.Exists: " + DBLocation + "dict_lottery.txt");
                return;
            }
            Info = new Dictionary<uint, CQ>();
            string[] array = File.ReadAllLines(DBLocation + "dict_lottery.txt");
            string[] array2 = array;
            foreach (string text in array2)
            {
                string[] array3 = text.Split(new string[2] { "@@", " " }, StringSplitOptions.RemoveEmptyEntries);
                CQ cQ = new CQ
                {
                    Id = Convert.ToUInt32(array3[0]),
                    Type = Convert.ToUInt32(array3[1]),
                    Value1 = Convert.ToUInt32(array3[2]),
                    Value2 = Convert.ToUInt32(array3[3]),
                    Value3 = Convert.ToUInt32(array3[4]),
                    rate = Convert.ToUInt32(array3[5])
                };
                Info.Add(cQ.Id, cQ);
            }
        }
        catch (Exception value)
        {
            Console.WriteLine(value);
        }
    }
}
