using GameMap = WolfConquer.Core.World.LegacyNativeGameMap;
using ProcesType = WolfConquer.Core.World.LegacyNativeProcesType;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMsgDisCity
{
    private ProcesType Mode;
    private DateTime FinishTime = default(DateTime);
    private DateTime TeleportToMap4 = default(DateTime);
    private int PlayersMap2 = 0;
    private int PlayersMap3 = 0;
    public List<uint> RewardPlayers = new List<uint>();
    private GameMap? Map1;
    private GameMap? Map2;
    private GameMap? Map3;
    private GameMap? Map4;

	public LegacyNativeMsgDisCity()
	{
		Mode = ProcesType.Dead;
	}
}
