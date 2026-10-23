using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeCalculatePhysical
{
    // Whole native Physical.OnMonster14..152. No floor/cold-path-only substitute.
public static void OnMonster(LegacyNativePlayerRuntime player, LegacyNativeMonsterRole monster, LegacyNativeMagicType.Magic? DBSpell, out LegacyNativeMsgSpellAnimation.SpellObj SpellObj, byte MultipleDamage = 0)
	{
		SpellObj = new LegacyNativeMsgSpellAnimation.SpellObj(monster.UID, 0u, LegacyNativeMsgAttackPacket.AttackEffect.None);
		if (monster.IsFloor)
		{
			SpellObj.Damage = 2u;
			return;
		}
		int damage = (int)LegacyNativeAttackBase.GetDamage(player.Owner.Status.MaxAttack, player.Owner.Status.MinAttack);
		damage = (int)player.Owner.AjustAttack((uint)damage, player);
		damage = (int)player.Owner.AjustMaxAttack((uint)damage);
		ushort defense = monster.Family.Defense;
		damage = Math.Max(0, damage - defense);
		if (player.ContainFlag(LegacyNativeMsgUpdate.Flags.Superman))
		{
			damage += damage * 2;
		}
		if (DBSpell != null && DBSpell.Damage < 10f)
		{
			DBSpell.Damage = 10f;
		}
		damage = (player.ContainFlag(LegacyNativeMsgUpdate.Flags.FatalStrike) ? LegacyNativeAttackBase.MulDiv(damage, 500, 100) : ((DBSpell != null && DBSpell.ID == 12860) ? LegacyNativeAttackBase.MulDiv(damage, 350, 100) : ((MultipleDamage != 0) ? (damage * MultipleDamage) : ((DBSpell != null && DBSpell.ID == 12770) ? ((monster.Boss != 1) ? LegacyNativeAttackBase.MulDiv(damage, 500, 100) : LegacyNativeAttackBase.MulDiv(damage, 350, 100)) : ((DBSpell == null || !LegacyNativeCombatGear.CheckSpells(DBSpell.ID)) ? LegacyNativeAttackBase.MulDiv(damage, PhysicalMonsterFactor(DBSpell?.Damage ?? ((float)LegacyNativeServerConfig.PhysicalDamage)), 100) : LegacyNativeCombatGear.GetDamage(damage, DBSpell.GDamage, DBSpell.DamageOnHuman, DBSpell.Damage2, DBSpell.Damage3, DBSpell.DamageOnMonster, Player: false))))));
		if (player.ContainFlag(LegacyNativeMsgUpdate.Flags.ManiacDance))
		{
			damage = ((monster.Boss != 0) ? (damage * 3) : (damage * 10));
		}
		if (player.ContainFlag(LegacyNativeMsgUpdate.Flags.Oblivion))
		{
			damage = LegacyNativeAttackBase.MulDiv(damage, 200, 100);
		}
		damage = LegacyNativeAttackBase.AdjustMinDamageUser2Monster(damage, player.Owner);
		damage = LegacyNativeAttackBase.CalcDamageUser2Monster(damage, monster.Family.Defense, player.Level, monster.Level, Range: false);
		damage = (int)LegacyNativeAttackBase.BigMulDiv(damage, monster.Family.Defense2, 10000L);
		if (monster.Family.Defense2 > 0)
		{
			SpellObj.Damage = (uint)Math.Max(1, damage);
		}
		if (LegacyNativeAttackBase.Rate(player.Owner.PerfectionStatus.LuckyStrike))
		{
			using (LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket())
			{
				LegacyNativePacket stream = recycledPacket.GetStream();
				player.View.SendView(stream.MsgRefineEffectCreate(new LegacyNativeMsgRefineEffect.RefineEffectProto
				{
					Effect = LegacyNativeMsgRefineEffect.RefineEffects.LuckyStrike,
					Id = player.UID
				}), me: true);
			}
			SpellObj.Effect |= LegacyNativeMsgAttackPacket.AttackEffect.LuckyStrike;
			SpellObj.Damage = (uint)LegacyNativeAttackBase.MulDiv((int)SpellObj.Damage, 200, 100);
		}
		uint num = 0u;
		uint num2 = 0u;
		if (DBSpell != null && (DBSpell.ID == 16420 || DBSpell.ID == 16430) && player.Owner.MyNinja!.TryGetValueEquip(LegacyNativeNinja.ItemType.WhirlSigilSage, out var obj))
		{
			num = (uint)obj.DBItem.Power;
			num2 = obj.DBItem.Damage;
		}
		if (LegacyNativeAttackBase.GetRefinery())
		{
			if (player.Owner.Status.CriticalStrike != 0)
			{
				SpellObj.Effect |= LegacyNativeMsgAttackPacket.AttackEffect.CriticalStrike;
				SpellObj.Damage += SpellObj.Damage * (player.Owner.AjustCriticalStrike() / 100) / 150;
				SpellObj.Damage += SpellObj.Damage * num2 / 100;
			}
		}
		else if (LegacyNativeAttackBase.Rate((int)(player.Owner.Status.DashRate / 100)))
		{
			SpellObj.Effect |= LegacyNativeMsgAttackPacket.AttackEffect.Dash;
			SpellObj.Damage += SpellObj.Damage * (player.Owner.AjustCriticalStrike() / 100) / 100;
			SpellObj.Damage += SpellObj.Damage * num2 / 100;
		}
		if ((monster.Family.Settings & LegacyNativeMonsterSettings.Guard) == LegacyNativeMonsterSettings.Guard)
		{
			SpellObj.Damage /= 10u;
		}
		if (monster.Family.ID == 20211)
		{
			SpellObj.Damage = 1u;
		}
		if (player.ContainFlag(LegacyNativeMsgUpdate.Flags.DeathSigh))
		{
			if (DBSpell == null || DBSpell.Passive)
			{
				SpellObj.Damage = PhysicalMonsterScale(SpellObj.Damage, player.DeathSighPassive);
			}
			else
			{
				SpellObj.Damage += PhysicalMonsterScale(SpellObj.Damage, player.DeathSighActive);
			}
		}
		player.Owner.CombatGear!.AttackEffect(ref SpellObj, DBSpell, monster, ref SpellObj.Damage);
		if (player.ContainFlag(LegacyNativeMsgUpdate.Flags.Infinity))
		{
			SpellObj.Damage = PhysicalMonsterScale(SpellObj.Damage, (DBSpell == null) ? player.InfinityDamage : player.InfinitySDamage);
		}
		if (monster.Name == "AncientDragon")
		{
			SpellObj.Damage = 1u;
		}
		else if (monster.Name == "WarDevil")
		{
			SpellObj.Damage = 1u;
		}
		else if (monster.Name!.Contains("Sprite"))
		{
			SpellObj.Damage = 1u;
		}
		if (DBSpell != null && (DBSpell.ID == 16540 || DBSpell.ID == 16550) && monster.HitPoints - SpellObj.Damage <= 1)
		{
			SpellObj.Damage = 0u;
		}
		if (DBSpell != null && (DBSpell.ID == 16330 || DBSpell.ID == 16340))
		{
			if (player.KylinSigil == 3)
			{
				player.KylinSigil = 0u;
			}
			player.KylinSigil++;
			if (player.KylinSigil == 1)
			{
				SpellObj.Damage += SpellObj.Damage * 50 / 100;
			}
			if (player.KylinSigil == 2)
			{
				SpellObj.Damage += SpellObj.Damage * 60 / 100;
			}
			if (player.KylinSigil == 3)
			{
				SpellObj.Damage += SpellObj.Damage * 75 / 100;
			}
		}
		byte itemLevel = 0;
		if (player.Owner.Rune!.IsEquipped("Assassin", ref itemLevel) && player.Owner.Player.OnAutoHunt)
		{
			SpellObj.Damage += 38000u;
		}
	}
}
