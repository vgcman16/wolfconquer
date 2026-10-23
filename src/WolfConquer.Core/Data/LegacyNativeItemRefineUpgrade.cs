#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeItemRefineUpgrade
{
	public class EffectEX
	{
		public uint ID;

		public uint ItemID;

		public uint StarReq;

		public uint AttributeNum;

		public uint Value;
	}

	public class Effect
	{
		public ushort Stars;

		public uint PAttack;

		public uint MAttack;

		public uint PDefense;

		public uint MDefense;

		public List<uint> Effects;
	}

	public static Dictionary<uint, uint> ProgresUpdates = new Dictionary<uint, uint>();

	public static Dictionary<uint, EffectEX> EffectsEX = new Dictionary<uint, EffectEX>();

	public static Dictionary<ushort, Effect> Effects = new Dictionary<ushort, Effect>();

	public static void Load()
	{
		string[] array = File.ReadAllLines(LegacyNativeServerConfig.DbLocation + "item_refine_upgrade.txt");
		string[] array2 = array;
		foreach (string text in array2)
		{
			string[] array3 = text.Split(new string[2] { "@@", " " }, StringSplitOptions.RemoveEmptyEntries);
			if (Convert.ToUInt16(array3[0]) == 1)
			{
				uint key = Convert.ToUInt32(array3[1]);
				uint value = Convert.ToUInt32(array3[2]);
				ProgresUpdates.Add(key, value);
			}
		}
		array = File.ReadAllLines(LegacyNativeServerConfig.DbLocation + "item_refine_effect_ex.txt");
		string[] array4 = array;
		foreach (string text2 in array4)
		{
			string[] array5 = text2.Split(new string[2] { "@@", " " }, StringSplitOptions.RemoveEmptyEntries);
			EffectEX effectEX = new EffectEX();
			effectEX.ID = Convert.ToUInt16(array5[0]);
			effectEX.ItemID = Convert.ToUInt32(array5[1]);
			effectEX.StarReq = Convert.ToUInt32(array5[2]);
			effectEX.AttributeNum = Convert.ToUInt32(array5[3]);
			effectEX.Value = Convert.ToUInt32(array5[4]);
			EffectsEX.Add(effectEX.ID, effectEX);
		}
		array = File.ReadAllLines(LegacyNativeServerConfig.DbLocation + "item_refine_effect.txt");
		string[] array6 = array;
		foreach (string text3 in array6)
		{
			string[] array7 = text3.Split(new string[2] { "@@", " " }, StringSplitOptions.RemoveEmptyEntries);
			Effect effect = new Effect();
			effect.Stars = Convert.ToUInt16(array7[0]);
			effect.PAttack = Convert.ToUInt32(array7[1]);
			effect.MAttack = Convert.ToUInt32(array7[2]);
			effect.PDefense = Convert.ToUInt32(array7[3]);
			effect.MDefense = Convert.ToUInt32(array7[4]);
			effect.Effects = new List<uint>();
			for (int l = 5; l < array7.Length; l++)
			{
				uint num = uint.Parse(array7[l]);
				if (num != 0)
				{
					effect.Effects.Add(num);
				}
			}
			Effects.Add(effect.Stars, effect);
		}
	}
}
