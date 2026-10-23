using ProtoBuf;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeSobNpcRuntime
{
	public bool AddFlag(LegacyNativeMsgUpdate.Flags Flag, int Secounds, bool RemoveOnDead, int StampSecounds = 0, uint showamount = 0u, uint amount = 0u)
	{
		if (BitVector is not null && BitVector.TryAdd((int)Flag, Secounds, RemoveOnDead, StampSecounds))
		{
			UpdateFlagScreen();
			return true;
		}
		return false;
	}

public bool RemoveFlag(LegacyNativeMsgUpdate.Flags Flag)
	{
		if (BitVector is not null && BitVector.TryRemove((int)Flag))
		{
			UpdateFlagScreen();
			return true;
		}
		return false;
	}

public bool ContainFlag(LegacyNativeMsgUpdate.Flags Flag)
	{
		return BitVector.ContainFlag((int)Flag);
	}

public void UpdateFlagScreen()
	{
		using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
		LegacyNativePacket stream = recycledPacket.GetStream();
		LegacyNativeMsgUpdate msgUpdate = new LegacyNativeMsgUpdate(stream, UID);
		stream = msgUpdate.Append(stream, LegacyNativeMsgUpdate.DataType.StatusFlag, BitVector.bits);
		stream = msgUpdate.GetArray(stream);
		foreach (LegacyNativeGameClientRuntime value in LegacyNativePoolClients.GamePoll!.Values)
		{
			if (value.Player.Map == Map)
			{
				value.Send(stream);
			}
		}
	}

public void SendString(LegacyNativePacket stream, LegacyNativeMsgStringPacket.StringID id, params string[] args)
	{
		LegacyNativeMsgStringPacket msgStringPacket = new LegacyNativeMsgStringPacket();
		msgStringPacket.ID = id;
		msgStringPacket.UID = UID;
		msgStringPacket.Strings = args;
		SendScrennPacket(stream.StringPacketCreate(msgStringPacket));
	}

public void SendScrennPacket(LegacyNativePacket packet)
	{
		foreach (LegacyNativeGameClientRuntime value in LegacyNativePoolClients.GamePoll!.Values)
		{
			if (value.Player.Map == Map && value.Player.GetMyDistance(X, Y) < 19)
			{
				value.Send(packet);
			}
		}
	}
}
