using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeGameMap
{
	public void AddMagnolia(LegacyNativePacket stream, uint Quality)
	{
		bool flag = false;
		if (Magnolia != null)
		{
			if (Magnolia.X == 99)
			{
				flag = true;
			}
			RemoveNpc(Magnolia, stream);
		}
		Magnolia = LegacyNativeNpcRuntime.Create();
		if (flag)
		{
			Magnolia.UID = 999900u;
			Magnolia.X = 106;
			Magnolia.Y = 99;
		}
		else
		{
			Magnolia.UID = 999901u;
			Magnolia.X = 99;
			Magnolia.Y = 112;
		}
		Magnolia.ObjType = LegacyNativeMapObjectType.Npc;
		Magnolia.NpcType = LegacyNativeNpcType.Talker;
		uint num = 0u;
		if (Quality % 10 == 7)
		{
			num = 10u;
		}
		else if (Quality % 10 == 8)
		{
			num = 20u;
		}
		if (Quality % 10 == 9)
		{
			num = 30u;
		}
		if (Quality % 10 == 0)
		{
			num = 40u;
		}
		Magnolia.Mesh = (ushort)(19340 + num);
		Magnolia.Map = ID;
		AddNpc(Magnolia);
	}

public void CheckUpSoldierReamins()
	{
		List<LegacyNativeNpcRuntime> list = new List<LegacyNativeNpcRuntime>();
		foreach (LegacyNativeNpcRuntime value2 in soldierRemains.Values)
		{
			if (ID == 1000)
			{
				if (DateTime.Now > value2.Respawn)
				{
					value2.X = (ushort)LegacyNativePoolRandom.GetRandom.Next(592, 656);
					value2.Y = (ushort)LegacyNativePoolRandom.GetRandom.Next(445, 509);
					AddNpc(value2);
					list.Add(value2);
				}
			}
			else if (ID == 1015)
			{
				if (value2.UID == 8551)
				{
					value2.X = (ushort)LegacyNativePoolRandom.GetRandom.Next(519, 583);
					value2.Y = (ushort)LegacyNativePoolRandom.GetRandom.Next(310, 374);
					AddNpc(value2);
					list.Add(value2);
				}
				else
				{
					value2.X = (ushort)LegacyNativePoolRandom.GetRandom.Next(364, 544);
					value2.Y = (ushort)LegacyNativePoolRandom.GetRandom.Next(484, 664);
					AddNpc(value2);
					list.Add(value2);
				}
			}
		}
		foreach (LegacyNativeNpcRuntime item in list)
		{
			soldierRemains.TryRemove(item.UID, out var _);
		}
	}

public bool SearchNpcInScreen(uint UID, ushort X, ushort Y, out LegacyNativeNpcRuntime? obj)
	{
		if (View!.TryGetObject<LegacyNativeNpcRuntime>(UID, LegacyNativeMapObjectType.Npc, X, Y, out obj))
		{
			return LegacyNativeRoleCore.GetDistance(X, Y, obj!.X, obj.Y) < 19;
		}
		obj = null;
		return false;
	}
}
