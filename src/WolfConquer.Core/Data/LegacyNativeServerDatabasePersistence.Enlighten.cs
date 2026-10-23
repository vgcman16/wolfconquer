using WolfConquer.Core.World;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.Data;

internal static partial class LegacyNativeServerDatabasePersistence
{
public static ushort CalculateEnlighten(LegacyNativePlayerRuntime player)
	{
		if (player.Level < 90)
		{
			return 0;
		}
		ushort num = 100;
		if (player.NobilityRank == LegacyNativeNobilityRank.Knight || player.NobilityRank == LegacyNativeNobilityRank.Baron)
		{
			num += 100;
		}
		if (player.NobilityRank == LegacyNativeNobilityRank.Earl || player.NobilityRank == LegacyNativeNobilityRank.Duke)
		{
			num += 200;
		}
		if (player.NobilityRank == LegacyNativeNobilityRank.Prince)
		{
			num += 300;
		}
		if (player.NobilityRank == LegacyNativeNobilityRank.King)
		{
			num += 400;
		}
		if (player.VipLevel <= 3)
		{
			num += 100;
		}
		if (player.VipLevel > 3 && player.VipLevel <= 5)
		{
			num += 200;
		}
		if (player.VipLevel > 5)
		{
			num += 300;
		}
		return num;
	}
}
