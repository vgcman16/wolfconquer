using ProtoBuf;
using WolfConquer.Core.Networking;
namespace WolfConquer.Core.World;

// Native MsgBuilder complete MapStatusProto and whole MapStatusCreate2263..2275.
internal static class LegacyNativeMapStatusPacket
{
[ProtoContract]
	internal class MapStatusProto
	{
		[ProtoMember(1, IsRequired = true)]
		public uint ID;

		[ProtoMember(2, IsRequired = true)]
		public uint BaseID;

		[ProtoMember(3, IsRequired = true)]
		public ulong Status;

		[ProtoMember(4)]
		public uint Weather;
	}
public static LegacyNativePacket MapStatusCreate(this LegacyNativePacket stream, uint MapID, uint BaseID, ulong Status)
	{
		stream.InitWriter();
		stream.ProtoBufferSerialize(new MapStatusProto
		{
			ID = MapID,
			BaseID = BaseID,
			Status = Status
		});
		stream.Finalize(2213);
		return stream;
	}
}
