using System;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeChiTable
{
	public static LegacyNativeChiRuntime.ChiAttribute[]? ReadPowers(string? str)
	{
		if (string.IsNullOrEmpty(str))
		{
			return null;
		}
		LegacyNativeChiRuntime.ChiAttribute[] array = new LegacyNativeChiRuntime.ChiAttribute[4];
		string[] array2 = str.Split('|');
		int num = 0;
		string[] array3 = array2;
		foreach (string text in array3)
		{
			string[] array4 = text.Split('-');
			array[num] = new LegacyNativeChiRuntime.ChiAttribute((LegacyNativeChiAttribute)int.Parse(array4[0]), ushort.Parse(array4[1]), locked: false);
			num++;
		}
		return array;
	}

	public static string PowersToString(LegacyNativeChiRuntime.ChiAttribute[]? Powers)
	{
		if (Powers == null)
		{
			return "";
		}
		string[] array = new string[15];
		int type = (int)Powers[0].Type;
		array[0] = type.ToString();
		array[1] = "-";
		array[2] = Powers[0].Value.ToString();
		array[3] = "|";
		type = (int)Powers[1].Type;
		array[4] = type.ToString();
		array[5] = "-";
		array[6] = Powers[1].Value.ToString();
		array[7] = "|";
		type = (int)Powers[2].Type;
		array[8] = type.ToString();
		array[9] = "-";
		array[10] = Powers[2].Value.ToString();
		array[11] = "|";
		type = (int)Powers[3].Type;
		array[12] = type.ToString();
		array[13] = "-";
		array[14] = Powers[3].Value.ToString();
		return string.Concat(array);
	}
}
