using System;
using System.Collections.Generic;

namespace WolfConquer.Core.World;

internal class LegacyNativeMonsterFamily
{
	public static Random Random;

	public int rest_secs = 0;

	public string? Name;

	public int MaxAttack;

	public int MinAttack;

	public int MaxHealth;

	public ushort Defense;

	public ushort Mesh;

	public ushort Level;

	public byte ViewRange;

	public sbyte AttackRange;

	public byte Dodge;

	public uint ID;

	public byte Boss;

	public int AttackSpeed;

	public int MoveSpeed;

	public uint SpellId;

	public int RespawnTime;

	public int Defense2;

	public byte DropBoots;

	public byte DropArmor;

	public byte DropShield;

	public byte DropWeapon;

	public byte DropArmet;

	public byte DropRing;

	public byte DropNecklace;

	public ushort DropMoney;

	public uint DropHPItem;

	public uint DropMPItem;

	public ushort SpawnX;

	public ushort SpawnY;

	public ushort MaxSpawnX;

	public ushort MaxSpawnY;

	public uint MapID;

	public byte SpawnCount;

	public int maxnpc;

	public int extra_battlelev;

	public int extra_exp;

	public int extra_damage;

	public uint ExtraCritical;

	public uint ExtraBreack;

	public LegacyNativeSpecialItemWatcher[]? DropSpecials;

	public LegacyNativeMonsterSettings Settings;

	public LegacyNativeMobItemGenerator? ItemGenerator;

	private static Dictionary<uint, LegacyNativeMobItemGenerator> ItemGeneratorLinker;

	static LegacyNativeMonsterFamily()
	{
		// Framework native Random() seeds from TickCount; explicit net9 seed keeps its legacy algorithm.
		Random = new Random(Environment.TickCount);
		ItemGeneratorLinker = new Dictionary<uint, LegacyNativeMobItemGenerator>();
	}

	public void CreateMonsterSettings()
	{
		Settings = LegacyNativeMonsterSettings.Standard;
		if ((Name!.Contains("Guard") && !Name!.Contains("Chaos")) || Name!.Contains("CitySoldier"))
		{
			Settings = LegacyNativeMonsterSettings.Guard;
		}
		else if (Name!.Contains("Reviver"))
		{
			Settings = LegacyNativeMonsterSettings.Reviver;
		}
		else if (Name!.Contains("King"))
		{
			Settings = LegacyNativeMonsterSettings.King;
		}
		else if (Name!.Contains("Messenger"))
		{
			Settings = LegacyNativeMonsterSettings.Messenger;
		}
	}

	public void CreateItemGenerator()
	{
		if (!ItemGeneratorLinker.TryGetValue(ID, out ItemGenerator))
		{
			ItemGenerator = new LegacyNativeMobItemGenerator(this);
			ItemGeneratorLinker.Add(ID, ItemGenerator);
		}
	}

	public LegacyNativeMonsterFamily Copy()
	{
		LegacyNativeMonsterFamily monsterFamily = new LegacyNativeMonsterFamily();
		monsterFamily.Name = Name;
		monsterFamily.MaxAttack = MaxAttack;
		monsterFamily.MinAttack = MinAttack;
		monsterFamily.MaxHealth = MaxHealth;
		monsterFamily.Defense = Defense;
		monsterFamily.Mesh = Mesh;
		monsterFamily.Level = Level;
		monsterFamily.ViewRange = ViewRange;
		monsterFamily.AttackRange = AttackRange;
		monsterFamily.Dodge = Dodge;
		monsterFamily.ID = ID;
		monsterFamily.DropBoots = DropBoots;
		monsterFamily.DropArmor = DropArmor;
		monsterFamily.DropShield = DropShield;
		monsterFamily.DropWeapon = DropWeapon;
		monsterFamily.DropRing = DropRing;
		monsterFamily.DropNecklace = DropNecklace;
		monsterFamily.DropMoney = DropMoney;
		monsterFamily.DropHPItem = DropHPItem;
		monsterFamily.DropMPItem = DropMPItem;
		monsterFamily.Boss = Boss;
		monsterFamily.Defense2 = Defense2;
		monsterFamily.AttackSpeed = AttackSpeed;
		monsterFamily.MoveSpeed = MoveSpeed;
		monsterFamily.SpellId = SpellId;
		monsterFamily.RespawnTime = Random.Next(10, 20);
		monsterFamily.ExtraCritical = ExtraCritical;
		monsterFamily.ExtraBreack = ExtraBreack;
		monsterFamily.MapID = MapID;
		monsterFamily.extra_battlelev = extra_battlelev;
		monsterFamily.extra_damage = extra_damage;
		monsterFamily.extra_exp = extra_exp;
		if (monsterFamily.MaxHealth > 100000 && monsterFamily.MaxHealth < 7000000 && monsterFamily.Boss > 0)
		{
			monsterFamily.AttackRange = 12;
			monsterFamily.RespawnTime = 3600;
		}
		if (monsterFamily.ID == 20211)
		{
			monsterFamily.RespawnTime = Random.Next(100, 200);
		}
		if (monsterFamily.ID == 2700 || monsterFamily.ID == 2699 || monsterFamily.ID == 7022 || monsterFamily.ID == 2164 || monsterFamily.ID == 2165)
		{
			monsterFamily.RespawnTime = rest_secs;
		}
		if (monsterFamily.ID == 20055 || monsterFamily.ID == 4171 || monsterFamily.ID == 32070)
		{
			monsterFamily.RespawnTime = 3600;
		}
		monsterFamily.DropSpecials = new LegacyNativeSpecialItemWatcher[DropSpecials!.Length];
		for (int i = 0; i < DropSpecials!.Length; i++)
		{
			monsterFamily.DropSpecials[i] = DropSpecials![i];
		}
		monsterFamily.CreateItemGenerator();
		monsterFamily.CreateMonsterSettings();
		return monsterFamily;
	}
}
