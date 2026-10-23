using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Complete original queries. No default gates, event start, population, or fake furniture.
internal partial class LegacyNativeGuildWar
{
	public ushort CountDown
	{
		get
		{
			int num = (TimeLeft.TotalMilliseconds - LegacyNativeTime32.Now.TotalMilliseconds) / 1000;
			if (num <= 0)
			{
				return 0;
			}
			return (ushort)num;
		}
	}

	internal uint AvailablePoints => (uint)(Map!.ContainItem(2619u) * 10 + Map!.ContainItem(2620u) * 5 + Map!.ContainItem(2621u) * 3 + Map!.ContainItem(2622u));

	internal uint TotalAvailablePoints
	{
		get
		{
			uint num = 0u;
			foreach (LegacyNativeGameClientRuntime value in LegacyNativePoolClients.GamePoll!.Values)
			{
				if (value != null && value.Socket != null && value.Socket.Alive && value.Player != null && value.Player.Map == 1038 && value.Player.MyGuildMember != null && value.Player.MyGuildMember.GuildWarPoints != 0)
				{
					num += value.Player.MyGuildMember.GuildWarPoints;
				}
			}
			return num;
		}
	}

	public bool LeftGateOpen
	{
		get
		{
			if (NPC_INFO[NPCID.LeftGate].Npc!.Mesh == (LegacyNativeSobNpcRuntime.StaticMesh)1501)
			{
				return true;
			}
			if (NPC_INFO[NPCID.LeftGate].Npc!.Mesh == (LegacyNativeSobNpcRuntime.StaticMesh)1561)
			{
				return true;
			}
			if (NPC_INFO[NPCID.LeftGate].Npc!.Mesh == (LegacyNativeSobNpcRuntime.StaticMesh)1621)
			{
				return true;
			}
			if (NPC_INFO[NPCID.LeftGate].Npc!.Mesh == (LegacyNativeSobNpcRuntime.StaticMesh)1681)
			{
				return true;
			}
			return false;
		}
	}

	public bool RightGateOpen
	{
		get
		{
			if (NPC_INFO[NPCID.RightGate].Npc!.Mesh == (LegacyNativeSobNpcRuntime.StaticMesh)1477)
			{
				return true;
			}
			if (NPC_INFO[NPCID.RightGate].Npc!.Mesh == LegacyNativeSobNpcRuntime.StaticMesh.OpenRightGate)
			{
				return true;
			}
			if (NPC_INFO[NPCID.RightGate].Npc!.Mesh == (LegacyNativeSobNpcRuntime.StaticMesh)1597)
			{
				return true;
			}
			if (NPC_INFO[NPCID.RightGate].Npc!.Mesh == (LegacyNativeSobNpcRuntime.StaticMesh)1657)
			{
				return true;
			}
			return false;
		}
	}

	public bool LeftGateDemolition
	{
		get
		{
			if (NPC_INFO[NPCID.LeftGate].Npc!.Mesh == LegacyNativeSobNpcRuntime.StaticMesh.OpenLeftGate)
			{
				return true;
			}
			if (NPC_INFO[NPCID.LeftGate].Npc!.Mesh == (LegacyNativeSobNpcRuntime.StaticMesh)1571)
			{
				return true;
			}
			if (NPC_INFO[NPCID.LeftGate].Npc!.Mesh == (LegacyNativeSobNpcRuntime.StaticMesh)1631)
			{
				return true;
			}
			if (NPC_INFO[NPCID.LeftGate].Npc!.Mesh == (LegacyNativeSobNpcRuntime.StaticMesh)1691)
			{
				return true;
			}
			return false;
		}
	}

