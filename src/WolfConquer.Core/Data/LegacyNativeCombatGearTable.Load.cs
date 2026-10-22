#nullable disable

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeCombatGearTable
{
    public static void Load(string DBLocation)
    {
        try
        {
            m_Load = true;
            if (!m_Load)
            {
                return;
            }
            Location = DBLocation;
            LegacyNativeDaoqiDictTypeTable.Load(DBLocation);
            LegacyNativeDictLotteryTable.Load(DBLocation);
            LegacyNativeDictCollectionTable.Load(DBLocation);
            RegisterMessages();
            Info = new Dictionary<uint, Dictionary<uint, CQ>>();
            string[] array = File.ReadAllLines(DBLocation + "combat_gear.txt");
            string[] array2 = array;
            foreach (string text in array2)
            {
                string[] array3 = text.Split(new string[2] { "@@", " " }, StringSplitOptions.RemoveEmptyEntries);
                CQ cQ = new CQ();
                cQ.ID = Convert.ToUInt32(array3[0]);
                cQ.Type = Convert.ToUInt32(array3[1]);
                cQ.Level = Convert.ToUInt32(array3[2]);
                cQ.Exp = Convert.ToUInt32(array3[3]);
                cQ.MaxHP = Convert.ToUInt32(array3[4]);
                cQ.PAttack = Convert.ToUInt32(array3[5]);
                cQ.PDefense = Convert.ToUInt32(array3[6]);
                cQ.MDefense = Convert.ToUInt32(array3[7]);
                cQ.Skills = new uint[10];
                CQ cQ2 = cQ;
                cQ2.Skills[0] = Convert.ToUInt32(array3[8]);
                cQ2.Skills[1] = Convert.ToUInt32(array3[9]);
                cQ2.Skills[2] = Convert.ToUInt32(array3[10]);
                cQ2.Skills[3] = Convert.ToUInt32(array3[11]);
                cQ2.Skills[4] = Convert.ToUInt32(array3[12]);
                cQ2.Skills[5] = Convert.ToUInt32(array3[13]);
                cQ2.Skills[6] = Convert.ToUInt32(array3[14]);
                cQ2.Skills[7] = Convert.ToUInt32(array3[15]);
                cQ2.Skills[8] = Convert.ToUInt32(array3[16]);
                cQ2.Skills[9] = Convert.ToUInt32(array3[17]);
                cQ2.Score = Convert.ToUInt32(array3[18]);
                if (Info.ContainsKey(cQ2.Type))
                {
                    if (!Info[cQ2.Type].ContainsKey(cQ2.Level))
                    {
                        Info[cQ2.Type].Add(cQ2.Level, cQ2);
                    }
                }
                else
                {
                    Info.Add(cQ2.Type, new Dictionary<uint, CQ>());
                    Info[cQ2.Type].Add(cQ2.Level, cQ2);
                }
            }
            LegacyNativeCombatGearRankTable.Load(DBLocation);
        }
        catch (Exception value)
        {
            Console.WriteLine(value);
        }
    }
}
