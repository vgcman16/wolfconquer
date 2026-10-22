using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Complete native MsgBuilder static-message primitives only; no Handler or host route.
internal static class LegacyNativeStaticMessagePacket
{
    public static void GetStaticMessage(this LegacyNativePacket stream, out uint Accept)
    {
        Accept = stream.ReadUInt32();
    }

    public static LegacyNativePacket StaticMessageCreate(this LegacyNativePacket stream, LegacyNativeMsgStaticMessage.Messages Message,
        LegacyNativeMsgStaticMessage.Action Mode, uint Secounds)
    {
        stream.InitWriter();
        stream.Write(0u);
        stream.Write((uint)Message);
        stream.Write((uint)Mode);
        stream.Write(Secounds);
        stream.Finalize(2320);
        return stream;
    }
}
