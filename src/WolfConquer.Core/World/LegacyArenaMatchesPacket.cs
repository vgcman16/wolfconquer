using System.Buffers;
using System.Text;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed record LegacyArenaMatchesEntry(
    uint FirstUid,
    string FirstName,
    uint FirstMemberCount,
    uint SecondUid,
    string SecondName,
    uint SecondMemberCount);

public sealed record LegacyArenaMatchesPlayerEntry(
    uint Uid,
    uint ServerId,
    uint Mesh,
    string Name,
    uint Level,
    uint Class,
    uint Reserved,
    uint TodayRank,
    uint ArenaPoints,
    uint TotalLose,
    uint TodayBattles,
    uint CurrentHonor,
    uint HistoryHonor);

public sealed record LegacyArenaMatchesRegularEntry(
    LegacyArenaMatchesPlayerEntry First,
    LegacyArenaMatchesPlayerEntry Second);

public readonly record struct LegacyArenaMatchesPacket(
    ushort Opcode,
    uint Page,
    uint ArenaId,
    uint MatchCount,
    uint PlayerCount,
    uint Reserved,
    uint PageCount,
    IReadOnlyList<LegacyArenaMatchesEntry> Entries,
    IReadOnlyList<LegacyArenaMatchesRegularEntry> RegularEntries)
{
    private static readonly Encoding NameEncoding = Encoding.UTF8;

    private const int PageOffset = 0;
    private const int ArenaIdOffset = PageOffset + sizeof(uint);
    private const int MatchCountOffset = ArenaIdOffset + sizeof(uint);
    private const int PlayerCountOffset = MatchCountOffset + sizeof(uint);
    private const int ReservedOffset = PlayerCountOffset + sizeof(uint);
    private const int PageCountOffset = ReservedOffset + sizeof(uint);
    private const int EntryFirstUidOffset = 0;
    private const int EntryFirstNameOffset = EntryFirstUidOffset + sizeof(uint);
    private const int EntryFirstMemberCountOffset = EntryFirstNameOffset + FixedNameLength;
    private const int EntrySecondUidOffset = EntryFirstMemberCountOffset + sizeof(uint);
    private const int EntrySecondNameOffset = EntrySecondUidOffset + sizeof(uint);
    private const int EntrySecondMemberCountOffset = EntrySecondNameOffset + FixedNameLength;
    private const int RegularPlayerUidOffset = 0;
    private const int RegularPlayerServerIdOffset = RegularPlayerUidOffset + sizeof(uint);
    private const int RegularPlayerMeshOffset = RegularPlayerServerIdOffset + sizeof(uint);
    private const int RegularPlayerNameOffset = RegularPlayerMeshOffset + sizeof(uint);
    private const int RegularPlayerLevelOffset = RegularPlayerNameOffset + FixedNameLength;
    private const int RegularPlayerClassOffset = RegularPlayerLevelOffset + sizeof(uint);
    private const int RegularPlayerReservedOffset = RegularPlayerClassOffset + sizeof(uint);
    private const int RegularPlayerTodayRankOffset = RegularPlayerReservedOffset + sizeof(uint);
    private const int RegularPlayerArenaPointsOffset = RegularPlayerTodayRankOffset + sizeof(uint);
    private const int RegularPlayerTotalLoseOffset = RegularPlayerArenaPointsOffset + sizeof(uint);
    private const int RegularPlayerTodayBattlesOffset = RegularPlayerTotalLoseOffset + sizeof(uint);
    private const int RegularPlayerCurrentHonorOffset = RegularPlayerTodayBattlesOffset + sizeof(uint);
    private const int RegularPlayerHistoryHonorOffset = RegularPlayerCurrentHonorOffset + sizeof(uint);

    public const ushort ArenaOpcode = WorldPacketOpcodes.LegacyArenaMatches;
    public const ushort TeamArenaOpcode = WorldPacketOpcodes.LegacyTeamArenaMatches;
    public const int QueryPayloadLength = sizeof(uint);
    public const int HeaderPayloadLength = PageCountOffset + sizeof(uint);
    public const int EntriesOffset = HeaderPayloadLength;
    public const int FixedNameLength = 32;
    public const int EntryPayloadLength = EntrySecondMemberCountOffset + sizeof(uint);
    public const int RegularPlayerPayloadLength = RegularPlayerHistoryHonorOffset + sizeof(uint);
    public const int RegularEntryPayloadLength = RegularPlayerPayloadLength * 2;
    public const int LegacyPageSize = 6;
    public const uint QualifierListArenaId = 6;

    public static LegacyArenaMatchesPacket Query(ushort opcode, uint page)
    {
        return new LegacyArenaMatchesPacket(
            opcode,
            page,
            ArenaId: 0,
            MatchCount: 0,
            PlayerCount: 0,
            Reserved: 0,
            PageCount: 0,
            Entries: [],
            RegularEntries: []);
    }

    public static LegacyArenaMatchesPacket Empty(ushort opcode, uint page)
    {
        return new LegacyArenaMatchesPacket(
            opcode,
            page,
            QualifierListArenaId,
            MatchCount: 0,
            PlayerCount: 0,
            Reserved: 0,
            PageCount: CalculateLegacyPageCount(matchCount: 0, page),
            Entries: [],
            RegularEntries: []);
    }

    public static LegacyArenaMatchesPacket CreatePage(
        ushort opcode,
        uint page,
        IReadOnlyList<LegacyArenaMatchesEntry> entries,
        uint playerCount)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return new LegacyArenaMatchesPacket(
            opcode,
            page,
            QualifierListArenaId,
            checked((uint)entries.Count),
            playerCount,
            Reserved: 0,
            CalculateLegacyPageCount(checked((uint)entries.Count), page),
            SliceLegacyPage(page, entries),
            RegularEntries: []);
    }

    public static LegacyArenaMatchesPacket CreateArenaPage(
        uint page,
        IReadOnlyList<LegacyArenaMatchesRegularEntry> entries,
        uint playerCount)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return new LegacyArenaMatchesPacket(
            ArenaOpcode,
            page,
            QualifierListArenaId,
            checked((uint)entries.Count),
            playerCount,
            Reserved: 0,
            CalculateLegacyPageCount(checked((uint)entries.Count), page),
            Entries: [],
            SliceLegacyPage(page, entries));
    }

    public static bool TryParseQuery(PacketFrame frame, out LegacyArenaMatchesPacket packet)
    {
        packet = default;

        if (!IsSupportedOpcode(frame.Header.Opcode) ||
            frame.Payload.Length < QueryPayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        packet = Query(frame.Header.Opcode, reader.ReadUInt32(PageOffset));
        return true;
    }

    public static bool TryParse(PacketFrame frame, out LegacyArenaMatchesPacket packet)
    {
        packet = default;

        if (!IsSupportedOpcode(frame.Header.Opcode) ||
            frame.Payload.Length < HeaderPayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        var entryPayloadLength = frame.Header.Opcode == ArenaOpcode
            ? RegularEntryPayloadLength
            : EntryPayloadLength;
        if ((frame.Payload.Length - HeaderPayloadLength) % entryPayloadLength != 0)
        {
            return false;
        }

        var entryCount = (frame.Payload.Length - HeaderPayloadLength) / entryPayloadLength;
        var entries = Array.Empty<LegacyArenaMatchesEntry>();
        var regularEntries = Array.Empty<LegacyArenaMatchesRegularEntry>();
        if (frame.Header.Opcode == ArenaOpcode)
        {
            regularEntries = new LegacyArenaMatchesRegularEntry[entryCount];
            for (var index = 0; index < regularEntries.Length; index++)
            {
                var offset = EntriesOffset + (index * RegularEntryPayloadLength);
                regularEntries[index] = new LegacyArenaMatchesRegularEntry(
                    ReadRegularPlayer(reader, offset),
                    ReadRegularPlayer(reader, offset + RegularPlayerPayloadLength));
            }
        }
        else
        {
            entries = new LegacyArenaMatchesEntry[entryCount];
            for (var index = 0; index < entries.Length; index++)
            {
                var offset = EntriesOffset + (index * EntryPayloadLength);
                entries[index] = new LegacyArenaMatchesEntry(
                    reader.ReadUInt32(offset + EntryFirstUidOffset),
                    reader.ReadCString(offset + EntryFirstNameOffset, FixedNameLength, NameEncoding),
                    reader.ReadUInt32(offset + EntryFirstMemberCountOffset),
                    reader.ReadUInt32(offset + EntrySecondUidOffset),
                    reader.ReadCString(offset + EntrySecondNameOffset, FixedNameLength, NameEncoding),
                    reader.ReadUInt32(offset + EntrySecondMemberCountOffset));
            }
        }

        packet = new LegacyArenaMatchesPacket(
            frame.Header.Opcode,
            reader.ReadUInt32(PageOffset),
            reader.ReadUInt32(ArenaIdOffset),
            reader.ReadUInt32(MatchCountOffset),
            reader.ReadUInt32(PlayerCountOffset),
            reader.ReadUInt32(ReservedOffset),
            reader.ReadUInt32(PageCountOffset),
            entries,
            regularEntries);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        if (!IsSupportedOpcode(Opcode))
        {
            throw new ArgumentOutOfRangeException(nameof(Opcode), Opcode, "Unsupported legacy arena matches opcode.");
        }

        var entries = Entries ?? Array.Empty<LegacyArenaMatchesEntry>();
        var regularEntries = RegularEntries ?? Array.Empty<LegacyArenaMatchesRegularEntry>();
        var isQuery = ArenaId == 0 &&
            MatchCount == 0 &&
            PlayerCount == 0 &&
            Reserved == 0 &&
            PageCount == 0 &&
            entries.Count == 0 &&
            regularEntries.Count == 0;
        var writer = new PacketWriter(
            Opcode,
            isQuery ? QueryPayloadLength : GetPayloadLength(Opcode, entries.Count, regularEntries.Count));
        writer.WriteUInt32(PageOffset, Page);
        if (isQuery)
        {
            return writer.ToArray();
        }

        writer.WriteUInt32(ArenaIdOffset, ArenaId);
        writer.WriteUInt32(MatchCountOffset, MatchCount);
        writer.WriteUInt32(PlayerCountOffset, PlayerCount);
        writer.WriteUInt32(ReservedOffset, Reserved);
        writer.WriteUInt32(PageCountOffset, PageCount);

        if (Opcode == ArenaOpcode)
        {
            for (var index = 0; index < regularEntries.Count; index++)
            {
                var entry = regularEntries[index];
                var offset = EntriesOffset + (index * RegularEntryPayloadLength);
                WriteRegularPlayer(writer, offset, entry.First);
                WriteRegularPlayer(writer, offset + RegularPlayerPayloadLength, entry.Second);
            }
        }
        else
        {
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                var offset = EntriesOffset + (index * EntryPayloadLength);
                writer.WriteUInt32(offset + EntryFirstUidOffset, entry.FirstUid);
                WriteFixedString(writer.Payload.Slice(offset + EntryFirstNameOffset, FixedNameLength), entry.FirstName);
                writer.WriteUInt32(offset + EntryFirstMemberCountOffset, entry.FirstMemberCount);
                writer.WriteUInt32(offset + EntrySecondUidOffset, entry.SecondUid);
                WriteFixedString(writer.Payload.Slice(offset + EntrySecondNameOffset, FixedNameLength), entry.SecondName);
                writer.WriteUInt32(offset + EntrySecondMemberCountOffset, entry.SecondMemberCount);
            }
        }

        return writer.ToArray();
    }

    private static IReadOnlyList<T> SliceLegacyPage<T>(
        uint page,
        IReadOnlyList<T> entries)
    {
        var start = checked((int)(page / LegacyPageSize) * LegacyPageSize);
        if (start >= entries.Count)
        {
            return [];
        }

        var count = Math.Min(LegacyPageSize, entries.Count - start);
        var pageEntries = new T[count];
        for (var index = 0; index < pageEntries.Length; index++)
        {
            pageEntries[index] = entries[start + index];
        }

        return pageEntries;
    }

    private static uint CalculateLegacyPageCount(uint matchCount, uint page)
    {
        var pageIndex = page == 0 ? 0 : page - 1;
        return matchCount >= pageIndex ? matchCount - pageIndex : 0;
    }

    private static bool IsSupportedOpcode(ushort opcode)
    {
        return opcode is ArenaOpcode or TeamArenaOpcode;
    }

    private static int GetPayloadLength(ushort opcode, int teamEntryCount, int regularEntryCount)
    {
        return opcode == ArenaOpcode
            ? checked(HeaderPayloadLength + (regularEntryCount * RegularEntryPayloadLength))
            : checked(HeaderPayloadLength + (teamEntryCount * EntryPayloadLength));
    }

    private static LegacyArenaMatchesPlayerEntry ReadRegularPlayer(PacketReader reader, int offset)
    {
        return new LegacyArenaMatchesPlayerEntry(
            reader.ReadUInt32(offset + RegularPlayerUidOffset),
            reader.ReadUInt32(offset + RegularPlayerServerIdOffset),
            reader.ReadUInt32(offset + RegularPlayerMeshOffset),
            reader.ReadCString(offset + RegularPlayerNameOffset, FixedNameLength, NameEncoding),
            reader.ReadUInt32(offset + RegularPlayerLevelOffset),
            reader.ReadUInt32(offset + RegularPlayerClassOffset),
            reader.ReadUInt32(offset + RegularPlayerReservedOffset),
            reader.ReadUInt32(offset + RegularPlayerTodayRankOffset),
            reader.ReadUInt32(offset + RegularPlayerArenaPointsOffset),
            reader.ReadUInt32(offset + RegularPlayerTotalLoseOffset),
            reader.ReadUInt32(offset + RegularPlayerTodayBattlesOffset),
            reader.ReadUInt32(offset + RegularPlayerCurrentHonorOffset),
            reader.ReadUInt32(offset + RegularPlayerHistoryHonorOffset));
    }

    private static void WriteRegularPlayer(
        PacketWriter writer,
        int offset,
        LegacyArenaMatchesPlayerEntry player)
    {
        writer.WriteUInt32(offset + RegularPlayerUidOffset, player.Uid);
        writer.WriteUInt32(offset + RegularPlayerServerIdOffset, player.ServerId);
        writer.WriteUInt32(offset + RegularPlayerMeshOffset, player.Mesh);
        WriteFixedString(writer.Payload.Slice(offset + RegularPlayerNameOffset, FixedNameLength), player.Name);
        writer.WriteUInt32(offset + RegularPlayerLevelOffset, player.Level);
        writer.WriteUInt32(offset + RegularPlayerClassOffset, player.Class);
        writer.WriteUInt32(offset + RegularPlayerReservedOffset, player.Reserved);
        writer.WriteUInt32(offset + RegularPlayerTodayRankOffset, player.TodayRank);
        writer.WriteUInt32(offset + RegularPlayerArenaPointsOffset, player.ArenaPoints);
        writer.WriteUInt32(offset + RegularPlayerTotalLoseOffset, player.TotalLose);
        writer.WriteUInt32(offset + RegularPlayerTodayBattlesOffset, player.TodayBattles);
        writer.WriteUInt32(offset + RegularPlayerCurrentHonorOffset, player.CurrentHonor);
        writer.WriteUInt32(offset + RegularPlayerHistoryHonorOffset, player.HistoryHonor);
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
