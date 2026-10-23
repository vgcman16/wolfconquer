using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
namespace WolfConquer.Core.World;
internal partial class LegacyNativeMsgFootball
{
// Entire actual state/constructor and nested Team state/constructor/Count.
// ITournament adoption is HELD until all contract members are genuine.
public partial class Team
	{
		public enum TeamTypes : byte
		{
			Spain,
			Argentina
		}

		public LegacyNativeMsgFootball Football;

		public int TeamNumber = 0;

		public TeamTypes TeamType = TeamTypes.Spain;

		public int Score = 0;

		public int Count => Football.Map!.Values.Where((LegacyNativeGameClientRuntime p) => p.Player.FootballTeamID == TeamNumber).Count();

		public Team(LegacyNativeMsgFootball _Football, TeamTypes type, int number)
		{
			TeamType = type;
			TeamNumber = number;
			Football = _Football;
		}

	}

public const uint MapID = 3844u;

	public Team? Team1;

	public Team? Team2;

	public LegacyNativeGameMap? Map;

	public DateTime StampScore;

	public DateTime StartTimer;

	public DateTime InfoTimer;

	public int Secounds;

	private LegacyNativeSobNpcRuntime npc = new LegacyNativeSobNpcRuntime();

	public bool OnFinishRound = false;

	public DateTime TeleportedTime = default(DateTime);

	public LegacyNativeProcesType Process { get; set; }

	public LegacyNativeTournamentType Type { get; set; }

	public LegacyNativeMsgFootball(LegacyNativeTournamentType _type)
	{
		Type = _type;
		Process = LegacyNativeProcesType.Dead;
	}


}
