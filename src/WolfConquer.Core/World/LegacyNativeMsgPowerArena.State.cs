using ProcesType = WolfConquer.Core.World.LegacyNativeProcesType;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMsgPowerArena
{
    public int StartMinutes = 5;
    public DateTime StartTimer = default(DateTime);
    public DateTime PrepareStart = default(DateTime);
    public ProcesType Process { get; set; }

	public LegacyNativeMsgPowerArena()
	{
		Process = ProcesType.Dead;
	}
}
