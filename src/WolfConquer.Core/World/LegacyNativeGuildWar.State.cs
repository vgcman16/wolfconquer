using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Whole native physical state, records and constructor, not event activation or scheduler Create.
internal partial class LegacyNativeGuildWar
{
	[System.Flags]
	public enum MapItem : uint
	{
		Points10 = 0xA3Bu,
		Points5 = 0xA3Cu,
		Points3 = 0xA3Du,
		Points1 = 0xA3Eu
	}

	[System.Flags]
	public enum StaticMesh : uint
	{
		Pole = 0x471u,
		RightGate = 0x3E8u,
		LeftGate = 0x3E9u,
		RightGate_Close_1 = 0x5BBu,
		RightGate_Open_1 = 0x5C5u,
		RightGate_Demolition_1 = 0x5CFu,
		LeftGate_Close_1 = 0x5D3u,
		LeftGate_Open_1 = 0x5DDu,
		LeftGate_Demolition_1 = 0x5E7u,
		RightGate_Close_2 = 0x5F7u,
		RightGate_Open_2 = 0x601u,
		RightGate_Demolition_2 = 0x60Bu,
		LeftGate_Close_2 = 0x60Fu,
		LeftGate_Open_2 = 0x619u,
		LeftGate_Demolition_2 = 0x623u,
		RightGate_Close_3 = 0x633u,
		RightGate_Open_3 = 0x63Du,
		RightGate_Demolition_3 = 0x647u,
		LeftGate_Close_3 = 0x64Bu,
		LeftGate_Open_3 = 0x655u,
		LeftGate_Demolition_3 = 0x65Fu,
		RightGate_Close_4 = 0x66Fu,
		RightGate_Open_4 = 0x679u,
		RightGate_Demolition_4 = 0x683u,
		LeftGate_Close_4 = 0x687u,
		LeftGate_Open_4 = 0x691u,
		LeftGate_Demolition_4 = 0x69Bu
	}

	[System.Flags]
	public enum NPCID : uint
	{
		RightGate = 0u,
		LeftGate = 1u,
		TenacityPillar = 2u,
		ExorcimPillar = 3u,
		GuildPillar = 4u,
		GuildWithHonorPillar = 5u
	}

	public class GuildRank
	{
		public uint GuildID;

		public string? Name;

		public uint Points;

		public uint Rank;
	}

	public class GuildRankTop4
	{
		public uint GuildID;

		public string? Name;

		public uint Points;

		public uint Rank;
	}

	public class UserRank
	{
		public string? Name;

		public uint Points;

		public uint UID;

		public uint Rank;
	}

	public class NpcInfo
	{
		public NPCID type;

		public uint GuildID;

		public LegacyNativeSobNpcRuntime? Npc;
	}

	public class ScoreList
	{
		public uint GuildID;

		public string? Name;

		public ulong Score;

		public int LeaderReward = 1;

		public int DeputiLeaderReward = 7;
	}
	internal ushort[][] StatueCoords = new ushort[7][]
	{
		new ushort[2] { 140, 134 },
		new ushort[2] { 144, 124 },
		new ushort[2] { 130, 138 },
		new ushort[2] { 153, 124 },
		new ushort[2] { 161, 124 },
		new ushort[2] { 130, 147 },
		new ushort[2] { 130, 155 }
	};

	public ConcurrentDictionary<uint, GuildRankTop4> GUILD_RANK_TOP4;

	public ConcurrentDictionary<uint, GuildRank> GUILD_RANK;

	public ConcurrentDictionary<uint, UserRank> USER_RANK;

	public ConcurrentDictionary<NPCID, NpcInfo> NPC_INFO;

	public ConcurrentDictionary<uint, ScoreList> SCORE_LIST;

	public ConcurrentDictionary<LegacyNativeNpcID, GuildConductor> GuildConductors;

	public LegacyNativeGameMap? Map;

	public ScoreList Winner;

	public DateTime StampUpdate = default(DateTime);

	public DateTime Stamp = default(DateTime);

	public LegacyNativeTime32 TimeLeft;

	public List<uint> RewardLeader = new List<uint>();

	public List<uint> RewardDeputiLeader = new List<uint>();

	public const uint MapID = 1038u;

	public bool S_30 = false;

	public LegacyNativeProcesType Proces { get; set; }
	public LegacyNativeGuildWar()
	{
		Proces = LegacyNativeProcesType.Dead;
		Winner = new ScoreList();
		GUILD_RANK_TOP4 = new ConcurrentDictionary<uint, GuildRankTop4>();
		SCORE_LIST = new ConcurrentDictionary<uint, ScoreList>();
		GUILD_RANK = new ConcurrentDictionary<uint, GuildRank>();
		USER_RANK = new ConcurrentDictionary<uint, UserRank>();
		NPC_INFO = new ConcurrentDictionary<NPCID, NpcInfo>();
		GuildConductors = new ConcurrentDictionary<LegacyNativeNpcID, GuildConductor>();
	}
}
