#nullable disable
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeEquipItemStorage
{
	public void LoadMythsoul(LegacyNativeGameItemRuntime Item)
	{
		if (Item.MythSoul != 0)
		{
			byte b = (byte)(Item.MythSoul % 10);
			switch (Item.MythSoul % 1000 - b)
			{
			case 10u:
				Owner.Status.VenomLevel += b;
				break;
			case 40u:
				Owner.Status.BloodthirstLevel += b;
				break;
			case 80u:
				Owner.Status.EtherealLevel += b;
				break;
			case 170u:
				Owner.Status.ElanLevel += b;
				break;
			case 180u:
				Owner.Status.SolidLevel += b;
				break;
			}
		}
	}

	public void LoadGearMythsoulBag()
	{
		using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
		LegacyNativePacket stream = recycledPacket.GetStream();
		if (Owner.Status.VenomLevel != 0)
		{
			Owner.MySpells.Add(stream, 20000, Owner.Status.VenomLevel, 0, 0);
		}
		else
		{
			Owner.MySpells.Remove(20000, stream);
		}
		Owner.Status.DodgeRate += (uint)LegacyNativeSpecialEffectAttributeTable.GetEthereal(Owner.Status.EtherealLevel) * 100;
		foreach (LegacyNativeGameItemRuntime value2 in Owner.GearMythsoulBag.Values)
		{
			if (LegacyNativeSpecialEffectAttributeTable.SpEffectAttribute.TryGetValue(value2.ITEM_ID, out var value))
			{
				switch ((LegacyNativeDaoqiDictTypeTable.AttrType)value.Type)
				{
				case LegacyNativeDaoqiDictTypeTable.AttrType.MaxHP:
					Owner.Status.MaxHitpoints += value.value;
					break;
				case LegacyNativeDaoqiDictTypeTable.AttrType.PAttack:
					Owner.Status.MinAttack += value.value;
					Owner.Status.MaxAttack += value.value;
					break;
				case LegacyNativeDaoqiDictTypeTable.AttrType.PDefense:
					Owner.Status.Defence += value.value;
					break;
				case LegacyNativeDaoqiDictTypeTable.AttrType.MAttack:
					Owner.Status.MagicAttack += value.value;
					break;
				case LegacyNativeDaoqiDictTypeTable.AttrType.MDefense:
					Owner.Status.MagicDefence += value.value;
					break;
				case LegacyNativeDaoqiDictTypeTable.AttrType.FinalPAttack:
					Owner.Status.PhysicalDamageIncrease += value.value;
					break;
				case LegacyNativeDaoqiDictTypeTable.AttrType.FinalPDamage:
					Owner.Status.PhysicalDamageDecrease += value.value;
					break;
				case LegacyNativeDaoqiDictTypeTable.AttrType.FinalMAttack:
					Owner.Status.MagicDamageIncrease += value.value;
					break;
				case LegacyNativeDaoqiDictTypeTable.AttrType.FinalMDamage:
					Owner.Status.MagicDamageDecrease += value.value;
					break;
				case LegacyNativeDaoqiDictTypeTable.AttrType.PStrike:
					Owner.Status.CriticalStrike += value.value;
					break;
				case LegacyNativeDaoqiDictTypeTable.AttrType.MStrike:
					Owner.Status.SkillCStrike += value.value;
					break;
				case LegacyNativeDaoqiDictTypeTable.AttrType.Immunity:
					Owner.Status.Immunity += value.value;
					break;
				case LegacyNativeDaoqiDictTypeTable.AttrType.Break:
					Owner.Status.Breakthrough += value.value;
					break;
				case LegacyNativeDaoqiDictTypeTable.AttrType.AntiBreak:
					Owner.Status.Counteraction += value.value;
					break;
				}
			}
		}
	}
}
