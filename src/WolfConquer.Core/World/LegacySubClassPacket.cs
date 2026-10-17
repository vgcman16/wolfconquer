using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacySubClassAction : ushort
{
    SwitchSubClass = 0,
    ActivateSubClass = 1,
    Upgrade = 2,
    SendUpdate = 3,
    LearnSubClass = 4,
    MartialPromoted = 5,
    Open = 6,
    ShowGUI = 7,
    Animation = 8,
    Join = 9,
    Pro = 10,
}

public readonly record struct LegacySubClassPacket(
    LegacySubClassAction Action,
    sbyte SubClassId)
{
    private const int ActionOffset = 0;
    private const int SubClassIdOffset = ActionOffset + sizeof(ushort);

    public const ushort Opcode = WorldPacketOpcodes.LegacySubClass;
    public const int PayloadLength = SubClassIdOffset + sizeof(byte);
    public const int FrameLength = PacketHeader.Size + PayloadLength;

    public static bool TryParse(PacketFrame frame, out LegacySubClassPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < PayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        packet = new LegacySubClassPacket(
            (LegacySubClassAction)reader.ReadUInt16(ActionOffset),
            unchecked((sbyte)reader.Payload[SubClassIdOffset]));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt16(ActionOffset, (ushort)Action);
        writer.Payload[SubClassIdOffset] = unchecked((byte)SubClassId);
        return writer.ToArray();
    }
}

public readonly record struct LegacySubClassUpdatePacket(
    LegacySubClassAction Action,
    byte SubClassId,
    byte Level,
    ushort StudyReceive)
{
    private const int ActionOffset = 0;
    private const int SubClassIdOffset = ActionOffset + sizeof(ushort);
    private const int LevelOffset = SubClassIdOffset + sizeof(byte);
    private const int PaddingAOffset = LevelOffset + sizeof(byte);
    private const int PaddingALength = 6;
    private const int StudyReceiveOffset = PaddingAOffset + PaddingALength;
    private const int PaddingBOffset = StudyReceiveOffset + sizeof(ushort);
    private const int PaddingBLength = 10;

    public const ushort Opcode = WorldPacketOpcodes.LegacySubClass;
    public const int PayloadLength = PaddingBOffset + PaddingBLength;

    public static bool TryParse(PacketFrame frame, out LegacySubClassUpdatePacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != PayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader) ||
            !IsZeroFilled(reader.Payload.Slice(PaddingAOffset, PaddingALength)) ||
            !IsZeroFilled(reader.Payload.Slice(PaddingBOffset, PaddingBLength)))
        {
            return false;
        }

        packet = new LegacySubClassUpdatePacket(
            (LegacySubClassAction)reader.ReadUInt16(ActionOffset),
            reader.Payload[SubClassIdOffset],
            reader.Payload[LevelOffset],
            reader.ReadUInt16(StudyReceiveOffset));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt16(ActionOffset, (ushort)Action);
        writer.Payload[SubClassIdOffset] = SubClassId;
        writer.Payload[LevelOffset] = Level;
        writer.WriteUInt16(StudyReceiveOffset, StudyReceive);
        return writer.ToArray();
    }

    private static bool IsZeroFilled(ReadOnlySpan<byte> bytes)
    {
        return bytes.IndexOfAnyExcept((byte)0) < 0;
    }
}

public readonly record struct LegacySubClassGuiEntry(byte Id, byte Phrase, byte Level);

public sealed record LegacySubClassGuiPacket(
    LegacySubClassAction Action,
    ushort StudyPoints,
    ushort StudyReceive,
    IReadOnlyList<LegacySubClassGuiEntry> Entries)
{
    private const int ActionOffset = 0;
    private const int StudyPointsOffset = ActionOffset + sizeof(ushort);
    private const int PaddingAOffset = StudyPointsOffset + sizeof(ushort);
    private const int PaddingLength = 6;
    private const int StudyReceiveOffset = PaddingAOffset + PaddingLength;
    private const int PaddingBOffset = StudyReceiveOffset + sizeof(ushort);
    private const int CountOffset = PaddingBOffset + PaddingLength;
    private const int EntriesOffset = CountOffset + sizeof(int);
    private const int EntryLength = sizeof(byte) + sizeof(byte) + sizeof(byte);

    public const ushort Opcode = WorldPacketOpcodes.LegacySubClass;

    public static bool TryParse(PacketFrame frame, out LegacySubClassGuiPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < EntriesOffset ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader) ||
            !IsZeroFilled(reader.Payload.Slice(PaddingAOffset, PaddingLength)) ||
            !IsZeroFilled(reader.Payload.Slice(PaddingBOffset, PaddingLength)))
        {
            return false;
        }

        var count = reader.ReadInt32(CountOffset);
        if (count < 0 ||
            count > (frame.Payload.Length - EntriesOffset) / EntryLength ||
            frame.Payload.Length != EntriesOffset + (count * EntryLength))
        {
            return false;
        }

        var entries = new LegacySubClassGuiEntry[count];
        for (var index = 0; index < entries.Length; index++)
        {
            var offset = EntriesOffset + (index * EntryLength);
            entries[index] = new LegacySubClassGuiEntry(
                reader.Payload[offset],
                reader.Payload[offset + sizeof(byte)],
                reader.Payload[offset + sizeof(byte) + sizeof(byte)]);
        }

        packet = new LegacySubClassGuiPacket(
            (LegacySubClassAction)reader.ReadUInt16(ActionOffset),
            reader.ReadUInt16(StudyPointsOffset),
            reader.ReadUInt16(StudyReceiveOffset),
            entries);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        ArgumentNullException.ThrowIfNull(Entries);

        var writer = new PacketWriter(Opcode, EntriesOffset + (Entries.Count * EntryLength));
        writer.WriteUInt16(ActionOffset, (ushort)Action);
        writer.WriteUInt16(StudyPointsOffset, StudyPoints);
        writer.WriteUInt16(StudyReceiveOffset, StudyReceive);
        writer.WriteInt32(CountOffset, Entries.Count);
        for (var index = 0; index < Entries.Count; index++)
        {
            var entry = Entries[index];
            var offset = EntriesOffset + (index * EntryLength);
            writer.Payload[offset] = entry.Id;
            writer.Payload[offset + sizeof(byte)] = entry.Phrase;
            writer.Payload[offset + sizeof(byte) + sizeof(byte)] = entry.Level;
        }

        return writer.ToArray();
    }

    private static bool IsZeroFilled(ReadOnlySpan<byte> bytes)
    {
        return bytes.IndexOfAnyExcept((byte)0) < 0;
    }
}
