using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyExchangeShopGoodsInfoType : uint
{
    Refresh = 1,
    Show = 2,
    Open = 3,
}

public sealed record LegacyExchangeShopGoodsInfoPacket(
    LegacyExchangeShopGoodsInfoType Type,
    uint NpcID,
    uint ShopID)
{
    private const int TypeField = 1;
    private const int NpcIDField = 2;
    private const int ShopIDField = 3;
    private const int VarintWireType = 0;

    public const ushort Opcode = WorldPacketOpcodes.LegacyExchangeShopGoodsInfo;

    public static bool TryParse(PacketFrame frame, out LegacyExchangeShopGoodsInfoPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new ProtoReader(frame.Payload.ToArray());
        var type = LegacyExchangeShopGoodsInfoType.Show;
        var npcId = 0u;
        var shopId = 0u;
        var hasType = false;
        var hasNpcId = false;
        var hasShopId = false;

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
                    if (!TryReadUInt32(ref reader, wireType, out var typeValue) ||
                        !TryToType(typeValue, out type))
                    {
                        return false;
                    }

                    hasType = true;
                    break;

                case NpcIDField:
                    if (!TryReadUInt32(ref reader, wireType, out npcId))
                    {
                        return false;
                    }

                    hasNpcId = true;
                    break;

                case ShopIDField:
                    if (!TryReadUInt32(ref reader, wireType, out shopId))
                    {
                        return false;
                    }

                    hasShopId = true;
                    break;

                default:
                    return false;
            }
        }

        if (!hasType || !hasNpcId || !hasShopId)
        {
            return false;
        }

        packet = new LegacyExchangeShopGoodsInfoPacket(type, npcId, shopId);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var payload = new List<byte>();
        WriteVarintField(payload, TypeField, (uint)Type);
        WriteVarintField(payload, NpcIDField, NpcID);
        WriteVarintField(payload, ShopIDField, ShopID);

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static bool TryToType(uint value, out LegacyExchangeShopGoodsInfoType type)
    {
        type = (LegacyExchangeShopGoodsInfoType)value;
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

    private static void WriteVarintField(List<byte> payload, int fieldNumber, uint value)
    {
        WriteVarint(payload, ((ulong)fieldNumber << 3) | VarintWireType);
        WriteVarint(payload, value);
    }

    internal static void WriteVarint(List<byte> payload, ulong value)
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

    internal ref struct ProtoReader(ReadOnlySpan<byte> payload)
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

        public bool TryReadLengthDelimited(out ReadOnlySpan<byte> value)
        {
            value = default;
            if (!TryReadVarint(out var length) ||
                length > int.MaxValue ||
                _offset + (int)length > _payload.Length)
            {
                return false;
            }

            value = _payload.Slice(_offset, (int)length);
            _offset += (int)length;
            return true;
        }
    }
}

public enum LegacyExchangeShopBuyType : uint
{
    Goods = 0,
    GoodsEx = 1,
}

public sealed record LegacyExchangeShopBuyItem(
    uint ID,
    uint Count);

