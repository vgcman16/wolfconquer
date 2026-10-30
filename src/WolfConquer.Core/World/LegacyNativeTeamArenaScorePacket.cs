using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static class LegacyNativeTeamArenaScorePacket
{
    // Entire native MsgBuilder2962..2977. Scalar codec is independent of Team birth.
    public static LegacyNativePacket TeamArenaMatchScoreCreate(this LegacyNativePacket stream,
        uint UidOne, uint RankOne, string nameone, uint damageone,
        uint UidTwo, uint RankTwo, string nametwo, uint damagetwo)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(nameone);
        ArgumentNullException.ThrowIfNull(nametwo);
        stream.PrepareFrame(2190, prepared =>
        {
            prepared.Write(UidOne);
            prepared.Write(RankOne);
            prepared.Write(nameone, 32);
            prepared.Write(damageone);
            prepared.Write(UidTwo);
            prepared.Write(RankTwo);
            prepared.Write(nametwo, 32);
            prepared.Write(damagetwo);
            prepared.Write(1u);
        });
        return stream;
    }
}
