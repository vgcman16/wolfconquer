using System;
using System.Collections.Generic;
using System.IO;

namespace WolfConquer.Core.World;

// Genuine native cache/row/Load; the shared row parser also serves explicit JSON startup.
internal class LegacyNativeBeastsAtrribute
{
	public class Attribute
	{
		public byte Level;

		public uint RequiredPoints;

		public uint HPAdd;

		public uint PAttack;

		public uint MAttack;

		public uint PDefense;

		public uint MDefense;

		public uint FinalPDmgDealt;

		public uint FinalMDmgDealt;

		public uint FinalPDmgReceived;

		public uint FinalMDmgReceived;

		public byte YellowRuneSlots;
	}

	public static Dictionary<byte, Attribute> Attributes = new Dictionary<byte, Attribute>();

	public static void Load()
	{
		if (File.Exists(LegacyNativeServerConfig.DbLocation + "beasts_attr.txt"))
		{
			string[] array = File.ReadAllLines(LegacyNativeServerConfig.DbLocation + "beasts_attr.txt");
			string[] array2 = array;
			foreach (string text in array2)
			{
				Attribute attribute = ParseRow(text);
				Attributes.Add(attribute.Level, attribute);
			}
		}
	}

	internal static Attribute ParseRow(string text)
	{
		string[] array3 = text.Split(new string[1] { "@@" }, StringSplitOptions.RemoveEmptyEntries);
		Attribute attribute = new Attribute();
		attribute.Level = byte.Parse(array3[0]);
		attribute.RequiredPoints = uint.Parse(array3[1]);
		attribute.HPAdd = uint.Parse(array3[2]);
		attribute.PAttack = uint.Parse(array3[3]);
		attribute.MAttack = uint.Parse(array3[4]);
		attribute.PDefense = uint.Parse(array3[5]);
		attribute.MDefense = uint.Parse(array3[6]);
		attribute.FinalPDmgDealt = uint.Parse(array3[7]);
		attribute.FinalMDmgDealt = uint.Parse(array3[8]);
		attribute.FinalPDmgReceived = uint.Parse(array3[9]);
		attribute.FinalMDmgReceived = uint.Parse(array3[10]);
		attribute.YellowRuneSlots = byte.Parse(array3[16]);
		return attribute;
	}
}
