using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeCalculatePhysical
{
    // Whole native Physical.OnMonster14..152. No floor/cold-path-only substitute.
public static void OnMonster(LegacyNativePlayerRuntime player, LegacyNativeMonsterRole monster, LegacyNativeMagicType.Magic? DBSpell, out LegacyNativeMsgSpellAnimation.SpellObj SpellObj, byte MultipleDamage = 0)
	{
		uint targetUid = monster.UID;
		if (monster.IsFloor)
		{
			SpellObj = new LegacyNativeMsgSpellAnimation.SpellObj(targetUid, 0u, LegacyNativeMsgAttackPacket.AttackEffect.None);
			SpellObj.Damage = 2u;
			return;
		}
		// Admission precedes the output assignment, RNG, local spell floor and proc sends.
		// It does not turn the later multi-owner calculation into an atomic transaction.
		var owners = RequireMonsterPhysicalStatus(player, monster, DBSpell);
		SpellObj = new LegacyNativeMsgSpellAnimation.SpellObj(targetUid, 0u, LegacyNativeMsgAttackPacket.AttackEffect.None);
		int damage = (int)LegacyNativeAttackBase.GetDamage(player.Owner.Status.MaxAttack, player.Owner.Status.MinAttack);
		damage = (int)player.Owner.AjustAttack((uint)damage, player);
		damage = (int)player.Owner.AjustMaxAttack((uint)damage);
		ushort defense = owners.Family.Defense;
		damage = Math.Max(0, damage - defense);
		if (player.ContainFlag(LegacyNativeMsgUpdate.Flags.Superman))
		{
			damage += damage * 2;
		}
		// Catalog rows are shared: the minimum is an effective value for this hit,
		// never a write to DBSpell. NaN/Infinity retain the existing comparison path.
		float effectiveSpellDamage = DBSpell?.Damage ?? 0f;
		if (DBSpell != null && effectiveSpellDamage < 10f)
		{
			effectiveSpellDamage = 10f;
		}
		damage = (player.ContainFlag(LegacyNativeMsgUpdate.Flags.FatalStrike) ? LegacyNativeAttackBase.MulDiv(damage, 500, 100) : ((DBSpell != null && DBSpell.ID == 12860) ? LegacyNativeAttackBase.MulDiv(damage, 350, 100) : ((MultipleDamage != 0) ? (damage * MultipleDamage) : ((DBSpell != null && DBSpell.ID == 12770) ? ((monster.Boss != 1) ? LegacyNativeAttackBase.MulDiv(damage, 500, 100) : LegacyNativeAttackBase.MulDiv(damage, 350, 100)) : ((DBSpell == null || !LegacyNativeCombatGear.CheckSpells(DBSpell.ID)) ? LegacyNativeAttackBase.MulDiv(damage, PhysicalMonsterFactor(DBSpell != null ? effectiveSpellDamage : (float)LegacyNativeServerConfig.PhysicalDamage), 100) : LegacyNativeCombatGear.GetDamage(damage, DBSpell.GDamage, DBSpell.DamageOnHuman, DBSpell.Damage2, DBSpell.Damage3, DBSpell.DamageOnMonster, Player: false))))));
		if (player.ContainFlag(LegacyNativeMsgUpdate.Flags.ManiacDance))
		{
			damage = ((monster.Boss != 0) ? (damage * 3) : (damage * 10));
		}
		if (player.ContainFlag(LegacyNativeMsgUpdate.Flags.Oblivion))
		{
			damage = LegacyNativeAttackBase.MulDiv(damage, 200, 100);
		}
		damage = Math.Max(owners.MinimumDamage, damage);
		damage = LegacyNativeAttackBase.CalcDamageUser2Monster(damage, owners.Family.Defense, player.Level, monster.Level, Range: false);
		damage = (int)LegacyNativeAttackBase.BigMulDiv(damage, owners.Family.Defense2, 10000L);
		if (owners.Family.Defense2 > 0)
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
		if (owners.Sigil is { Found: true } sigil)
		{
			num = (uint)sigil.Power;
			num2 = sigil.Damage;
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
		if ((owners.Family.Settings & LegacyNativeMonsterSettings.Guard) == LegacyNativeMonsterSettings.Guard)
		{
			SpellObj.Damage /= 10u;
		}
		if (owners.Family.ID == 20211)
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
		owners.CombatGear.AttackEffect(ref SpellObj, DBSpell, monster, ref SpellObj.Damage);
		if (player.ContainFlag(LegacyNativeMsgUpdate.Flags.Infinity))
		{
			SpellObj.Damage = PhysicalMonsterScale(SpellObj.Damage, (DBSpell == null) ? player.InfinityDamage : player.InfinitySDamage);
		}
		if (owners.Name == "AncientDragon")
		{
			SpellObj.Damage = 1u;
		}
		else if (owners.Name == "WarDevil")
		{
			SpellObj.Damage = 1u;
		}
		else if (owners.Name.Contains("Sprite"))
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
		if (owners.Rune.IsEquipped("Assassin", ref itemLevel) && player.Owner.Player.OnAutoHunt)
		{
			SpellObj.Damage += 38000u;
		}
	}
}
