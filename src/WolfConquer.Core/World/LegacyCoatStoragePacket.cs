using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public readonly record struct LegacyCoatStorageItem(
    uint ItemUid,
    uint ItemId,
    uint DwParam3,
    uint DwParam4,
    uint DwParam5,
    uint DwParam6,
    uint DwParam7,
    uint Plus,
    uint Bless,
    uint Type1,
    uint DwParam11,
    uint DwParam12,
    uint DwParam13,
    uint DwParam14,
    uint Type2,
    uint DwParam16,
    uint DwParam17,
    uint DwParam18,
    uint TimeLeft,
    uint DwParam20,
    uint Stack,
    uint MinDurability,
    uint MaxDurability);

public sealed record LegacyCoatStoragePacket(
    uint ActionId,
    uint DwParam1,
    uint DwParam2,
    uint DwParam3,
    IReadOnlyList<LegacyCoatStorageItem> Items)
{
    private const int ActionIdField = 1;
    private const int DwParam1Field = 2;
    private const int DwParam2Field = 3;
    private const int DwParam3Field = 4;
    private const int ItemField = 5;
    private const int VarintWireType = 0;
    private const int LengthDelimitedWireType = 2;

    public const ushort Opcode = WorldPacketOpcodes.LegacyCoatStorage;
    public const uint InventoryToWardrobeActionId = 1;
    public const uint RetrieveActionId = 2;
    public const uint EquipRetrieveNoOpActionId = InventoryToWardrobeActionId | RetrieveActionId;
    public const uint EquipFromWardrobeActionId = 4;
    public const uint TakeOffActionId = 5;

    public static IReadOnlyList<LegacyCoatStoragePacket> CreateLegacyStartup(PlayerItemSnapshot playerItems)
    {
        ArgumentNullException.ThrowIfNull(playerItems);

        return CreateLegacyStartup(playerItems.WardrobeItems);
    }

    public static IReadOnlyList<LegacyCoatStoragePacket> CreateLegacyStartup(
        IReadOnlyList<PlayerItemSnapshotItem> wardrobeItems)
    {
        ArgumentNullException.ThrowIfNull(wardrobeItems);

        var packets = new List<LegacyCoatStoragePacket>();
        AddBucket(packets, wardrobeItems.Where(item => ItemType.GetLegacyEquipmentPosition(item.ItemId) == 9));
        AddBucket(packets, wardrobeItems.Where(item => ItemType.GetLegacyEquipmentPosition(item.ItemId) == 17));
        return packets;
    }

    public static LegacyCoatStoragePacket CreateEmptyStartup()
    {
        return new LegacyCoatStoragePacket(ActionId: 0, DwParam1: 0, DwParam2: 0, DwParam3: 0, []);
    }

    public static LegacyCoatStoragePacket CreateTakeOffResponse(PlayerItemSnapshotItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new LegacyCoatStoragePacket(
            TakeOffActionId,
            item.Uid,
            item.ItemId,
            DwParam3: 0,
            []);
    }

    public static LegacyCoatStoragePacket CreateInventoryToWardrobeResponse(PlayerItemSnapshotItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new LegacyCoatStoragePacket(
            InventoryToWardrobeActionId,
            item.Uid,
            DwParam2: 0,
            DwParam3: 0,
            []);
    }

    public static LegacyCoatStoragePacket CreateRetrieveResponse(PlayerItemSnapshotItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new LegacyCoatStoragePacket(
            RetrieveActionId,
            item.Uid,
            item.ItemId,
            DwParam3: 0,
            []);
    }

    public static LegacyCoatStoragePacket CreateEquipFromWardrobeResponse(PlayerItemSnapshotItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new LegacyCoatStoragePacket(
            EquipFromWardrobeActionId,
            item.Uid,
            item.ItemId,
            DwParam3: 0,
            []);
    }

    public static LegacyCoatStorageItem FromSnapshotItem(PlayerItemSnapshotItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var durability = item.MaximumDurability;
        return new LegacyCoatStorageItem(
            item.Uid,
            item.ItemId,
            DwParam3: 0,
            DwParam4: 0,
            DwParam5: 0,
            DwParam6: 0,
            DwParam7: 0,
            item.Plus,
            item.Bless,
            Type1: 3,
            DwParam11: 0,
            DwParam12: 0,
            DwParam13: 0,
            DwParam14: 0,
            Type2: 3,
            DwParam16: 0,
            DwParam17: 0,
            DwParam18: 0,
            item.TimeLeftInMinutes,
            DwParam20: 0,
            Stack: 1,
            durability,
            durability);
    }

    private static void AddBucket(
        List<LegacyCoatStoragePacket> packets,
        IEnumerable<PlayerItemSnapshotItem> items)
    {
        var bucket = items
            .Where(item => item.Position == 0)
            .ToList();
        var remaining = bucket.Count;

        for (var offset = 0; offset < bucket.Count / 10 + 1; offset++)
        {
            var count = remaining > 10 ? 10 : remaining;
            var packetItems = new LegacyCoatStorageItem[count];
            for (var index = 0; index < packetItems.Length; index++)
            {
                if (remaining <= 0)
                {
                    break;
                }

                packetItems[index] = FromSnapshotItem(bucket[remaining - 1]);
                remaining--;
            }

            packets.Add(new LegacyCoatStoragePacket(
                ActionId: 0,
                DwParam1: 0,
                DwParam2: 0,
                DwParam3: 0,
                packetItems));
        }
    }

    public static bool TryParse(PacketFrame frame, out LegacyCoatStoragePacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new ProtoReader(frame.Payload.ToArray());
        var actionId = 0u;
        var dwParam1 = 0u;
        var dwParam2 = 0u;
        var dwParam3 = 0u;
        var items = new List<LegacyCoatStorageItem>();
        var hasActionId = false;
        var hasDwParam3 = false;

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
                case ActionIdField:
                    if (!TryReadUInt32Field(ref reader, wireType, out actionId))
                    {
                        return false;
                    }

                    hasActionId = true;
                    break;
                case DwParam1Field:
                    if (!TryReadUInt32Field(ref reader, wireType, out dwParam1))
                    {
                        return false;
                    }

                    break;
                case DwParam2Field:
                    if (!TryReadUInt32Field(ref reader, wireType, out dwParam2))
                    {
                        return false;
                    }

                    break;
                case DwParam3Field:
                    if (!TryReadUInt32Field(ref reader, wireType, out dwParam3))
                    {
                        return false;
                    }

                    hasDwParam3 = true;
                    break;
                case ItemField:
                    if (wireType != LengthDelimitedWireType ||
                        !reader.TryReadBytes(out var itemBytes) ||
                        !TryParseItem(itemBytes, out var item))
                    {
                        return false;
                    }

                    items.Add(item);
                    break;
                default:
                    return false;
            }
        }

        if (!hasActionId || !hasDwParam3)
        {
            return false;
        }

        packet = new LegacyCoatStoragePacket(actionId, dwParam1, dwParam2, dwParam3, items);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var payload = new List<byte>();
        WriteVarintField(payload, ActionIdField, ActionId);
        WriteOptionalVarintField(payload, DwParam1Field, DwParam1);
        WriteOptionalVarintField(payload, DwParam2Field, DwParam2);
        WriteVarintField(payload, DwParam3Field, DwParam3);
        foreach (var item in Items)
        {
            var itemPayload = new List<byte>();
            WriteItem(itemPayload, item);
            WriteBytes(payload, ItemField, itemPayload);
        }

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static bool TryReadUInt32Field(ref ProtoReader reader, int wireType, out uint value)
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

    private static bool TryParseItem(byte[] payload, out LegacyCoatStorageItem item)
    {
        var reader = new ProtoReader(payload);
        var values = new uint[23];
        var seen = new bool[23];

        while (!reader.End)
        {
            if (!reader.TryReadVarint(out var tagValue) || tagValue == 0)
            {
                item = default;
                return false;
            }

            var fieldNumber = (int)(tagValue >> 3);
            var wireType = (int)(tagValue & 0x7);
            if (fieldNumber is < 1 or > 23 ||
                !TryReadUInt32Field(ref reader, wireType, out var value))
            {
                item = default;
                return false;
            }

            values[fieldNumber - 1] = value;
            seen[fieldNumber - 1] = true;
        }

        if (seen.Any(present => !present))
        {
            item = default;
            return false;
        }

        item = new LegacyCoatStorageItem(
            values[0],
            values[1],
            values[2],
            values[3],
            values[4],
            values[5],
            values[6],
            values[7],
            values[8],
            values[9],
            values[10],
            values[11],
            values[12],
            values[13],
            values[14],
            values[15],
            values[16],
            values[17],
            values[18],
            values[19],
            values[20],
            values[21],
            values[22]);
        return true;
    }

    private static void WriteItem(List<byte> payload, LegacyCoatStorageItem item)
    {
        WriteVarintField(payload, 1, item.ItemUid);
        WriteVarintField(payload, 2, item.ItemId);
        WriteVarintField(payload, 3, item.DwParam3);
        WriteVarintField(payload, 4, item.DwParam4);
        WriteVarintField(payload, 5, item.DwParam5);
        WriteVarintField(payload, 6, item.DwParam6);
        WriteVarintField(payload, 7, item.DwParam7);
        WriteVarintField(payload, 8, item.Plus);
        WriteVarintField(payload, 9, item.Bless);
        WriteVarintField(payload, 10, item.Type1);
        WriteVarintField(payload, 11, item.DwParam11);
        WriteVarintField(payload, 12, item.DwParam12);
        WriteVarintField(payload, 13, item.DwParam13);
        WriteVarintField(payload, 14, item.DwParam14);
        WriteVarintField(payload, 15, item.Type2);
        WriteVarintField(payload, 16, item.DwParam16);
        WriteVarintField(payload, 17, item.DwParam17);
        WriteVarintField(payload, 18, item.DwParam18);
        WriteVarintField(payload, 19, item.TimeLeft);
        WriteVarintField(payload, 20, item.DwParam20);
        WriteVarintField(payload, 21, item.Stack);
        WriteVarintField(payload, 22, item.MinDurability);
        WriteVarintField(payload, 23, item.MaxDurability);
    }

    private static void WriteOptionalVarintField(List<byte> payload, int fieldNumber, uint value)
    {
        if (value != 0)
        {
            WriteVarintField(payload, fieldNumber, value);
        }
    }

    private static void WriteVarintField(List<byte> payload, int fieldNumber, ulong value)
    {
        WriteVarint(payload, ((ulong)fieldNumber << 3) | VarintWireType);
        WriteVarint(payload, value);
    }

    private static void WriteBytes(List<byte> payload, int fieldNumber, IReadOnlyCollection<byte> value)
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

        public bool TryReadBytes(out byte[] value)
        {
            value = [];

            if (!TryReadVarint(out var length) ||
                length > int.MaxValue ||
                _offset > _payload.Length - (int)length)
            {
                return false;
            }

            value = _payload.Slice(_offset, (int)length).ToArray();
            _offset += (int)length;
            return true;
        }
    }
}
