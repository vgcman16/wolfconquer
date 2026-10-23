using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeStaticRoleRuntime
{
	public LegacyNativeMsgRacePotion.RaceItemType Type
	{
		get
		{
			switch (Mesh)
			{
			case 760u:
				return LegacyNativeMsgRacePotion.RaceItemType.GuardPotion;
			case 761u:
				return LegacyNativeMsgRacePotion.RaceItemType.FrozenTrap;
			case 762u:
				return LegacyNativeMsgRacePotion.RaceItemType.SluggishPotion;
			case 763u:
				return LegacyNativeMsgRacePotion.RaceItemType.DizzyHammer;
			case 764u:
				return LegacyNativeMsgRacePotion.RaceItemType.RestorePotion;
			case 765u:
				return LegacyNativeMsgRacePotion.RaceItemType.ScreamBomb;
			case 766u:
				return LegacyNativeMsgRacePotion.RaceItemType.TransformItem;
			case 768u:
				return LegacyNativeMsgRacePotion.RaceItemType.SpiritPotion;
			case 769u:
				return LegacyNativeMsgRacePotion.RaceItemType.ExcitementPotion;
			case 770u:
				return LegacyNativeMsgRacePotion.RaceItemType.ChaosBomb;
			default:
			{
				int num = 8338;
				while (num == 8338 || num == 8333)
				{
					num = LegacyNativePoolRandom.GetRandom.Next(8330, 8340);
				}
				return (LegacyNativeMsgRacePotion.RaceItemType)num;
			}
			}
		}
	}

	public void Pick()
	{
		Viable = true;
		Tuple<uint, string, ushort> tuple = raceItems[LegacyNativePoolRandom.GetRandom.Next(raceItems.Length)];
		Mesh = tuple.Item1;
		Name = tuple.Item2;
		Level = (byte)tuple.Item3;
	}

	public void DoFrozenTrap(uint setter)
	{
		SetBy = setter;
		Viable = true;
		Mesh = 761u;
		Name = "Frozen Trap";
		Level = 1;
	}
}
