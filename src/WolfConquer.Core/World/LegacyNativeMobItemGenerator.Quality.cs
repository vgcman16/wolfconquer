namespace WolfConquer.Core.World;

internal partial class LegacyNativeMobItemGenerator
{
	public byte GeneratePurity()
	{
		if ((bool)PlusOne)
		{
			return 1;
		}
		if ((bool)PlusTwo)
		{
			return 2;
		}
		return 0;
	}

	public byte GenerateBless()
	{
		if (LegacyNativePoolRandom.GetRandom.Next(0, 1000) < 250)
		{
			int num = LegacyNativePoolRandom.GetRandom.Next(0, 100);
			if (num < 1)
			{
				return 5;
			}
			if (num < 6)
			{
				return 3;
			}
		}
		return 0;
	}

	public byte GenerateSocketCount(uint ItemID)
	{
		if (ItemID >= 410000 && ItemID <= 601999)
		{
			int num = LegacyNativePoolRandom.GetRandom.Next(0, 1000) % 100;
			if (num < 5)
			{
				return 2;
			}
			if (num < 20)
			{
				return 1;
			}
		}
		return 0;
	}

	private byte GenerateQuality()
	{
		if ((bool)Refined)
		{
			return 6;
		}
		if ((bool)Unique)
		{
			return 7;
		}
		if ((bool)Elite)
		{
			return 8;
		}
		if ((bool)Super)
		{
			return 9;
		}
		return 3;
	}

	public uint GenerateGold(out uint ItemID, bool normal = false, bool twin = false)
	{
		uint num = (uint)LegacyNativePoolRandom.GetRandom.Next(1, 200000);
		ItemID = LegacyNativeItemTypeRuneMetadata.MoneyItemID(num);
		return num;
	}

	private uint AlterItemLevel(uint dwItemLev, uint dwItemSort)
	{
		int num = LegacyNativeBaseFunc.RandGet(100, bRealRand: true);
		if (num < 50)
		{
			uint num2 = dwItemLev;
			dwItemLev = (uint)LegacyNativeBaseFunc.RandGet((int)(num2 / 2 + num2 / 3), bRealRand: false);
			if (dwItemLev > 1)
			{
				dwItemLev--;
			}
		}
		else if (num > 80)
		{
			dwItemLev = (((dwItemSort < 110 || dwItemSort > 114) && (dwItemSort < 130 || dwItemSort > 134) && (dwItemSort < 900 || dwItemSort > 999)) ? Math.Min(dwItemLev + 1, 23u) : Math.Min(dwItemLev + 1, 9u));
		}
		return dwItemLev;
	}
}
