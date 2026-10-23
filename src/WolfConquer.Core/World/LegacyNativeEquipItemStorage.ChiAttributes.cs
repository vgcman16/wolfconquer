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
	public void AddChiAtribute(LegacyNativeChiRuntime chi, uint percent = 100u)
	{
		Owner.Status.CriticalStrike += chi.CriticalStrike * percent / 100;
		Owner.Status.SkillCStrike += chi.SkillCriticalStrike * percent / 100;
		Owner.Status.Immunity += chi.Immunity * percent / 100;
		Owner.Status.Counteraction += chi.Counteraction * percent / 100;
		Owner.Status.Breakthrough += chi.Breakthrough * percent / 100;
		Owner.Status.MaxHitpoints += chi.MaxLife * percent / 100;
		Owner.Status.MaxAttack += chi.AddAttack * percent / 100;
		Owner.Status.MinAttack += chi.AddAttack * percent / 100;
		Owner.Status.MagicAttack += chi.AddMagicAttack * percent / 100;
		Owner.Status.MagicDefence += chi.AddMagicDefense * percent / 100;
		Owner.Status.PhysicalDamageIncrease += chi.FinalAttack * percent / 100;
		Owner.Status.PhysicalDamageDecrease += chi.FinalDefense * percent / 100;
		Owner.Status.MagicDamageIncrease += chi.FinalMagicAttack * percent / 100;
		Owner.Status.MagicDamageDecrease += chi.FinalMagicDefense * percent / 100;
	}
}
