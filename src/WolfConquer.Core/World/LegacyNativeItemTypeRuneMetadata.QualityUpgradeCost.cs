namespace WolfConquer.Core.World;

// Literal native ItemType.GetUpEpQualityInfo only; no upgrade attempt or spending.
internal sealed partial class LegacyNativeItemTypeRuneMetadata
{
	public static int GetUpEpQualityInfo(uint ID)
	{
		LegacyNativeItemRuneMetadata dBItem = LegacyNativePoolItems.ItemsBase![ID];
		int num = 100;
		num = (ID % 10) switch
		{
			6u => 50,
			7u => 33,
			8u => 20,
			_ => 100,
		};
		int level = dBItem.Level;
		if (level > 70)
		{
			num = (int)((double)num * (100.0 - (double)(level - 70) * 1.0) / 100.0);
		}
		return Math.Max(1, 100 / num);
	}
}
