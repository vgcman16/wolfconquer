using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Complete native MsgBuilder.NobilityIconCreate only; no receiver or host adoption.
internal static class LegacyNativeNobilityPacket
{
    public static LegacyNativePacket NobilityIconCreate(this LegacyNativePacket stream, LegacyNativeNobility nobility)
    {
        stream.InitWriter();
        stream.Write(3u);
        stream.Write(nobility.UID);
        string text = nobility.UID + " " + nobility.Donation + " " + (byte)nobility.Rank + " " + nobility.Position;
        stream.ZeroFill(108);
        stream.Write(text);
        stream.Finalize(2091);
        return stream;
    }
}
