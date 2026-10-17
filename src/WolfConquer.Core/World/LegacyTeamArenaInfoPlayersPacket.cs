using System.Buffers;
using System.Text;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyTeamArenaInfoPlayerKind : uint
{
    Neutral = 0,
    Opponents = 1,
}

public sealed record LegacyTeamArenaInfoPlayerEntry(
    uint Uid,
    uint Level,
    uint Class,
    uint Mesh,
    uint TodayRank,
    uint ArenaPoints,
    string Name);

public sealed record LegacyTeamArenaInfoPlayersPacket(
    uint Mode,
    uint LeaderUid,
    uint Count,
    IReadOnlyList<LegacyTeamArenaInfoPlayerEntry> Entries)
{
    private static readonly Encoding NameEncoding = Encoding.UTF8;

    private const int ModeOffset = 0;
    private const int LeaderUidOffset = ModeOffset + sizeof(uint);
    private const int CountOffset = LeaderUidOffset + sizeof(uint);
    private const int EntryUidOffset = 0;
    private const int EntryLevelOffset = EntryUidOffset + sizeof(uint);
    private const int EntryClassOffset = EntryLevelOffset + sizeof(uint);
    private const int EntryMeshOffset = EntryClassOffset + sizeof(uint);
    private const int EntryTodayRankOffset = EntryMeshOffset + sizeof(uint);
    private const int EntryArenaPointsOffset = EntryTodayRankOffset + sizeof(uint);
    private const int EntryNameOffset = EntryArenaPointsOffset + sizeof(uint);

    public const ushort Opcode = WorldPacketOpcodes.LegacyTeamArenaInfoPlayers;
    public const int QueryPayloadLength = 0;
    public const int EntriesOffset = CountOffset + sizeof(uint);
    public const int FixedNameLength = 32;
    public const int EntryPayloadLength = EntryNameOffset + FixedNameLength;

    public static LegacyTeamArenaInfoPlayersPacket Empty(LegacyTeamArenaInfoPlayerKind mode, uint leaderUid)
    {
        return new LegacyTeamArenaInfoPlayersPacket((uint)mode, leaderUid, Count: 0, Entries: []);
    }

    public static bool IsQuery(PacketFrame frame)
    {
        return frame.Header.Opcode == Opcode && frame.Payload.Length == QueryPayloadLength;
    }

    public static bool TryParse(PacketFrame frame, out LegacyTeamArenaInfoPlayersPacket packet)
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
        var count = reader.ReadUInt32(CountOffset);
        if (count != entryCount)
        {
            return false;
        }

        var entries = new List<LegacyTeamArenaInfoPlayerEntry>(entryCount);
        for (var index = 0; index < entryCount; index++)
        {
            var offset = EntriesOffset + (index * EntryPayloadLength);
            entries.Add(new LegacyTeamArenaInfoPlayerEntry(
                reader.ReadUInt32(offset + EntryUidOffset),
                reader.ReadUInt32(offset + EntryLevelOffset),
                reader.ReadUInt32(offset + EntryClassOffset),
                reader.ReadUInt32(offset + EntryMeshOffset),
                reader.ReadUInt32(offset + EntryTodayRankOffset),
                reader.ReadUInt32(offset + EntryArenaPointsOffset),
                reader.ReadCString(offset + EntryNameOffset, FixedNameLength, NameEncoding)));
        }

        packet = new LegacyTeamArenaInfoPlayersPacket(
            reader.ReadUInt32(ModeOffset),
            reader.ReadUInt32(LeaderUidOffset),
            count,
            entries);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        ArgumentNullException.ThrowIfNull(Entries);
        if (Count != Entries.Count)
        {
            throw new InvalidOperationException("Legacy TeamArena info-player count must match the number of entries.");
        }

        var writer = new PacketWriter(Opcode, checked(EntriesOffset + (Entries.Count * EntryPayloadLength)));
        writer.WriteUInt32(ModeOffset, Mode);
        writer.WriteUInt32(LeaderUidOffset, LeaderUid);
        writer.WriteUInt32(CountOffset, Count);

        for (var index = 0; index < Entries.Count; index++)
        {
            var entry = Entries[index];
            var offset = EntriesOffset + (index * EntryPayloadLength);
            writer.WriteUInt32(offset + EntryUidOffset, entry.Uid);
            writer.WriteUInt32(offset + EntryLevelOffset, entry.Level);
            writer.WriteUInt32(offset + EntryClassOffset, entry.Class);
            writer.WriteUInt32(offset + EntryMeshOffset, entry.Mesh);
            writer.WriteUInt32(offset + EntryTodayRankOffset, entry.TodayRank);
            writer.WriteUInt32(offset + EntryArenaPointsOffset, entry.ArenaPoints);
            WriteFixedString(writer.Payload.Slice(offset + EntryNameOffset, FixedNameLength), entry.Name);
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