public sealed record LegacyExchangeShopBuyPacket(
    LegacyExchangeShopBuyType Type,
    uint NpcID,
    uint ShopID,
    uint Count,
    uint Index,
    IReadOnlyList<LegacyExchangeShopBuyItem> Items)
{
    private const int TypeField = 1;
    private const int NpcIDField = 2;
    private const int ShopIDField = 3;
    private const int CountField = 4;
    private const int IndexField = 5;
    private const int ItemsField = 6;
    private const int ItemIDField = 1;
    private const int ItemCountField = 2;
    private const int VarintWireType = 0;
    private const int LengthDelimitedWireType = 2;

    public const ushort Opcode = WorldPacketOpcodes.LegacyExchangeShopBuy;

    public static bool TryParse(PacketFrame frame, out LegacyExchangeShopBuyPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new LegacyExchangeShopGoodsInfoPacket.ProtoReader(frame.Payload.ToArray());
        var type = LegacyExchangeShopBuyType.Goods;
        var npcId = 0u;
        var shopId = 0u;
        var count = 0u;
        var index = 0u;
        var hasType = false;
        var hasNpcId = false;
        var hasShopId = false;
        var hasCount = false;
        var hasIndex = false;
        var items = new List<LegacyExchangeShopBuyItem>();

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
                    if (!TryReadUInt32(ref reader, wireType, out var typeValue))
                    {
                        return false;
                    }

                    type = (LegacyExchangeShopBuyType)typeValue;
                    hasType = true;
                    break;

                case NpcIDField:
                    if (!TryReadUInt32(ref reader, wireType, out npcId))
                    {
                        return false;
                    }

                    hasNpcId = true;
                    break;

                case ShopIDField:
                    if (!TryReadUInt32(ref reader, wireType, out shopId))
                    {
                        return false;
                    }

                    hasShopId = true;
                    break;

                case CountField:
                    if (!TryReadUInt32(ref reader, wireType, out count))
                    {
                        return false;
                    }

                    hasCount = true;
                    break;

                case IndexField:
                    if (!TryReadUInt32(ref reader, wireType, out index))
                    {
                        return false;
                    }

                    hasIndex = true;
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

        if (!hasType || !hasNpcId || !hasShopId || !hasCount || !hasIndex)
        {
            return false;
        }

        packet = new LegacyExchangeShopBuyPacket(type, npcId, shopId, count, index, items.ToArray());
        return true;
    }

    public byte[] ToFrameBytes()
    {
        ArgumentNullException.ThrowIfNull(Items);

        var payload = new List<byte>();
        WriteVarintField(payload, TypeField, (uint)Type);
        WriteVarintField(payload, NpcIDField, NpcID);
        WriteVarintField(payload, ShopIDField, ShopID);
        WriteVarintField(payload, CountField, Count);
        WriteVarintField(payload, IndexField, Index);

        foreach (var item in Items)
        {
            var itemPayload = new List<byte>();
            WriteVarintField(itemPayload, ItemIDField, item.ID);
            WriteVarintField(itemPayload, ItemCountField, item.Count);
            WriteLengthDelimitedField(payload, ItemsField, itemPayload);
        }

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static bool TryParseItem(ReadOnlySpan<byte> payload, out LegacyExchangeShopBuyItem item)
    {
        item = default!;
        var reader = new LegacyExchangeShopGoodsInfoPacket.ProtoReader(payload);
        var id = 0u;
        var count = 0u;
        var hasId = false;
        var hasCount = false;

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
                case ItemIDField:
                    if (!TryReadUInt32(ref reader, wireType, out id))
                    {
                        return false;
                    }

                    hasId = true;
                    break;

                case ItemCountField:
                    if (!TryReadUInt32(ref reader, wireType, out count))
                    {
                        return false;
                    }

                    hasCount = true;
                    break;

                default:
                    return false;
            }
        }

        if (!hasId || !hasCount)
        {
            return false;
        }

        item = new LegacyExchangeShopBuyItem(id, count);
        return true;
    }

    private static bool TryReadUInt32(
        ref LegacyExchangeShopGoodsInfoPacket.ProtoReader reader,
        int wireType,
        out uint value)
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

    private static void WriteVarintField(List<byte> payload, int fieldNumber, uint value)
    {
        LegacyExchangeShopGoodsInfoPacket.WriteVarint(payload, ((ulong)fieldNumber << 3) | VarintWireType);
        LegacyExchangeShopGoodsInfoPacket.WriteVarint(payload, value);
    }

    private static void WriteLengthDelimitedField(List<byte> payload, int fieldNumber, List<byte> value)
    {
        LegacyExchangeShopGoodsInfoPacket.WriteVarint(payload, ((ulong)fieldNumber << 3) | LengthDelimitedWireType);
        LegacyExchangeShopGoodsInfoPacket.WriteVarint(payload, (ulong)value.Count);
        payload.AddRange(value);
    }
}

public sealed record LegacyExchangeShopGoodsItem(
    uint Index,
    uint Count,
    uint Unknown);

