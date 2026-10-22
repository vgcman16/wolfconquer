namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeItemTypeRuneMetadata
{
	public static int GetUpEpLevelInfo(uint ID)
	{
		int num = 0;
		int level = GetLevel(ID);
		if (LegacyNativeItemPosition.ItemPosition(ID) == 3 || LegacyNativeItemPosition.ItemPosition(ID) == 1 || IsShield(ID))
		{
			switch (level)
			{
			case 5:
				num = 50;
				break;
			case 6:
				num = 40;
				break;
			case 7:
				num = 30;
				break;
			case 8:
			case 9:
				num = 20;
				break;
			default:
				num = 500;
				break;
			}
			switch ((int)(ID % 10))
			{
			case 6:
				num = num * 90 / 100;
				break;
			case 7:
				num = num * 70 / 100;
				break;
			case 8:
				num = num * 30 / 100;
				break;
			case 9:
				num = num * 10 / 100;
				break;
			}
		}
		else
		{
			num = level switch
			{
				11 => 95,
				12 => 90,
				13 => 85,
				14 => 80,
				15 => 75,
				16 => 70,
				17 => 65,
				18 => 60,
				19 => 55,
				20 => 50,
				21 => 45,
				22 => 40,
				_ => 500,
			};
			switch ((int)(ID % 10))
			{
			case 6:
				num = num * 90 / 100;
				break;
			case 7:
				num = num * 70 / 100;
				break;
			case 8:
				num = num * 30 / 100;
				break;
			case 9:
				num = num * 10 / 100;
				break;
			}
		}
		return (100 / num + 1) * 12 / 10;
	}

	public static int RateSuccForQuality(uint wQuality)
	{
		switch (wQuality)
		{
		case 0u:
		case 1u:
		case 2u:
		case 3u:
		case 4u:
		case 5u:
		case 6u:
			return 30;
		case 7u:
			return 12;
		case 8u:
			return 6;
		case 9u:
			return 5;
		default:
			return 0;
		}
	}
}
