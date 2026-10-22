using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeQuestInfo
{
    public static void Init()
    {
        LegacyNativeItemCounterShellIni iniFile = new LegacyNativeItemCounterShellIni("\\Questinfo.ini");
        int num = iniFile.ReadUInt16("TotalMission", "TotalMission", 0);
        for (ushort num2 = 1; num2 < num; num2++)
        {
            DBQuest dBQuest = new DBQuest();
            dBQuest.TypeId = iniFile.ReadByte(num2.ToString(), "TypeId", 0);
            dBQuest.CompleteFlag = iniFile.ReadByte(num2.ToString(), "CompleteFlag", 0);
            dBQuest.ActivityType = iniFile.ReadByte(num2.ToString(), "ActivityType", 0);
            dBQuest.MissionId = iniFile.ReadUInt16(num2.ToString(), "MissionId", 0);
            dBQuest.Intentions = iniFile.ReadUInt16(num2.ToString(), "IntentAmount", 0);
            string text = iniFile.ReadString(num2.ToString(), "Prequest", "");
            if (text != "")
            {
                if (text.Contains('|'))
                {
                    string[] array = text.Split('|');
                    dBQuest.Prequest = new ushort[array.Length];
                    for (int i = 0; i < array.Length; i++)
                    {
                        dBQuest.Prequest[i] = ushort.Parse(array[i]);
                    }
                }
                else if (text.Contains(','))
                {
                    string[] array2 = text.Split(',');
                    dBQuest.Prequest = new ushort[array2.Length];
                    for (int j = 0; j < array2.Length; j++)
                    {
                        dBQuest.Prequest[j] = ushort.Parse(array2[j]);
                    }
                }
                else
                {
                    dBQuest.Prequest = new ushort[1] { ushort.Parse(text) };
                }
            }
            else
            {
                dBQuest.Prequest = new ushort[1];
            }
            string text2 = iniFile.ReadString(num2.ToString(), "Profession", "");
            if (text2 != "")
            {
                if (text2.Contains(','))
                {
                    string[] array3 = text2.Split(',');
                    dBQuest.Profession = new uint[array3.Length];
                    for (int k = 0; k < array3.Length; k++)
                    {
                        dBQuest.Profession[k] = uint.Parse(array3[k]);
                    }
                }
                else
                {
                    dBQuest.Profession = new uint[1] { uint.Parse(text2) };
                }
            }
            else
            {
                dBQuest.Profession = new uint[1];
            }
            string text3 = iniFile.ReadString(num2.ToString(), "Prize", "");
            if (text3.Contains('['))
            {
                string[] array4 = text3.Split('[');
                dBQuest.Prize = new uint[array4.Length - 1];
                try
                {
                    for (int l = 1; l < array4.Length; l++)
                    {
                        string text4 = array4[l];
                        string[] array5 = text4.Split(' ');
                        string[] array6 = array5[1].Split(',');
                        dBQuest.Prize[l - 1] = uint.Parse(array6[0]);
                    }
                }
                catch
                {
                }
            }
            dBQuest.BeginNpcId = new DBQuest.NpcInfo(iniFile.ReadString(num2.ToString(), "BeginNpcId", ""));
            dBQuest.FinishNpcId = new DBQuest.NpcInfo(iniFile.ReadString(num2.ToString(), "FinishNpcId", ""));
            if (!AllQuests.ContainsKey(dBQuest.MissionId))
            {
                AllQuests.Add(dBQuest.MissionId, dBQuest);
            }
            if (NpcQuests.ContainsKey(dBQuest.BeginNpcId.ID))
            {
                NpcQuests[dBQuest.BeginNpcId.ID].Quests.Add(dBQuest);
            }
            else
            {
                NpcQuests.Add(dBQuest.BeginNpcId.ID, new ListNpcQuests());
                NpcQuests[dBQuest.BeginNpcId.ID].Quests.Add(dBQuest);
            }
        }
    }
}
