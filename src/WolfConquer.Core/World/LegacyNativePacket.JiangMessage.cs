using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static class LegacyNativeMsgJiangMessage
{
    // Entire native MsgJiangMessage7..13. InitWriter discards the incoming cursor.
    public static LegacyNativePacket CreateJiangMessage(this LegacyNativePacket stream, uint SpellID)
    {
        stream.InitWriter();
        stream.Write((ushort)SpellID);
        stream.Finalize(2309);
        return stream;
    }
}
