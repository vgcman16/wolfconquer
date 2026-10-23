#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeProfessionTable
{
	public class ProfLevelUp
	{
		public uint Class;

		public byte Level;

		public uint QuestID;

		public uint PrestigeScore;

		public uint EXP;
	}

	public class Benefit
	{
		public uint Class;

		public byte BattlePower;

		public AttributeValue[] AttributeValue;

		public uint PrestigeScore;
	}

	public class AttributeValue
	{
		public LegacyNativeChiAttribute Type;

		public uint Value;
	}

	public class TitleBenefit
	{
		public byte Class;

		public byte Rank;

		public AttributeValue[] AttributeValue;
	}

	public static Dictionary<uint, ProfLevelUp> ProfLevelUPList = new Dictionary<uint, ProfLevelUp>();

	public static Dictionary<uint, Benefit> Benefits = new Dictionary<uint, Benefit>();

	public static Dictionary<uint, TitleBenefit> TitleBenefits = new Dictionary<uint, TitleBenefit>();

	private static void LoadProfLevUP()
	{
		if (File.Exists(LegacyNativeServerConfig.DbLocation + "prof_lev_up.txt"))
		{
			string[] array = File.ReadAllLines(LegacyNativeServerConfig.DbLocation + "prof_lev_up.txt");
			string[] array2 = array;
			foreach (string text in array2)
			{
				string[] array3 = text.Split(new string[1] { "@@" }, StringSplitOptions.RemoveEmptyEntries);
				ProfLevelUp profLevelUp = new ProfLevelUp();
				profLevelUp.Class = Convert.ToUInt32(array3[1]);
				profLevelUp.Level = Convert.ToByte(array3[2]);
				profLevelUp.QuestID = Convert.ToUInt32(array3[3]);
				profLevelUp.PrestigeScore = Convert.ToUInt32(array3[4]);
				profLevelUp.EXP = Convert.ToUInt32(array3[5]);
				ProfLevelUPList.Add(Convert.ToUInt32(array3[0]), profLevelUp);
			}
		}
	}

	private static void LoadProfBenefits()
	{
		if (!File.Exists(LegacyNativeServerConfig.DbLocation + "prof_lev_benefit.txt"))
		{
			return;
		}
		string[] array = File.ReadAllLines(LegacyNativeServerConfig.DbLocation + "prof_lev_benefit.txt");
		string[] array2 = array;
		foreach (string text in array2)
		{
			string[] array3 = text.Split(new string[1] { "@@" }, StringSplitOptions.RemoveEmptyEntries);
			Benefit benefit = new Benefit();
			benefit.Class = Convert.ToUInt32(array3[1]);
			benefit.BattlePower = Convert.ToByte(array3[2]);
			benefit.AttributeValue = new AttributeValue[4];
			for (int j = 0; j < benefit.AttributeValue.Length; j++)
			{
				benefit.AttributeValue[j] = new AttributeValue();
				benefit.AttributeValue[j].Type = (LegacyNativeChiAttribute)(Convert.ToUInt32(array3[j + 3]) / 10000);
				benefit.AttributeValue[j].Value = Convert.ToUInt32(array3[j + 3]) % 10000;
			}
			benefit.PrestigeScore = Convert.ToUInt32(array3[7]);
			Benefits.Add(benefit.Class, benefit);
		}
	}

	private static void LoadProfTitleBenefits()
	{
		if (!File.Exists(LegacyNativeServerConfig.DbLocation + "prof_title_benefit.txt"))
		{
			return;
		}
		string[] array = File.ReadAllLines(LegacyNativeServerConfig.DbLocation + "prof_title_benefit.txt");
		string[] array2 = array;
		foreach (string text in array2)
		{
			string[] array3 = text.Split(new string[1] { "@@" }, StringSplitOptions.RemoveEmptyEntries);
			TitleBenefit titleBenefit = new TitleBenefit();
			titleBenefit.Class = Convert.ToByte(array3[1]);
			titleBenefit.Rank = Convert.ToByte(array3[2]);
			titleBenefit.AttributeValue = new AttributeValue[4];
			for (int j = 0; j < titleBenefit.AttributeValue.Length; j++)
			{
				titleBenefit.AttributeValue[j] = new AttributeValue();
				titleBenefit.AttributeValue[j].Type = (LegacyNativeChiAttribute)(Convert.ToUInt32(array3[j + 3]) / 10000);
				titleBenefit.AttributeValue[j].Value = Convert.ToUInt32(array3[j + 3]) % 10000;
			}
			TitleBenefits.Add(Convert.ToUInt32(array3[0]), titleBenefit);
		}
	}

	public static void Load()
	{
		LoadProfBenefits();
		LoadProfLevUP();
		LoadProfTitleBenefits();
	}
}
