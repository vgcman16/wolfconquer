using System.Text;

namespace WolfConquer.Core.World;

internal static class LegacyProtoWire
{
    public const int VarintWireType = 0;
    public const int Fixed64WireType = 1;
    public const int LengthDelimitedWireType = 2;
    public const int Fixed32WireType = 5;

    public static void WriteVarintField(List<byte> payload, int fieldNumber, ulong value)
    {
        WriteVarint(payload, ((ulong)fieldNumber << 3) | VarintWireType);
        WriteVarint(payload, value);
    }

    public static void WriteBoolField(List<byte> payload, int fieldNumber, bool value)
    {
        WriteVarintField(payload, fieldNumber, value ? 1UL : 0UL);
    }

    public static void WriteStringField(List<byte> payload, int fieldNumber, string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var bytes = Encoding.UTF8.GetBytes(value);
        WriteBytesField(payload, fieldNumber, bytes);
    }

    public static void WriteBytesField(List<byte> payload, int fieldNumber, IReadOnlyCollection<byte> value)
    {
        ArgumentNullException.ThrowIfNull(value);

        WriteVarint(payload, ((ulong)fieldNumber << 3) | LengthDelimitedWireType);
        WriteVarint(payload, (ulong)value.Count);
        payload.AddRange(value);
    }

    public static void WriteVarint(List<byte> payload, ulong value)
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

    public ref struct Reader(ReadOnlySpan<byte> payload)
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
            return true;
        }

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

        public bool TryReadBool(out bool value)
        {
            value = false;
            if (!TryReadVarint(out var parsed) || parsed > 1)
            {
                return false;
            }

            value = parsed != 0;
            return true;
        }

        public bool TryReadString(out string value)
        {
            value = string.Empty;
            if (!TryReadLengthDelimited(out var bytes))
            {
                return false;
            }

            value = Encoding.UTF8.GetString(bytes);
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

        public bool TrySkipField(int wireType)
        {
            return wireType switch
            {
                VarintWireType => TryReadVarint(out _),
                Fixed64WireType => TrySkipBytes(sizeof(ulong)),
                LengthDelimitedWireType => TryReadLengthDelimited(out _),
                Fixed32WireType => TrySkipBytes(sizeof(uint)),
                _ => false,
            };
        }

        public bool TryReadVarint(out ulong value)
        {
            value = 0;

            for (var index = 0; index < 10; index++)
            {
                if (_offset >= _payload.Length)
                {
                    return false;
                }

                var current = _payload[_offset++];
                if (index == 9 && (current & 0xFE) != 0)
                {
                    return false;
                }

                value |= (ulong)(current & 0x7F) << (index * 7);
                if ((current & 0x80) == 0)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TrySkipBytes(int length)
        {
            if (length < 0 || _payload.Length - _offset < length)
            {
                return false;
            }

            _offset += length;
            return true;
        }
    }
}
