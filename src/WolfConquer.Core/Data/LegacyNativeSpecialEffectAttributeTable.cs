#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

// Genuine native state/loader/lookups; the full combat-dispatch Get remains held separately.
internal partial class LegacyNativeSpecialEffectAttributeTable
{
	public enum AttriType : byte
	{
		HPLimit = 1,
		PAttack,
		PDefense,
		MAttack,
		MDefense,
		FinalPAttack,
		FinalPDamage,
		FinalMAttack,
		FinalMDamage,
		PStrike,
		MStrike,
		Immunity,
		Break,
		AntiBreak
	}

	public class Attribute
	{
		public uint ID;

		public uint Type;

		public uint value;

		public uint Score;

		public uint Exp;

		public uint Points;

		public uint uk6;

		public uint uk7;
	}

	public static Dictionary<uint, Attribute> SpEffectAttribute;

	public static void Load()
	{
		try
		{
			if (!File.Exists(LegacyNativeServerConfig.DbLocation + "speffect_attribute.txt"))
			{
				Console.WriteLine("!File.Exists: " + LegacyNativeServerConfig.DbLocation + "speffect_attribute.txt");
				return;
			}
			SpEffectAttribute = new Dictionary<uint, Attribute>();
			string[] array = File.ReadAllLines(LegacyNativeServerConfig.DbLocation + "speffect_attribute.txt");
			string[] array2 = array;
			foreach (string text in array2)
			{
				string[] array3 = text.Split(new string[1] { "@@" }, StringSplitOptions.RemoveEmptyEntries);
				Attribute attribute = new Attribute();
				attribute.ID = Convert.ToUInt32(array3[0]);
				attribute.Type = Convert.ToUInt32(array3[1]);
				attribute.value = Convert.ToUInt32(array3[2]);
				attribute.Score = Convert.ToUInt32(array3[3]);
				attribute.Exp = Convert.ToUInt32(array3[4]);
				attribute.Points = Convert.ToUInt32(array3[5]);
				attribute.uk6 = Convert.ToUInt32(array3[6]);
				attribute.uk7 = Convert.ToUInt32(array3[7]);
				SpEffectAttribute.Add(attribute.ID, attribute);
			}
		}
		catch (Exception value)
		{
			Console.WriteLine(value);
		}
	}
}
