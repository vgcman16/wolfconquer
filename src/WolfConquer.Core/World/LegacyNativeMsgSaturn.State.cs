using ProcesType = WolfConquer.Core.World.LegacyNativeProcesType;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMsgSaturn
{
    public ProcesType Mode;
    private DateTime FinishTime = default(DateTime);

	public LegacyNativeMsgSaturn()
	{
		Mode = ProcesType.Dead;
	}

	public bool IsInSaturn(uint Map)
	{
		return Map == 2353;
	}
}
