namespace WolfConquer.Core.Data;

internal partial class LegacyNativeDaoqiDictTypeTable
{
    public static void Load(string DBLocation)
    {
        try
        {
            if (!LegacyNativeCombatGearTable.m_Load)
            {
                return;
            }
            if (!File.Exists(DBLocation + "daoqi_dict_type.txt"))
            {
                Console.WriteLine("!File.Exists: " + DBLocation + "daoqi_dict_type.txt");
                return;
            }
            Info = new Dictionary<uint, CQ>();
            string[] array = File.ReadAllLines(DBLocation + "daoqi_dict_type.txt");
            string[] array2 = array;
            foreach (string text in array2)
            {
                string[] array3 = text.Split(new string[2] { "@@", " " }, StringSplitOptions.RemoveEmptyEntries);
                CQ cQ = new CQ
                {
                    ID = Convert.ToUInt32(array3[0]),
                    Type = (Type)Convert.ToUInt32(array3[1]),
                    attr_type1 = Convert.ToUInt32(array3[2]),
                    attr_value1 = Convert.ToUInt32(array3[3]),
                    attr_type2 = Convert.ToUInt32(array3[4]),
                    attr_value2 = Convert.ToUInt32(array3[5]),
                    score = Convert.ToUInt32(array3[6]),
                    points = Convert.ToUInt32(array3[7]),
                    rate = Convert.ToUInt32(array3[8]),
                    TypeRune = Convert.ToUInt32(array3[9]),
                    Level = Convert.ToUInt32(array3[10]),
                    Exp = Convert.ToUInt32(array3[11]),
                    u12 = Convert.ToUInt32(array3[12]),
                    u13 = Convert.ToUInt32(array3[13]),
                    TypeCombatGear = Convert.ToUInt32(array3[14])
                };
                Info.Add(cQ.ID, cQ);
            }
        }
        catch (Exception value)
        {
            Console.WriteLine(value);
        }
    }
}
