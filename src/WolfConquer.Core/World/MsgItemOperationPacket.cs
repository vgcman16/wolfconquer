using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum MsgItemOperationAction : uint
{
    AddVendingItemGold = 22,
    RemoveVendingItem = 23,
    BuyVendingItem = 24,
    AddVendingItemConquerPoints = 29,
    EmbedAnima = 69,
    RemoveAnima = 70,
}

public sealed record MsgItemOperationPacket(
    uint ItemUid,
    ulong Price,
    uint SecondaryValue,
    MsgItemOperationAction Action,
    uint TimestampToken,
    bool HasPrice,
    bool HasSecondaryValue)
{
    private const int ItemUidField = 1;
    private const int PriceField = 2;
    private const int SecondaryValueField = 3;
    private const int ActionField = 6;
    private const int TimestampTokenField = 7;

    public const ushort Opcode = WorldPacketOpcodes.MsgItemOperation;

    public uint HostItemUid => ItemUid;

    public uint SecondaryItemUid => SecondaryValue;

    public static MsgItemOperationPacket WithPrice(
        uint itemUid,
        ulong price,
        MsgItemOperationAction action,
        uint timestampToken)
    {
        return new MsgItemOperationPacket(
            itemUid,
            price,
            SecondaryValue: 0,
            action,
            timestampToken,
            HasPrice: true,
            HasSecondaryValue: false);
    }

    public static MsgItemOperationPacket WithSecondaryValue(
        uint itemUid,
        uint secondaryValue,
        MsgItemOperationAction action,
        uint timestampToken)
    {
        return new MsgItemOperationPacket(
            itemUid,
            Price: 0,
            secondaryValue,
            action,
            timestampToken,
            HasPrice: false,
            HasSecondaryValue: true);
    }

    public static bool TryParse(PacketFrame frame, out MsgItemOperationPacket packet)
    {
        packet = default!;
        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new LegacyProtoWire.Reader(frame.Payload.ToArray());
        var itemUid = 0U;
        var price = 0UL;
        var secondaryValue = 0U;
        var action = 0U;
        var timestampToken = 0U;
        var hasItemUid = false;
        var hasPrice = false;
        var hasSecondaryValue = false;
        var hasAction = false;
        var hasTimestampToken = false;

        while (!reader.End)
        {
            if (!reader.TryReadTag(out var fieldNumber, out var wireType))
            {
                return false;
            }

            if (wireType != LegacyProtoWire.VarintWireType)
            {
                if (!reader.TrySkipField(wireType))
                {
                    return false;
                }

                continue;
            }

            switch (fieldNumber)
            {
                case ItemUidField:
                    if (!reader.TryReadUInt32(out itemUid))
                    {
                        return false;
                    }

                    hasItemUid = true;
                    break;
                case PriceField:
                    if (!reader.TryReadVarint(out price))
                    {
                        return false;
                    }

                    hasPrice = true;
                    break;
                case SecondaryValueField:
                    if (!reader.TryReadUInt32(out secondaryValue))
                    {
                        return false;
                    }

                    hasSecondaryValue = true;
                    break;
                case ActionField:
                    if (!reader.TryReadUInt32(out action))
                    {
                        return false;
                    }

                    hasAction = true;
                    break;
                case TimestampTokenField:
                    if (!reader.TryReadUInt32(out timestampToken))
                    {
                        return false;
                    }

                    hasTimestampToken = true;
                    break;
                default:
                    if (!reader.TrySkipField(wireType))
                    {
                        return false;
                    }

                    break;
            }
        }

        if (!hasItemUid || !hasAction || !hasTimestampToken)
        {
            return false;
        }

        packet = new MsgItemOperationPacket(
            itemUid,
            price,
            secondaryValue,
            (MsgItemOperationAction)action,
            timestampToken,
            hasPrice,
            hasSecondaryValue);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var payload = new List<byte>();
        LegacyProtoWire.WriteVarintField(payload, ItemUidField, ItemUid);
        if (HasPrice)
        {
            LegacyProtoWire.WriteVarintField(payload, PriceField, Price);
        }

        if (HasSecondaryValue)
        {
            LegacyProtoWire.WriteVarintField(payload, SecondaryValueField, SecondaryValue);
        }

        LegacyProtoWire.WriteVarintField(payload, ActionField, (uint)Action);
        LegacyProtoWire.WriteVarintField(payload, TimestampTokenField, TimestampToken);

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }
}
