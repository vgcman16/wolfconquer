#nullable disable
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Complete native CombatGear.Load/Save. Uses the actual Win32 INI owner and raw
// ServerConfig prefix; no SQL adapter, host adoption, directory creation or repair.
internal partial class LegacyNativeCombatGear
{
    public void Load(uint UID)
    {
        if (!LegacyNativeCombatGearTable.m_Load)
        {
            return;
        }
        LegacyNativeItemCounterShellIni iniFile = new LegacyNativeItemCounterShellIni("PlayersCombatGear\\" + UID + ".ini");
        MasteryPoints = iniFile.ReadUInt32("CombatGear", "MasteryPoints");
        TaoistReiki = iniFile.ReadUInt32("CombatGear", "TaoistReiki");
        PirateExp = iniFile.ReadUInt32("CombatGear", "PirateExp");
        Lottery = iniFile.ReadUInt32("CombatGear", "Lottery");
        AirPowers.SenseLevel = iniFile.ReadUInt32("AirPowers", "SenseLevel");
        AirPowers.ArmedLevel = iniFile.ReadUInt32("AirPowers", "ArmedLevel");
        AirPowers.OverLoadLevel = iniFile.ReadUInt32("AirPowers", "OverLoadLevel");
        AirPowers.Points = iniFile.ReadUInt32("AirPowers", "Points");
        AirPowers.SenseAipPower = iniFile.ReadUInt32("AirPowers", "SenseAipPower");
        AirPowers.ArmedAipPower = iniFile.ReadUInt32("AirPowers", "ArmedAipPower");
        AirPowers.OverLoadAipPower = iniFile.ReadUInt32("AirPowers", "OverLoadAipPower");
        int num = iniFile.ReadInt32("CombatGear", "Count", 0);
        for (int i = 0; i < num; i++)
        {
            Weapon weapon = new Weapon();
            weapon.ItemID = (TypeID)iniFile.ReadUInt32("Item" + i, "ItemID");
            weapon.Level = iniFile.ReadUInt32("Item" + i, "Level");
            weapon.Exp = iniFile.ReadUInt32("Item" + i, "Exp");
            weapon.Equip = iniFile.ReadUInt32("Item" + i, "Equip");
            weapon.Transform = iniFile.ReadUInt32("Item" + i, "Transform");
            weapon.MasteryScore = iniFile.ReadUInt32("Item" + i, "MasteryScore");
            for (int j = 0; j < 2; j++)
            {
                for (int k = 0; k < 7; k++)
                {
                    weapon.TypeAnima[j][k] = iniFile.ReadUInt32("Item" + i, "Animas[" + j + "]" + k);
                }
            }
            for (int l = 0; l < 6; l++)
            {
                weapon.BlackJades[l].Type = iniFile.ReadUInt32("Item" + i, "BlackJades" + l);
            }
            if (LegacyNativeCombatGearTable.TryGetValue((uint)weapon.ItemID, (weapon.Level > 1) ? (weapon.Level - 1) : weapon.Level, out var _) && !Items.ContainsKey(weapon.ItemID))
            {
                Items.TryAdd(weapon.ItemID, weapon);
            }
        }
        num = iniFile.ReadInt32("BlackJadeBag", "Count", 0);
        for (int m = 0; m < num; m++)
        {
            AddBlackJade(iniFile.ReadUInt32("BlackJadeBag", "BlackJade" + m), 0u);
        }
    }

    public void Save(uint UID)
    {
        if (!LegacyNativeCombatGearTable.m_Load)
        {
            return;
        }
        LegacyNativeItemCounterShellIni iniFile = new LegacyNativeItemCounterShellIni("PlayersCombatGear\\" + UID + ".ini");
        iniFile.Write("CombatGear", "MasteryPoints", MasteryPoints);
        iniFile.Write("CombatGear", "TaoistReiki", TaoistReiki);
        iniFile.Write("CombatGear", "PirateExp", PirateExp);
        iniFile.Write("CombatGear", "Lottery", Lottery);
        iniFile.Write("CombatGear", "Count", Items.Count);
        iniFile.Write("AirPowers", "SenseLevel", AirPowers.SenseLevel);
        iniFile.Write("AirPowers", "ArmedLevel", AirPowers.ArmedLevel);
        iniFile.Write("AirPowers", "OverLoadLevel", AirPowers.OverLoadLevel);
        iniFile.Write("AirPowers", "Points", AirPowers.Points);
        iniFile.Write("AirPowers", "SenseAipPower", AirPowers.SenseAipPower);
        iniFile.Write("AirPowers", "ArmedAipPower", AirPowers.ArmedAipPower);
        iniFile.Write("AirPowers", "OverLoadAipPower", AirPowers.OverLoadAipPower);
        int num = 0;
        foreach (Weapon value in Items.Values)
        {
            iniFile.Write("Item" + num, "ItemID", (uint)value.ItemID);
            iniFile.Write("Item" + num, "Level", value.Level);
            iniFile.Write("Item" + num, "Exp", value.Exp);
            iniFile.Write("Item" + num, "Equip", value.Equip);
            iniFile.Write("Item" + num, "Transform", value.Transform);
            iniFile.Write("Item" + num, "MasteryScore", value.MasteryScore);
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 7; j++)
                {
                    iniFile.Write("Item" + num, "Animas[" + i + "]" + j, value.TypeAnima[i][j]);
                }
            }
            for (int k = 0; k < 6; k++)
            {
                iniFile.Write("Item" + num, "BlackJades" + k, value.BlackJades[k].Type);
            }
            num++;
        }
        num = 0;
        iniFile.Write("BlackJadeBag", "Count", BlackJadeBag.Count);
        foreach (BlackJade value2 in BlackJadeBag.Values)
        {
            iniFile.Write("BlackJadeBag", "BlackJade" + num, value2.Type);
            num++;
        }
    }
}
