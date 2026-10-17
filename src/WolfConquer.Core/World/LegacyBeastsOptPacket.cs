using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed class LegacyBeastsOptPacket : IEquatable<LegacyBeastsOptPacket>
{
    private const int TypeField = 1;
    private const int ItemUidField = 1;
    private const int ItemStackField = 2;
    private const int ItemsField = 4;
    private const int VarintWireType = 0;
    private const int LengthDelimitedWireType = 2;

    public const ushort Opcode = WorldPacketOpcodes.LegacyBeastsOpt;

    public LegacyBeastsOptPacket(uint Type, IReadOnlyList<Item> Items)
    {
        this.Type = Type;
        this.Items = Items.ToArray();
    }

    public uint Type { get; }

    public IReadOnlyList<Item> Items { get; }

    public static bool TryParse(PacketFrame frame, out LegacyBeastsOptPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new ProtoReader(frame.Payload.ToArray());
        uint type = 0;
        var hasType = false;
        var items = new List<Item>();

        while (!reader.End)
        {
            if (!reader.TryReadTag(out var fieldNumber, out var wireType))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case TypeField:
                    if (!TryReadUInt32(ref reader, wireType, out type))
                    {
                        return false;
                    }

                    hasType = true;
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

        if (!hasType)
        {
            return false;
        }

        packet = new LegacyBeastsOptPacket(type, items);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var payload = new List<byte>();
        WriteRequiredVarintField(payload, TypeField, Type);

        foreach (var item in Items)
        {
            var itemPayload = new List<byte>();
            WriteRequiredVarintField(itemPayload, ItemUidField, item.UID);
            WriteRequiredVarintField(itemPayload, ItemStackField, item.Stack);
            WriteLengthDelimitedField(payload, ItemsField, itemPayload);
        }

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    public bool Equals(LegacyBeastsOptPacket? other)
    {
        return other is not null &&
            Type == other.Type &&
            Items.SequenceEqual(other.Items);
    }

    public override bool Equals(object? obj)
    {
        return obj is LegacyBeastsOptPacket other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Type);
        foreach (var item in Items)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }

    private static bool TryParseItem(ReadOnlySpan<byte> payload, out Item item)
    {
        item = default;

        var reader = new ProtoReader(payload);
        uint uid = 0;
        ushort stack = 0;
        var hasUid = false;
        var hasStack = false;

        while (!reader.End)
        {
            if (!reader.TryReadTag(out var fieldNumber, out var wireType))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case ItemUidField:
                    if (!TryReadUInt32(ref reader, wireType, out uid))
                    {
                        return false;
                    }

                    hasUid = true;
                    break;
                case ItemStackField:
                    if (!TryReadUInt16(ref reader, wireType, out stack))
                    {
                        return false;
                    }

                    hasStack = true;
                    break;
                default:
                    return false;
            }
        }

        if (!hasUid || !hasStack)
        {
            return false;
        }

        item = new Item(uid, stack);
        return true;
    }

    private static bool TryReadUInt32(ref ProtoReader reader, int wireType, out uint value)
    {
        value = 0;
        if (wireType != VarintWireType ||
            !reader.TryReadVarint(out var raw) ||
            raw > uint.MaxValue)
        {
            return false;
        }

        value = (uint)raw;
        return true;
    }

    private static bool TryReadUInt16(ref ProtoReader reader, int wireType, out ushort value)
    {
        value = 0;
        if (wireType != VarintWireType ||
            !reader.TryReadVarint(out var raw) ||
            raw > ushort.MaxValue)
        {
            return false;
        }

        value = (ushort)raw;
        return true;
    }

    private static void WriteRequiredVarintField(List<byte> payload, int fieldNumber, uint value)
    {
        WriteVarint(payload, ((ulong)fieldNumber << 3) | VarintWireType);
        WriteVarint(payload, value);
    }

    private static void WriteLengthDelimitedField(List<byte> payload, int fieldNumber, IReadOnlyList<byte> value)
    {
        WriteVarint(payload, ((ulong)fieldNumber << 3) | LengthDelimitedWireType);
        WriteVarint(payload, (ulong)value.Count);
        payload.AddRange(value);
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

    public readonly record struct Item(uint UID, ushort Stack);

    private ref struct ProtoReader(ReadOnlySpan<byte> payload)
    {
        private readonly ReadOnlySpan<byte> _payload = payload;
        private int _offset;

        public readonly bool End => _offset >= _payload.Length;

        public bool TryReadTag(out int fieldNumber, out int wireType)
        {
            fieldNumber = 0;
            wireType = 0;

            if (!TryReadVarint(out var tagValue) || tagValue == 0)
            {
                return false;
            }

            fieldNumber = (int)(tagValue >> 3);
            wireType = (int)(tagValue & 0x7);
            return fieldNumber > 0;
        }

        public bool TryReadLengthDelimited(out ReadOnlySpan<byte> value)
        {
            value = default;
            if (!TryReadVarint(out var length) ||
                length > int.MaxValue ||
                _offset > _payload.Length - (int)length)
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
