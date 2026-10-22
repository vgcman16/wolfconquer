namespace WolfConquer.Core.World;

// Literal native single-ID items helpers on the existing owner.
internal sealed partial class LegacyNativeItemTypeRuneMetadata
{
	public static bool IsMoneyBag(uint ID)
	{
		return (ID >= 723713 && ID <= 723723) || ID == 3005945 || ID == 3008452;
	}

	public static byte GetSashCounts(uint ID)
	{
		return ID switch
		{
			1100009u => 12,
			1100006u => 6,
			1100003u => 3,
			_ => 0,
		};
	}

	public static bool IsSash(uint ID)
	{
		if (ID == 1100009 || ID == 1100006 || ID == 1100003)
		{
			return true;
		}
		return false;
	}

	public static bool IsTalisman(uint ID)
	{
		return LegacyNativeItemPosition.ItemPosition(ID) == 11 || LegacyNativeItemPosition.ItemPosition(ID) == 10 || LegacyNativeItemPosition.ItemPosition(ID) == 19;
	}

	public static int TypeofItem(uint ID)
	{
		int result = -1;
		uint num = ID % 1000000;
		switch (num / 1000)
		{
		case 123u:
			return 1;
		case 203u:
			return 21;
		case 204u:
			return 22;
		case 300u:
			if (ID % 10 != 0)
			{
				return 14;
			}
			break;
		case 601u:
			return 4;
		case 201u:
		case 202u:
			return 12;
		case 200u:
			return 20;
		case 350u:
			return 16;
		case 360u:
			return 15;
		case 370u:
			return 17;
		case 380u:
			return 18;
		}
		uint num2 = ID % 10000000 / 100000;
		uint num3 = num / 10000;
		switch (num2)
		{
		case 1u:
			switch (num3)
			{
			case 11u:
			case 14u:
			case 17u:
				return 1;
			case 18u:
			case 19u:
				return 11;
			case 12u:
				return 2;
			case 13u:
				return 3;
			case 15u:
				return 7;
			case 16u:
				return 8;
			}
			break;
		case 4u:
			return 4;
		case 5u:
			return 5;
		case 7u:
			return 9;
		case 9u:
			return 6;
		case 10u:
			return 0;
		case 0u:
		case 2u:
		case 3u:
		case 6u:
		case 8u:
		case 11u:
		case 12u:
		case 13u:
		case 14u:
		case 15u:
		case 16u:
		case 17u:
		case 18u:
		case 19u:
		case 20u:
		case 21u:
		case 22u:
		case 23u:
		case 24u:
		case 25u:
		case 26u:
		case 27u:
		case 28u:
		case 29u:
			if (num2 > 7)
			{
				return 9;
			}
			goto default;
		default:
			if (num2 != 6)
			{
				return result;
			}
			break;
		}
		return result;
	}

	public static bool IsHolyGrail(uint ID)
	{
		return ID == 2100095;
	}

	public static bool IsAccessory(uint ID)
	{
		return ID >= 350001 && ID <= 380046;
	}

	public static byte GetLevel(uint ID)
	{
		if (LegacyNativeItemPosition.ItemPosition(ID) == 3 || LegacyNativeItemPosition.ItemPosition(ID) == 1 || IsShield(ID))
		{
			return (byte)(ID % 100 / 10);
		}
		return (byte)(ID % 1000 / 10);
	}
}
