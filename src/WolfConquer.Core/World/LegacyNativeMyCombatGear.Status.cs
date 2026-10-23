using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMyCombatGear
{
    // Whole native MyCombatGear.Status542..732. No optional branch removed.
	public void Status()
	{
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
		if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.CannonBarrage | LegacyNativeMsgUpdate.Flags.HawksAmbition))
		{
			user.Status.ItemBless += NativeCombatStatusUInt32(GetValue("HolyProtection")[0]);
			user.Status.ItemBless = Math.Min(user.Status.ItemBless, 60u);
		}
		if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.Top8Weekly | LegacyNativeMsgUpdate.Flags.Top4Weekly | LegacyNativeMsgUpdate.Flags.HawksAmbition))
		{
			user.Status.ItemBless += NativeCombatStatusUInt32(GetValue("PhoenixBlessing")[0]);
			user.Status.ItemBless = Math.Min(user.Status.ItemBless, 60u);
		}
		if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.DragonPierceTortoise | LegacyNativeMsgUpdate.Flags.Top4Weekly))
		{
			user.Status.MagicDefence += NativeCombatStatusUInt32(GetValue("NobleSpirit")[0]) / 100;
			user.Status.Penetration += NativeCombatStatusUInt32(GetValue("NobleSpirit")[0] % 100.0) * 100;
		}
		if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.Confused | LegacyNativeMsgUpdate.Flags.Top4Weekly | LegacyNativeMsgUpdate.Flags.HawksAmbition))
		{
			user.Status.PhysicalDamageDecrease += NativeCombatStatusUInt32(GetValue("FlameShield")[0]);
			user.Status.MagicDamageDecrease += NativeCombatStatusUInt32(GetValue("FlameShield")[1]);
		}
		if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.TopWaterTaoist | LegacyNativeMsgUpdate.Flags.Top4Weekly | LegacyNativeMsgUpdate.Flags.HawksAmbition))
		{
			user.Status.PhysicalDamageDecrease += NativeCombatStatusUInt32(GetValue("FlowKnack")[0]);
			user.Status.MagicDamageDecrease += NativeCombatStatusUInt32(GetValue("FlowKnack")[1]);
		}
		if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.AzureShield | LegacyNativeMsgUpdate.Flags.PaperDance))
		{
			user.Status.DodgeRate += NativeCombatStatusUInt32((double)user.Status.Dodge * (GetValue("DivineEmptiness")[0] / 100.0 / 100.0));
		}
		if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.CTF_Flag | LegacyNativeMsgUpdate.Flags.HawksAmbition))
		{
			user.Status.DodgeRate += NativeCombatStatusUInt32(GetValue("WackeSpirit")[0]);
		}
		if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.TopPirate | LegacyNativeMsgUpdate.Flags.HawksAmbition))
		{
			if ((double)user.Status.Defence >= GetValue("CrackMantra1")[0])
			{
				user.Status.Defence -= NativeCombatStatusUInt32(GetValue("CrackMantra1")[0]);
			}
			else
			{
				user.Status.Defence = 0u;
			}
		}
		if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.Freeze | LegacyNativeMsgUpdate.Flags.Top4Weekly | LegacyNativeMsgUpdate.Flags.HawksAmbition))
		{
			if ((double)user.Status.MagicDefence >= GetValue("CrackMantra2")[0])
			{
				user.Status.MagicDefence -= NativeCombatStatusUInt32(GetValue("CrackMantra2")[0]);
			}
			else
			{
				user.Status.MagicDefence = 0u;
			}
		}
		if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.WildSigilBurning | LegacyNativeMsgUpdate.Flags.MagicDefender))
		{
			user.Status.DodgeRate += NativeCombatStatusUInt32(GetValue("Sense")[1]);
		}
		if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.Ghost | LegacyNativeMsgUpdate.Flags.MagicDefender | LegacyNativeMsgUpdate.Flags.HawksAmbition))
		{
			user.Status.DodgeRate += NativeCombatStatusUInt32(GetValue("Diabolize")[0]);
		}
		if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.Water | LegacyNativeMsgUpdate.Flags.MagicDefender))
		{
			A(LegacyNativeDaoqiDictTypeTable.AttrType.AntiBreak, NativeCombatStatusUInt32(GetValue("Barrier")[0]) * 10);
		}
		if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.TopSuperGuildWarFiveStars | LegacyNativeMsgUpdate.Flags.HawksAmbition))
		{
			A(LegacyNativeDaoqiDictTypeTable.AttrType.Immunity, NativeCombatStatusUInt32(GetValue("Shell")[0]));
		}
		if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.BladeFlurry | LegacyNativeMsgUpdate.Flags.HawksAmbition))
		{
			A(LegacyNativeDaoqiDictTypeTable.AttrType.PStrike, NativeCombatStatusUInt32(GetValue("Storm")[0]));
		}
		if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.KineticSpark | LegacyNativeMsgUpdate.Flags.HawksAmbition))
		{
			A(LegacyNativeDaoqiDictTypeTable.AttrType.FinalPDamage, NativeCombatStatusUInt32(GetValue("Thrash")[0]));
		}
		if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.PathOfShadow | LegacyNativeMsgUpdate.Flags.HawksAmbition))
		{
			A(LegacyNativeDaoqiDictTypeTable.AttrType.FinalPDamage, NativeCombatStatusUInt32(GetValue("Thunder")[0]));
		}
		if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.Lightning | LegacyNativeMsgUpdate.Flags.MagicDefender))
		{
			A(LegacyNativeDaoqiDictTypeTable.AttrType.PAttack, NativeCombatStatusUInt32(GetValue("Torrent")[0]));
		}
		if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.Wind | LegacyNativeMsgUpdate.Flags.MagicDefender))
		{
			A(LegacyNativeDaoqiDictTypeTable.AttrType.PDefense, NativeCombatStatusUInt32(GetValue("Tide")[0]));
		}
		if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.SuperCyclone | LegacyNativeMsgUpdate.Flags.HawksAmbition))
		{
			A(LegacyNativeDaoqiDictTypeTable.AttrType.MaxHP, NativeCombatStatusUInt32(GetValue("Splash")[0]));
		}
		if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.LotusDemon | LegacyNativeMsgUpdate.Flags.MagicDefender))
		{
			user.Status.Block += NativeCombatStatusUInt32(GetValue("Sailing")[0]) * 100;
		}
		if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.Earth | LegacyNativeMsgUpdate.Flags.MagicDefender))
		{
			A(LegacyNativeDaoqiDictTypeTable.AttrType.Break, NativeCombatStatusUInt32(GetValue("Vast")[0]) * 10);
		}
		if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.TopDragonLee | LegacyNativeMsgUpdate.Flags.HawksAmbition))
		{
			A(LegacyNativeDaoqiDictTypeTable.AttrType.PDefense, NativeCombatStatusUInt32(GetValue("ColdBloodline")[0]));
		}
	}
}
