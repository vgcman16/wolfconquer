namespace WolfConquer.Core.World;

// Complete native Calculate.Base method family; no modern combat projection.
internal static partial class LegacyNativeAttackBase
{
	public static bool GetRefinery(uint attacker, uint Attacked)
	{
		if (attacker <= Attacked)
		{
			return false;
		}
		return Rate((int)(attacker - Attacked));
	}

	public static bool GetRefinery()
	{
		return Rate(40);
	}

	public static uint MathMin(uint val1, uint val2)
	{
		if (val1 < val2)
		{
			return val1;
		}
		if (val2 <= val1)
		{
			return val2;
		}
		return 0u;
	}

	public static uint MathMax(uint val1, uint val2)
	{
		if (val1 < val2)
		{
			return val2;
		}
		if (val2 <= val1)
		{
			return val1;
		}
		return 0u;
	}

	public static uint GetDamage(uint MaxAttack, uint MinAttack)
	{
		if (MaxAttack == MinAttack)
		{
			MaxAttack++;
		}
		if (MaxAttack < MinAttack)
		{
			MaxAttack = MinAttack + 1;
		}
		return (uint)(MinAttack + MyRandom.Next((int)(MaxAttack - MinAttack)) / 2);
	}
}
