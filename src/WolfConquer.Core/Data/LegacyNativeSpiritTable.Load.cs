using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeSpiritTable
{
    // Whole native SpiritTable31..60. Missing file retains state; individual rows catch broadly.
	public static void Load()
	{
		if (!File.Exists(LegacyNativeServerConfig.DbLocation + "spirit_rate.txt"))
		{
			return;
		}
		string[] array = File.ReadAllLines(LegacyNativeServerConfig.DbLocation + "spirit_rate.txt");
		string[] array2 = array;
		foreach (string text in array2)
		{
			if (!string.IsNullOrEmpty(text))
			{
				string[] array3 = text.Split(new string[1] { "@@" }, StringSplitOptions.RemoveEmptyEntries);
				try
				{
					Spirit spirit = new Spirit();
					spirit.Index = uint.Parse(array3[0]);
					spirit.AnimaID = uint.Parse(array3[2]);
					spirit.Type = (Type)byte.Parse(array3[3]);
					spirit.IsItem = byte.Parse(array3[4]) == 1;
					spirit.NewAnimaID = uint.Parse(array3[5]);
					SpiritRates.Add(spirit);
				}
				catch
				{
					Console.WriteLine("ERORRO" + text);
				}
			}
		}
	}
}
