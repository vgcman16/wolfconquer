using System.Buffers;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public readonly record struct LegacySpecialTitlePacket(
    uint HeaderValue,
    uint Reserved1,
    uint Reserved2,
    uint Reserved3,
    uint Reserved4,
    uint TitleId,
    uint Action,
    uint Reserved5)
{
    private const int HeaderValueOffset = 0;
    private const int Reserved1Offset = HeaderValueOffset + sizeof(uint);
    private const int Reserved2Offset = Reserved1Offset + sizeof(uint);
    private const int Reserved3Offset = Reserved2Offset + sizeof(uint);
    private const int Reserved4Offset = Reserved3Offset + sizeof(uint);
    private const int TitleIdOffset = Reserved4Offset + sizeof(uint);
    private const int ActionOffset = TitleIdOffset + sizeof(uint);
    private const int Reserved5Offset = ActionOffset + sizeof(uint);

    private const string SpecialTitlesAttribute = "SpecialTitles";

    public const ushort Opcode = WorldPacketOpcodes.LegacySpecialTitle;
    public const uint DefaultHeaderValue = 65_539;
    public const uint DefaultAction = 2;
    public const int PayloadLength = Reserved5Offset + sizeof(uint);
    public const int FrameLength = PacketHeader.Size + PayloadLength;

    public static LegacySpecialTitlePacket FromTitleId(uint titleId)
    {
        return new LegacySpecialTitlePacket(DefaultHeaderValue, 0, 0, 0, 0, titleId, DefaultAction, 0);
    }

    public static IReadOnlyList<LegacySpecialTitlePacket> FromSnapshot(CharacterSnapshot snapshot)
    {
        return FromPersistedSpecialTitles(snapshot);
    }

    public static bool TryCreateFromSnapshot(
        CharacterSnapshot snapshot,
        out IReadOnlyList<LegacySpecialTitlePacket> packets)
    {
        return TryCreateFromPersistedSpecialTitles(snapshot, out packets);
    }

    public static IReadOnlyList<LegacySpecialTitlePacket> FromPersistedSpecialTitles(CharacterSnapshot snapshot)
    {
        return TryCreateFromPersistedSpecialTitles(snapshot, out var packets) ? packets : [];
    }

    public static bool TryCreateFromPersistedSpecialTitles(
        CharacterSnapshot snapshot,
        out IReadOnlyList<LegacySpecialTitlePacket> packets)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        packets = [];

        if (!snapshot.Attributes.TryGetValue(SpecialTitlesAttribute, out var raw) ||
            string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return true;
        }

        var startIndex = TryReadLegacyCountPrefix(parts, out var endIndex);
        if (startIndex < 0)
        {
            return false;
        }

        var selected = new List<LegacySpecialTitlePacket>();
        for (var index = startIndex; index < endIndex; index += 2)
        {
            if (!uint.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var titleId) ||
                !uint.TryParse(parts[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var state))
            {
                packets = [];
                return false;
            }

            if (state == 0)
            {
                selected.Add(FromTitleId(titleId));
            }
        }

        packets = selected;
        return true;
    }

    private static int TryReadLegacyCountPrefix(string[] parts, out int endIndex)
    {
        endIndex = parts.Length;

        if (uint.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var declaredCount) &&
            declaredCount <= int.MaxValue / 2)
        {
            var declaredPairFieldCount = checked((int)declaredCount * 2);
            if (declaredPairFieldCount == parts.Length - 1)
            {
                return 1;
            }

            if (declaredCount == 0 && AllRemainingPartsAreZero(parts))
            {
                endIndex = 1;
                return 1;
            }
        }

        return parts.Length % 2 == 0 ? 0 : -1;
    }

    private static bool AllRemainingPartsAreZero(string[] parts)
    {
        for (var index = 1; index < parts.Length; index++)
        {
            if (!uint.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ||
                value != 0)
            {
                return false;
            }
        }

        return true;
    }

    public static bool TryParse(PacketFrame frame, out LegacySpecialTitlePacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != PayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        packet = new LegacySpecialTitlePacket(
            reader.ReadUInt32(HeaderValueOffset),
            reader.ReadUInt32(Reserved1Offset),
            reader.ReadUInt32(Reserved2Offset),
            reader.ReadUInt32(Reserved3Offset),
            reader.ReadUInt32(Reserved4Offset),
            reader.ReadUInt32(TitleIdOffset),
            reader.ReadUInt32(ActionOffset),
            reader.ReadUInt32(Reserved5Offset));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(HeaderValueOffset, HeaderValue);
        writer.WriteUInt32(Reserved1Offset, Reserved1);
        writer.WriteUInt32(Reserved2Offset, Reserved2);
        writer.WriteUInt32(Reserved3Offset, Reserved3);
        writer.WriteUInt32(Reserved4Offset, Reserved4);
        writer.WriteUInt32(TitleIdOffset, TitleId);
        writer.WriteUInt32(ActionOffset, Action);
        writer.WriteUInt32(Reserved5Offset, Reserved5);
        return writer.ToArray();
    }
}
