namespace WolfConquer.Core.World;

// Complete native Calculate.Base method family; no modern combat projection.
internal static partial class LegacyNativeAttackBase
{
	public static uint CalculatePoisonDamage(uint Hitpoints, byte Level)
	{
		Hitpoints = (uint)(Hitpoints * (10 * Math.Min(Level + 1, 5)) / 100);
		if (Hitpoints == 0)
		{
			Hitpoints = 1u;
		}
		return Hitpoints;
	}

	public static uint CalculateBless(uint Damage, uint bless)
	{
		uint num = Damage * bless / 100;
		Damage = ((Damage <= num) ? 1u : (Damage - num));
		return Damage;
	}

	public static uint CalcaulateDeffence(uint Damage, uint Defense)
	{
		Damage = ((Damage <= Defense) ? 1u : (Damage - Defense));
		return Damage;
	}

	public static uint CalculateExtraAttack(uint Damage, uint pattack, uint pdeffence)
	{
		Damage += pattack;
		Damage = ((Damage <= pdeffence) ? 1u : (Damage - pdeffence));
		return Damage;
	}

	public static uint GetFinalDmg(uint Damage, uint mindmg, uint maxdmag)
	{
		if (Damage > maxdmag)
		{
			Damage = GetDamage(mindmg, maxdmag);
		}
		return Damage;
	}

	public static uint CalculateSoul(uint Damage, byte LevelSoul)
	{
		return Damage;
	}

	public static uint CalculateArtefactsDmg(uint Damage, uint AtackerPercent, uint TargetPercent)
	{
		if (AtackerPercent == TargetPercent)
		{
			return Damage;
		}
		if (AtackerPercent > TargetPercent)
		{
			uint num = AtackerPercent - TargetPercent;
			Damage += (uint)MulDiv((int)Damage, (int)(num / 100), 100);
		}
		return Damage;
	}

	public static int CalculatePotencyDamage(int Damage, int AttackerBattle, int TargetBattle, bool range = false)
	{
		if (AttackerBattle == TargetBattle)
		{
			return Damage;
		}
		int num = AttackerBattle - TargetBattle;
		if (num != 0)
		{
			num *= (range ? 10 : 3);
			if (num > 0)
			{
				num = Math.Min(60, num);
			}
			else if (num < 0)
			{
				num = Math.Max(-30, num);
			}
			Damage = MulDiv(Damage, 100 + num, 100);
		}
		return Damage;
	}

	public static int CalculateSoulsDamage(int Damage, int AttackerBattle, int TargetBattle)
	{
		if (AttackerBattle == TargetBattle)
		{
			return Damage;
		}
		int num = AttackerBattle - TargetBattle;
		if (num != 0)
		{
			if (num > 0)
			{
				num = Math.Min(60, num);
			}
			else if (num < 0)
			{
				num = Math.Max(-60, num);
			}
			Damage = MulDiv(Damage, 100 + num, 100);
		}
		return Damage;
	}

	public static uint CalculateHealtDmg(uint Damage, uint MaxHitPoints, uint MinHitPoints)
	{
		if (MaxHitPoints == MinHitPoints)
		{
			return 0u;
		}
		if (MaxHitPoints > MinHitPoints)
		{
			uint num = MaxHitPoints - MinHitPoints;
			if (num >= Damage)
			{
				return Damage;
			}
			return num;
		}
		return 0u;
	}
}
