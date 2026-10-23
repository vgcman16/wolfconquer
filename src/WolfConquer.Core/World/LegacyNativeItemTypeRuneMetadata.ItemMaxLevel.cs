namespace WolfConquer.Core.World;

// Whole native ItemType1755..1776, no modern item metadata adapter.
internal sealed partial class LegacyNativeItemTypeRuneMetadata
{
	public static byte ItemMaxLevel(LegacyNativeGameItemFlags.ConquerItem Position)
	{
		return Position switch
		{
			LegacyNativeGameItemFlags.ConquerItem.Inventory => 0,
			LegacyNativeGameItemFlags.ConquerItem.Head => 140,
			LegacyNativeGameItemFlags.ConquerItem.Necklace => 139,
			LegacyNativeGameItemFlags.ConquerItem.Armor => 140,
			LegacyNativeGameItemFlags.ConquerItem.LeftWeapon => 140,
			LegacyNativeGameItemFlags.ConquerItem.RightWeapon => 140,
			LegacyNativeGameItemFlags.ConquerItem.Boots => 129,
			LegacyNativeGameItemFlags.ConquerItem.Ring => 136,
			LegacyNativeGameItemFlags.ConquerItem.Tower => 100,
			LegacyNativeGameItemFlags.ConquerItem.Fan => 100,
			LegacyNativeGameItemFlags.ConquerItem.Steed => 0,
			LegacyNativeGameItemFlags.ConquerItem.SteedMount => 30,
			LegacyNativeGameItemFlags.ConquerItem.Wing => 100,
			_ => 0,
		};
	}

}
