using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static class LegacyNativeTeamArenaRankingPacket
{
    // Whole native MsgBuilder3004..3036; page/header values are not normalized.
    public static void GetTeamArenaRanking(this LegacyNativePacket stream, out ushort PageNumber)
    {
        PageNumber = stream.ReadUInt16();
    }

    public static LegacyNativePacket TeamArenaRankingCreate(this LegacyNativePacket stream, ushort PageNumber, ushort Count, LegacyNativeArenaRuntime.ArenaIDs ArenaID)
    {
        stream.InitWriter();
        stream.Write(PageNumber);
        stream.Write(Count);
        stream.Write((uint)ArenaID);
        return stream;
    }

    public static LegacyNativePacket AddItemTeamArenaRanking(this LegacyNativePacket stream, uint Rank, uint Points, uint Class, uint Level, byte Gender, string name)
    {
        stream.Write(Rank);
        stream.Write(2979015);
        stream.Write(Points);
        stream.Write(Class);
        stream.Write(Level);
        stream.Write(Gender);
        stream.Write(name, 32);
        stream.Write((byte)0);
        stream.Write((ushort)0);
        return stream;
    }

    public static LegacyNativePacket MsgTeamArenaRankingFinalize(this LegacyNativePacket stream)
    {
        stream.Finalize(2186);
        return stream;
    }
}
