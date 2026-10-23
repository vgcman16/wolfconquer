using System.Collections.Generic;
using System.Linq;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

// Complete native MrRayzo.Database/InfoHeroReward.cs; no host birth, startup, save caller or SQL route installed.
internal class LegacyNativeInfoHeroRewardTable : LegacyNativeDbSafeDictionary<uint, LegacyNativeInfoHeroRewardTable.StageGoal>
{
	public class StageGoal
	{
		public class Item
		{
			public uint UID;

			public string Prize = "";

			public byte Progres;

			public uint GetPrize()
			{
				return uint.Parse(Prize.Split(new string[1] { "@@" }, StringSplitOptions.RemoveEmptyEntries)[0]);
			}

			public byte GetCount()
			{
				return byte.Parse(Prize.Split(new string[1] { "@@" }, StringSplitOptions.RemoveEmptyEntries)[1]);
			}
		}

		public uint UID = 0u;

		public uint OpenLev = 0u;

		public uint CountGoals = 0u;

		public string Prize = "";

		public LegacyNativeDbSafeDictionary<uint, Item> ArrayItem = new LegacyNativeDbSafeDictionary<uint, Item>();

		public bool CheckOpen(ushort level, byte reborn)
		{
			uint num = OpenLev % 1000;
			uint num2 = (OpenLev - num) / 1000;
			return level >= num && reborn >= num2;
		}

		public uint GetPrize()
		{
			return uint.Parse(Prize.Split(new string[1] { "@@" }, StringSplitOptions.RemoveEmptyEntries)[0]);
		}

		public byte GetCount()
		{
			return byte.Parse(Prize.Split(new string[1] { "@@" }, StringSplitOptions.RemoveEmptyEntries)[1]);
		}
	}

	internal void LoadInformations()
	{
		LegacyNativeItemCounterShellIni iniFile = new LegacyNativeItemCounterShellIni("StageGoal.txt");
		int num = iniFile.ReadByte("StageGoal", "StageAmount", 0);
		for (uint num2 = 1u; num2 <= num; num2++)
		{
			StageGoal stageGoal = new StageGoal
			{
				UID = num2,
				CountGoals = iniFile.ReadByte(num2.ToString(), "GoalNum", 0),
				Prize = iniFile.ReadString(num2.ToString(), "Prize1", "")
			};
			Add(num2, stageGoal);
			for (uint num3 = 1u; num3 <= stageGoal.CountGoals; num3++)
			{
				uint num4 = num2 * 100 + num3;
				string section = num2 + "-" + num3;
				stageGoal.ArrayItem.Add(num4, new StageGoal.Item
				{
					UID = num4,
					Progres = iniFile.ReadByte(section, "Progress", 0),
					Prize = iniFile.ReadString(section, "Prize1", "")
				});
			}
		}
	}
}
