using System;
using System.Collections.Generic;
using System.IO;

namespace WolfConquer.Core.World;

internal class LegacyNativeRifinery : Dictionary<uint, LegacyNativeRifinery.Item>
{
	public enum RefineryType
	{
		None,
		MDefence,
		CriticalStrike,
		SkillCriticalStrike,
		Immunity,
		Break,
		Counteraction,
		Detoxication,
		Block,
		Penetration,
		Intensification,
		FinalMDamage,
		FinalMAttack
	}

	public class Item
	{
		public string Name = "";

		public uint ItemID = 0u;

		public uint Level = 0u;

		public uint ForItemPosition = 0u;

		public uint Procent = 0u;

		public uint Procent2 = 0u;

		public RefineryType Type = RefineryType.None;

		public RefineryType Type2 = RefineryType.None;
	}

	public LegacyNativeRifinery()
	{
		string[] array = File.ReadAllLines(LegacyNativeServerConfig.DbLocation + "Rifinery.txt");
		string[] array2 = array;
		foreach (string text in array2)
		{
			string[] array3 = text.Split(' ');
			Item item = new Item
			{
				ItemID = uint.Parse(array3[0]),
				Level = CalculateLevel(array3[1]),
				Procent = uint.Parse(array3[2]),
				Type = (RefineryType)byte.Parse(array3[3]),
				Type2 = (RefineryType)byte.Parse(array3[4]),
				Procent2 = uint.Parse(array3[5])
			};
			string text2 = array3[53];
			if (item.ItemID >= 3006165 && item.ItemID <= 3006170)
			{
				text2 = text2.Replace("(", "");
				text2 = text2.Split(')')[0];
			}
			else if (item.ItemID >= 3004136)
			{
				text2 = text2.Replace("[", "");
				text2 = text2.Split(']')[0];
			}
			item.Name = text2;
			item.ForItemPosition = ForItemPosition(text2);
			if (!ContainsKey(item.ItemID))
			{
				Add(item.ItemID, item);
			}
			if (!LegacyNativeItemTypeRuneMetadata.Refinary.ContainsKey(item.Level))
			{
				LegacyNativeItemTypeRuneMetadata.Refinary.Add(item.Level, new Dictionary<uint, Item>());
			}
			if (!LegacyNativeItemTypeRuneMetadata.Refinary[item.Level].ContainsKey(item.ItemID))
			{
				LegacyNativeItemTypeRuneMetadata.Refinary[item.Level].Add(item.ItemID, item);
			}
		}
		GC.Collect();
	}

	public uint ForItemPosition(string name)
	{
		uint result = 0u;
		switch (name)
		{
		default:
			if (!(name == "1-handed"))
			{
				break;
			}
			goto case "Bow";
		case "Bow":
		case "2-Handed":
		case "1-Handed":
		case "Backsword":
		case "2-handed":
			result = 4u;
			break;
		}
		if (name == "Shield" || name == "Hossu")
		{
			result = 5u;
		}
		if (name == "Ring" || name == "Bracelet")
		{
			result = 6u;
		}
		if (name == "Armor")
		{
			result = 3u;
		}
		if (name == "Boots")
		{
			result = 8u;
		}
		if (name == "Headgear")
		{
			result = 1u;
		}
		if (name == "Necklace" || name == "Bag")
		{
			result = 2u;
		}
		return result;
	}

	public static uint CalculateLevel(string name)
	{
		byte result = 0;
		if (name.Contains("Normal"))
		{
			result = 1;
		}
		if (name.Contains("Refined"))
		{
			result = 2;
		}
		if (name.Contains("Unique"))
		{
			result = 3;
		}
		if (name.Contains("Elite"))
		{
			result = 4;
		}
		if (name.Contains("Super"))
		{
			result = 5;
		}
		if (name.Contains("Sacred"))
		{
			result = 6;
		}
		return result;
	}
}
