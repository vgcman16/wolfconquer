using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMyCombatGear
{
    // Whole native MyCombatGear.A465..540; integer /100 and /10 remain in source order.
	public void A(LegacyNativeDaoqiDictTypeTable.AttrType type, double value)
	{
		switch (type)
		{
		case LegacyNativeDaoqiDictTypeTable.AttrType.MaxHP:
			user.Status.MaxHitpoints += NativeCombatStatusUInt32(value);
			break;
		case LegacyNativeDaoqiDictTypeTable.AttrType.PAttack:
			user.Status.MinAttack += NativeCombatStatusUInt32(value);
			user.Status.MaxAttack += NativeCombatStatusUInt32(value);
			break;
		case LegacyNativeDaoqiDictTypeTable.AttrType.PDefense:
			user.Status.Defence += NativeCombatStatusUInt32(value);
			break;
		case LegacyNativeDaoqiDictTypeTable.AttrType.MAttack:
			user.Status.MagicAttack += NativeCombatStatusUInt32(value);
			break;
		case LegacyNativeDaoqiDictTypeTable.AttrType.MDefense:
			user.Status.MagicDefence += NativeCombatStatusUInt32(value);
			break;
		case LegacyNativeDaoqiDictTypeTable.AttrType.FinalPAttack:
			user.Status.PhysicalDamageIncrease += NativeCombatStatusUInt32(value);
			break;
		case LegacyNativeDaoqiDictTypeTable.AttrType.FinalPDamage:
			user.Status.PhysicalDamageDecrease += NativeCombatStatusUInt32(value);
			break;
		case LegacyNativeDaoqiDictTypeTable.AttrType.FinalMAttack:
			user.Status.MagicDamageIncrease += NativeCombatStatusUInt32(value);
			break;
		case LegacyNativeDaoqiDictTypeTable.AttrType.FinalMDamage:
			user.Status.MagicDamageDecrease += NativeCombatStatusUInt32(value);
			break;
		case LegacyNativeDaoqiDictTypeTable.AttrType.PStrike:
			user.Status.CriticalStrike += NativeCombatStatusUInt32(value);
			break;
		case LegacyNativeDaoqiDictTypeTable.AttrType.MStrike:
			user.Status.SkillCStrike += NativeCombatStatusUInt32(value);
			break;
		case LegacyNativeDaoqiDictTypeTable.AttrType.Immunity:
			user.Status.Immunity += NativeCombatStatusUInt32(value);
			break;
		case LegacyNativeDaoqiDictTypeTable.AttrType.Break:
			user.Status.Breakthrough += NativeCombatStatusUInt32(value) / 10;
			break;
		case LegacyNativeDaoqiDictTypeTable.AttrType.AntiBreak:
			user.Status.Counteraction += NativeCombatStatusUInt32(value) / 10;
			break;
		case LegacyNativeDaoqiDictTypeTable.AttrType.Parry:
			user.PerfectionStatus.StrikeLock += NativeCombatStatusInt32(value) / 100;
			break;
		case LegacyNativeDaoqiDictTypeTable.AttrType.LuckyStrike:
			user.PerfectionStatus.LuckyStrike += NativeCombatStatusInt32(value) / 100;
			break;
		case LegacyNativeDaoqiDictTypeTable.AttrType.CoreStrike:
			user.PerfectionStatus.CoreStrike += NativeCombatStatusInt32(value) / 100;
			break;
		case LegacyNativeDaoqiDictTypeTable.AttrType.InvisibleArrow:
			user.PerfectionStatus.InvisibleArrow += NativeCombatStatusInt32(value) / 100;
			break;
		case LegacyNativeDaoqiDictTypeTable.AttrType.DrainingTouch:
			user.PerfectionStatus.DrainingTouch += NativeCombatStatusInt32(value) / 100;
			break;
		case LegacyNativeDaoqiDictTypeTable.AttrType.BloodSpawn:
			user.PerfectionStatus.BloodSpawn += NativeCombatStatusInt32(value) / 100;
			break;
		case LegacyNativeDaoqiDictTypeTable.AttrType.KillingFlash:
			user.PerfectionStatus.KillingFlash += NativeCombatStatusInt32(value) / 100;
			break;
		case LegacyNativeDaoqiDictTypeTable.AttrType.MirrorOfSin:
			user.PerfectionStatus.MirrorOfSin += NativeCombatStatusInt32(value) / 100;
			break;
		case LegacyNativeDaoqiDictTypeTable.AttrType.MaxMP:
			user.Status.MaxMana += NativeCombatStatusUInt32(value);
			break;
		}
	}
}
