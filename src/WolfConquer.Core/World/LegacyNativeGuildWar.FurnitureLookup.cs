using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Whole original AddNpc, including catch/write behavior and insertion ordering.
internal partial class LegacyNativeGuildWar
{
	internal void AddNpc(NPCID type, uint npcid)
	{
		try
		{
			NpcInfo npcInfo = new NpcInfo();
			npcInfo.type = type;
			npcInfo.Npc = Map!.View!.GetMapObject<LegacyNativeSobNpcRuntime>(LegacyNativeMapObjectType.SobNpc, npcid);
			NPC_INFO.TryAdd(npcInfo.type, npcInfo);
		}
		catch (Exception ex)
		{
			Console.WriteLine(ex.ToString());
		}
	}
}
