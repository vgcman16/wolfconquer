using System;
using System.Collections.Generic;
using System.IO;

namespace WolfConquer.Core.World;

// Genuine native mutable table/row/load owner; native host startup remains separate.
internal class LegacyNativeHairfaceStorageTable
{
	public class Hairface
	{
		public LegacyHairFaceStorageType Type;

		public uint ID;

		public string? Name;

		public byte RareLevel;

		public byte RequiredVIPLevel;

		public uint Cost;

		public List<byte>? Classes;

		public byte Sex;

		public byte EquippedColor;

		public byte[]? Colors;
	}

	public class HairColor
	{
		public uint ID;

		public uint Cost;

		public byte Color;
	}

	public static List<Hairface> Hairfaces = new List<Hairface>();

	public static List<HairColor> HairColors = new List<HairColor>();

	public static void LoadFaces()
	{
		if (!File.Exists(LegacyNativeServerConfig.DbLocation + "hairface_storage_type.txt"))
		{
			return;
		}
		string[] array = File.ReadAllLines(LegacyNativeServerConfig.DbLocation + "hairface_storage_type.txt");
		string[] array2 = array;
		foreach (string text in array2)
		{
			string[] array3 = text.Split(new string[1] { "@@" }, StringSplitOptions.None);
			Hairface hairface = new Hairface();
			hairface.Type = (LegacyHairFaceStorageType)Convert.ToByte(array3[0]);
			hairface.ID = Convert.ToUInt32(array3[1]);
			hairface.Name = array3[3];
			hairface.RareLevel = Convert.ToByte(array3[4]);
			hairface.RequiredVIPLevel = Convert.ToByte(array3[5]);
			hairface.Cost = Convert.ToUInt32(array3[7]);
			hairface.Classes = new List<byte>();
			string[] array4 = array3[9].Split(new string[1] { "," }, StringSplitOptions.RemoveEmptyEntries);
			string[] array5 = array4;
			foreach (string s in array5)
			{
				hairface.Classes.Add(byte.Parse(s));
			}
			try
			{
				hairface.Sex = Convert.ToByte(array3[10]);
			}
			catch
			{
				hairface.Sex = 0;
			}
			hairface.Colors = new byte[7] { 1, 0, 0, 0, 0, 0, 0 };
			Hairfaces.Add(hairface);
		}
	}

	public static void LoadColors()
	{
		if (File.Exists(LegacyNativeServerConfig.DbLocation + "hair_color_type.txt"))
		{
			string[] array = File.ReadAllLines(LegacyNativeServerConfig.DbLocation + "hair_color_type.txt");
			string[] array2 = array;
			foreach (string text in array2)
			{
				string[] array3 = text.Split(new string[1] { "@@" }, StringSplitOptions.None);
				HairColor hairColor = new HairColor();
				hairColor.ID = Convert.ToUInt32(array3[0]);
				hairColor.Color = Convert.ToByte(array3[1]);
				hairColor.Cost = Convert.ToUInt32(array3[4]);
				HairColors.Add(hairColor);
			}
		}
	}

	public static void Load()
	{
		LoadFaces();
		LoadColors();
	}
}
