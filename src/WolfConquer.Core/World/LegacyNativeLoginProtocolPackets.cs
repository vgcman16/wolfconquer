using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static class LegacyNativeLoginProtocolPackets
{
    // Whole native MsgBuilder2231..2240.
    public static LegacyNativePacket LoginHandlerCreate(this LegacyNativePacket stream, uint Type, uint Map)
    {
        stream.InitWriter();
        stream.Write(0);
        stream.Write(Type);
        stream.Write(Map);
        stream.Finalize(1044);
        return stream;
    }

    // Whole native MsgBuilder2574..2590. Real DateTime.Now sampled once, no clock adapter.
    public static LegacyNativePacket ServerTimerCreate(this LegacyNativePacket stream)
    {
        stream.InitWriter();
        DateTime now = DateTime.Now;
        stream.ProtoBufferSerialize(new LegacyNativeMsgDataProto
        {
            Year = now.Year - 1900,
            Month = now.Month - 1,
            DayOfYear = now.DayOfYear,
            DayOfMonth = now.Day,
            Hour = now.Hour,
            Minute = now.Minute,
            Second = now.Second
        });
        stream.Finalize(2224);
        return stream;
    }

    // Whole native MsgBuilder2604..2612.
    public static LegacyNativePacket ServerVerCreate(this LegacyNativePacket stream)
    {
        stream.InitWriter();
        stream.Write(0);
        stream.Write(0);
        stream.Finalize(2161);
        return stream;
    }
}
