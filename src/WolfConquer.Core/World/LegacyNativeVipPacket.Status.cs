using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeVipPacket
{
    // Whole native MsgBuilder3370..3376. No sender or runtime registration here.
    public static LegacyNativePacket VipStatusCreate(this LegacyNativePacket stream,
        LegacyNativeMsgVipStatus.VipFlags Mode)
    {
        stream.InitWriter();
        stream.Write((uint)Mode);
        stream.Finalize(2284);
        return stream;
    }
}
