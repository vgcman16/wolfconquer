using System.Buffers;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed record LegacyTeamArenaRank10Entry(
    string Name,
    uint LastSeasonRank,
    uint Mesh,
    uint Class,
    uint Level,
    uint LastSeasonArenaPoints,
    uint LastSeasonWin,
    uint LastSeasonLose);

public sealed record LegacyTeamArenaRank10Packet(
    uint ArenaId,
    IReadOnlyList<LegacyTeamArenaRank10Entry> Entries)
{
    private static readonly Encoding NameEncoding = Encoding.UTF8;

    private const int ArenaIdOffset = 0;
    private const int EntryNameOffset = 0;
    private const int EntryLastSeasonRankOffset = EntryNameOffset + FixedNameLength;
    private const int EntryMeshOffset = EntryLastSeasonRankOffset + sizeof(uint);
    private const int EntryClassOffset = EntryMeshOffset + sizeof(uint);
    private const int EntryLevelOffset = EntryClassOffset + sizeof(uint);
    private const int EntryLastSeasonArenaPointsOffset = EntryLevelOffset + sizeof(uint);
    private const int EntryLastSeasonWinOffset = EntryLastSeasonArenaPointsOffset + sizeof(uint);
    private const int EntryLastSeasonLoseOffset = EntryLastSeasonWinOffset + sizeof(uint);

    public const ushort Opcode = WorldPacketOpcodes.LegacyTeamArenaRank10;
    public const int QueryPayloadLength = 0;
    public const int EntriesOffset = ArenaIdOffset + sizeof(uint);
    public const int FixedNameLength = 32;
    public const int EntryPayloadLength = EntryLastSeasonLoseOffset + sizeof(uint);
    public const uint ShowPlayerRankListArenaId = 10;

    public static byte[] QueryFrameBytes()
    {
        return new PacketWriter(Opcode, QueryPayloadLength).ToArray();
    }

    public static LegacyTeamArenaRank10Packet Create(IReadOnlyList<TeamArenaRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var entries = records
            .OrderByDescending(LastSeasonArenaPoints)
            .Take(10)
            .Select(CreateEntry)
            .ToArray();

        return new LegacyTeamArenaRank10Packet(ShowPlayerRankListArenaId, entries);
    }

    public static bool IsQuery(PacketFrame frame)
    {
        return frame.Header.Opcode == Opcode;
    }

    public static bool TryParse(PacketFrame frame, out LegacyTeamArenaRank10Packet packet)
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
        var entries = new List<LegacyTeamArenaRank10Entry>(entryCount);
        for (var index = 0; index < entryCount; index++)
        {
            var offset = EntriesOffset + (index * EntryPayloadLength);
            entries.Add(new LegacyTeamArenaRank10Entry(
                reader.ReadCString(offset + EntryNameOffset, FixedNameLength, NameEncoding),
                reader.ReadUInt32(offset + EntryLastSeasonRankOffset),
                reader.ReadUInt32(offset + EntryMeshOffset),
                reader.ReadUInt32(offset + EntryClassOffset),
                reader.ReadUInt32(offset + EntryLevelOffset),
                reader.ReadUInt32(offset + EntryLastSeasonArenaPointsOffset),
                reader.ReadUInt32(offset + EntryLastSeasonWinOffset),
                reader.ReadUInt32(offset + EntryLastSeasonLoseOffset)));
        }

        packet = new LegacyTeamArenaRank10Packet(
            reader.ReadUInt32(ArenaIdOffset),
            entries);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        ArgumentNullException.ThrowIfNull(Entries);

        var writer = new PacketWriter(Opcode, checked(EntriesOffset + (Entries.Count * EntryPayloadLength)));
        writer.WriteUInt32(ArenaIdOffset, ArenaId);

        for (var index = 0; index < Entries.Count; index++)
        {
            var entry = Entries[index];
            var offset = EntriesOffset + (index * EntryPayloadLength);
            WriteFixedString(writer.Payload.Slice(offset + EntryNameOffset, FixedNameLength), entry.Name);
            writer.WriteUInt32(offset + EntryLastSeasonRankOffset, entry.LastSeasonRank);
            writer.WriteUInt32(offset + EntryMeshOffset, entry.Mesh);
            writer.WriteUInt32(offset + EntryClassOffset, entry.Class);
            writer.WriteUInt32(offset + EntryLevelOffset, entry.Level);
            writer.WriteUInt32(offset + EntryLastSeasonArenaPointsOffset, entry.LastSeasonArenaPoints);
            writer.WriteUInt32(offset + EntryLastSeasonWinOffset, entry.LastSeasonWin);
            writer.WriteUInt32(offset + EntryLastSeasonLoseOffset, entry.LastSeasonLose);
        }

        return writer.ToArray();
    }

    private static LegacyTeamArenaRank10Entry CreateEntry(TeamArenaRecord record)
    {
        return new LegacyTeamArenaRank10Entry(
            record.PlayerName,
            checked((uint)LastSeasonRank(record)),
            checked((uint)Mesh(record)),
            checked((uint)Class(record)),
            checked((uint)record.Level),
            checked((uint)LastSeasonArenaPoints(record)),
            checked((uint)LastSeasonWin(record)),
            checked((uint)LastSeasonLose(record)));
    }

    private static int Class(TeamArenaRecord record)
    {
        return record.Values.Count > 1 ? record.Values[1] : 0;
    }

    private static int Mesh(TeamArenaRecord record)
    {
        return record.Values.Count > 2 ? record.Values[2] : 0;
    }

    private static int LastSeasonArenaPoints(TeamArenaRecord record)
    {
        return record.Values.Count > 10 ? record.Values[10] : 0;
    }

    private static int LastSeasonWin(TeamArenaRecord record)
    {
        return record.Values.Count > 11 ? record.Values[11] : 0;
    }

    private static int LastSeasonLose(TeamArenaRecord record)
    {
        return record.Values.Count > 12 ? record.Values[12] : 0;
    }

    private static int LastSeasonRank(TeamArenaRecord record)
    {
        return record.Values.Count > 13 ? record.Values[13] : 0;
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
