using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMyCombatGear
{
public void AttackEffect(ref LegacyNativeMsgSpellAnimation.SpellObj SpellObj, LegacyNativeMagicType.Magic? DBSpell, ILegacyNativeMapObj? target, ref uint Damage)
	{
		if (isOpen(TypeID.Dragonhowl))
		{
			LegacyNativeMsgUpdate.Flags flag = LegacyNativeMsgUpdate.Flags.DragonPierceCrit;
			if (user.Player.ContainFlag(flag) && (SpellObj.Effect & LegacyNativeMsgAttackPacket.AttackEffect.CriticalStrike) == LegacyNativeMsgAttackPacket.AttackEffect.CriticalStrike)
			{
				SpellObj.Effect |= LegacyNativeMsgAttackPacket.AttackEffect.Destroy;
				SpellObj.Damage += SpellObj.Damage * 10 / 100;
			}
		}
		if (!isOpen(TypeID.Taoist5) || DBSpell == null || (DBSpell.ID != 1165 && DBSpell.ID != 1001 && DBSpell.ID != 1002))
		{
			return;
		}
		LegacyNativeMsgUpdate.Flags flag2 = LegacyNativeMsgUpdate.Flags.Oblivion | LegacyNativeMsgUpdate.Flags.HawksAmbition;
		if (user.Player.ContainFlag(flag2))
		{
			switch (target!.ObjType)
			{
			case LegacyNativeMapObjectType.Player:
				Damage += (uint)LegacyNativeAttackBase.MulDiv((int)Damage, NativeGearAttackInt32(GetValue("WildPhoenix")[0] % 100.0), 100);
				break;
			case LegacyNativeMapObjectType.Monster:
			case LegacyNativeMapObjectType.SobNpc:
				Damage += (uint)LegacyNativeAttackBase.MulDiv((int)Damage, NativeGearAttackInt32(GetValue("WildPhoenix")[0] / 1000.0), 100);
				break;
			}
		}
	}
}
