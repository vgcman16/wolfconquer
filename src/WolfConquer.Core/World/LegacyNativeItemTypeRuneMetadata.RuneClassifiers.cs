namespace WolfConquer.Core.World;

// Literal native single-ID runes helpers on the existing owner.
internal sealed partial class LegacyNativeItemTypeRuneMetadata
{
	public static bool isRedRune(uint itemid)
	{
		if (itemid / 10000 == 401)
		{
			return true;
		}
		return false;
	}

	public static bool isBlueRune(uint itemid)
	{
		if (itemid / 10000 == 402)
		{
			return true;
		}
		return false;
	}

	public static bool isYellowRune(uint itemid)
	{
		if (itemid / 10000 == 403)
		{
			return true;
		}
		return false;
	}

	public static byte MaxRuneLevel(uint id)
	{
		byte result = (byte)((id / 10000 == 403) ? 9u : 27u);
		if (id / 10000 == 401 || id / 10000 == 401)
		{
			result = 1;
		}
		return result;
	}

	public static uint FlashShieldRunePoints(uint itemID)
	{
		uint result = 5000u;
		switch (itemID % 100)
		{
		case 2u:
			result = 6000u;
			break;
		case 3u:
			result = 7000u;
			break;
		case 4u:
			result = 7500u;
			break;
		case 5u:
			result = 8000u;
			break;
		case 6u:
			result = 8500u;
			break;
		case 7u:
			result = 9000u;
			break;
		case 8u:
			result = 9500u;
			break;
		case 9u:
			result = 10000u;
			break;
		case 10u:
			result = 11000u;
			break;
		case 11u:
			result = 11500u;
			break;
		case 12u:
			result = 12000u;
			break;
		case 13u:
			result = 12500u;
			break;
		case 14u:
			result = 13000u;
			break;
		case 15u:
			result = 13500u;
			break;
		case 16u:
			result = 14000u;
			break;
		case 17u:
			result = 14500u;
			break;
		case 18u:
			result = 15000u;
			break;
		case 19u:
			result = 15500u;
			break;
		case 20u:
			result = 16000u;
			break;
		case 21u:
			result = 16500u;
			break;
		case 22u:
			result = 17000u;
			break;
		case 23u:
			result = 17500u;
			break;
		case 24u:
			result = 18000u;
			break;
		case 25u:
			result = 18500u;
			break;
		case 26u:
			result = 19000u;
			break;
		case 27u:
			result = 20000u;
			break;
		}
		return result;
	}
}
