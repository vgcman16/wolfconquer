using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeChiPacket
{
    // Complete native MsgBuilder1430..1438; attribute's native implicit int
    // conversion is deliberately consumed by the caller, not normalized here.
	public static LegacyNativePacket ChiMessageCreate(this LegacyNativePacket stream, LegacyNativeMsgChiInfo.ChiPowerType Typ, int UID, string Name)
	{
		ArgumentNullException.ThrowIfNull(stream);
		ArgumentNullException.ThrowIfNull(Name);
		stream.PrepareFrame(2276, prepared =>
		{
			prepared.Write((byte)Typ);
			prepared.Write(UID);
			prepared.Write(Name, 32);
		});
		return stream;
	}
}
