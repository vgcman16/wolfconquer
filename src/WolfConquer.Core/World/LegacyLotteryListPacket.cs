using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed record LegacyLotteryListItem(
    uint Bliss,
    uint Id,
    uint ItemId,
    uint Unknown4,
    string Name);

public sealed record LegacyLotteryListPacket(uint Type, IReadOnlyList<LegacyLotteryListItem> Items)
{
    private const int TypeField = 1;
    private const int ItemsField = 2;
    private const int ItemBlissField = 1;
    private const int ItemIdField = 2;
    private const int ItemTypeIdField = 3;
    private const int ItemUnknown4Field = 4;
    private const int ItemNameField = 5;

    public const ushort Opcode = WorldPacketOpcodes.LegacyLotteryList;
    public const uint AwardListType = 0;

    public bool IsAwardListRequest => Type == AwardListType;

    public static LegacyLotteryListPacket Query()
    {
        return new LegacyLotteryListPacket(Type: AwardListType, Items: []);
    }

    public static LegacyLotteryListPacket EmptyAwardPage()
    {
        return new LegacyLotteryListPacket(Type: AwardListType, Items: []);
    }

    public static bool TryParse(PacketFrame frame, out LegacyLotteryListPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new LegacyProtoWire.Reader(frame.Payload.ToArray());
        var type = 0U;
        var hasType = false;
        var items = new List<LegacyLotteryListItem>();

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
                case ItemsField:
                    if (wireType != LegacyProtoWire.LengthDelimitedWireType ||
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

        packet = new LegacyLotteryListPacket(type, items);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        ArgumentNullException.ThrowIfNull(Items);

        var payload = new List<byte>();
        LegacyProtoWire.WriteVarintField(payload, TypeField, Type);

        foreach (var item in Items)
        {
            var itemPayload = new List<byte>();
            LegacyProtoWire.WriteVarintField(itemPayload, ItemBlissField, item.Bliss);
            LegacyProtoWire.WriteVarintField(itemPayload, ItemIdField, item.Id);
            LegacyProtoWire.WriteVarintField(itemPayload, ItemTypeIdField, item.ItemId);
            LegacyProtoWire.WriteVarintField(itemPayload, ItemUnknown4Field, item.Unknown4);
            LegacyProtoWire.WriteStringField(itemPayload, ItemNameField, item.Name);
            LegacyProtoWire.WriteBytesField(payload, ItemsField, itemPayload);
        }

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.ToArray().CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static bool TryParseItem(ReadOnlySpan<byte> payload, out LegacyLotteryListItem item)
    {
        item = default!;

        var reader = new LegacyProtoWire.Reader(payload);
        var bliss = 0U;
        var id = 0U;
        var itemId = 0U;
        var unknown4 = 0U;
        var name = string.Empty;
        var hasBliss = false;
        var hasId = false;
        var hasItemId = false;
        var hasUnknown4 = false;
        var hasName = false;

        while (!reader.End)
        {
            if (!reader.TryReadTag(out var fieldNumber, out var wireType))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case ItemBlissField:
                    if (wireType != LegacyProtoWire.VarintWireType ||
                        !reader.TryReadUInt32(out bliss))
                    {
                        return false;
                    }

                    hasBliss = true;
                    break;
                case ItemIdField:
                    if (wireType != LegacyProtoWire.VarintWireType ||
                        !reader.TryReadUInt32(out id))
                    {
                        return false;
                    }

                    hasId = true;
                    break;
                case ItemTypeIdField:
                    if (wireType != LegacyProtoWire.VarintWireType ||
                        !reader.TryReadUInt32(out itemId))
                    {
                        return false;
                    }

                    hasItemId = true;
                    break;
                case ItemUnknown4Field:
                    if (wireType != LegacyProtoWire.VarintWireType ||
                        !reader.TryReadUInt32(out unknown4))
                    {
                        return false;
                    }

                    hasUnknown4 = true;
                    break;
                case ItemNameField:
                    if (wireType != LegacyProtoWire.LengthDelimitedWireType ||
                        !reader.TryReadString(out name))
                    {
                        return false;
                    }

                    hasName = true;
                    break;
                default:
                    return false;
            }
        }

        if (!hasBliss || !hasId || !hasItemId || !hasUnknown4 || !hasName)
        {
            return false;
        }

        item = new LegacyLotteryListItem(bliss, id, itemId, unknown4, name);
        return true;
    }
}
