using System.Buffers;
using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyNobilityAction : uint
{
    Donate = 1,
    RankListen = 2,
    Icon = 3,
    NobilityInformation = 4,
}

public enum LegacyNobilityDonationType : byte
{
    Money = 0,
    ConquerPoints = 1,
}

public readonly record struct LegacyNobilityRequestPacket(
    LegacyNobilityAction Action,
    ulong Value,
    byte DonationType)
{
    private const int ActionOffset = 0;
    private const int CompactValueOffset = ActionOffset + sizeof(uint);
    private const int CompactDonationTypeOffset = CompactValueOffset + sizeof(uint);
    private const int CompactPayloadLength = CompactDonationTypeOffset + sizeof(byte);
    private const int LegacyValueOffset = ActionOffset + sizeof(uint);
    private const int LegacyReservedLength = 88;
    private const int LegacyDonationTypeOffset = LegacyValueOffset + sizeof(ulong) + LegacyReservedLength;
    public const int LegacyPayloadLength = LegacyDonationTypeOffset + sizeof(byte);

    public const ushort Opcode = WorldPacketOpcodes.LegacyNobilityIcon;

    public static bool TryParse(PacketFrame frame, out LegacyNobilityRequestPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < CompactPayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        var action = (LegacyNobilityAction)reader.ReadUInt32(ActionOffset);
        if (action is not (LegacyNobilityAction.Donate or
            LegacyNobilityAction.RankListen or
            LegacyNobilityAction.Icon or
            LegacyNobilityAction.NobilityInformation))
        {
            return false;
        }

        if (reader.Payload.Length >= LegacyPayloadLength)
        {
            packet = new LegacyNobilityRequestPacket(
                action,
                reader.ReadUInt64(LegacyValueOffset),
                reader.Payload[LegacyDonationTypeOffset]);
            return true;
        }

        packet = new LegacyNobilityRequestPacket(
            action,
            reader.ReadUInt32(CompactValueOffset),
            reader.Payload[CompactDonationTypeOffset]);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, LegacyPayloadLength);
        writer.WriteUInt32(ActionOffset, (uint)Action);
        writer.WriteUInt64(LegacyValueOffset, Value);
        writer.Payload[LegacyDonationTypeOffset] = DonationType;
        return writer.ToArray();
    }
}

