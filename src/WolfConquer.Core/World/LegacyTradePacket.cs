using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed record LegacyTradePacket(uint Type)
{
    private const int TypeField = 1;

    public const ushort Opcode = WorldPacketOpcodes.LegacyTrade;

    // Native MsgTrade.Trade fields. Type is the legacy low-32-bit action view.
    // A with-expression changing Type must also change Action for native output.
    public ulong Action { get; init; } = Type;
    public long Money { get; init; }
    public ulong ItemUID { get; init; }
    public uint CPS { get; init; }
    public ulong Unknown { get; init; }
    public ulong UID { get; init; }

    public static bool TryParse(PacketFrame frame, out LegacyTradePacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new LegacyProtoWire.Reader(frame.Payload.ToArray());
        var action = 0UL;
        var money = 0L;
        var itemUid = 0UL;
        var cps = 0U;
        var unknown = 0UL;
        var uid = 0UL;
        var hasType = false;

        while (!reader.End)
        {
            // Check the full tag before narrowing: an oversized field number
            // must not wrap into one of the six accepted native fields.
            if (!reader.TryReadVarint(out var tag) || tag == 0 ||
                (tag & 7) != LegacyProtoWire.VarintWireType ||
                (tag >> 3) > 6 || !reader.TryReadVarint(out var value))
            {
                return false;
            }

            switch (tag >> 3)
            {
                case TypeField:
                    action = value;
                    hasType = true;
                    break;
                case 2:
                    // protobuf int64 uses two's-complement varints, not zigzag.
                    money = unchecked((long)value);
                    break;
                case 3:
                    itemUid = value;
                    break;
                case 4:
                    if (value > uint.MaxValue)
                    {
                        return false;
                    }

                    cps = (uint)value;
                    break;
                case 5:
                    unknown = value;
                    break;
                case 6:
                    uid = value;
                    break;
                default:
                    return false;
            }
        }

        if (!hasType)
        {
            return false;
        }

        packet = new LegacyTradePacket(unchecked((uint)action))
        {
            Action = action,
            Money = money,
            ItemUID = itemUid,
            CPS = cps,
            Unknown = unknown,
            UID = uid
        };
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

    // Source-derived native output, distinct from the captured type-only form.
    // All six ProtoMember(IsRequired=true) fields are written, including zeros.
    public byte[] ToNativeFrameBytes()
    {
        var payload = new List<byte>();
        LegacyProtoWire.WriteVarintField(payload, 1, Action);
        LegacyProtoWire.WriteVarintField(payload, 2, unchecked((ulong)Money));
        LegacyProtoWire.WriteVarintField(payload, 3, ItemUID);
        LegacyProtoWire.WriteVarintField(payload, 4, CPS);
        LegacyProtoWire.WriteVarintField(payload, 5, Unknown);
        LegacyProtoWire.WriteVarintField(payload, 6, UID);

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.ToArray().CopyTo(writer.Payload);
        return writer.ToArray();
    }
}
