using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Complete source family; full lifecycle/interface adoption is joint, never a no-op.
internal partial class LegacyNativeMsgExtremePk
{
	public DateTime StartTimer = default(DateTime);

	public DateTime ScoreStamp = default(DateTime);

	public DateTime InfoTimer = default(DateTime);

	public LegacyNativeGameMap? Map;

	public uint DinamicID;

	public uint Secounds = 0u;

	public LegacyNativeKillerSystem? KillSystem;

	public LegacyNativeProcesType Process { get; set; }

	public LegacyNativeTournamentType Type { get; set; }

	public LegacyNativeMsgExtremePk(LegacyNativeTournamentType _type)
	{
		Type = _type;
		Process = LegacyNativeProcesType.Dead;
	}
}