public readonly record struct LegacyNobilityRankListPacket(
    ushort Page,
    ushort TotalPages,
    ushort TotalCount,
    IReadOnlyList<LegacyNobilityRankListEntry> Entries)
{
    private const int ActionOffset = 0;
    private const int PageOffset = ActionOffset + sizeof(uint);
    private const int TotalPagesOffset = PageOffset + sizeof(ushort);
    private const int TotalCountOffset = TotalPagesOffset + sizeof(ushort);
    private const int ReservedOffset = TotalCountOffset + sizeof(ushort);
    private const int MarkerOffset = ReservedOffset + 102;
    private const int EntriesOffset = MarkerOffset + sizeof(uint);
    private const int EntryUidOffset = 0;
    private const int EntryGenderOffset = EntryUidOffset + sizeof(uint);
    private const int EntryMeshOffset = EntryGenderOffset + sizeof(uint);
    private const int EntryNameOffset = EntryMeshOffset + sizeof(uint);
    private const int EntryNameLength = 32;
    private const int EntryReservedOffset = EntryNameOffset + EntryNameLength;
    private const int EntryDonationOffset = EntryReservedOffset + sizeof(uint);
    private const int EntryRankOffset = EntryDonationOffset + sizeof(ulong);
    private const int EntryPositionOffset = EntryRankOffset + sizeof(uint);
    private const int EntryLength = EntryPositionOffset + sizeof(int);
    private const int HeaderPayloadLength = EntriesOffset;

    public const ushort Opcode = WorldPacketOpcodes.LegacyNobilityIcon;
    public const uint Marker = 1;

    public LegacyNobilityRankListPacket(ushort page, ushort totalPages, ushort totalCount)
        : this(page, totalPages, totalCount, Array.Empty<LegacyNobilityRankListEntry>())
    {
    }

    public static LegacyNobilityRankListPacket Empty(ushort page)
    {
        return new LegacyNobilityRankListPacket(page, 0, 0);
    }

    public static LegacyNobilityRankListPacket FromEntries(
        ushort page,
        IReadOnlyList<LegacyNobilityRankListEntry> rankedEntries)
    {
        ArgumentNullException.ThrowIfNull(rankedEntries);

        var pageStart = checked(page * 10);
        var pageEntries = pageStart >= rankedEntries.Count
            ? Array.Empty<LegacyNobilityRankListEntry>()
            : rankedEntries.Skip(pageStart).Take(10).ToArray();
        return new LegacyNobilityRankListPacket(
            page,
            TotalPages: checked((ushort)(rankedEntries.Count / 10)),
            TotalCount: checked((ushort)rankedEntries.Count),
            pageEntries);
    }

    public static bool TryParse(PacketFrame frame, out LegacyNobilityRankListPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < HeaderPayloadLength ||
            (frame.Payload.Length - HeaderPayloadLength) % EntryLength != 0 ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader) ||
            reader.ReadUInt32(ActionOffset) != (uint)LegacyNobilityAction.RankListen ||
            reader.ReadUInt32(MarkerOffset) != Marker)
        {
            return false;
        }

        var entryCount = (frame.Payload.Length - HeaderPayloadLength) / EntryLength;
        var entries = entryCount == 0
            ? Array.Empty<LegacyNobilityRankListEntry>()
            : new LegacyNobilityRankListEntry[entryCount];
        for (var index = 0; index < entries.Length; index++)
        {
            var offset = EntriesOffset + index * EntryLength;
            var rank = reader.ReadUInt32(offset + EntryRankOffset);
            if (rank > byte.MaxValue)
            {
                return false;
            }

            entries[index] = new LegacyNobilityRankListEntry(
                reader.ReadUInt32(offset + EntryUidOffset),
                reader.ReadUInt32(offset + EntryGenderOffset),
                reader.ReadUInt32(offset + EntryMeshOffset),
                ReadFixedName(reader.Payload.Slice(offset + EntryNameOffset, EntryNameLength)),
                reader.ReadUInt64(offset + EntryDonationOffset),
                (byte)rank,
                reader.ReadInt32(offset + EntryPositionOffset));
        }

        packet = new LegacyNobilityRankListPacket(
            reader.ReadUInt16(PageOffset),
            reader.ReadUInt16(TotalPagesOffset),
            reader.ReadUInt16(TotalCountOffset),
            entries);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, checked(HeaderPayloadLength + Entries.Count * EntryLength));
        writer.WriteUInt32(ActionOffset, (uint)LegacyNobilityAction.RankListen);
        writer.WriteUInt16(PageOffset, Page);
        writer.WriteUInt16(TotalPagesOffset, TotalPages);
        writer.WriteUInt16(TotalCountOffset, TotalCount);
        writer.WriteUInt32(MarkerOffset, Marker);

        for (var index = 0; index < Entries.Count; index++)
        {
            var entry = Entries[index];
            var offset = EntriesOffset + index * EntryLength;
            writer.WriteUInt32(offset + EntryUidOffset, entry.Uid);
            writer.WriteUInt32(offset + EntryGenderOffset, entry.Gender);
            writer.WriteUInt32(offset + EntryMeshOffset, entry.Mesh);
            WriteFixedName(writer.Payload.Slice(offset + EntryNameOffset, EntryNameLength), entry.Name);
            writer.WriteUInt64(offset + EntryDonationOffset, entry.Donation);
            writer.WriteUInt32(offset + EntryRankOffset, entry.Rank);
            writer.WriteInt32(offset + EntryPositionOffset, entry.Position);
        }

        return writer.ToArray();
    }

    private static string ReadFixedName(ReadOnlySpan<byte> bytes)
    {
        var terminator = bytes.IndexOf((byte)0);
        var nameBytes = terminator >= 0 ? bytes[..terminator] : bytes;
        return Encoding.UTF8.GetString(nameBytes);
    }

    private static void WriteFixedName(Span<byte> destination, string name)
    {
        var bytes = Encoding.UTF8.GetBytes(name);
        bytes.AsSpan(0, Math.Min(bytes.Length, destination.Length)).CopyTo(destination);
    }
}

