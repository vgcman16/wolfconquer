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
	public void AddGem(Flags.Gem gem)
	{
		switch (gem)
		{
		case Flags.Gem.SuperThunderGem:
		case Flags.Gem.SuperGloryGem:
			Owner.AddGem(gem, 500);
			break;
		case Flags.Gem.RefinedThunderGem:
		case Flags.Gem.RefinedGloryGem:
			Owner.AddGem(gem, 300);
			break;
		case Flags.Gem.NormalThunderGem:
		case Flags.Gem.NormalGloryGem:
			Owner.AddGem(gem, 100);
			break;
		case Flags.Gem.NormalDragonGem:
			Owner.AddGem(gem, 5);
			break;
		case Flags.Gem.RefinedPhoenixGem:
		case Flags.Gem.RefinedDragonGem:
			Owner.AddGem(gem, 10);
			break;
		case Flags.Gem.SuperPhoenixGem:
		case Flags.Gem.SuperDragonGem:
			Owner.AddGem(gem, 15);
			break;
		case Flags.Gem.NormalTortoiseGem:
			Owner.AddGem(gem, 2);
			break;
		case Flags.Gem.RefinedTortoiseGem:
			Owner.AddGem(gem, 4);
			break;
		case Flags.Gem.SuperTortoiseGem:
			Owner.AddGem(gem, 6);
			break;
		case Flags.Gem.SuperRainbowGem:
			Owner.AddGem(gem, 25);
			break;
		case Flags.Gem.RefinedRainbowGem:
			Owner.AddGem(gem, 15);
			break;
		case Flags.Gem.NormalRainbowGem:
			Owner.AddGem(gem, 10);
			break;
		}
	}
}
