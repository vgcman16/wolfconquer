using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyDaoQiType : uint
{
    Info = 0,
    LoadItem = 1,
    Buy = 2,
    TaoistReiki = 3,
    Draws = 4,
    Equip = 5,
    UnEquip = 6,
    AddObject = 7,
    Replace = 8,
    PirateUpgrade = 9,
    PirateItem = 10,
    PiratePoints = 11,
    Lottery = 12,
}

public readonly record struct LegacyDaoQiItem(uint Id);

public sealed record LegacyDaoQiPacket(
    LegacyDaoQiType Type,
    uint ItemId,
    uint TaoistReiki,
    uint Lottery,
    uint Data5,
    uint Data6,
    uint MasteryPoints,
    uint Data8,
    uint PirateExp,
    uint Data10,
    uint Position,
    uint Done,
    IReadOnlyList<LegacyDaoQiItem> Items,
    IReadOnlyList<uint> Positions)
{
    private const int TypeField = 1;
    private const int ItemIdField = 2;
    private const int TaoistReikiField = 3;
    private const int LotteryField = 4;
    private const int Data5Field = 5;
    private const int Data6Field = 6;
    private const int MasteryPointsField = 7;
    private const int Data8Field = 8;
    private const int PirateExpField = 9;
    private const int Data10Field = 10;
    private const int PositionField = 11;
    private const int DoneField = 12;
    private const int ItemsField = 13;
    private const int PositionsField = 14;
    private const int NestedItemIdField = 1;
    private const int VarintWireType = 0;
    private const int LengthDelimitedWireType = 2;

    public const ushort Opcode = WorldPacketOpcodes.LegacyDaoQi;

    public static LegacyDaoQiPacket Info(
        uint taoistReiki,
        uint lottery,
        uint masteryPoints,
        uint pirateExp)
    {
        return new LegacyDaoQiPacket(
            LegacyDaoQiType.Info,
            ItemId: 0,
            taoistReiki,
            lottery,
            Data5: 6,
            Data6: 9,
            masteryPoints,
            Data8: 1,
            pirateExp,
            Data10: 3,
            Position: 0,
            Done: 0,
            Items: [],
            Positions: []);
    }

    public static LegacyDaoQiPacket Info(CombatGearSnapshot combatGear)
    {
        ArgumentNullException.ThrowIfNull(combatGear);

        return Info(
            combatGear.TaoistReiki,
            combatGear.Lottery,
            combatGear.MasteryPoints,
            combatGear.PirateExp);
    }

    public static LegacyDaoQiPacket TaoistReikiUpdate(uint taoistReiki)
    {
        return new LegacyDaoQiPacket(
            LegacyDaoQiType.TaoistReiki,
            ItemId: 0,
            taoistReiki,
            Lottery: 0,
            Data5: 0,
            Data6: 0,
            MasteryPoints: 0,
            Data8: 0,
            PirateExp: 0,
            Data10: 0,
            Position: 0,
            Done: 0,
            Items: [],
            Positions: []);
    }

    public static LegacyDaoQiPacket LotteryUpdate(uint lottery)
    {
        return new LegacyDaoQiPacket(
            LegacyDaoQiType.Lottery,
            ItemId: 0,
            TaoistReiki: 0,
            lottery,
            Data5: 0,
            Data6: 0,
            MasteryPoints: 0,
            Data8: 0,
            PirateExp: 0,
            Data10: 0,
            Position: 0,
            Done: 0,
            Items: [],
            Positions: []);
    }

    public static LegacyDaoQiPacket PiratePointsUpdate(uint pirateExp)
    {
        return new LegacyDaoQiPacket(
            LegacyDaoQiType.PiratePoints,
            ItemId: 0,
            TaoistReiki: 0,
            Lottery: 0,
            Data5: 0,
            Data6: 0,
            MasteryPoints: 0,
            Data8: 0,
            pirateExp,
            Data10: 0,
            Position: 0,
            Done: 0,
            Items: [],
            Positions: []);
    }

    public static LegacyDaoQiPacket AddObject(uint itemId)
    {
        return new LegacyDaoQiPacket(
            LegacyDaoQiType.AddObject,
            ItemId: 0,
            TaoistReiki: 0,
            Lottery: 0,
            Data5: 0,
            Data6: 0,
            MasteryPoints: 0,
            Data8: 0,
            PirateExp: 0,
            Data10: 0,
            Position: 0,
            Done: 0,
            Items: [new LegacyDaoQiItem(itemId)],
            Positions: []);
    }

    public static LegacyDaoQiPacket LoadItem(CombatGearSnapshot combatGear)
    {
        ArgumentNullException.ThrowIfNull(combatGear);

        var items = new LegacyDaoQiItem[combatGear.BlackJadeBag.BlackJades.Count];
        for (var i = 0; i < items.Length; i++)
        {
            items[i] = new LegacyDaoQiItem(combatGear.BlackJadeBag.BlackJades[i]);
        }

        return new LegacyDaoQiPacket(
            LegacyDaoQiType.LoadItem,
            ItemId: 0,
            TaoistReiki: 0,
            Lottery: 0,
            Data5: 0,
            Data6: 0,
            MasteryPoints: 0,
            Data8: 0,
            PirateExp: 0,
            Data10: 0,
            Position: 0,
            Done: 0,
            Items: items,
            Positions: []);
    }

    public static bool TryParse(PacketFrame frame, out LegacyDaoQiPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new LegacyCombatGearPacket.ProtoReader(frame.Payload.ToArray());
        var type = LegacyDaoQiType.Info;
        var itemId = 0U;
        var taoistReiki = 0U;
        var lottery = 0U;
        var data5 = 0U;
        var data6 = 0U;
        var masteryPoints = 0U;
        var data8 = 0U;
        var pirateExp = 0U;
        var data10 = 0U;
        var position = 0U;
        var done = 0U;
        var items = new List<LegacyDaoQiItem>();
        var positions = new List<uint>();
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
                    if (!TryReadType(ref reader, wireType, out type))
                    {
                        return false;
                    }

                    hasType = true;
                    break;
                case ItemIdField:
                    if (!TryReadUInt32(ref reader, wireType, out itemId))
                    {
                        return false;
                    }

                    break;
                case TaoistReikiField:
                    if (!TryReadUInt32(ref reader, wireType, out taoistReiki))
                    {
                        return false;
                    }

                    break;
                case LotteryField:
                    if (!TryReadUInt32(ref reader, wireType, out lottery))
                    {
                        return false;
                    }

                    break;
                case Data5Field:
                    if (!TryReadUInt32(ref reader, wireType, out data5))
                    {
                        return false;
                    }

                    break;
                case Data6Field:
                    if (!TryReadUInt32(ref reader, wireType, out data6))
                    {
                        return false;
                    }

                    break;
                case MasteryPointsField:
                    if (!TryReadUInt32(ref reader, wireType, out masteryPoints))
                    {
                        return false;
                    }

                    break;
                case Data8Field:
                    if (!TryReadUInt32(ref reader, wireType, out data8))
                    {
                        return false;
                    }

                    break;
                case PirateExpField:
                    if (!TryReadUInt32(ref reader, wireType, out pirateExp))
                    {
                        return false;
                    }

                    break;
                case Data10Field:
                    if (!TryReadUInt32(ref reader, wireType, out data10))
                    {
                        return false;
                    }

                    break;
                case PositionField:
                    if (!TryReadUInt32(ref reader, wireType, out position))
                    {
                        return false;
                    }

                    break;
                case DoneField:
                    if (!TryReadUInt32(ref reader, wireType, out done))
                    {
                        return false;
                    }

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
                case PositionsField:
                    if (!TryReadPosition(ref reader, wireType, positions))
                    {
                        return false;
                    }

                    break;
                default:
                    return false;
            }
        }

        if (!hasType)
        {
            return false;
        }

        packet = new LegacyDaoQiPacket(
            type,
            itemId,
            taoistReiki,
            lottery,
            data5,
            data6,
            masteryPoints,
            data8,
            pirateExp,
            data10,
            position,
            done,
            items,
            positions);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        ArgumentNullException.ThrowIfNull(Items);
        ArgumentNullException.ThrowIfNull(Positions);

        var payload = new List<byte>();
        WriteVarintField(payload, TypeField, (uint)Type);
        WriteVarintField(payload, ItemIdField, ItemId);
        WriteVarintField(payload, TaoistReikiField, TaoistReiki);
        WriteVarintField(payload, LotteryField, Lottery);
        WriteVarintField(payload, Data5Field, Data5);
        WriteVarintField(payload, Data6Field, Data6);
        WriteVarintField(payload, MasteryPointsField, MasteryPoints);
        WriteVarintField(payload, Data8Field, Data8);
        WriteVarintField(payload, PirateExpField, PirateExp);
        WriteVarintField(payload, Data10Field, Data10);
        WriteVarintField(payload, PositionField, Position);
        WriteVarintField(payload, DoneField, Done);
        foreach (var item in Items)
        {
            var itemPayload = new List<byte>();
            WriteVarintField(itemPayload, NestedItemIdField, item.Id);
            WriteLengthDelimitedField(payload, ItemsField, itemPayload);
        }

        foreach (var position in Positions)
        {
            WriteVarintField(payload, PositionsField, position);
        }

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static bool TryParseItem(ReadOnlySpan<byte> payload, out LegacyDaoQiItem item)
    {
        item = default;
        var reader = new LegacyCombatGearPacket.ProtoReader(payload);
        var id = 0U;
        var hasId = false;

        while (!reader.End)
        {
            if (!reader.TryReadTag(out var fieldNumber, out var wireType))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case NestedItemIdField:
                    if (!TryReadUInt32(ref reader, wireType, out id))
                    {
                        return false;
                    }

                    hasId = true;
                    break;
                default:
                    return false;
            }
        }

        if (!hasId)
        {
            return false;
        }

        item = new LegacyDaoQiItem(id);
        return true;
    }

    private static bool TryReadType(
        ref LegacyCombatGearPacket.ProtoReader reader,
        int wireType,
        out LegacyDaoQiType type)
    {
        type = LegacyDaoQiType.Info;
        if (!TryReadUInt32(ref reader, wireType, out var raw) ||
            raw > (uint)LegacyDaoQiType.Lottery)
        {
            return false;
        }

        type = (LegacyDaoQiType)raw;
        return true;
    }

    private static bool TryReadPosition(
        ref LegacyCombatGearPacket.ProtoReader reader,
        int wireType,
        List<uint> positions)
    {
        switch (wireType)
        {
            case VarintWireType:
                if (!TryReadUInt32(ref reader, wireType, out var position))
                {
                    return false;
                }

                positions.Add(position);
                return true;
            case LengthDelimitedWireType:
                if (!reader.TryReadLengthDelimited(out var payload))
                {
                    return false;
                }

                var packedReader = new LegacyCombatGearPacket.ProtoReader(payload);
                while (!packedReader.End)
                {
                    if (!packedReader.TryReadVarint(out var raw) || raw > uint.MaxValue)
                    {
                        return false;
                    }

                    positions.Add((uint)raw);
                }

                return true;
            default:
                return false;
        }
    }

    private static bool TryReadUInt32(
        ref LegacyCombatGearPacket.ProtoReader reader,
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
        LegacyCombatGearPacket.WriteVarint(payload, ((ulong)fieldNumber << 3) | VarintWireType);
        LegacyCombatGearPacket.WriteVarint(payload, value);
    }

    private static void WriteLengthDelimitedField(List<byte> payload, int fieldNumber, IReadOnlyList<byte> value)
    {
        LegacyCombatGearPacket.WriteLengthDelimitedField(payload, fieldNumber, value);
    }
}
