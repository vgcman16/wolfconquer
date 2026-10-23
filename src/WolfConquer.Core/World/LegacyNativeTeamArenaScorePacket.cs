using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static class LegacyNativeTeamArenaScorePacket
{
    // Entire native MsgBuilder2962..2977. Scalar codec is independent of Team birth.
    public static LegacyNativePacket TeamArenaMatchScoreCreate(this LegacyNativePacket stream,
        uint UidOne, uint RankOne, string nameone, uint damageone,
        uint UidTwo, uint RankTwo, string nametwo, uint damagetwo)
    {
        stream.InitWriter();
        stream.Write(UidOne);
        stream.Write(RankOne);
        stream.Write(nameone, 32);
        stream.Write(damageone);
        stream.Write(UidTwo);
        stream.Write(RankTwo);
        stream.Write(nametwo, 32);
        stream.Write(damagetwo);
        stream.Write(1u);
        stream.Finalize(2190);
        return stream;
    }
}
