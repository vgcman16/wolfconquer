using System.Buffers;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed record LegacyArenaRank10Entry(
    uint Uid,
    string Name,
    uint Mesh,
    uint Level,
    uint Class,
    uint LastSeasonRank,
    uint LastSeasonRankMirror,
    uint LastSeasonArenaPoints,
    uint LastSeasonWin,
    uint LastSeasonLose);

public sealed record LegacyArenaRank10Packet(
    uint ArenaId,
    int Marker,
    IReadOnlyList<LegacyArenaRank10Entry> Entries)
{
    private static readonly Encoding NameEncoding = Encoding.UTF8;

    private const int ArenaIdOffset = 0;
    private const int MarkerOffset = ArenaIdOffset + sizeof(uint);
    private const int EntryUidOffset = 0;
    private const int EntryNameOffset = EntryUidOffset + sizeof(uint);
    private const int EntryMeshOffset = EntryNameOffset + FixedNameLength;
    private const int EntryLevelOffset = EntryMeshOffset + sizeof(uint);
    private const int EntryClassOffset = EntryLevelOffset + sizeof(uint);
    private const int EntryLastSeasonRankOffset = EntryClassOffset + sizeof(uint);
    private const int EntryLastSeasonRankMirrorOffset = EntryLastSeasonRankOffset + sizeof(uint);
    private const int EntryLastSeasonArenaPointsOffset = EntryLastSeasonRankMirrorOffset + sizeof(uint);
    private const int EntryLastSeasonWinOffset = EntryLastSeasonArenaPointsOffset + sizeof(uint);
    private const int EntryLastSeasonLoseOffset = EntryLastSeasonWinOffset + sizeof(uint);

    public const ushort Opcode = WorldPacketOpcodes.LegacyArenaRank10;
    public const int QueryPayloadLength = 0;
    public const int EntriesOffset = MarkerOffset + sizeof(int);
    public const int FixedNameLength = 32;
    public const int EntryPayloadLength = EntryLastSeasonLoseOffset + sizeof(uint);
    public const uint ShowPlayerRankListArenaId = 10;
    public const int LegacyMarker = 1;

    public static byte[] QueryFrameBytes()
    {
        return new PacketWriter(Opcode, QueryPayloadLength).ToArray();
    }

    public static LegacyArenaRank10Packet Create(IReadOnlyList<ArenaRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var entries = records
            .OrderByDescending(LastSeasonArenaPoints)
            .Take(10)
            .Select(CreateEntry)
            .ToArray();

        return new LegacyArenaRank10Packet(ShowPlayerRankListArenaId, LegacyMarker, entries);
    }

    public static bool IsQuery(PacketFrame frame)
    {
        return frame.Header.Opcode == Opcode;
    }

    public static bool TryParse(PacketFrame frame, out LegacyArenaRank10Packet packet)
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
        var entries = new List<LegacyArenaRank10Entry>(entryCount);
        for (var index = 0; index < entryCount; index++)
        {
            var offset = EntriesOffset + (index * EntryPayloadLength);
            entries.Add(new LegacyArenaRank10Entry(
                reader.ReadUInt32(offset + EntryUidOffset),
                reader.ReadCString(offset + EntryNameOffset, FixedNameLength, NameEncoding),
                reader.ReadUInt32(offset + EntryMeshOffset),
                reader.ReadUInt32(offset + EntryLevelOffset),
                reader.ReadUInt32(offset + EntryClassOffset),
                reader.ReadUInt32(offset + EntryLastSeasonRankOffset),
                reader.ReadUInt32(offset + EntryLastSeasonRankMirrorOffset),
                reader.ReadUInt32(offset + EntryLastSeasonArenaPointsOffset),
                reader.ReadUInt32(offset + EntryLastSeasonWinOffset),
                reader.ReadUInt32(offset + EntryLastSeasonLoseOffset)));
        }

        packet = new LegacyArenaRank10Packet(
            reader.ReadUInt32(ArenaIdOffset),
            reader.ReadInt32(MarkerOffset),
            entries);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        ArgumentNullException.ThrowIfNull(Entries);

        var writer = new PacketWriter(Opcode, checked(EntriesOffset + (Entries.Count * EntryPayloadLength)));
        writer.WriteUInt32(ArenaIdOffset, ArenaId);
        writer.WriteInt32(MarkerOffset, Marker);

        for (var index = 0; index < Entries.Count; index++)
        {
            var entry = Entries[index];
            var offset = EntriesOffset + (index * EntryPayloadLength);
            writer.WriteUInt32(offset + EntryUidOffset, entry.Uid);
            WriteFixedString(writer.Payload.Slice(offset + EntryNameOffset, FixedNameLength), entry.Name);
            writer.WriteUInt32(offset + EntryMeshOffset, entry.Mesh);
            writer.WriteUInt32(offset + EntryLevelOffset, entry.Level);
            writer.WriteUInt32(offset + EntryClassOffset, entry.Class);
            writer.WriteUInt32(offset + EntryLastSeasonRankOffset, entry.LastSeasonRank);
            writer.WriteUInt32(offset + EntryLastSeasonRankMirrorOffset, entry.LastSeasonRankMirror);
            writer.WriteUInt32(offset + EntryLastSeasonArenaPointsOffset, entry.LastSeasonArenaPoints);
            writer.WriteUInt32(offset + EntryLastSeasonWinOffset, entry.LastSeasonWin);
            writer.WriteUInt32(offset + EntryLastSeasonLoseOffset, entry.LastSeasonLose);
        }

        return writer.ToArray();
    }

    private static LegacyArenaRank10Entry CreateEntry(ArenaRecord record)
    {
        var lastSeasonRank = checked((uint)LastSeasonRank(record));
        return new LegacyArenaRank10Entry(
            checked((uint)record.PlayerId),
            record.PlayerName,
            checked((uint)Mesh(record)),
            checked((uint)record.Level),
            checked((uint)Class(record)),
            lastSeasonRank,
            lastSeasonRank,
            checked((uint)LastSeasonArenaPoints(record)),
            checked((uint)LastSeasonWin(record)),
            checked((uint)LastSeasonLose(record)));
    }

    private static int Class(ArenaRecord record)
    {
        return record.Values.Count > 1 ? record.Values[1] : 0;
    }

    private static int Mesh(ArenaRecord record)
    {
        return record.Values.Count > 2 ? record.Values[2] : 0;
    }

    private static int LastSeasonArenaPoints(ArenaRecord record)
    {
        return record.Values.Count > 12 ? record.Values[12] : 0;
    }

    private static int LastSeasonWin(ArenaRecord record)
    {
        return record.Values.Count > 13 ? record.Values[13] : 0;
    }

    private static int LastSeasonLose(ArenaRecord record)
    {
        return record.Values.Count > 14 ? record.Values[14] : 0;
    }

    private static int LastSeasonRank(ArenaRecord record)
    {
        return record.Values.Count > 15 ? record.Values[15] : 0;
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
