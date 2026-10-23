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
	public void IncreaseRifainaryStatus(uint ID)
	{
		if (LegacyNativePoolItems.RifineryItems.TryGetValue(ID, out var value))
		{
			switch (value.Type)
			{
			case LegacyNativeRifinery.RefineryType.CriticalStrike:
				Owner.Status.CriticalStrike += value.Procent * 100;
				break;
			case LegacyNativeRifinery.RefineryType.SkillCriticalStrike:
				Owner.Status.SkillCStrike += value.Procent * 100;
				break;
			case LegacyNativeRifinery.RefineryType.Break:
				Owner.Status.Breakthrough += value.Procent * 10;
				break;
			case LegacyNativeRifinery.RefineryType.Detoxication:
				Owner.Status.Detoxication += value.Procent;
				break;
			case LegacyNativeRifinery.RefineryType.MDefence:
				Owner.Status.PhysicalDamageDecrease += value.Procent;
				break;
			case LegacyNativeRifinery.RefineryType.Block:
				Owner.Status.Block += value.Procent * 100;
				break;
			case LegacyNativeRifinery.RefineryType.Immunity:
				Owner.Status.Immunity += value.Procent * 100;
				break;
			case LegacyNativeRifinery.RefineryType.Penetration:
				Owner.Status.Penetration += value.Procent * 100;
				break;
			case LegacyNativeRifinery.RefineryType.Intensification:
				Owner.Status.MaxHitpoints += value.Procent;
				break;
			case LegacyNativeRifinery.RefineryType.Counteraction:
				Owner.Status.Counteraction += value.Procent * 10;
				break;
			case LegacyNativeRifinery.RefineryType.FinalMDamage:
				Owner.Status.PhysicalDamageIncrease += value.Procent;
				break;
			case LegacyNativeRifinery.RefineryType.FinalMAttack:
				Owner.Status.MagicDamageIncrease += value.Procent;
				break;
			}
			switch (value.Type2)
			{
			case LegacyNativeRifinery.RefineryType.CriticalStrike:
				Owner.Status.CriticalStrike += value.Procent2 * 100;
				break;
			case LegacyNativeRifinery.RefineryType.SkillCriticalStrike:
				Owner.Status.SkillCStrike += value.Procent2 * 100;
				break;
			case LegacyNativeRifinery.RefineryType.Break:
				Owner.Status.Breakthrough += value.Procent2 * 10;
				break;
			case LegacyNativeRifinery.RefineryType.Detoxication:
				Owner.Status.Detoxication += value.Procent2;
				break;
			case LegacyNativeRifinery.RefineryType.MDefence:
				Owner.Status.PhysicalDamageDecrease += value.Procent2;
				break;
			case LegacyNativeRifinery.RefineryType.Block:
				Owner.Status.Block += value.Procent2;
				break;
			case LegacyNativeRifinery.RefineryType.Immunity:
				Owner.Status.Immunity += value.Procent2 * 100;
				break;
			case LegacyNativeRifinery.RefineryType.Penetration:
				Owner.Status.Penetration += value.Procent2 * 100;
				break;
			case LegacyNativeRifinery.RefineryType.Intensification:
				Owner.Status.MaxHitpoints += value.Procent2;
				break;
			case LegacyNativeRifinery.RefineryType.Counteraction:
				Owner.Status.Counteraction += value.Procent2;
				break;
			case LegacyNativeRifinery.RefineryType.FinalMDamage:
				Owner.Status.PhysicalDamageIncrease += value.Procent2;
				break;
			case LegacyNativeRifinery.RefineryType.FinalMAttack:
				Owner.Status.MagicDamageIncrease += value.Procent2;
				break;
			}
		}
		else
		{
			Console.WriteLine("error refinery id " + ID);
		}
	}
}
