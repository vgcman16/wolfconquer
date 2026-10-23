using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Complete source family; full lifecycle/interface adoption is joint, never a no-op.
internal partial class LegacyNativeMsgLastManStand
{
	public DateTime StartTimer = default(DateTime);

	public DateTime InfoTimer = default(DateTime);

	public uint Secounds = 60u;

	public LegacyNativeGameMap? Map;

	public uint DinamicMap = 0u;

	public LegacyNativeKillerSystem? KillSystem;

	public LegacyNativeProcesType Process { get; set; }

	public LegacyNativeTournamentType Type { get; set; }

	public LegacyNativeMsgLastManStand(LegacyNativeTournamentType _type)
	{
		Type = _type;
		Process = LegacyNativeProcesType.Dead;
	}
}
