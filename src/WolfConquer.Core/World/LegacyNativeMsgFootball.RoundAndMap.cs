using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
namespace WolfConquer.Core.World;
internal partial class LegacyNativeMsgFootball
{
public void AddNpc()
	{
		if (Map!.View!.Contain(3333333u, 85, 85))
		{
			return;
		}
		npc = new LegacyNativeSobNpcRuntime();
		npc.X = 85;
		npc.Map = Map!.ID;
		npc.ObjType = LegacyNativeMapObjectType.SobNpc;
		npc.Y = 85;
		npc.UID = 3333333u;
		npc.Type = LegacyNativeNpcType.Talker;
		npc.Mesh = (LegacyNativeSobNpcRuntime.StaticMesh)480;
		npc.Name = "";
		Map!.View!.EnterMap((ILegacyNativeMapObj)npc);
		Map!.SetFlagNpc(npc.X, npc.Y);
		using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
		LegacyNativePacket stream = recycledPacket.GetStream();
		foreach (ILegacyNativeMapObj item in Map!.View!.Roles(LegacyNativeMapObjectType.Player, npc.X, npc.Y))
		{
			item.Send(npc.GetArray(stream, view: false, st: false));
		}
	}

public void RemoveNpc()
	{
		using (LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket())
		{
			LegacyNativePacket stream = recycledPacket.GetStream();
			LegacyNativeMsgStringPacket msgStringPacket = new LegacyNativeMsgStringPacket();
			msgStringPacket.ID = LegacyNativeMsgStringPacket.StringID.Effect;
			msgStringPacket.UID = npc.UID;
			msgStringPacket.Strings = new string[1] { "accession1" };
			LegacyNativeActionQuery pQuery = new LegacyNativeActionQuery
			{
				ObjId = npc.UID,
				Type = 135
			};
			foreach (ILegacyNativeMapObj item in Map!.View!.Roles(LegacyNativeMapObjectType.Player, npc.X, npc.Y))
			{
				item.Send(stream.StringPacketCreate(msgStringPacket));
				item.Send(stream.ActionCreate(pQuery));
			}
		}
		Map!.View!.LeaveMap((ILegacyNativeMapObj)npc);
		Map!.RemoveFlagNpc(npc.X, npc.Y);
	}

public void FinishRound()
	{
		OnFinishRound = true;
		TeleportedTime = DateTime.Now.AddSeconds(2.0);
		using (LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket())
		{
			LegacyNativePacket stream = recycledPacket.GetStream();
			LegacyNativeMsgMessage msgMessage = new LegacyNativeMsgMessage("The next round will start in 8 seconds.", LegacyNativeMsgMessage.MsgColor.red, LegacyNativeMsgMessage.ChatMode.Talk);
			SendMapPacket(msgMessage.GetArray(stream));
		}
		LegacyNativeGameClientRuntime[] array = Players();
		foreach (LegacyNativeGameClientRuntime gameClient in array)
		{
			gameClient.Player.AddFlag(LegacyNativeMsgUpdate.Flags.Freeze, 10, RemoveOnDead: true);
		}
	}

public bool LeftNaked(ushort x, ushort y)
	{
		for (ushort num = 0; num < 6; num++)
		{
			ushort x2 = (ushort)(80 + 3 * num);
			ushort y2 = (ushort)(123 + 3 * num);
			if (GetDistance(x, y, x2, y2) <= 3)
			{
				return true;
			}
		}
		return false;
	}

public bool RightNaked(ushort x, ushort y)
	{
		for (ushort num = 0; num < 6; num++)
		{
			ushort x2 = (ushort)(85 + 3 * num);
			ushort y2 = (ushort)(39 + 3 * num);
			if (GetDistance(x, y, x2, y2) <= 3)
			{
				return true;
			}
		}
		return false;
	}

public static short GetDistance(ushort X, ushort Y, ushort X2, ushort Y2)
	{
		short num = 0;
		short num2 = 0;
		if (X >= X2)
		{
			num = (short)(X - X2);
		}
		else if (X2 >= X)
		{
			num = (short)(X2 - X);
		}
		if (Y >= Y2)
		{
			num2 = (short)(Y - Y2);
		}
		else if (Y2 >= Y)
		{
			num2 = (short)(Y2 - Y);
		}
		if (num > num2)
		{
			return num;
		}
		return num2;
	}

public void SendMapPacket(LegacyNativePacket stream)
	{
		LegacyNativeGameClientRuntime[] array = Players();
		foreach (LegacyNativeGameClientRuntime gameClient in array)
		{
			gameClient.Send(stream);
		}
	}

public LegacyNativeGameClientRuntime[] Players()
	{
		return Map!.Values;
	}

public bool InTournament(LegacyNativeGameClientRuntime user)
	{
		return user.Player.Map == 3844;
	}

public void CheckBall()
	{
		if (Map!.View!.Contain(3333333u, 85, 85))
		{
			return;
		}
		bool flag = true;
		LegacyNativeGameClientRuntime[] array = Players();
		foreach (LegacyNativeGameClientRuntime gameClient in array)
		{
			if (gameClient.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.lianhuaran04))
			{
				flag = false;
			}
		}
		if (flag)
		{
			FinishRound();
		}
	}
}