	public bool RightGateDemolition
	{
		get
		{
			if (NPC_INFO[NPCID.RightGate].Npc!.Mesh == (LegacyNativeSobNpcRuntime.StaticMesh)1487)
			{
				return true;
			}
			if (NPC_INFO[NPCID.RightGate].Npc!.Mesh == (LegacyNativeSobNpcRuntime.StaticMesh)1547)
			{
				return true;
			}
			if (NPC_INFO[NPCID.RightGate].Npc!.Mesh == (LegacyNativeSobNpcRuntime.StaticMesh)1607)
			{
				return true;
			}
			if (NPC_INFO[NPCID.RightGate].Npc!.Mesh == (LegacyNativeSobNpcRuntime.StaticMesh)1667)
			{
				return true;
			}
			return false;
		}
	}
	internal bool Verified(LegacyNativePlayerRuntime attacker, LegacyNativeSobNpcRuntime target)
	{
		if (target.UID == NPC_INFO[NPCID.GuildPillar].Npc!.UID)
		{
			if (attacker.MyGuild == null)
			{
				return false;
			}
			if (NPC_INFO[NPCID.GuildPillar].Npc!.HitPoints == 0)
			{
				return false;
			}
			if (attacker.GuildID == Winner.GuildID)
			{
				return false;
			}
			if (Proces != LegacyNativeProcesType.Alive)
			{
				return false;
			}
		}
		if (NPC_INFO.ContainsKey(NPCID.GuildWithHonorPillar) && target.UID == NPC_INFO[NPCID.GuildWithHonorPillar].Npc!.UID)
		{
			if (attacker.MyGuild == null)
			{
				return false;
			}
			if (attacker.GuildID == NPC_INFO[NPCID.GuildWithHonorPillar].Npc!.GuildID)
			{
				return false;
			}
			if (Proces != LegacyNativeProcesType.Alive)
			{
				return false;
			}
		}
		if (NPC_INFO.ContainsKey(NPCID.ExorcimPillar) && target.UID == NPC_INFO[NPCID.ExorcimPillar].Npc!.UID)
		{
			if (attacker.MyGuild == null)
			{
				return false;
			}
			if (attacker.GuildID == NPC_INFO[NPCID.ExorcimPillar].Npc!.GuildID)
			{
				return false;
			}
			if (Proces != LegacyNativeProcesType.Alive)
			{
				return false;
			}
		}
		if (NPC_INFO.ContainsKey(NPCID.TenacityPillar) && target.UID == NPC_INFO[NPCID.TenacityPillar].Npc!.UID)
		{
			if (attacker.MyGuild == null)
			{
				return false;
			}
			if (attacker.GuildID == NPC_INFO[NPCID.TenacityPillar].Npc!.GuildID)
			{
				return false;
			}
			if (Proces != LegacyNativeProcesType.Alive)
			{
				return false;
			}
		}
		return true;
	}
	internal bool ValidJump(int Current, out int New, ushort X, ushort Y)
	{
		if (LegacyNativeRoleCore.GetDistance(217, 177, X, Y) <= 3)
		{
			New = 0;
			return true;
		}
		New = Current;
		int num = Map!.FloorType[X, Y];
		if (Current == 3 && (num == 0 || num == 9 || num == 13))
		{
			if (LegacyNativeRoleCore.GetDistance(X, Y, 164, 209) <= 20 && LeftGateOpen)
			{
				New = num;
				return true;
			}
			if (LegacyNativeRoleCore.GetDistance(X, Y, 222, 177) <= 15 && RightGateOpen)
			{
				New = num;
				return true;
			}
			return false;
		}
		New = num;
		return true;
	}

	internal bool ValidWalk(int Current, out int New, ushort X, ushort Y)
	{
		if (LegacyNativeRoleCore.GetDistance(217, 177, X, Y) <= 3)
		{
			New = 0;
			return true;
		}
		New = Current;
		int num = Map!.FloorType[X, Y];
		if (Current == 3 && (num == 0 || num == 9 || num == 13))
		{
			if (Y == 209 || Y == 208)
			{
				if (LegacyNativeRoleCore.GetDistance(X, Y, 164, 209) <= 3 && LeftGateOpen)
				{
					New = num;
					return true;
				}
			}
			else if (X == 216 && LegacyNativeRoleCore.GetDistance(X, Y, 216, 177) <= 4 && RightGateOpen)
			{
				New = num;
				return true;
			}
			return false;
		}
		New = num;
		return true;
	}
}
