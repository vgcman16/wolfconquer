namespace WolfConquer.Core.World;

// Literal native ItemType slot/family predicate only; no equip-route or active-set policy.
internal sealed partial class LegacyNativeItemTypeRuneMetadata
{
	public static bool isRuneCheck(uint ID, uint Position)
	{
		if ((Position == 101 || Position == 121) && ID / 10000 == 401)
		{
			return true;
		}
		if ((Position == 102 || Position == 122) && ID / 10000 == 402)
		{
			return true;
		}
		if (((Position >= 103 && Position <= 114) || (Position >= 123 && Position <= 134)) && ID / 10000 == 403)
		{
			return true;
		}
		return false;
	}
}
