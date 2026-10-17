using System.Buffers;
using System.Buffers.Binary;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public readonly record struct LegacyUserAbilityScoreEntry(uint Position, uint Points);

public readonly record struct LegacyUserAbilityScorePacket(
    uint Type,
    uint Uid,
    uint Level,
    IReadOnlyList<LegacyUserAbilityScoreEntry> Items)
{
    private const int TypeField = 1;
    private const int UidField = 2;
    private const int LevelField = 3;
    private const int ItemsField = 4;
    private const int PositionField = 1;
    private const int PointsField = 2;
    private const int VarintWireType = 0;
    private const int LengthDelimitedWireType = 2;

    public const ushort Opcode = WorldPacketOpcodes.LegacyUserAbilityScore;
    public const uint ResponseType = 1;

    public static LegacyUserAbilityScorePacket Query(uint uid)
    {
        return new LegacyUserAbilityScorePacket(0, uid, 0, []);
    }

    public static LegacyUserAbilityScorePacket CreateResponse(
        uint uid,
        uint level,
        IReadOnlyList<uint> points)
    {
        var items = new LegacyUserAbilityScoreEntry[points.Count];
        for (var index = 0; index < points.Count; index++)
        {
            items[index] = new LegacyUserAbilityScoreEntry(checked((uint)(index + 1)), points[index]);
        }

        return new LegacyUserAbilityScorePacket(ResponseType, uid, level, items);
    }

    public static bool TryParse(PacketFrame frame, out LegacyUserAbilityScorePacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        if (payload.Length == sizeof(uint))
        {
            packet = Query(BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0, sizeof(uint))));
            return true;
        }

        var reader = new ProtoReader(payload);
        var seen = 0;
        var type = 0U;
        var uid = 0U;
        var level = 0U;
        var items = new List<LegacyUserAbilityScoreEntry>();

        while (!reader.End)
        {
            if (!reader.TryReadVarint(out var tagValue) || tagValue == 0)
            {
                return false;
            }

            var fieldNumber = (int)(tagValue >> 3);
            var wireType = (int)(tagValue & 0x7);
            switch (fieldNumber)
            {
                case TypeField:
                    if (wireType != VarintWireType ||
                        !reader.TryReadUInt32(out type))
                    {
                        return false;
                    }

                    seen |= 1 << TypeField;
                    break;

                case UidField:
                    if (wireType != VarintWireType ||
                        !reader.TryReadUInt32(out uid))
                    {
                        return false;
                    }

                    seen |= 1 << UidField;
                    break;

                case LevelField:
                    if (wireType != VarintWireType ||
                        !reader.TryReadUInt32(out level))
                    {
                        return false;
                    }

                    seen |= 1 << LevelField;
                    break;

                case ItemsField:
                    if (wireType != LengthDelimitedWireType ||
                        !reader.TryReadLengthDelimited(out var itemPayload) ||
                        !TryParseItem(itemPayload, out var item))
                    {
                        return false;
                    }

                    items.Add(item);
                    break;

                default:
                    return false;
            }
        }

        var required = (1 << TypeField) | (1 << UidField) | (1 << LevelField);
        if ((seen & required) != required)
        {
            if (frame.Payload.Length >= sizeof(uint))
            {
                packet = Query(BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0, sizeof(uint))));
                return true;
            }

            return false;
        }

        packet = new LegacyUserAbilityScorePacket(type, uid, level, items);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var payload = new List<byte>();
        WriteVarintField(payload, TypeField, Type);
        WriteVarintField(payload, UidField, Uid);
        WriteVarintField(payload, LevelField, Level);

        foreach (var item in Items)
        {
            var itemPayload = new List<byte>();
            WriteVarintField(itemPayload, PositionField, item.Position);
            WriteVarintField(itemPayload, PointsField, item.Points);

            WriteVarint(payload, ((ulong)ItemsField << 3) | LengthDelimitedWireType);
            WriteVarint(payload, (ulong)itemPayload.Count);
            payload.AddRange(itemPayload);
        }

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static bool TryParseItem(
        ReadOnlySpan<byte> payload,
        out LegacyUserAbilityScoreEntry item)
    {
        item = default;

        var reader = new ProtoReader(payload);
        var seen = 0;
        var position = 0U;
        var points = 0U;
        while (!reader.End)
        {
            if (!reader.TryReadVarint(out var tagValue) || tagValue == 0)
            {
                return false;
            }

            var fieldNumber = (int)(tagValue >> 3);
            var wireType = (int)(tagValue & 0x7);
            if (wireType != VarintWireType)
            {
                return false;
            }

            switch (fieldNumber)
            {
                case PositionField:
                    if (!reader.TryReadUInt32(out position))
                    {
                        return false;
                    }

                    seen |= 1 << PositionField;
                    break;

                case PointsField:
                    if (!reader.TryReadUInt32(out points))
                    {
                        return false;
                    }

                    seen |= 1 << PointsField;
                    break;

                default:
                    return false;
            }
        }

        var required = (1 << PositionField) | (1 << PointsField);
        if ((seen & required) != required)
        {
            return false;
        }

        item = new LegacyUserAbilityScoreEntry(position, points);
        return true;
    }

    private static void WriteVarintField(List<byte> payload, int fieldNumber, ulong value)
    {
        WriteVarint(payload, ((ulong)fieldNumber << 3) | VarintWireType);
        WriteVarint(payload, value);
    }

    private static void WriteVarint(List<byte> payload, ulong value)
    {
        do
        {
            var next = (byte)(value & 0x7F);
            value >>= 7;
            if (value != 0)
            {
                next |= 0x80;
            }

            payload.Add(next);
        }
        while (value != 0);
    }

    private ref struct ProtoReader(ReadOnlySpan<byte> payload)
    {
        private readonly ReadOnlySpan<byte> _payload = payload;
        private int _offset;

        public readonly bool End => _offset >= _payload.Length;

        public bool TryReadUInt32(out uint value)
        {
            value = 0;
            if (!TryReadVarint(out var parsed) || parsed > uint.MaxValue)
            {
                return false;
            }

            value = (uint)parsed;
            return true;
        }

        public bool TryReadLengthDelimited(out ReadOnlySpan<byte> value)
        {
            value = default;
            if (!TryReadVarint(out var length) ||
                length > int.MaxValue ||
                _payload.Length - _offset < (int)length)
            {
                return false;
            }

            value = _payload.Slice(_offset, (int)length);
            _offset += (int)length;
            return true;
        }

        public bool TryReadVarint(out ulong value)
        {
            value = 0;

            for (var shift = 0; shift < 64; shift += 7)
            {
                if (_offset >= _payload.Length)
                {
                    return false;
                }

                var current = _payload[_offset++];
                value |= (ulong)(current & 0x7F) << shift;
                if ((current & 0x80) == 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
