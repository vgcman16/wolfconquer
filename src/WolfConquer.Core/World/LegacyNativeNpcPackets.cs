using ProtoBuf;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Whole native NpcSpawnProto contract/NpcCreate/GetNpc, not a server-dialog query DTO.
internal static class LegacyNativeNpcPackets
{
	[ProtoContract]
	public class LegacyNativeNpcSpawnProto
	{
		[ProtoMember(1, IsRequired = true)]
		public uint UID;

		[ProtoMember(2, IsRequired = true)]
		public uint UID2;

		[ProtoMember(3, IsRequired = true)]
		public uint UNK;

		[ProtoMember(4, IsRequired = true)]
		public ushort X;

		[ProtoMember(5, IsRequired = true)]
		public ushort Y;

		[ProtoMember(6, IsRequired = true)]
		public ushort Mesh;

		[ProtoMember(7, IsRequired = true)]
		public ushort Type;

		[ProtoMember(8, IsRequired = true)]
		public ushort Action;
	}

	public enum StaticGUIType : byte
	{
		Header,
		Body,
		Footer
	}

	public static LegacyNativePacket NpcCreate(this LegacyNativePacket stream, LegacyNativeNpcRuntime npc, ushort newMesh = 0)
	{
		stream.InitWriter();
		LegacyNativeNpcSpawnProto npcSpawnProto = new LegacyNativeNpcSpawnProto
		{
			UID = npc.UID,
			X = npc.X,
			Y = npc.Y,
			Type = (ushort)npc.NpcType,
			Action = npc.Sort,
			UID2 = npc.UnKnow
		};
		if (newMesh != 0)
		{
			npcSpawnProto.Mesh = newMesh;
		}
		else
		{
			npcSpawnProto.Mesh = npc.Mesh;
		}
		stream.ProtoBufferSerialize(npcSpawnProto);
		stream.Finalize(2210);
		return stream;
	}

	public static void GetNpc(this LegacyNativePacket stream, out LegacyNativeNpcRuntime npc)
	{
		LegacyNativeNpcSpawnProto obj = new LegacyNativeNpcSpawnProto();
		obj = stream.ProtoBufferDeserialize(obj);
		npc = new LegacyNativeNpcRuntime();
		npc.UID = obj.UID;
		npc.UnKnow = obj.UID2;
		npc.X = obj.X;
		npc.Y = obj.Y;
		npc.Mesh = obj.Mesh;
		npc.NpcType = (LegacyNativeNpcType)obj.Type;
		npc.Sort = obj.Action;
	}
}
