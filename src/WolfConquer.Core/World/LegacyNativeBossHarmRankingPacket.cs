using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static class LegacyNativeBossHarmRankingPacket
{
    // Entire native MsgBuilder400..406. Uses the actual pinned generic codec.
    public static LegacyNativePacket CreateBossHarmRankList(this LegacyNativePacket stream, LegacyNativeMsgBossHarmRanking obj)
    {
        stream.InitWriter();
        stream.ProtoBufferSerialize(obj);
        stream.Finalize(2001);
        return stream;
    }
}
