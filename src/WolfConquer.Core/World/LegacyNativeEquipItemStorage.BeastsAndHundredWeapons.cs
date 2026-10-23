#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using WolfConquer.Core.Data;
using Flags = WolfConquer.Core.World.LegacyNativeGameItemFlags;

namespace WolfConquer.Core.World;

// Complete genuine Equip methods; full QueryEquipment/UpdateStats adoption remains held.
internal sealed partial class LegacyNativeEquipItemStorage
{
	public void AddBeastsAtributes()
	{
		if (Owner.Beasts.Activated)
		{
			LegacyNativeBeastsAtrribute.Attribute attribute = LegacyNativeBeastsAtrribute.Attributes[Owner.Beasts.Level];
			Owner.Status.MaxHitpoints += attribute.HPAdd;
			Owner.Status.MinAttack += attribute.PAttack;
			Owner.Status.MaxAttack += attribute.PAttack;
			Owner.Status.MagicAttack += attribute.MAttack;
			Owner.Status.Defence += attribute.PDefense;
			Owner.Status.MagicDefence += attribute.MDefense;
			Owner.Status.MagicDamageIncrease += (ushort)attribute.FinalMDmgDealt;
			Owner.Status.PhysicalDamageIncrease += (ushort)attribute.FinalPDmgDealt;
			Owner.Status.PhysicalDamageDecrease += (ushort)attribute.FinalPDmgReceived;
			Owner.Status.MagicDamageDecrease += (ushort)attribute.FinalMDmgReceived;
		}
	}

	public void AddHWStats()
	{
		LegacyNativeHundredWeaponInfo hundredWeaponInfo = Owner.HundredWeapons.Objects.Values.FirstOrDefault((LegacyNativeHundredWeaponInfo i) => i.BitVector.Contain(1));
		if (hundredWeaponInfo != null)
		{
			Owner.Status.MaxHitpoints += (uint)(hundredWeaponInfo.Attributes[LegacyNativeHundredWeaponsTable.AttributeType.Hitpoints] - (int)((uint)(hundredWeaponInfo.Attributes[LegacyNativeHundredWeaponsTable.AttributeType.Hitpoints] * 2) / 100u));
			Owner.Status.MinAttack += (uint)(hundredWeaponInfo.Attributes[LegacyNativeHundredWeaponsTable.AttributeType.PhysicalAttack] - (int)((uint)(hundredWeaponInfo.Attributes[LegacyNativeHundredWeaponsTable.AttributeType.PhysicalAttack] * 2) / 100u));
			Owner.Status.MaxAttack += (uint)(hundredWeaponInfo.Attributes[LegacyNativeHundredWeaponsTable.AttributeType.PhysicalAttack] - (int)((uint)(hundredWeaponInfo.Attributes[LegacyNativeHundredWeaponsTable.AttributeType.PhysicalAttack] * 2) / 100u));
			Owner.Status.Defence += (uint)(hundredWeaponInfo.Attributes[LegacyNativeHundredWeaponsTable.AttributeType.PhysicalDefense] - (int)((uint)(hundredWeaponInfo.Attributes[LegacyNativeHundredWeaponsTable.AttributeType.PhysicalDefense] * 2) / 100u));
			Owner.Status.MagicAttack += (uint)(hundredWeaponInfo.Attributes[LegacyNativeHundredWeaponsTable.AttributeType.MagicAttack] - (int)((uint)(hundredWeaponInfo.Attributes[LegacyNativeHundredWeaponsTable.AttributeType.MagicAttack] * 2) / 100u));
			Owner.Status.MDefence += (uint)(hundredWeaponInfo.Attributes[LegacyNativeHundredWeaponsTable.AttributeType.MagicDefense] - (int)((uint)(hundredWeaponInfo.Attributes[LegacyNativeHundredWeaponsTable.AttributeType.MagicDefense] * 2) / 100u));
		}
	}
}
