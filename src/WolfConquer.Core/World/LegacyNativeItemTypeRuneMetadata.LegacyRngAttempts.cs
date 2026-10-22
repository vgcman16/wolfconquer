namespace WolfConquer.Core.World;

// Literal native ItemType RNG callers only; no spending, host dispatch or RNG substitution.
internal sealed partial class LegacyNativeItemTypeRuneMetadata
{
	public static byte GetNextRefineryItem()
	{
		if (LegacyNativeBaseFunc.RandGet(100, bRealRand: false) < 30)
		{
			return 2;
		}
		if (LegacyNativeBaseFunc.RandGet(100, bRealRand: false) < 30)
		{
			return 1;
		}
		return 0;
	}

	public static bool UpQualityDB(uint ID, uint DBs)
	{
		int upEpQualityInfo = GetUpEpQualityInfo(ID);
		if (DBs >= upEpQualityInfo)
		{
			return true;
		}
		double num = 100 / upEpQualityInfo;
		double num2 = (double)DBs * num;
		return (double)LegacyNativeBaseFunc.RandGet(100, bRealRand: true) < num2;
	}

	public static bool UpItemMeteors(uint ID, uint Meteors)
	{
		int upEpLevelInfo = GetUpEpLevelInfo(ID);
		if (Meteors >= upEpLevelInfo)
		{
			return true;
		}
		double num = 100 / upEpLevelInfo;
		double num2 = (double)Meteors * num;
		return (double)LegacyNativeBaseFunc.RandGet(100, bRealRand: true) < num2;
	}
}