public readonly record struct LegacyNobilityInformationPacket(
    ulong DukeDonation = 0,
    ulong PrinceDonation = 0,
    ulong KingDonation = 0)
{
    private const int ActionOffset = 0;
    private const int PayloadLength = 100;
    private const ulong KnightDonation = 30_000_000;
    private const ulong BaronDonation = 100_000_000;
    private const ulong EarlDonation = 200_000_000;

    public const ushort Opcode = WorldPacketOpcodes.LegacyNobilityIcon;
    public const uint DonationMaximum = uint.MaxValue;

    public static bool TryParse(PacketFrame frame, out LegacyNobilityInformationPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != PayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader) ||
            reader.ReadUInt32(ActionOffset) != (uint)LegacyNobilityAction.NobilityInformation)
        {
            return false;
        }

        packet = new LegacyNobilityInformationPacket(
            reader.ReadUInt64(ActionOffset + sizeof(uint) + 3 * ThresholdLength),
            reader.ReadUInt64(ActionOffset + sizeof(uint) + 4 * ThresholdLength),
            reader.ReadUInt64(ActionOffset + sizeof(uint) + 5 * ThresholdLength));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(ActionOffset, (uint)LegacyNobilityAction.NobilityInformation);

        var offset = ActionOffset + sizeof(uint);
        WriteThreshold(ref writer, ref offset, KnightDonation, DonationMaximum, 1);
        WriteThreshold(ref writer, ref offset, BaronDonation, DonationMaximum, 3);
        WriteThreshold(ref writer, ref offset, EarlDonation, DonationMaximum, 5);
        WriteThreshold(ref writer, ref offset, DukeDonation, DonationMaximum, 7);
        WriteThreshold(ref writer, ref offset, PrinceDonation, DonationMaximum, 9);
        WriteThreshold(ref writer, ref offset, KingDonation, DonationMaximum, 12);
        return writer.ToArray();
    }

    private const int ThresholdLength = sizeof(ulong) + sizeof(uint) + sizeof(uint);

    private static void WriteThreshold(ref PacketWriter writer, ref int offset, ulong donation, uint maximum, uint rank)
    {
        writer.WriteUInt64(offset, donation);
        offset += sizeof(ulong);
        writer.WriteUInt32(offset, maximum);
        offset += sizeof(uint);
        writer.WriteUInt32(offset, rank);
        offset += sizeof(uint);
    }
}

public readonly record struct LegacyNobilityIconPacket(
    ulong Uid,
    ulong Donation,
    byte Rank,
    int Position)
{
    private const int ActionOffset = 0;
    private const int UidOffset = ActionOffset + sizeof(uint);
    private const int ReservedOffset = UidOffset + sizeof(uint);
    private const int StringCountOffset = ReservedOffset + 108;
    private const int TextLengthOffset = StringCountOffset + sizeof(byte);
    private const int TextBytesOffset = TextLengthOffset + sizeof(ushort);

    public const ushort Opcode = WorldPacketOpcodes.LegacyNobilityIcon;
    public const uint IconAction = 3;
    public const int LegacyDefaultPosition = -1;

    public string Text => string.Create(
        CultureInfo.InvariantCulture,
        $"{Uid} {Donation} {Rank} {Position}");

    public int PayloadLength => TextBytesOffset + Encoding.UTF8.GetByteCount(Text);

    public static LegacyNobilityIconPacket FromSnapshot(
        CharacterSnapshot snapshot,
        string? usersRoot = null,
        WorldSessionStore? sessions = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var state = LegacyNobilityRanking.ResolveForSnapshot(snapshot, usersRoot, sessions);

        return new LegacyNobilityIconPacket(
            state.Uid,
            state.Donation,
            state.Rank,
            state.Position);
    }

    public static bool TryParse(PacketFrame frame, out LegacyNobilityIconPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < TextBytesOffset ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader) ||
            reader.ReadUInt32(ActionOffset) != IconAction ||
            reader.Payload[StringCountOffset] != 1)
        {
            return false;
        }

        var textLength = reader.ReadUInt16(TextLengthOffset);
        if (reader.Payload.Length != TextBytesOffset + textLength)
        {
            return false;
        }

        var text = Encoding.UTF8.GetString(reader.Payload[TextBytesOffset..]);
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4 ||
            !ulong.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var uid) ||
            !ulong.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var donation) ||
            !byte.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rank) ||
            !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var position) ||
            reader.ReadUInt32(UidOffset) != uid)
        {
            return false;
        }

        packet = new LegacyNobilityIconPacket(uid, donation, rank, position);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var textBytes = Encoding.UTF8.GetBytes(Text);
        if (textBytes.Length > ushort.MaxValue)
        {
            throw new InvalidOperationException("Legacy nobility icon text is too long.");
        }

        var writer = new PacketWriter(Opcode, TextBytesOffset + textBytes.Length);
        writer.WriteUInt32(ActionOffset, IconAction);
        writer.WriteUInt32(UidOffset, CheckedUInt32(Uid, nameof(Uid)));
        writer.Payload[StringCountOffset] = 1;
        writer.WriteUInt16(TextLengthOffset, (ushort)textBytes.Length);
        textBytes.CopyTo(writer.Payload[TextBytesOffset..]);
        return writer.ToArray();
    }

    private static uint CheckedUInt32(ulong value, string name)
    {
        if (value > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy nobility icon UID must fit in uint.");
        }

        return (uint)value;
    }
}
