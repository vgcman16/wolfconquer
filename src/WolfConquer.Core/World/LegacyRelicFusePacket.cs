using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public readonly record struct LegacyRelicFusePacket(uint Type, uint RelicID1, uint RelicID2, uint RelicID3)
{
    private const int TypeField = 1;
    private const int RelicID1Field = 2;
    private const int RelicID2Field = 3;
    private const int RelicID3Field = 4;
    private const int VarintWireType = 0;

    public const ushort Opcode = WorldPacketOpcodes.LegacyRelicFuse;
    public const uint ComposeType = 0;
    public const uint ReturnType = 1;

    public static bool TryParse(PacketFrame frame, out LegacyRelicFusePacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new ProtoReader(frame.Payload.ToArray());
        uint type = 0;
        uint relicID1 = 0;
        uint relicID2 = 0;
        uint relicID3 = 0;
        var hasType = false;
        var hasRelicID1 = false;
        var hasRelicID2 = false;
        var hasRelicID3 = false;

        while (!reader.End)
        {
            if (!reader.TryReadVarint(out var tagValue) || tagValue == 0)
            {
                return false;
            }

            var fieldNumber = (int)(tagValue >> 3);
            var wireType = (int)(tagValue & 0x7);
            if (wireType != VarintWireType || !reader.TryReadVarint(out var value) || value > uint.MaxValue)
            {
                return false;
            }

            switch (fieldNumber)
            {
                case TypeField:
                    type = (uint)value;
                    hasType = true;
                    break;
                case RelicID1Field:
                    relicID1 = (uint)value;
                    hasRelicID1 = true;
                    break;
                case RelicID2Field:
                    relicID2 = (uint)value;
                    hasRelicID2 = true;
                    break;
                case RelicID3Field:
                    relicID3 = (uint)value;
                    hasRelicID3 = true;
                    break;
                default:
                    return false;
            }
        }

        if (!hasType || !hasRelicID1 || !hasRelicID2 || !hasRelicID3)
        {
            return false;
        }

        packet = new LegacyRelicFusePacket(type, relicID1, relicID2, relicID3);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var payload = new List<byte>();
        WriteVarintField(payload, TypeField, Type);
        WriteVarintField(payload, RelicID1Field, RelicID1);
        WriteVarintField(payload, RelicID2Field, RelicID2);
        WriteVarintField(payload, RelicID3Field, RelicID3);

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static void WriteVarintField(List<byte> payload, int fieldNumber, uint value)
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
