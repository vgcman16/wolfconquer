using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMsgMovement
{
	public static void GetWalk(this LegacyNativePacket stream, out LegacyNativeWalkQuery pQuery)
	{
		pQuery = new LegacyNativeWalkQuery();
		pQuery = stream.ProtoBufferDeserialize(pQuery);
	}

	public static LegacyNativePacket MovementCreate(this LegacyNativePacket stream, LegacyNativeWalkQuery pQuery)
	{
		stream.InitWriter();
		pQuery.TimeStamp = (uint)DateTime.Now.Value();
		stream.ProtoBufferSerialize(pQuery);
		stream.Finalize(2205);
		return stream;
	}
}
