using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public readonly record struct LegacyServerInfoPacket(uint Vigor)
{
    private const int TypeField = 1;
    private const int YearField = 2;
    private const int LastRequiredField = 8;
    private const uint TypeValue = 2;
    private const int VarintWireType = 0;

    public const ushort Opcode = WorldPacketOpcodes.LegacyServerTimer;

    public static bool TryParse(PacketFrame frame, out LegacyServerInfoPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new ServerInfoProtoReader(frame.Payload.ToArray());
        var fieldsSeen = 0;
        uint vigor = 0;

        while (!reader.End)
        {
            if (!reader.TryReadVarint(out var tagValue) || tagValue == 0)
            {
                return false;
            }

            var fieldNumber = (int)(tagValue >> 3);
            var wireType = (int)(tagValue & 0x7);
            if (wireType != VarintWireType || !reader.TryReadVarint(out var value))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case TypeField:
                    if ((fieldsSeen & (1 << TypeField)) != 0 || value != TypeValue)
                    {
                        return false;
                    }

                    fieldsSeen |= 1 << TypeField;
                    break;
                case YearField:
                    if ((fieldsSeen & (1 << YearField)) != 0 || value > uint.MaxValue)
                    {
                        return false;
                    }

                    vigor = (uint)value;
                    fieldsSeen |= 1 << YearField;
                    break;
                case >= 3 and <= LastRequiredField:
                    if ((fieldsSeen & (1 << fieldNumber)) != 0 || value != 0)
                    {
                        return false;
                    }

                    fieldsSeen |= 1 << fieldNumber;
                    break;
                default:
                    return false;
            }
        }

        const int requiredFields = (1 << TypeField) | (1 << YearField);
        if ((fieldsSeen & requiredFields) != requiredFields)
        {
            return false;
        }

        packet = new LegacyServerInfoPacket(vigor);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        Span<byte> payload = stackalloc byte[24];
        var length = WritePayload(payload);
        var writer = new PacketWriter(Opcode, length);
        payload[..length].CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private readonly int WritePayload(Span<byte> payload)
    {
        var offset = 0;
        WriteField(payload, ref offset, TypeField, TypeValue);
        WriteField(payload, ref offset, YearField, Vigor);
        for (var fieldNumber = 3; fieldNumber <= LastRequiredField; fieldNumber++)
        {
            WriteField(payload, ref offset, fieldNumber, 0);
        }

        return offset;
    }

    private static void WriteField(Span<byte> payload, ref int offset, int fieldNumber, uint value)
    {
        WriteVarint(payload, ref offset, (uint)(fieldNumber << 3));
        WriteVarint(payload, ref offset, value);
    }

    private static void WriteVarint(Span<byte> payload, ref int offset, uint value)
    {
        do
        {
            var next = (byte)(value & 0x7F);
            value >>= 7;
            if (value != 0)
            {
                next |= 0x80;
            }

            payload[offset++] = next;
        }
        while (value != 0);
    }

    private ref struct ServerInfoProtoReader(ReadOnlySpan<byte> payload)
    {
        private readonly ReadOnlySpan<byte> _payload = payload;
        private int _offset;

        public readonly bool End => _offset >= _payload.Length;

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
