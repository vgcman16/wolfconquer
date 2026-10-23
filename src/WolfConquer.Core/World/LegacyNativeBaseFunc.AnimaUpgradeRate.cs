namespace WolfConquer.Core.World;

public static partial class LegacyNativeBaseFunc
{
    // Complete native BaseFunc29..50. No RNG draw or floating expression occurs here.
	public static byte AnimaUpgradeRate(uint AnimaID, bool forging = false)
	{
		switch (AnimaID % 100)
		{
		case 10u:
		case 11u:
			return 35;
		case 12u:
			return 25;
		case 13u:
		case 14u:
		case 15u:
		case 16u:
		case 17u:
		case 18u:
			return 100;
		case 19u:
			return 0;
		default:
			return (byte)(forging ? 58u : 55u);
		}
	}
}
