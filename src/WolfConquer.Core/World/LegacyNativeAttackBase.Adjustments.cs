namespace WolfConquer.Core.World;

// Complete native Calculate.Base method family; no modern combat projection.
internal static partial class LegacyNativeAttackBase
{
	public class StatusConstants
	{
		public const int AdjustSet = -30000;

		public const int AdjustFull = -32768;

		public const int AdjustPercent = 30000;

		public const int NAME_GREEN = 1;

		public const int NAME_WHITE = 2;

		public const int NAME_RED = 3;

		public const int NAME_BLACK = 4;
	}

	public static int CutTrail(int x, int y)
	{
		return (x >= y) ? x : y;
	}

	public static int AdjustHitrate(int hitrate, int power)
	{
		int num = 0;
		num += Math.Max(0, AdjustDataEx(hitrate, power)) - hitrate;
		return hitrate + num;
	}

	public static int GetNameType(int nAtkerLev, int nMonsterLev)
	{
		int num = nAtkerLev - nMonsterLev;
		if (num >= 3)
		{
			return 1;
		}
		if (num >= 0)
		{
			return 2;
		}
		if (num >= -5)
		{
			return 3;
		}
		return 4;
	}

	public static int CalcDamageUser2Monster(int nAtk, int nDef, int nAtkLev, int nDefLev, bool Range)
	{
		if (Range && nAtkLev >= 117)
		{
			nAtkLev = 117;
		}
		int val = nAtk - nDef;
		if (GetNameType(nAtkLev, nDefLev) > 2)
		{
			return Math.Max(0, val);
		}
		int num = nAtkLev - nDefLev;
		if (num >= 0 && num < 3)
		{
			nAtk = MulDiv(nAtk, 120, 100);
		}
		else if (num >= 3 && num <= 5)
		{
			nAtk = MulDiv(nAtk, 150, 100);
		}
		else if (num > 5 && num <= 10)
		{
			nAtk = MulDiv(nAtk, 200, 100);
		}
		else if (num > 10 && num <= 20)
		{
			nAtk = MulDiv(nAtk, 250, 100);
		}
		else if (num > 20)
		{
			nAtk = MulDiv(nAtk, 300, 100);
		}
		return Math.Max(0, nAtk - nDef);
	}

	public static int AdjustMinDamageUser2Monster(int nDamage, LegacyNativeGameClientRuntime pAtker)
	{
		int num = 1;
		num += pAtker.Player.Level / 10;
		num += (int)(pAtker.Equipment.LeftWeapon % 10);
		num += (int)(pAtker.Equipment.RightWeapon % 10);
		return Math.Max(num, nDamage);
	}

	public static int MulDiv(int number, int numerator, int denominator)
	{
		return number * numerator / denominator;
	}

	public static long BigMulDiv(long number, long numerator, long denominator)
	{
		return (number * numerator + denominator / 2) / denominator;
	}

	public static int AdjustDataEx(int data, int adjust, int maxData = 0)
	{
		if (adjust >= 30000)
		{
			return MulDiv(data, adjust - 30000, 100);
		}
		if (adjust <= -30000)
		{
			return -1 * adjust + -30000;
		}
		if (adjust == -32768)
		{
			return maxData;
		}
		return data + adjust;
	}

	public static int AdjustAttack(int attack, int power, int defense)
	{
		int num = 0;
		if (defense > 0)
		{
			num += Math.Max(0, AdjustDataEx(defense, power, 100)) - attack;
		}
		num += Math.Max(0, AdjustDataEx(attack, power)) - attack;
		return attack + num;
	}

	public static int AdjustDefense(int defense, int power, int decrease = 0)
	{
		int num = 0;
		num += Math.Max(0, AdjustDataEx(defense, power, decrease)) - defense;
		return defense + num;
	}
}
