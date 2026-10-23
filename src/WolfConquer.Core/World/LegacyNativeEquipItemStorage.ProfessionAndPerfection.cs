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
	public void AddProfessionAtributes()
	{
		if (!LegacyNativeProfessionTable.Benefits.ContainsKey(Owner.Player.Class))
		{
			return;
		}
		LegacyNativeProfessionTable.Benefit benefit = LegacyNativeProfessionTable.Benefits[Owner.Player.Class];
		foreach (LegacyNativeProfessionTable.AttributeValue item in benefit.AttributeValue.Where((LegacyNativeProfessionTable.AttributeValue i) => i.Type != LegacyNativeChiAttribute.None))
		{
			switch (item.Type)
			{
			case LegacyNativeChiAttribute.HPAdd:
				Owner.Status.MaxHitpoints += item.Value;
				break;
			case LegacyNativeChiAttribute.AddAttack:
				Owner.Status.MinAttack += item.Value;
				Owner.Status.MaxAttack += item.Value;
				break;
			case LegacyNativeChiAttribute.AddMagicAttack:
				Owner.Status.MagicAttack += item.Value;
				break;
			case LegacyNativeChiAttribute.Breakthrough:
				Owner.Status.Breakthrough += item.Value / 10;
				break;
			case LegacyNativeChiAttribute.Counteraction:
				Owner.Status.Counteraction += item.Value / 10;
				break;
			case LegacyNativeChiAttribute.CriticalStrike:
				Owner.Status.CriticalStrike += item.Value;
				break;
			case LegacyNativeChiAttribute.Immunity:
				Owner.Status.Immunity += item.Value;
				break;
			case LegacyNativeChiAttribute.SkillCriticalStrike:
				Owner.Status.SkillCStrike += item.Value;
				break;
			}
		}
	}

	public void AddPerfectionAtributes()
	{
		if (LegacyNativeItemRefineUpgrade.Effects.TryGetValue((ushort)Owner.PrestigeLevel, out var value))
		{
			Owner.Status.MaxAttack += value.PAttack;
			Owner.Status.MinAttack += value.PAttack;
			Owner.Status.Defence += (ushort)value.PDefense;
			Owner.Status.MagicAttack += value.MAttack;
			Owner.Status.MagicDefence += (ushort)value.MDefense;
		}
	}
}
