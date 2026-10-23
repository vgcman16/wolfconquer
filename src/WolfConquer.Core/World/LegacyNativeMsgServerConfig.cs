using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Entire native MsgServerConfig owner, both complete writers.
internal static class LegacyNativeMsgServerConfig
{
    public static LegacyNativePacket ServerConfig(this LegacyNativePacket stream)
    {
        stream.InitWriter();
        stream.Write(0);
        stream.Write(0);
        stream.Write(15);
        stream.Write(9999);
        stream.Write(3);
        stream.Write((byte)1);
        stream.Finalize(2047);
        return stream;
    }

    public static LegacyNativePacket CreateBenefitsConfig(this LegacyNativePacket stream)
    {
        stream.InitWriter();
        stream.Write(4);
        stream.Write(1);
        stream.Write(20191203);
        stream.Write(20200106);
        stream.Write(3);
        stream.Write(20190917);
        stream.Write(20201220);
        stream.Write(12);
        stream.Write(20200509);
        stream.Write(20200601);
        stream.Finalize(2131);
        return stream;
    }
}
