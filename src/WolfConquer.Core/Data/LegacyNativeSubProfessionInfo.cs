#nullable disable
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

// Complete native metadata owner; no host/startup Load caller or equipment stand-in.
internal class LegacyNativeSubProfessionInfo :
    Dictionary<LegacyNativeDbLevExp.Sort, Dictionary<byte, LegacyNativeSubProfessionInfo.ProfessionInfo>>
{
    public class ProfessionInfo
    {
        public uint[] Damage;

        public uint NeedLevel;

        public byte NeedReborn;
    }

    public uint[] GetDamage(LegacyNativeDbLevExp.Sort ID, byte level)
    {
        return base[ID][level].Damage;
    }

    public bool AllowUpdate(LegacyNativeGameClientRuntime client, LegacyNativeDbLevExp.Sort ID, byte level)
    {
        ProfessionInfo professionInfo = base[ID][level];
        if (client.Player.Level < professionInfo.NeedLevel)
        {
            return false;
        }
        if (client.Player.Reborn < professionInfo.NeedReborn)
        {
            return false;
        }
        return true;
    }

    public void Load()
    {
        LegacyNativeItemCounterShellIni iniFile = new LegacyNativeItemCounterShellIni("SubProfessionInfo.ini");
        for (byte b = 1; b < 10; b++)
        {
            if (b != 7 && b != 8)
            {
                string section = b.ToString();
                for (byte b2 = 0; b2 < 10; b2++)
                {
                    if (b2 != 0)
                    {
                        section = b.ToString() + b2;
                    }
                    ProfessionInfo professionInfo = new ProfessionInfo();
                    if (b == 6)
                    {
                        professionInfo.Damage = new uint[2];
                        string text = iniFile.ReadString(section, "intro", "");
                        string[] array = text.Split(';');
                        professionInfo.Damage[0] = uint.Parse(array[0].Split('+')[1].ToString());
                        professionInfo.Damage[1] = uint.Parse(array[1].Split('+')[1].ToString());
                    }
                    else
                    {
                        professionInfo.Damage = new uint[1];
                        string text2 = iniFile.ReadString(section, "intro", "");
                        string[] array2 = text2.Split('+');
                        string s = array2[1].Split('%')[0];
                        professionInfo.Damage[0] = uint.Parse(s);
                        professionInfo.NeedLevel = iniFile.ReadByte(section, "req_lv", 0);
                        professionInfo.NeedReborn = iniFile.ReadByte(section, "req_meto", 0);
                    }
                    if (!ContainsKey((LegacyNativeDbLevExp.Sort)b))
                    {
                        Add((LegacyNativeDbLevExp.Sort)b, new Dictionary<byte, ProfessionInfo>());
                    }
                    base[(LegacyNativeDbLevExp.Sort)b].Add(b2, professionInfo);
                }
            }
        }
    }
}
