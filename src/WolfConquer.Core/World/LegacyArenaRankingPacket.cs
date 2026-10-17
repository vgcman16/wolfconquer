using System.Buffers;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyArenaRankingRankType : ushort
{
    Today = 0,
    History = 1,
}

public sealed record LegacyArenaRankingEntry(
    ushort Rank,
    string Name,
    ushort Unknown,
    uint Points,
    uint Class,
    uint Level,
    uint Unknown2,
    uint Mesh);

public sealed record LegacyArenaRankingPacket(
    LegacyArenaRankingRankType RankType,
    ushort Page,
    uint TotalCount,
    int PageSizeMarker,
    uint ArenaId,
    IReadOnlyList<LegacyArenaRankingEntry> Entries)
{
    private static readonly Encoding NameEncoding = Encoding.UTF8;

    private const int RankTypeOffset = 0;
    private const int PageOffset = RankTypeOffset + sizeof(ushort);
    private const int TotalCountOffset = PageOffset + sizeof(ushort);
    private const int PageSizeMarkerOffset = TotalCountOffset + sizeof(uint);
    private const int ArenaIdOffset = PageSizeMarkerOffset + sizeof(int);
    private const int EntryRankOffset = 0;
    private const int EntryNameOffset = EntryRankOffset + sizeof(ushort);
    private const int EntryUnknownOffset = EntryNameOffset + FixedNameLength;
    private const int EntryPointsOffset = EntryUnknownOffset + sizeof(ushort);
    private const int EntryClassOffset = EntryPointsOffset + sizeof(uint);
    private const int EntryLevelOffset = EntryClassOffset + sizeof(uint);
    private const int EntryUnknown2Offset = EntryLevelOffset + sizeof(uint);
    private const int EntryMeshOffset = EntryUnknown2Offset + sizeof(uint);

    public const ushort Opcode = WorldPacketOpcodes.LegacyArenaRanking;
    public const int QueryPayloadLength = sizeof(ushort) + sizeof(ushort);
    public const int EntriesOffset = ArenaIdOffset + sizeof(uint);
    public const int FixedNameLength = 32;
    public const int EntryPayloadLength = EntryMeshOffset + sizeof(uint);
    public const int LegacyPageSize = 8;
    public const int LegacyPageSizeMarker = 10;
    public const uint ShowPlayerRankListArenaId = 10;
    public const ushort HistoryUnknown = 6004;
    public const uint TodayUnknown2 = 1;
    public const uint HistoryUnknown2 = 33_942_209;

    public static LegacyArenaRankingPacket Query(LegacyArenaRankingRankType rankType, ushort page)
    {
        return new LegacyArenaRankingPacket(
            rankType,
            page,
            TotalCount: 0,
            PageSizeMarker: 0,
            ArenaId: 0,
            Entries: []);
    }

    public static LegacyArenaRankingPacket CreatePage(
        LegacyArenaRankingRankType rankType,
        ushort page,
        IReadOnlyList<ArenaRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var safePage = page == 0 ? (ushort)1 : page;
        var todayRanks = BuildTodayRanks(records);
        var rankedRecords = rankType == LegacyArenaRankingRankType.History
            ? records
                .OrderByDescending(CurrentHonor)
                .Take(1000)
                .ToArray()
            : records
                .OrderByDescending(record => record.Points)
                .Take(1000)
                .ToArray();

        var offset = checked((safePage - 1) * LegacyPageSize);
        var pageRecords = rankedRecords
            .Skip(offset)
            .Take(LegacyPageSize)
            .Select((record, index) => (record, rank: checked((ushort)(offset + index + 1))))
            .Where(entry => TodayRank(todayRanks, entry.record) < 1000)
            .Select(entry => CreateEntry(rankType, entry.rank, entry.record))
            .ToArray();

        return new LegacyArenaRankingPacket(
            rankType,
            page,
            checked((uint)rankedRecords.Length),
            LegacyPageSizeMarker,
            ShowPlayerRankListArenaId,
            pageRecords);
    }

    public static bool TryParseQuery(PacketFrame frame, out LegacyArenaRankingPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < QueryPayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        packet = Query(
            (LegacyArenaRankingRankType)reader.ReadUInt16(RankTypeOffset),
            reader.ReadUInt16(PageOffset));
        return true;
    }

    public static bool TryParse(PacketFrame frame, out LegacyArenaRankingPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < EntriesOffset ||
            (frame.Payload.Length - EntriesOffset) % EntryPayloadLength != 0 ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        var entryCount = checked((int)((frame.Payload.Length - EntriesOffset) / EntryPayloadLength));
        var entries = new List<LegacyArenaRankingEntry>(entryCount);
        for (var index = 0; index < entryCount; index++)
        {
            var offset = EntriesOffset + (index * EntryPayloadLength);
            entries.Add(new LegacyArenaRankingEntry(
                reader.ReadUInt16(offset + EntryRankOffset),
                reader.ReadCString(offset + EntryNameOffset, FixedNameLength, NameEncoding),
                reader.ReadUInt16(offset + EntryUnknownOffset),
                reader.ReadUInt32(offset + EntryPointsOffset),
                reader.ReadUInt32(offset + EntryClassOffset),
                reader.ReadUInt32(offset + EntryLevelOffset),
                reader.ReadUInt32(offset + EntryUnknown2Offset),
                reader.ReadUInt32(offset + EntryMeshOffset)));
        }

        packet = new LegacyArenaRankingPacket(
            (LegacyArenaRankingRankType)reader.ReadUInt16(RankTypeOffset),
            reader.ReadUInt16(PageOffset),
            reader.ReadUInt32(TotalCountOffset),
            reader.ReadInt32(PageSizeMarkerOffset),
            reader.ReadUInt32(ArenaIdOffset),
            entries);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        ArgumentNullException.ThrowIfNull(Entries);

        var hasResponseHeader = PageSizeMarker != 0 || ArenaId != 0 || TotalCount != 0 || Entries.Count > 0;
        if (!hasResponseHeader)
        {
            var queryWriter = new PacketWriter(Opcode, QueryPayloadLength);
            queryWriter.WriteUInt16(RankTypeOffset, (ushort)RankType);
            queryWriter.WriteUInt16(PageOffset, Page);
            return queryWriter.ToArray();
        }

        var writer = new PacketWriter(Opcode, checked(EntriesOffset + (Entries.Count * EntryPayloadLength)));
        writer.WriteUInt16(RankTypeOffset, (ushort)RankType);
        writer.WriteUInt16(PageOffset, Page);
        writer.WriteUInt32(TotalCountOffset, TotalCount);
        writer.WriteInt32(PageSizeMarkerOffset, PageSizeMarker);
        writer.WriteUInt32(ArenaIdOffset, ArenaId);

        for (var index = 0; index < Entries.Count; index++)
        {
            var entry = Entries[index];
            var offset = EntriesOffset + (index * EntryPayloadLength);
            writer.WriteUInt16(offset + EntryRankOffset, entry.Rank);
            WriteFixedString(writer.Payload.Slice(offset + EntryNameOffset, FixedNameLength), entry.Name);
            writer.WriteUInt16(offset + EntryUnknownOffset, entry.Unknown);
            writer.WriteUInt32(offset + EntryPointsOffset, entry.Points);
            writer.WriteUInt32(offset + EntryClassOffset, entry.Class);
            writer.WriteUInt32(offset + EntryLevelOffset, entry.Level);
            writer.WriteUInt32(offset + EntryUnknown2Offset, entry.Unknown2);
            writer.WriteUInt32(offset + EntryMeshOffset, entry.Mesh);
        }

        return writer.ToArray();
    }

    private static LegacyArenaRankingEntry CreateEntry(
        LegacyArenaRankingRankType rankType,
        ushort rank,
        ArenaRecord record)
    {
        return rankType == LegacyArenaRankingRankType.History
            ? new LegacyArenaRankingEntry(
                rank,
                record.PlayerName,
                HistoryUnknown,
                checked((uint)HistoryHonor(record)),
                checked((uint)Class(record)),
                checked((uint)record.Level),
                HistoryUnknown2,
                checked((uint)Mesh(record)))
            : new LegacyArenaRankingEntry(
                rank,
                record.PlayerName,
                Unknown: 0,
                checked((uint)record.Points),
                checked((uint)Class(record)),
                checked((uint)record.Level),
                TodayUnknown2,
                checked((uint)Mesh(record)));
    }

    private static IReadOnlyDictionary<int, uint> BuildTodayRanks(IReadOnlyList<ArenaRecord> records)
    {
        return records
            .OrderByDescending(record => record.Points)
            .Take(1000)
            .Select((record, index) => (record.PlayerId, Rank: checked((uint)(index + 1))))
            .GroupBy(entry => entry.PlayerId)
            .ToDictionary(group => group.Key, group => group.First().Rank);
    }

    private static uint TodayRank(IReadOnlyDictionary<int, uint> todayRanks, ArenaRecord record)
    {
        return todayRanks.TryGetValue(record.PlayerId, out var rank) ? rank : 0;
    }

    private static int Class(ArenaRecord record)
    {
        return record.Values.Count > 1 ? record.Values[1] : 0;
    }

    private static int Mesh(ArenaRecord record)
    {
        return record.Values.Count > 2 ? record.Values[2] : 0;
    }

    private static int CurrentHonor(ArenaRecord record)
    {
        return record.Values.Count > 4 ? record.Values[4] : 0;
    }

    private static int HistoryHonor(ArenaRecord record)
    {
        return record.Values.Count > 5 ? record.Values[5] : 0;
    }

    private static void WriteFixedString(Span<byte> destination, string value)
    {
        destination.Clear();
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        var encoder = NameEncoding.GetEncoder();
        encoder.Convert(value.AsSpan(), destination, flush: true, out _, out _, out _);
    }
}
