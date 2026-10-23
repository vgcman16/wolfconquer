using WolfConquer.Core.Networking;
using Actions = WolfConquer.Core.World.LegacyHairFaceStorageAction;
using HairfaceType = WolfConquer.Core.World.LegacyHairFaceStorageType;

namespace WolfConquer.Core.World;

// Whole source method; private receiver is deliberately unregistered, no Packet2282 adoption.
internal static partial class LegacyNativeMsgHairfaceStorage
{
public static LegacyNativePacket CreateHairface(this LegacyNativePacket stream, MsgHairfaceStorageProto obj)
	{
		stream.InitWriter();
		stream.ProtoBufferSerialize(obj);
		stream.Finalize(2282);
		return stream;
	}

public static void GetHairface(this LegacyNativePacket stream, out MsgHairfaceStorageProto pQuery)
	{
		pQuery = new MsgHairfaceStorageProto();
		pQuery = stream.ProtoBufferDeserialize(pQuery);
	}
}
