using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using Flags = WolfConquer.Core.World.LegacyNativeGameItemFlags;

namespace WolfConquer.Core.World;

[StructLayout(LayoutKind.Explicit, Size = 322)]
internal partial struct LegacyNativeMsgDetainedItem
{
	public enum ContainerType : uint
	{
		DetainPage = 13369344u,
		ClaimPage,
		RewardCps
	}

	[FieldOffset(0)]
	public uint UID;

	[FieldOffset(4)]
	public uint ItemUID;

	[FieldOffset(8)]
	public uint ItemID;

	[FieldOffset(12)]
	public ushort Durability;

	[FieldOffset(14)]
	public ushort MaximDurability;

	[FieldOffset(16)]
	public ContainerType Action;

	[FieldOffset(20)]
	public uint SocketProgress;

	[FieldOffset(24)]
	public Flags.Gem SocketOne;

	[FieldOffset(25)]
	public Flags.Gem SocketTwo;

	[FieldOffset(26)]
	public Flags.ItemEffect Effect;

	[FieldOffset(30)]
	public byte Plus;

	[FieldOffset(31)]
	public byte Bless;

	[FieldOffset(32)]
	public bool Bound;

	[FieldOffset(33)]
	public byte Enchant;

	[FieldOffset(34)]
	public uint PlusProgres;

	[FieldOffset(38)]
	public ushort Suspicious;

	[FieldOffset(40)]
	public ushort Lock;

	[FieldOffset(42)]
	public uint OwnerUID;

	[FieldOffset(46)]
	private unsafe fixed sbyte szOwnerName[32];

	[FieldOffset(78)]
	public uint GainerUID;

	[FieldOffset(82)]
	private unsafe fixed sbyte szGainerName[32];

	[FieldOffset(114)]
	public int Date;

	[FieldOffset(118)]
	public int RewardConquerPoints;

	[FieldOffset(122)]
	public int ConquerPointsCost;

	[FieldOffset(126)]
	public uint DaysLeft;

	[FieldOffset(130)]
	public uint PerfectionLevel;

	[FieldOffset(134)]
	public uint PerfectionProgres;

	[FieldOffset(138)]
	private unsafe fixed sbyte _OwnerName[32];

	[FieldOffset(170)]
	private unsafe fixed sbyte _Signature[36];

	[FieldOffset(206)]
	public LegacyNativeItemExtra.Purification Purification;

	[FieldOffset(246)]
	public LegacyNativeItemExtra.Refinery Refinary;

	[FieldOffset(294)]
	public uint Attribute1;

	[FieldOffset(298)]
	public uint Attribute2;

	[FieldOffset(302)]
	public uint Attribute3;

	[FieldOffset(306)]
	public uint Attribute4;

	[FieldOffset(310)]
	public uint Attribute5;

	[FieldOffset(314)]
	public uint RuneEXP;

	[FieldOffset(318)]
	public uint AnimaItemID;

}
