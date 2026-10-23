using System;
using System.Collections.Generic;
using System.IO;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal class LegacyNativeFateExpTable
{
	public class Value
	{
		public uint Index;

		public LegacyNativeMsgChiInfo.ChiPowerType ChiType;

		public byte Level;

		public uint RequiredPoints;

		public LegacyNativeChiRuntime.ChiAttribute[]? Attributes;

		public uint MinScore;

		public uint MDefense;

		public uint MaxChiPoints;

		public uint StudyLuck;

		public uint DailyFreePoints;

		public uint CostReduction;

		public uint PrizeItemID;
	}

	public static Dictionary<uint, Value> Values = new Dictionary<uint, Value>();

	public static void Load()
	{
		if (!File.Exists(LegacyNativeServerConfig.DbLocation + "fate_exp.txt"))
		{
			return;
		}
		string[] array = File.ReadAllLines(LegacyNativeServerConfig.DbLocation + "fate_exp.txt");
		string[] array2 = array;
		foreach (string text in array2)
		{
			string[] array3 = text.Split(new string[1] { "@@" }, StringSplitOptions.RemoveEmptyEntries);
			Value value = new Value();
			value.Index = uint.Parse(array3[0]);
			value.ChiType = (LegacyNativeMsgChiInfo.ChiPowerType)byte.Parse(array3[1]);
			value.Level = byte.Parse(array3[2]);
			value.RequiredPoints = uint.Parse(array3[3]);
			value.Attributes = new LegacyNativeChiRuntime.ChiAttribute[4];
			for (int j = 0; j < value.Attributes.Length; j++)
			{
				value.Attributes[j] = new LegacyNativeChiRuntime.ChiAttribute((LegacyNativeChiAttribute)(uint.Parse(array3[4 + j]) / 10000), (ushort)(uint.Parse(array3[4 + j]) % 10000), locked: false);
			}
			value.MinScore = uint.Parse(array3[8]);
			value.MDefense = uint.Parse(array3[9]);
			value.CostReduction = uint.Parse(array3[10]) / 100;
			value.StudyLuck = uint.Parse(array3[11]);
			value.DailyFreePoints = uint.Parse(array3[12]);
			value.MaxChiPoints = uint.Parse(array3[13]);
			value.PrizeItemID = uint.Parse(array3[14]);
			Values.Add(value.Index, value);
		}
	}
}
