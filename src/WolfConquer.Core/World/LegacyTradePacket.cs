using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed record LegacyTradePacket(uint Type)
{
    private const int TypeField = 1;

    public const ushort Opcode = WorldPacketOpcodes.LegacyTrade;

    public static bool TryParse(PacketFrame frame, out LegacyTradePacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new LegacyProtoWire.Reader(frame.Payload.ToArray());
        var type = 0U;
        var hasType = false;

        while (!reader.End)
        {
            if (!reader.TryReadTag(out var fieldNumber, out var wireType))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case TypeField:
                    if (wireType != LegacyProtoWire.VarintWireType ||
                        !reader.TryReadUInt32(out type))
                    {
                        return false;
                    }

                    hasType = true;
                    break;
                default:
                    return false;
            }
        }

        if (!hasType)
        {
            return false;
        }

        packet = new LegacyTradePacket(type);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var payload = new List<byte>();
        LegacyProtoWire.WriteVarintField(payload, TypeField, Type);

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.ToArray().CopyTo(writer.Payload);
        return writer.ToArray();
    }
}
