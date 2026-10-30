using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static class LegacyNativeArenaRankingPacket
{
    // Whole native MsgBuilder796..830. Fixed 10 marker is not a row count.
    public static LegacyNativePacket ArenaRankingCreate(this LegacyNativePacket stream,
        LegacyNativeMsgArenaRanking.RankTyp rank, ushort page, uint Count, LegacyNativeArenaRuntime.ArenaIDs ArenaID)
    {
        ArgumentNullException.ThrowIfNull(stream);
        stream.PrepareWriteAt(4, prepared =>
        {
            prepared.Write((ushort)rank);
            prepared.Write(page);
            prepared.Write(Count);
            prepared.Write(10);
            prepared.Write((uint)ArenaID);
        });
        return stream;
    }

    public static LegacyNativePacket AddItemArenaRanking(this LegacyNativePacket stream,
        ushort Rank, string Name, ushort UnKnow, uint Points, uint Class, uint level, uint unknow2, uint Mesh)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(Name);
        stream.PrepareAppend(prepared =>
        {
            prepared.Write(Rank);
            prepared.Write(Name, 32);
            prepared.Write(UnKnow);
            prepared.Write(Points);
            prepared.Write(Class);
            prepared.Write(level);
            prepared.Write(unknow2);
            prepared.Write(Mesh);
        });
        return stream;
    }

    public static LegacyNativePacket ArenaRankingFinalize(this LegacyNativePacket stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        stream.Finalize(2093);
        return stream;
    }

    public static void GetArenaRanking(this LegacyNativePacket stream,
        out LegacyNativeMsgArenaRanking.RankTyp rank, out ushort PageNumber)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var prepared = stream.ReadPrepared(static packet =>
        {
            var parsedRank = (LegacyNativeMsgArenaRanking.RankTyp)packet.ReadUInt16();
            ushort parsedPage = packet.ReadUInt16();
            return (Rank: parsedRank, Page: parsedPage);
        });
        rank = prepared.Rank;
        PageNumber = prepared.Page;
    }
}
