using System.Buffers;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed record LegacyTeamArenaRankingEntry(
    uint Rank,
    int Unknown,
    uint Points,
    uint Class,
    uint Level,
    byte Gender,
    string Name,
    byte ReservedByte,
    ushort Reserved);

public sealed record LegacyTeamArenaRankingPacket(
    ushort Page,
    ushort Count,
    uint ArenaId,
    IReadOnlyList<LegacyTeamArenaRankingEntry> Entries)
{
    private static readonly Encoding NameEncoding = Encoding.UTF8;

    private const int PageOffset = 0;
    private const int CountOffset = PageOffset + sizeof(ushort);
    private const int ArenaIdOffset = CountOffset + sizeof(ushort);
    private const int EntryRankOffset = 0;
    private const int EntryUnknownOffset = EntryRankOffset + sizeof(uint);
    private const int EntryPointsOffset = EntryUnknownOffset + sizeof(int);
    private const int EntryClassOffset = EntryPointsOffset + sizeof(uint);
    private const int EntryLevelOffset = EntryClassOffset + sizeof(uint);
    private const int EntryGenderOffset = EntryLevelOffset + sizeof(uint);
    private const int EntryNameOffset = EntryGenderOffset + sizeof(byte);
    private const int EntryReservedByteOffset = EntryNameOffset + FixedNameLength;
    private const int EntryReservedOffset = EntryReservedByteOffset + sizeof(byte);

    public const ushort Opcode = WorldPacketOpcodes.LegacyTeamArenaRanking;
    public const int QueryPayloadLength = sizeof(ushort);
    public const int EntriesOffset = ArenaIdOffset + sizeof(uint);
    public const int FixedNameLength = 32;
    public const int EntryPayloadLength = EntryReservedOffset + sizeof(ushort);
    public const int LegacyPageSize = 10;
    public const int LegacyUnknown = 2_979_015;
    public const uint ShowPlayerRankListArenaId = 10;

    public static LegacyTeamArenaRankingPacket Query(ushort page)
    {
        return new LegacyTeamArenaRankingPacket(page, Count: 0, ArenaId: 0, Entries: []);
    }

    public static LegacyTeamArenaRankingPacket CreatePage(ushort page, IReadOnlyList<TeamArenaRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var safePage = page == 0 ? (ushort)1 : page;
        var rankedRecords = records
            .OrderByDescending(record => record.Points)
            .Take(1000)
            .ToArray();

        var offset = checked((safePage - 1) * LegacyPageSize);
        var entries = rankedRecords
            .Skip(offset)
            .Take(LegacyPageSize)
            .Select((record, index) => (record, rank: checked((uint)(offset + index + 1))))
            .Where(entry => entry.rank < 1000)
            .Select(entry => CreateEntry(entry.rank, entry.record))
            .ToArray();

        return new LegacyTeamArenaRankingPacket(
            page,
            checked((ushort)rankedRecords.Length),
            ShowPlayerRankListArenaId,
            entries);
    }

    public static bool TryParseQuery(PacketFrame frame, out LegacyTeamArenaRankingPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != QueryPayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        packet = Query(reader.ReadUInt16(PageOffset));
        return true;
    }

    public static bool TryParse(PacketFrame frame, out LegacyTeamArenaRankingPacket packet)
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
        var entries = new List<LegacyTeamArenaRankingEntry>(entryCount);
        for (var index = 0; index < entryCount; index++)
        {
            var offset = EntriesOffset + (index * EntryPayloadLength);
            entries.Add(new LegacyTeamArenaRankingEntry(
                reader.ReadUInt32(offset + EntryRankOffset),
                reader.ReadInt32(offset + EntryUnknownOffset),
                reader.ReadUInt32(offset + EntryPointsOffset),
                reader.ReadUInt32(offset + EntryClassOffset),
                reader.ReadUInt32(offset + EntryLevelOffset),
                reader.Payload[offset + EntryGenderOffset],
                reader.ReadCString(offset + EntryNameOffset, FixedNameLength, NameEncoding),
                reader.Payload[offset + EntryReservedByteOffset],
                reader.ReadUInt16(offset + EntryReservedOffset)));
        }

        packet = new LegacyTeamArenaRankingPacket(
            reader.ReadUInt16(PageOffset),
            reader.ReadUInt16(CountOffset),
            reader.ReadUInt32(ArenaIdOffset),
            entries);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        ArgumentNullException.ThrowIfNull(Entries);

        if (ArenaId == 0 && Count == 0 && Entries.Count == 0)
        {
            var queryWriter = new PacketWriter(Opcode, QueryPayloadLength);
            queryWriter.WriteUInt16(PageOffset, Page);
            return queryWriter.ToArray();
        }

        var writer = new PacketWriter(Opcode, checked(EntriesOffset + (Entries.Count * EntryPayloadLength)));
        writer.WriteUInt16(PageOffset, Page);
        writer.WriteUInt16(CountOffset, Count);
        writer.WriteUInt32(ArenaIdOffset, ArenaId);

        for (var index = 0; index < Entries.Count; index++)
        {
            var entry = Entries[index];
            var offset = EntriesOffset + (index * EntryPayloadLength);
            writer.WriteUInt32(offset + EntryRankOffset, entry.Rank);
            writer.WriteInt32(offset + EntryUnknownOffset, entry.Unknown);
            writer.WriteUInt32(offset + EntryPointsOffset, entry.Points);
            writer.WriteUInt32(offset + EntryClassOffset, entry.Class);
            writer.WriteUInt32(offset + EntryLevelOffset, entry.Level);
            writer.Payload[offset + EntryGenderOffset] = entry.Gender;
            WriteFixedString(writer.Payload.Slice(offset + EntryNameOffset, FixedNameLength), entry.Name);
            writer.Payload[offset + EntryReservedByteOffset] = entry.ReservedByte;
            writer.WriteUInt16(offset + EntryReservedOffset, entry.Reserved);
        }

        return writer.ToArray();
    }

    private static LegacyTeamArenaRankingEntry CreateEntry(uint rank, TeamArenaRecord record)
    {
        return new LegacyTeamArenaRankingEntry(
            rank,
            LegacyUnknown,
            checked((uint)record.Points),
            checked((uint)Class(record)),
            checked((uint)record.Level),
            Gender(record),
            record.PlayerName,
            ReservedByte: 0,
            Reserved: 0);
    }

    private static int Class(TeamArenaRecord record)
    {
        return record.Values.Count > 1 ? record.Values[1] : 0;
    }

    private static int Mesh(TeamArenaRecord record)
    {
        return record.Values.Count > 2 ? record.Values[2] : 0;
    }

    private static byte Gender(TeamArenaRecord record)
    {
        return Mesh(record) % 10 >= 3 ? (byte)0 : (byte)1;
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
