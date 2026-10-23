using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Complete source family; full lifecycle/interface adoption is joint, never a no-op.
internal partial class LegacyNativeMsgTeamDeathMatch
{
	public uint GetGarmentID(TeamTyps typ)
	{
		return typ switch
		{
			TeamTyps.BlueTeam => 185825u,
			TeamTyps.RedTeam => 185625u,
			TeamTyps.BlackTeam => 181525u,
			TeamTyps.WhiteTeam => 185325u,
			_ => 0u,
		};
	}

	public TeamTyps GetTeamTyp(uint GarmentID)
	{
		return GarmentID switch
		{
			185625u => TeamTyps.RedTeam,
			185325u => TeamTyps.WhiteTeam,
			185825u => TeamTyps.BlueTeam,
			181525u => TeamTyps.BlackTeam,
			_ => TeamTyps.Count,
		};
	}

	public bool InTournament(LegacyNativeGameClientRuntime user)
	{
		return user.Player.Map == 1082;
	}

	public string FinishTimer()
	{
		TimeSpan timeSpan = new TimeSpan(DateTime.Now.Ticks);
		TimeSpan timeSpan2 = new TimeSpan(StartTimer.AddMinutes(10.0).Ticks);
		return timeSpan2.Minutes - timeSpan.Minutes + " : " + (timeSpan2.Seconds - timeSpan.Seconds);
	}

	internal void SendMapPacket(LegacyNativePacket packet)
	{
		foreach (LegacyNativeGameClientRuntime value in LegacyNativePoolClients.GamePoll!.Values)
		{
			if (value.Player.Map == 1082)
			{
				value.Send(packet);
			}
		}
	}

	public uint GetTeamScore(Dictionary<uint, LegacyNativeGameClientRuntime>? players)
	{
		uint num = 0u;
		try
		{
			if (players != null && players.Count > 0)
			{
				foreach (LegacyNativeGameClientRuntime value in players.Values)
				{
					num += value.Player.TeamDeathMacthKills;
				}
			}
		}
		catch
		{
		}
		return num;
	}
}
