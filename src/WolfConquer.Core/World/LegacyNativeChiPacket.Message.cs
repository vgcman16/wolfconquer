using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeChiPacket
{
    // Complete native MsgBuilder1430..1438; attribute's native implicit int
    // conversion is deliberately consumed by the caller, not normalized here.
	public static LegacyNativePacket ChiMessageCreate(this LegacyNativePacket stream, LegacyNativeMsgChiInfo.ChiPowerType Typ, int UID, string Name)
	{
		stream.InitWriter();
		stream.Write((byte)Typ);
		stream.Write(UID);
		stream.Write(Name, 32);
		stream.Finalize(2276);
		return stream;
	}
}