public sealed record LegacyExchangeShopGoodsPacket(
    uint NpcID,
    uint ShopID,
    uint Uid,
    uint TimeInSeconds,
    uint Unknown5,
    IReadOnlyList<LegacyExchangeShopGoodsItem> Items)
{
    private const int NpcIDField = 1;
    private const int ShopIDField = 2;
    private const int UidField = 3;
    private const int TimeInSecondsField = 4;
    private const int Unknown5Field = 5;
    private const int ItemsField = 6;
    private const int ItemIndexField = 1;
    private const int ItemCountField = 2;
    private const int ItemUnknownField = 3;
    private const int VarintWireType = 0;
    private const int LengthDelimitedWireType = 2;

    public const ushort Opcode = WorldPacketOpcodes.LegacyExchangeShopGoods;

    public static LegacyExchangeShopGoodsPacket Empty(uint npcId, uint shopId, uint uid, uint timeInSeconds)
    {
        return new LegacyExchangeShopGoodsPacket(npcId, shopId, uid, timeInSeconds, Unknown5: 0, []);
    }

    public static bool TryParse(PacketFrame frame, out LegacyExchangeShopGoodsPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new LegacyExchangeShopGoodsInfoPacket.ProtoReader(frame.Payload.ToArray());
        var npcId = 0u;
        var shopId = 0u;
        var uid = 0u;
        var timeInSeconds = 0u;
        var unknown5 = 0u;
        var hasNpcId = false;
        var hasShopId = false;
        var hasUid = false;
        var hasTimeInSeconds = false;
        var hasUnknown5 = false;
        var items = new List<LegacyExchangeShopGoodsItem>();

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
                case NpcIDField:
                    if (!TryReadUInt32(ref reader, wireType, out npcId))
                    {
                        return false;
                    }

                    hasNpcId = true;
                    break;

                case ShopIDField:
                    if (!TryReadUInt32(ref reader, wireType, out shopId))
                    {
                        return false;
                    }

                    hasShopId = true;
                    break;

                case UidField:
                    if (!TryReadUInt32(ref reader, wireType, out uid))
                    {
                        return false;
                    }

                    hasUid = true;
                    break;

                case TimeInSecondsField:
                    if (!TryReadUInt32(ref reader, wireType, out timeInSeconds))
                    {
                        return false;
                    }

                    hasTimeInSeconds = true;
                    break;

                case Unknown5Field:
                    if (!TryReadUInt32(ref reader, wireType, out unknown5))
                    {
                        return false;
                    }

                    hasUnknown5 = true;
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

        if (!hasNpcId || !hasShopId || !hasUid || !hasTimeInSeconds || !hasUnknown5)
        {
            return false;
        }

        packet = new LegacyExchangeShopGoodsPacket(npcId, shopId, uid, timeInSeconds, unknown5, items.ToArray());
        return true;
    }

    public byte[] ToFrameBytes()
    {
        ArgumentNullException.ThrowIfNull(Items);

        var payload = new List<byte>();
        WriteVarintField(payload, NpcIDField, NpcID);
        WriteVarintField(payload, ShopIDField, ShopID);
        WriteVarintField(payload, UidField, Uid);
        WriteVarintField(payload, TimeInSecondsField, TimeInSeconds);
        WriteVarintField(payload, Unknown5Field, Unknown5);

        foreach (var item in Items)
        {
            var itemPayload = new List<byte>();
            WriteVarintField(itemPayload, ItemIndexField, item.Index);
            WriteVarintField(itemPayload, ItemCountField, item.Count);
            WriteVarintField(itemPayload, ItemUnknownField, item.Unknown);
            WriteLengthDelimitedField(payload, ItemsField, itemPayload);
        }

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static bool TryParseItem(ReadOnlySpan<byte> payload, out LegacyExchangeShopGoodsItem item)
    {
        item = default!;
        var reader = new LegacyExchangeShopGoodsInfoPacket.ProtoReader(payload);
        var index = 0u;
        var count = 0u;
        var unknown = 0u;
        var hasIndex = false;
        var hasCount = false;
        var hasUnknown = false;

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
                case ItemIndexField:
                    if (!TryReadUInt32(ref reader, wireType, out index))
                    {
                        return false;
                    }

                    hasIndex = true;
                    break;

                case ItemCountField:
                    if (!TryReadUInt32(ref reader, wireType, out count))
                    {
                        return false;
                    }

                    hasCount = true;
                    break;

                case ItemUnknownField:
                    if (!TryReadUInt32(ref reader, wireType, out unknown))
                    {
                        return false;
                    }

                    hasUnknown = true;
                    break;

                default:
                    return false;
            }
        }

        if (!hasIndex || !hasCount || !hasUnknown)
        {
            return false;
        }

        item = new LegacyExchangeShopGoodsItem(index, count, unknown);
        return true;
    }

    private static bool TryReadUInt32(
        ref LegacyExchangeShopGoodsInfoPacket.ProtoReader reader,
        int wireType,
        out uint value)
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

    private static void WriteVarintField(List<byte> payload, int fieldNumber, uint value)
    {
        LegacyExchangeShopGoodsInfoPacket.WriteVarint(payload, ((ulong)fieldNumber << 3) | VarintWireType);
        LegacyExchangeShopGoodsInfoPacket.WriteVarint(payload, value);
    }

    private static void WriteLengthDelimitedField(List<byte> payload, int fieldNumber, List<byte> value)
    {
        LegacyExchangeShopGoodsInfoPacket.WriteVarint(payload, ((ulong)fieldNumber << 3) | LengthDelimitedWireType);
        LegacyExchangeShopGoodsInfoPacket.WriteVarint(payload, (ulong)value.Count);
        payload.AddRange(value);
    }
}
