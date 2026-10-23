using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static class LegacyNativeServerInfoPacket
{
    // Whole native MsgBuilder2592..2603; Vigor narrows to signed Year, no timestamp invented.
	public static LegacyNativePacket ServerInfoCreate(this LegacyNativePacket stream, uint Vigor)
	{
		stream.InitWriter();
		stream.ProtoBufferSerialize(new LegacyNativeMsgDataProto
		{
			Year = (int)Vigor,
			Type = 2u
		});
		stream.Finalize(2224);
		return stream;
	}
}
