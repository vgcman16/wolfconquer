using System.Runtime.InteropServices;

namespace WolfConquer.Core.World;

// Complete native three enums/struct metadata. Economic Handler is not installed.
[StructLayout(LayoutKind.Sequential, Size = 1)]
internal partial struct LegacyNativeMsgFlower
{
	public enum FlowerAction
	{
		None,
		GirlSend,
		FlowerSender,
		Flower
	}

	public enum FlowersType : uint
	{
		Rouse,
		Lilies,
		Orchids,
		Tulips,
		Kiss,
		love,
		Tins,
		Jade
	}

	public enum FlowerEffect : uint
	{
		None = 0u,
		Rouse = 1u,
		Lilies = 2u,
		Orchids = 3u,
		Tulips = 4u,
		Kiss = 1u,
		love = 2u,
		Tins = 3u,
		Jade = 4u
	}

}
