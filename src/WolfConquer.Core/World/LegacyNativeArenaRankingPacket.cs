using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static class LegacyNativeArenaRankingPacket
{
    // Whole native MsgBuilder796..830. Fixed 10 marker is not a row count.
    public static LegacyNativePacket ArenaRankingCreate(this LegacyNativePacket stream,
        LegacyNativeMsgArenaRanking.RankTyp rank, ushort page, uint Count, LegacyNativeArenaRuntime.ArenaIDs ArenaID)
    {
        stream.InitWriter();
        stream.Write((ushort)rank);
        stream.Write(page);
        stream.Write(Count);
        stream.Write(10);
        stream.Write((uint)ArenaID);
        return stream;
    }

    public static LegacyNativePacket AddItemArenaRanking(this LegacyNativePacket stream,
        ushort Rank, string Name, ushort UnKnow, uint Points, uint Class, uint level, uint unknow2, uint Mesh)
    {
        stream.Write(Rank);
        stream.Write(Name, 32);
        stream.Write(UnKnow);
        stream.Write(Points);
        stream.Write(Class);
        stream.Write(level);
        stream.Write(unknow2);
        stream.Write(Mesh);
        return stream;
    }

    public static LegacyNativePacket ArenaRankingFinalize(this LegacyNativePacket stream)
    {
        stream.Finalize(2093);
        return stream;
    }

    public static void GetArenaRanking(this LegacyNativePacket stream,
        out LegacyNativeMsgArenaRanking.RankTyp rank, out ushort PageNumber)
    {
        rank = (LegacyNativeMsgArenaRanking.RankTyp)stream.ReadUInt16();
        PageNumber = stream.ReadUInt16();
    }
}
