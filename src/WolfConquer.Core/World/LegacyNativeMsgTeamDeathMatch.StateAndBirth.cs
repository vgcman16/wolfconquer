using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Complete source family; full lifecycle/interface adoption is joint, never a no-op.
internal partial class LegacyNativeMsgTeamDeathMatch
{
	public enum TeamTyps : byte
	{
		BlueTeam,
		RedTeam,
		BlackTeam,
		WhiteTeam,
		Count
	}

	public const uint RedGarment = 185625u;

	public const uint WhiteGarment = 185325u;

	public const uint BlackGarment = 181525u;

	public const uint BlueGarment = 185825u;

	public const uint MapID = 1082u;

	public DateTime StampScore = default(DateTime);

	public Dictionary<uint, LegacyNativeGameClientRuntime>[]? Teams;

	public DateTime StartTimer = default(DateTime);

	public LegacyNativeKillerSystem? KillSystem;

	public LegacyNativeTournamentType Type { get; set; }

	public LegacyNativeProcesType Process { get; set; }

	public LegacyNativeMsgTeamDeathMatch(LegacyNativeTournamentType _type)
	{
		Type = _type;
		Process = LegacyNativeProcesType.Dead;
	}
}
