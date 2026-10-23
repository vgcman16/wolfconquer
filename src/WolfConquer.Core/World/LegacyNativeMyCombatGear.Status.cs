using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMyCombatGear
{
    // Whole native MyCombatGear.Status542..732. No optional branch removed.
	public void Status()
	{
		var selected = PrepareSelectedStatusFlagValues();
		using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
		LegacyNativePacket stream = recycledPacket.GetStream();
		if (Check(user.Player.Class))
		{
			VRANK valueRank = LegacyNativeCombatGear.GetValueRank(user.Player.UID, user.Player.Class);
			A(LegacyNativeDaoqiDictTypeTable.AttrType.LuckyStrike, valueRank.CoreStrike);
			A(LegacyNativeDaoqiDictTypeTable.AttrType.Parry, valueRank.Parry);
			A(LegacyNativeDaoqiDictTypeTable.AttrType.CoreStrike, valueRank.CoreStrike);
			A(LegacyNativeDaoqiDictTypeTable.AttrType.PAttack, valueRank.PAttack);
			A(LegacyNativeDaoqiDictTypeTable.AttrType.PDefense, valueRank.PDefense);
			A(LegacyNativeDaoqiDictTypeTable.AttrType.MDefense, valueRank.MDefense);
			if (LegacyNativeAtributesStatus.IsPirate(user.Player.Class))
			{
				if (valueRank.Magic != 0)
				{
					user.MySpells.Add(stream, valueRank.Magic, valueRank.MagicLevel, 0, 0);
				}
				else if (user.MySpells.ClientSpells.ContainsKey(21930))
				{
					user.MySpells.Remove(21930, stream);
				}
			}
			else if (user.MySpells.ClientSpells.ContainsKey(21930))
			{
				user.MySpells.Remove(21930, stream);
			}
			if (LegacyNativeAtributesStatus.IsPirate(user.Player.Class))
			{
				foreach (LegacyNativeDictCollectionTable.CQ item in GetNutBook())
				{
					A(item.attr_type1, item.attr_value1);
				}
			}
			Weapon? weapon = isOpen();
			if (weapon != null)
			{
				user.Status.MaxHitpoints += weapon.DBItem.MaxHP;
				user.Status.MaxAttack += weapon.DBItem.PAttack;
				user.Status.MinAttack += weapon.DBItem.PAttack;
				user.Status.MagicDefence += weapon.DBItem.MDefense;
				user.Status.Defence += weapon.DBItem.PDefense;
				BlackJade[] blackJades = weapon.BlackJades;
				foreach (BlackJade blackJade in blackJades)
				{
					A((LegacyNativeDaoqiDictTypeTable.AttrType)blackJade.DBItem.attr_type1, blackJade.DBItem.attr_value1);
					A((LegacyNativeDaoqiDictTypeTable.AttrType)blackJade.DBItem.attr_type2, blackJade.DBItem.attr_value2);
				}
			}
			Weapon[] items = GetItems(user.Player.Class);
			foreach (Weapon weapon2 in items)
			{
				if (weapon2 != null && weapon != null && weapon.ItemID != weapon2.ItemID)
				{
					user.Status.MaxHitpoints += weapon2.DBItem.MaxHP * 5 / 100;
					user.Status.MaxAttack += weapon2.DBItem.PAttack * 5 / 100;
					user.Status.MinAttack += weapon2.DBItem.PAttack * 5 / 100;
					user.Status.MagicDefence += weapon2.DBItem.MDefense * 5 / 100;
					user.Status.Defence += weapon2.DBItem.PDefense * 5 / 100;
				}
			}
		}
		LegacyNativeMsgSpell? value = null;
		if (user.MySpells.ClientSpells.TryGetValue(18160, out value))
		{
			LegacyNativeMagicType.Magic magic = LegacyNativePoolMagic.Magic[value.ID][value.Level];
			if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.PrisonSigilFutility | LegacyNativeMsgUpdate.Flags.Cursed))
			{
				user.Status.MaxAttack += (uint)magic.GDamage;
				user.Status.MinAttack += (uint)magic.GDamage;
			}
			if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.WhirlSigilAura | LegacyNativeMsgUpdate.Flags.Cursed))
			{
				user.Status.Defence += (uint)magic.DamageOnHuman;
				user.Status.MagicDefence += (uint)magic.Damage2;
			}
			if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.Top8Weekly | LegacyNativeMsgUpdate.Flags.HawksAmbition))
			{
				user.Status.Breakthrough += (uint)magic.Damage3;
				user.Status.Counteraction += (uint)magic.Damage3;
			}
		}
		if (selected.HolyProtection.Active)
		{
			user.Status.ItemBless += NativeCombatStatusUInt32(selected.HolyProtection.First);
			user.Status.ItemBless = Math.Min(user.Status.ItemBless, 60u);
		}
		if (selected.PhoenixBlessing.Active)
		{
			user.Status.ItemBless += NativeCombatStatusUInt32(selected.PhoenixBlessing.First);
			user.Status.ItemBless = Math.Min(user.Status.ItemBless, 60u);
		}
		if (selected.NobleSpirit.Active)
		{
			user.Status.MagicDefence += NativeCombatStatusUInt32(selected.NobleSpirit.First) / 100;
			user.Status.Penetration += NativeCombatStatusUInt32(selected.NobleSpirit.First % 100.0) * 100;
		}
		if (selected.FlameShield.Active)
		{
			user.Status.PhysicalDamageDecrease += NativeCombatStatusUInt32(selected.FlameShield.First);
			user.Status.MagicDamageDecrease += NativeCombatStatusUInt32(selected.FlameShield.Second);
		}
		if (selected.FlowKnack.Active)
		{
			user.Status.PhysicalDamageDecrease += NativeCombatStatusUInt32(selected.FlowKnack.First);
			user.Status.MagicDamageDecrease += NativeCombatStatusUInt32(selected.FlowKnack.Second);
		}
		if (selected.DivineEmptiness.Active)
		{
			user.Status.DodgeRate += NativeCombatStatusUInt32((double)user.Status.Dodge * (selected.DivineEmptiness.First / 100.0 / 100.0));
		}
		if (selected.WackeSpirit.Active)
		{
			user.Status.DodgeRate += NativeCombatStatusUInt32(selected.WackeSpirit.First);
		}
		if (selected.CrackMantra1.Active)
		{
			if ((double)user.Status.Defence >= selected.CrackMantra1.First)
			{
				user.Status.Defence -= NativeCombatStatusUInt32(selected.CrackMantra1.First);
			}
			else
			{
				user.Status.Defence = 0u;
			}
		}
		if (selected.CrackMantra2.Active)
		{
			if ((double)user.Status.MagicDefence >= selected.CrackMantra2.First)
			{
				user.Status.MagicDefence -= NativeCombatStatusUInt32(selected.CrackMantra2.First);
			}
			else
			{
				user.Status.MagicDefence = 0u;
			}
		}
		if (selected.Sense.Active)
		{
			user.Status.DodgeRate += NativeCombatStatusUInt32(selected.Sense.Second);
		}
		if (selected.Diabolize.Active)
		{
			user.Status.DodgeRate += NativeCombatStatusUInt32(selected.Diabolize.First);
		}
		if (selected.Barrier.Active)
		{
			A(LegacyNativeDaoqiDictTypeTable.AttrType.AntiBreak, NativeCombatStatusUInt32(selected.Barrier.First) * 10);
		}
		if (selected.Shell.Active)
		{
			A(LegacyNativeDaoqiDictTypeTable.AttrType.Immunity, NativeCombatStatusUInt32(selected.Shell.First));
		}
		if (selected.Storm.Active)
		{
			A(LegacyNativeDaoqiDictTypeTable.AttrType.PStrike, NativeCombatStatusUInt32(selected.Storm.First));
		}
		if (selected.Thrash.Active)
		{
			A(LegacyNativeDaoqiDictTypeTable.AttrType.FinalPDamage, NativeCombatStatusUInt32(selected.Thrash.First));
		}
		if (selected.Thunder.Active)
		{
			A(LegacyNativeDaoqiDictTypeTable.AttrType.FinalPDamage, NativeCombatStatusUInt32(selected.Thunder.First));
		}
		if (selected.Torrent.Active)
		{
			A(LegacyNativeDaoqiDictTypeTable.AttrType.PAttack, NativeCombatStatusUInt32(selected.Torrent.First));
		}
		if (selected.Tide.Active)
		{
			A(LegacyNativeDaoqiDictTypeTable.AttrType.PDefense, NativeCombatStatusUInt32(selected.Tide.First));
		}
		if (selected.Splash.Active)
		{
			A(LegacyNativeDaoqiDictTypeTable.AttrType.MaxHP, NativeCombatStatusUInt32(selected.Splash.First));
		}
		if (selected.Sailing.Active)
		{
			user.Status.Block += NativeCombatStatusUInt32(selected.Sailing.First) * 100;
		}
		if (selected.Vast.Active)
		{
			A(LegacyNativeDaoqiDictTypeTable.AttrType.Break, NativeCombatStatusUInt32(selected.Vast.First) * 10);
		}
		if (selected.ColdBloodline.Active)
		{
			A(LegacyNativeDaoqiDictTypeTable.AttrType.PDefense, NativeCombatStatusUInt32(selected.ColdBloodline.First));
		}
	}
}
