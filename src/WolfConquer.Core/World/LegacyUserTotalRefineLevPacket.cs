using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed record LegacyUserTotalRefineLevPacket(uint Type, uint Uid, uint Level)
{
    private const int TypeField = 1;
    private const int UidField = 2;
    private const int LevelField = 3;
    private const int VarintWireType = 0;

    public const ushort Opcode = WorldPacketOpcodes.LegacyUserTotalRefineLev;

    public static LegacyUserTotalRefineLevPacket FromEquipmentPerfection(uint uid, PlayerItemSnapshot playerItems)
    {
        ArgumentNullException.ThrowIfNull(playerItems);

        return new LegacyUserTotalRefineLevPacket(
            Type: 0,
            uid,
            playerItems.EquipmentItems.Aggregate(0u, (sum, item) => checked(sum + item.PerfectionLevel)));
    }

    public static bool TryParse(PacketFrame frame, out LegacyUserTotalRefineLevPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new ProtoReader(frame.Payload.ToArray());
        var type = 0u;
        var uid = 0u;
        var level = 0u;
        var hasType = false;
        var hasUid = false;
        var hasLevel = false;

        while (!reader.End)
        {
            if (!reader.TryReadVarint(out var tagValue) || tagValue == 0)
            {
                return false;
            }

            var fieldNumber = (int)(tagValue >> 3);
            var wireType = (int)(tagValue & 0x7);
            if (wireType != VarintWireType ||
                !reader.TryReadVarint(out var value) ||
                value > uint.MaxValue)
            {
                return false;
            }

            switch (fieldNumber)
            {
                case TypeField:
                    type = (uint)value;
                    hasType = true;
                    break;
                case UidField:
                    uid = (uint)value;
                    hasUid = true;
                    break;
                case LevelField:
                    level = (uint)value;
                    hasLevel = true;
                    break;
                default:
                    return false;
            }
        }

        if (!hasType || !hasUid || !hasLevel)
        {
            return false;
        }

        packet = new LegacyUserTotalRefineLevPacket(type, uid, level);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var payload = new List<byte>();
        WriteVarintField(payload, TypeField, Type);
        WriteVarintField(payload, UidField, Uid);
        WriteVarintField(payload, LevelField, Level);

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
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
