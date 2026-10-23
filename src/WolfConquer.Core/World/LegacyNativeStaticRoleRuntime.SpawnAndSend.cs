using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeStaticRoleRuntime
{
	public void SendString(LegacyNativePacket stream, LegacyNativeMsgStringPacket.StringID id, params string[] args)
	{
		LegacyNativeMsgStringPacket msgStringPacket = new LegacyNativeMsgStringPacket();
		msgStringPacket.ID = id;
		msgStringPacket.UID = UID;
		msgStringPacket.Strings = args;
		Send(stream.StringPacketCreate(msgStringPacket));
	}

	public LegacyNativePacket GetArray(LegacyNativePacket stream, bool view, bool st)
	{
		stream.InitWriter();
		LegacyNativePlayerRuntime.SpawnPacketProto spawnPacketProto = new LegacyNativePlayerRuntime.SpawnPacketProto();
		spawnPacketProto.UID = UID;
		spawnPacketProto.Mesh = Mesh;
		spawnPacketProto.MonsterLevel = Level;
		spawnPacketProto.Hitpoints = 0u;
		spawnPacketProto.X = X;
		spawnPacketProto.Y = Y;
		spawnPacketProto.Action = (ushort)Action;
		spawnPacketProto.Level = Level;
		spawnPacketProto.Names = new string[2]
		{
			Name,
			string.Empty
		};
		LegacyNativePlayerRuntime.SpawnPacketProto spawnPacketProto2 = spawnPacketProto;
		spawnPacketProto2.StatusFlags = new ulong[BitVector.bits.Length];
		for (int i = 0; i < BitVector.bits.Length; i++)
		{
			spawnPacketProto2.StatusFlags[i] = BitVector.bits[i];
		}
		stream.ProtoBufferSerialize(spawnPacketProto2);
		stream.Finalize(2227);
		return stream;
	}

	public void Send(LegacyNativePacket msg)
	{
		foreach (ILegacyNativeMapObj item in GMap.View!.Roles(LegacyNativeMapObjectType.Player, X, Y, (ILegacyNativeMapObj p) => CanSee(p)))
		{
			item.Send(msg);
		}
	}

	public bool CanSee(ILegacyNativeMapObj obj)
	{
		if (obj.Map != Map)
		{
			return false;
		}
		if (obj.DynamicID != DynamicID)
		{
			return false;
		}
		if (obj.UID == UID)
		{
			return false;
		}
		return LegacyNativeRoleCore.GetDistance(obj.X, obj.Y, X, Y) <= 18;
	}
}
