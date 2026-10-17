using System.Buffers;
using System.Text;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyArenaWatcherMode : ushort
{
    RequestView = 0,
    QuitButton = 1,
    Watchers = 2,
    Leave = 3,
    Fighters = 4,
}

public sealed record LegacyArenaWatcherEntry(
    uint Mesh,
    string Name,
    uint Uid,
    uint Level,
    uint Class,
    uint TodayRank);

public sealed record LegacyArenaWatchersPacket(
    LegacyArenaWatcherMode Mode,
    uint DwParam,
    uint Uid,
    uint Count,
    uint Player1Cheers,
    uint Player2Cheers,
    IReadOnlyList<LegacyArenaWatcherEntry> Entries,
    bool IsQuery = false)
{
    private static readonly Encoding NameEncoding = Encoding.UTF8;

    private const int ModeOffset = 0;
    private const int DwParamOffset = ModeOffset + sizeof(ushort);
    private const int UidOffset = DwParamOffset + sizeof(uint);
    private const int CountOffset = UidOffset + sizeof(uint);
    private const int Player1CheersOffset = CountOffset + sizeof(uint);
    private const int Player2CheersOffset = Player1CheersOffset + sizeof(uint);
    private const int EntryMeshOffset = 0;
    private const int EntryNameOffset = EntryMeshOffset + sizeof(uint);
    private const int EntryUidOffset = EntryNameOffset + FixedNameLength;
    private const int EntryLevelOffset = EntryUidOffset + sizeof(uint);
    private const int EntryClassOffset = EntryLevelOffset + sizeof(uint);
    private const int EntryTodayRankOffset = EntryClassOffset + sizeof(uint);

    public const ushort Opcode = WorldPacketOpcodes.LegacyArenaWatchers;
    public const int QueryPayloadLength = UidOffset + sizeof(uint);
    public const int HeaderPayloadLength = Player2CheersOffset + sizeof(uint);
    public const int EntriesOffset = HeaderPayloadLength;
    public const int FixedNameLength = 32;
    public const int EntryPayloadLength = EntryTodayRankOffset + sizeof(uint) + 16;

    public static LegacyArenaWatchersPacket Query(LegacyArenaWatcherMode mode, uint uid)
    {
        return new LegacyArenaWatchersPacket(
            mode,
            DwParam: 0,
            uid,
            Count: 0,
            Player1Cheers: 0,
            Player2Cheers: 0,
            Entries: [],
            IsQuery: true);
    }

    public static LegacyArenaWatchersPacket Create(
        LegacyArenaWatcherMode mode,
        uint dwParam,
        uint uid,
        uint count,
        uint player1Cheers,
        uint player2Cheers,
        IReadOnlyList<LegacyArenaWatcherEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return new LegacyArenaWatchersPacket(
            mode,
            dwParam,
            uid,
            count,
            player1Cheers,
            player2Cheers,
            entries);
    }

    public static bool TryParseQuery(PacketFrame frame, out LegacyArenaWatchersPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != QueryPayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        packet = Query(
            (LegacyArenaWatcherMode)reader.ReadUInt16(ModeOffset),
            reader.ReadUInt32(UidOffset));
        return true;
    }

    public static bool TryParse(PacketFrame frame, out LegacyArenaWatchersPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < HeaderPayloadLength ||
            (frame.Payload.Length - HeaderPayloadLength) % EntryPayloadLength != 0 ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        var entryCount = (frame.Payload.Length - HeaderPayloadLength) / EntryPayloadLength;
        var entries = new LegacyArenaWatcherEntry[entryCount];
        for (var index = 0; index < entries.Length; index++)
        {
            var offset = EntriesOffset + (index * EntryPayloadLength);
            entries[index] = new LegacyArenaWatcherEntry(
                reader.ReadUInt32(offset + EntryMeshOffset),
                reader.ReadCString(offset + EntryNameOffset, FixedNameLength, NameEncoding),
                reader.ReadUInt32(offset + EntryUidOffset),
                reader.ReadUInt32(offset + EntryLevelOffset),
                reader.ReadUInt32(offset + EntryClassOffset),
                reader.ReadUInt32(offset + EntryTodayRankOffset));
        }

        packet = new LegacyArenaWatchersPacket(
            (LegacyArenaWatcherMode)reader.ReadUInt16(ModeOffset),
            reader.ReadUInt32(DwParamOffset),
            reader.ReadUInt32(UidOffset),
            reader.ReadUInt32(CountOffset),
            reader.ReadUInt32(Player1CheersOffset),
            reader.ReadUInt32(Player2CheersOffset),
            entries);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var entries = Entries ?? Array.Empty<LegacyArenaWatcherEntry>();
        var writer = new PacketWriter(
            Opcode,
            IsQuery ? QueryPayloadLength : checked(HeaderPayloadLength + (entries.Count * EntryPayloadLength)));

        writer.WriteUInt16(ModeOffset, (ushort)Mode);
        writer.WriteUInt32(DwParamOffset, DwParam);
        writer.WriteUInt32(UidOffset, Uid);
        if (IsQuery)
        {
            return writer.ToArray();
        }

        writer.WriteUInt32(CountOffset, Count);
        writer.WriteUInt32(Player1CheersOffset, Player1Cheers);
        writer.WriteUInt32(Player2CheersOffset, Player2Cheers);

        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            var offset = EntriesOffset + (index * EntryPayloadLength);
            writer.WriteUInt32(offset + EntryMeshOffset, entry.Mesh);
            WriteFixedString(writer.Payload.Slice(offset + EntryNameOffset, FixedNameLength), entry.Name);
            writer.WriteUInt32(offset + EntryUidOffset, entry.Uid);
            writer.WriteUInt32(offset + EntryLevelOffset, entry.Level);
            writer.WriteUInt32(offset + EntryClassOffset, entry.Class);
            writer.WriteUInt32(offset + EntryTodayRankOffset, entry.TodayRank);
        }

        return writer.ToArray();
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
