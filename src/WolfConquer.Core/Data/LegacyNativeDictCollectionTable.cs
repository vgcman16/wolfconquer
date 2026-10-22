#nullable disable

namespace WolfConquer.Core.Data;

// Complete native CQ_DICT_COLLECTION row, mutable static cache and loader.
// No CQ startup/metadata registration or host publication is installed here.
internal class LegacyNativeDictCollectionTable
{
    public class CQ
    {
        public uint Id;
        public uint Type;
        public uint Min;
        public uint Mix;
        public LegacyNativeDaoqiDictTypeTable.AttrType attr_type1;
        public uint attr_value1;
        public uint value2;
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
            if (!File.Exists(DBLocation + "dict_collection.txt"))
            {
                Console.WriteLine("!File.Exists: " + DBLocation + "dict_collection.txt");
                return;
            }
            Info = new Dictionary<uint, CQ>();
            string[] array = File.ReadAllLines(DBLocation + "dict_collection.txt");
            string[] array2 = array;
            foreach (string text in array2)
            {
                string[] array3 = text.Split(new string[2] { "@@", " " }, StringSplitOptions.RemoveEmptyEntries);
                CQ cQ = new CQ
                {
                    Id = Convert.ToUInt32(array3[0]),
                    Type = Convert.ToUInt32(array3[1]),
                    Min = Convert.ToUInt32(array3[2]),
                    Mix = Convert.ToUInt32(array3[3]),
                    attr_type1 = (LegacyNativeDaoqiDictTypeTable.AttrType)Convert.ToUInt32(array3[4]),
                    attr_value1 = Convert.ToUInt32(array3[5]),
                    value2 = Convert.ToUInt32(array3[6])
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
