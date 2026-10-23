using WolfConquer.Core.World;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativePrestigeRanking
{
    // Native PrestigeRanking12..73, state and all public methods live in these partials.
	public enum Type : uint
	{
		World,
		Trojan,
		Warrior,
		Archer,
		Ninja,
		Monk,
		Pirate,
		DragonWarrior,
		WaterTao,
		FireTao,
		WindWalker,
		Thunderstriker,
		Count
	}

	public partial class Entry
	{
		public uint UID;

		public uint[]? Points;

		public uint TotalPoints = 0u;

		public uint Class;

		public byte Level;

		public Type type;

		public uint Mesh;

		public string Name = "";

		public uint HairStyle;

		public uint Head;

		public uint Garment;

		public uint LeftWeapon;

		public uint LefttWeaponAccessory;

		public uint RightWeapon;

		public uint RightWeaponAccessory;

		public uint MountArmor;

		public uint Armor;

		public uint Wing;

		public uint WingPlus;

		public uint Title;

		public uint Flag;

		public string GuildName = "";
	}
}
