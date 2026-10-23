using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static class LegacyNativeTeamArenaRank10Packet
{
    // Whole native MsgBuilder2978..3002.
    public static LegacyNativePacket TeamArenaRank10Create(this LegacyNativePacket stream, LegacyNativeArenaRuntime.ArenaIDs ArenaMode)
    {
        stream.InitWriter();
        stream.Write((uint)ArenaMode);
        return stream;
    }

    public static LegacyNativePacket AddItemTeamArenaRank10(this LegacyNativePacket stream, LegacyNativeTeamArenaUser user)
    {
        stream.Write(user.Name, 32);
        stream.Write(user.LastSeasonRank);
        stream.Write(user.Mesh);
        stream.Write(user.Class);
        stream.Write((uint)user.Level);
        stream.Write(user.LastSeasonArenaPoints);
        stream.Write(user.LastSeasonWin);
        stream.Write(user.LastSeasonLose);
        return stream;
    }

    public static LegacyNativePacket TeamArenaRank10Finalize(this LegacyNativePacket stream)
    {
        stream.Finalize(2287);
        return stream;
    }
}
