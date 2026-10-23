namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    // Native declaration209; full GameClient ctor owns initialization, not partial birth.
    internal System.Collections.Concurrent.ConcurrentDictionary<LegacyNativeRoleStatus.StatuTyp, LegacyNativeRoleStatus>? ExtraStatus;

public uint AjustDefense
	{
		get
		{
			uint defence = Status.Defence;
			uint num = 0u;
			uint num2 = GemValues(LegacyNativeGameItemFlags.Gem.NormalTortoiseGem);
			if (num2 != 0)
			{
				num += (uint)LegacyNativeAttackBase.MulDiv((int)defence, (int)num2, 100);
			}
			if (Player.ContainFlag(LegacyNativeMsgUpdate.Flags.MagicShield) || Player.OnDefensePotion)
			{
				num += (uint)(LegacyNativeAttackBase.MulDiv((int)defence, 120, 100) - (int)defence);
			}
			if (Player.ContainFlag(LegacyNativeMsgUpdate.Flags.DragonSwing))
			{
				num += (uint)(LegacyNativeAttackBase.MulDiv((int)defence, 115, 100) - (int)defence);
			}
			if (Player.ContainFlag(LegacyNativeMsgUpdate.Flags.DivineGuard))
			{
				num += (uint)(LegacyNativeAttackBase.MulDiv((int)defence, 300, 100) - (int)defence);
			}
			return Player.ContainFlag(LegacyNativeMsgUpdate.Flags.HookMoon) ? ((defence + num) * Player.HookMoonAttackedPower / 100) : (defence + num);
		}
	}

internal uint AjustAttack(uint Damage, ILegacyNativeMapObj? attacked)
	{
		uint num = 0u;
		if (Player.ContainFlag(LegacyNativeMsgUpdate.Flags.Stigma) || Player.OnAttackPotion)
		{
			num += (uint)(LegacyNativeAttackBase.MulDiv((int)Damage, 130, 100) - (int)Damage);
		}
		if (Status.PhysicalPercent != 0)
		{
			num += (uint)LegacyNativeAttackBase.MulDiv((int)Damage, (int)Status.PhysicalPercent, 100);
		}
		if (Player.doFocus)
		{
			num += (uint)(LegacyNativeAttackBase.MulDiv((int)Damage, Player.IntensifyDamage, 100) - (int)Damage);
		}
		return Damage + num;
	}

	public int GetDefense2()
	{
		return (Player.Reborn >= 2) ? 5000 : 10000;
	}

	public uint AjustCriticalStrike()
	{
		if (ExtraStatus!.TryGetValue(LegacyNativeRoleStatus.StatuTyp.IncreasePStrike, out var value) && (bool)value)
		{
			return Status.CriticalStrike + (uint)value;
		}
		return Status.CriticalStrike;
	}

	public uint AjustMCriticalStrike()
	{
		if (Player != null && Player.Map == 10137 && ExtraStatus!.TryGetValue(LegacyNativeRoleStatus.StatuTyp.IncreaseMStrike, out var value) && (bool)value)
		{
			return Status.SkillCStrike + (uint)value;
		}
		return Status.SkillCStrike;
	}

	public uint AjustImunity()
	{
		if (ExtraStatus!.TryGetValue(LegacyNativeRoleStatus.StatuTyp.IncreaseImunity, out var value) && (bool)value)
		{
			return Status.Immunity + (uint)value;
		}
		return Status.Immunity;
	}

	public uint AjustBreakthrough()
	{
		if (Player != null && Player.Map == 10137 && ExtraStatus!.TryGetValue(LegacyNativeRoleStatus.StatuTyp.IncreaseBreack, out var value) && (bool)value)
		{
			return Status.Breakthrough + (uint)value;
		}
		return Status.Breakthrough;
	}

	public uint AjustAntiBreack()
	{
		if (ExtraStatus!.TryGetValue(LegacyNativeRoleStatus.StatuTyp.IncreaseAntiBreack, out var value) && (bool)value)
		{
			return Status.Counteraction + (uint)value;
		}
		return Status.Counteraction;
	}

	public uint AjustMagicDamageIncrease()
	{
		if (ExtraStatus!.TryGetValue(LegacyNativeRoleStatus.StatuTyp.IncreaseFinalMAttack, out var value) && (bool)value)
		{
			return Status.MagicDamageIncrease + (uint)value;
		}
		return Status.MagicDamageIncrease;
	}

	public uint AjustMagicDamageDecrease()
	{
		if (ExtraStatus!.TryGetValue(LegacyNativeRoleStatus.StatuTyp.IncreaseFinalMDamage, out var value) && (bool)value)
		{
			return Status.MagicDamageDecrease + (uint)value;
		}
		return Status.MagicDamageDecrease;
	}

	public uint AjustPhysicalDamageIncrease()
	{
		if (ExtraStatus!.TryGetValue(LegacyNativeRoleStatus.StatuTyp.IncreaseFinalPAttack, out var value) && (bool)value)
		{
			return Status.PhysicalDamageIncrease + (uint)value;
		}
		return Status.PhysicalDamageIncrease;
	}

	public uint AjustPhysicalDamageDecrease()
	{
		if (ExtraStatus!.TryGetValue(LegacyNativeRoleStatus.StatuTyp.IncreaseFinalPDamage, out var value) && (bool)value)
		{
			return Status.PhysicalDamageDecrease + (uint)value;
		}
		return Status.PhysicalDamageDecrease;
	}

	public uint AjustMagicAttack()
	{
		if (ExtraStatus!.TryGetValue(LegacyNativeRoleStatus.StatuTyp.IncreaseMAttack, out var value) && (bool)value)
		{
			return Status.MagicAttack + (uint)value;
		}
		return Status.MagicAttack;
	}

	public uint AjustMaxHitpoints()
	{
		if (ExtraStatus!.TryGetValue(LegacyNativeRoleStatus.StatuTyp.IncreaseMaxHp, out var value) && (bool)value)
		{
			return Status.MaxHitpoints + (uint)value;
		}
		return Status.MaxHitpoints;
	}

	public uint AjustMaxAttack(uint damage)
	{
		if (ExtraStatus!.TryGetValue(LegacyNativeRoleStatus.StatuTyp.IncreasePAttack, out var value) && (bool)value)
		{
			return damage + (uint)value;
		}
		return damage;
	}
}
