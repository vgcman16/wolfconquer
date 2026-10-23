using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

// Complete native Server method family; no startup or worker is registered.
internal static partial class LegacyNativeServerMapPopulation
{
	public static void LoadMonsters()
	{
		try
		{
			LegacyNativeItemCounterShellIni iniFile = new LegacyNativeItemCounterShellIni("");
			string[] files = Directory.GetFiles(LegacyNativeServerConfig.DbLocation + "\\Monsters\\");
			for (int i = 0; i < files.Length; i++)
			{
				string text = (iniFile.FileName = files[i]);
				LegacyNativeMonsterFamily monsterFamily = new LegacyNativeMonsterFamily();
				monsterFamily.ID = iniFile.ReadUInt32("cq_monstertype", "id");
				monsterFamily.Name = iniFile.ReadString("cq_monstertype", "name", "INVALID_MOB");
				monsterFamily.Level = iniFile.ReadUInt16("cq_monstertype", "level", 0);
				monsterFamily.MaxAttack = iniFile.ReadInt32("cq_monstertype", "attack_max", 0);
				monsterFamily.MinAttack = iniFile.ReadInt32("cq_monstertype", "attack_min", 0);
				if (monsterFamily.Name == "INVALID_MOB" || monsterFamily.Level == 0 || monsterFamily.ID == 0 || monsterFamily.MinAttack > monsterFamily.MaxAttack)
				{
					LegacyNativeConsole.WriteLine("[Error] Error Reading Monster File: " + text, ConsoleColor.DarkRed);
					continue;
				}
				monsterFamily.Defense = iniFile.ReadUInt16("cq_monstertype", "defence", 0);
				monsterFamily.Mesh = iniFile.ReadUInt16("cq_monstertype", "lookface", 0);
				monsterFamily.MaxHealth = iniFile.ReadInt32("cq_monstertype", "life", 0);
				monsterFamily.ViewRange = 16;
				monsterFamily.AttackRange = iniFile.ReadSByte("cq_monstertype", "attack_range", 0);
				monsterFamily.Dodge = iniFile.ReadByte("cq_monstertype", "dodge", 0);
				monsterFamily.DropBoots = iniFile.ReadByte("cq_monstertype", "drop_shoes", 0);
				monsterFamily.DropNecklace = iniFile.ReadByte("cq_monstertype", "drop_necklace", 0);
				monsterFamily.DropRing = iniFile.ReadByte("cq_monstertype", "drop_ring", 0);
				monsterFamily.DropArmet = iniFile.ReadByte("cq_monstertype", "drop_armet", 0);
				monsterFamily.DropArmor = iniFile.ReadByte("cq_monstertype", "drop_armor", 0);
				monsterFamily.DropShield = iniFile.ReadByte("cq_monstertype", "drop_shield", 0);
				monsterFamily.DropWeapon = iniFile.ReadByte("cq_monstertype", "drop_weapon", 0);
				monsterFamily.DropMoney = iniFile.ReadUInt16("cq_monstertype", "drop_money", 0);
				monsterFamily.DropHPItem = iniFile.ReadUInt32("cq_monstertype", "drop_hp");
				monsterFamily.DropMPItem = iniFile.ReadUInt32("cq_monstertype", "drop_mp");
				monsterFamily.Boss = iniFile.ReadByte("cq_monstertype", "Boss", 0);
				monsterFamily.Defense2 = iniFile.ReadInt32("cq_monstertype", "defence2", 0);
				if (monsterFamily.Boss != 0)
				{
					monsterFamily.AttackRange = 3;
				}
				monsterFamily.MoveSpeed = iniFile.ReadInt32("cq_monstertype", "move_speed", 0);
				monsterFamily.AttackSpeed = iniFile.ReadInt32("cq_monstertype", "attack_speed", 0);
				monsterFamily.SpellId = iniFile.ReadUInt32("cq_monstertype", "magic_type");
				monsterFamily.ExtraCritical = iniFile.ReadUInt32("cq_monstertype", "critical");
				monsterFamily.ExtraBreack = iniFile.ReadUInt32("cq_monstertype", "break");
				monsterFamily.extra_battlelev = iniFile.ReadInt32("cq_monstertype", "extra_battlelev", 0);
				monsterFamily.extra_exp = iniFile.ReadInt32("cq_monstertype", "extra_exp", 0);
				monsterFamily.extra_damage = iniFile.ReadInt32("cq_monstertype", "extra_damage", 0);
				if (monsterFamily.Boss == 0 && monsterFamily.MaxAttack > 3000)
				{
					monsterFamily.MaxAttack /= 2;
					monsterFamily.MinAttack /= 2;
				}
				monsterFamily.DropSpecials = new LegacyNativeSpecialItemWatcher[iniFile.ReadInt32("SpecialDrop", "Count", 0)];
				for (int j = 0; j < monsterFamily.DropSpecials.Length; j++)
				{
					string[] array = iniFile.ReadString("SpecialDrop", j.ToString(), "", 32).Split(',');
					monsterFamily.DropSpecials[j] = new LegacyNativeSpecialItemWatcher(uint.Parse(array[0]), int.Parse(array[1]));
				}
				monsterFamily.CreateItemGenerator();
				monsterFamily.CreateMonsterSettings();
				LegacyNativePoolMonsters.MonsterFamilies.Add(monsterFamily.ID, monsterFamily);
			}
		}
		catch (Exception ex)
		{
			LegacyNativeConsole.WriteLine(ex.ToString());
		}
	}
}
