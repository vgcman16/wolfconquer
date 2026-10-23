using WolfConquer.Core.World;
using System;
using System.Collections.Generic;
using System.IO;

namespace WolfConquer.Core.Data;

internal class LegacyNativeClanTable
{
	public static Dictionary<uint, List<uint>> clanally = new Dictionary<uint, List<uint>>();

	public static Dictionary<uint, List<uint>> clanenemy = new Dictionary<uint, List<uint>>();

	internal static void Save()
	{
		foreach (KeyValuePair<uint, LegacyNativeClanRuntime> clan in LegacyNativeClanRuntime.Clans)
		{
			LegacyNativeClanRuntime value = clan.Value;
			using LegacyNativeDbWrite write = new LegacyNativeDbWrite("Clans\\" + clan.Key + ".txt");
			write.Add(value.ToString()).Add(value.SaveAlly()).Add(value.SaveEnemy())
				.Execute(LegacyNativeDbWriteMode.Open);
		}
	}

	internal static void Load()
	{
		string[] files = Directory.GetFiles(LegacyNativeServerConfig.DbLocation + "\\Clans\\");
		foreach (string file in files)
		{
			using LegacyNativeDbRead read = new LegacyNativeDbRead(file, setlocation: true);
			if (read.Reader())
			{
				LegacyNativeClanRuntime clan = new LegacyNativeClanRuntime();
				clan.Load(read.ReadString(""));
				if (clan.ID > LegacyNativeClanRuntime.CounterClansID.Count)
				{
					LegacyNativeClanRuntime.CounterClansID.Set(clan.ID + 1);
				}
				LoadclanAlly(clan.ID, read.ReadString(""));
				LoadclanEnemy(clan.ID, read.ReadString(""));
				if (!LegacyNativeClanRuntime.Clans.ContainsKey(clan.ID))
				{
					LegacyNativeClanRuntime.Clans.TryAdd(clan.ID, clan);
				}
			}
		}
		LoadMemebers();
		ClanExecuteAllyAndEnemy();
		GC.Collect();
	}

	public static void ClanExecuteAllyAndEnemy()
	{
		foreach (KeyValuePair<uint, List<uint>> item in clanally)
		{
			foreach (uint item2 in item.Value)
			{
				if (LegacyNativeClanRuntime.Clans.TryGetValue(item2, out var value) && LegacyNativeClanRuntime.Clans.ContainsKey(item.Key))
				{
					LegacyNativeClanRuntime.Clans[item.Key].Ally.TryAdd(value.ID, value);
				}
			}
		}
		foreach (KeyValuePair<uint, List<uint>> item3 in clanenemy)
		{
			foreach (uint item4 in item3.Value)
			{
				if (LegacyNativeClanRuntime.Clans.TryGetValue(item4, out var value2) && LegacyNativeClanRuntime.Clans.ContainsKey(item3.Key))
				{
					LegacyNativeClanRuntime.Clans[item3.Key].Enemy.TryAdd(value2.ID, value2);
				}
			}
		}
	}

	private static void LoadMemebers()
	{
		LegacyNativeItemCounterShellIni iniFile = new LegacyNativeItemCounterShellIni("");
		string[] files = Directory.GetFiles(LegacyNativeServerConfig.DbLocation + "\\Users\\");
		foreach (string fileName in files)
		{
			iniFile.FileName = fileName;
			uint uID = iniFile.ReadUInt32("Character", "UID");
			string name = iniFile.ReadString("Character", "Name", "None");
			uint num = iniFile.ReadUInt32("Character", "ClanID");
			if (num != 0 && LegacyNativeClanRuntime.Clans.TryGetValue(num, out var value))
			{
				LegacyNativeClanMember member = new LegacyNativeClanMember();
				member.UID = uID;
				member.Name = name;
				member.Rank = (LegacyNativeClanRanks)iniFile.ReadUInt16("Character", "ClanRank", 200);
				member.Class = iniFile.ReadByte("Character", "Class", 0);
				member.Level = (byte)iniFile.ReadUInt16("Character", "Level", 0);
				member.Donation = iniFile.ReadUInt32("Character", "ClanDonation");
				if (!value.Members.ContainsKey(member.UID))
				{
					value.Members.TryAdd(member.UID, member);
				}
			}
		}
	}

	private static void LoadclanAlly(uint id, string line)
	{
		LegacyNativeDbReadLine readLine = new LegacyNativeDbReadLine(line, '/');
		int num = readLine.Read(0);
		for (int i = 0; i < num; i++)
		{
			uint item = readLine.Read(0u);
			if (clanally.ContainsKey(id))
			{
				clanally[id].Add(item);
				continue;
			}
			clanally.Add(id, new List<uint> { item });
		}
	}

	private static void LoadclanEnemy(uint id, string line)
	{
		LegacyNativeDbReadLine readLine = new LegacyNativeDbReadLine(line, '/');
		int num = readLine.Read(0);
		for (int i = 0; i < num; i++)
		{
			uint item = readLine.Read(0u);
			if (clanenemy.ContainsKey(id))
			{
				clanenemy[id].Add(item);
				continue;
			}
			clanenemy.Add(id, new List<uint> { item });
		}
	}
}
