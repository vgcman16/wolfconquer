using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeGuildSkillTypeTable
{
    // Whole native cq_syn_skill_type.Load; same replace/partial-publish/catch boundary.
    public static void Load()
    {
        try
        {
            if (!File.Exists(LegacyNativeServerConfig.DbLocation + "syn_skill_type.txt"))
            {
                Console.WriteLine("!File.Exists: " + LegacyNativeServerConfig.DbLocation + "syn_skill_type.txt");
                return;
            }
            _syn_skill_type = new Dictionary<uint, Dictionary<uint, SkillType>>();
            string[] array = File.ReadAllLines(LegacyNativeServerConfig.DbLocation + "syn_skill_type.txt");
            string[] array2 = array;
            foreach (string text in array2)
            {
                string[] array3 = text.Split(new string[2] { "@@", " " }, StringSplitOptions.RemoveEmptyEntries);
                SkillType skill_type = new SkillType();
                skill_type.ID = Convert.ToUInt32(array3[0]);
                skill_type.Type = Convert.ToUInt32(array3[1]);
                skill_type.Level = Convert.ToUInt32(array3[2]);
                skill_type.data1 = Convert.ToUInt32(array3[3]);
                skill_type.data2 = Convert.ToUInt32(array3[4]);
                skill_type.Silver = Convert.ToUInt32(array3[5]);
                skill_type.DonationPoints = Convert.ToUInt32(array3[6]);
                if (_syn_skill_type.ContainsKey(skill_type.Type))
                {
                    if (!_syn_skill_type[skill_type.Type].ContainsKey(skill_type.Level))
                    {
                        _syn_skill_type[skill_type.Type].Add(skill_type.Level, skill_type);
                    }
                }
                else
                {
                    _syn_skill_type.Add(skill_type.Type, new Dictionary<uint, SkillType>());
                    _syn_skill_type[skill_type.Type].Add(skill_type.Level, skill_type);
                }
            }
        }
        catch (Exception value)
        {
            Console.WriteLine(value);
        }
    }
}
