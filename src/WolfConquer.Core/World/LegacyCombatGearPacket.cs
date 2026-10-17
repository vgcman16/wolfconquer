using System.Buffers;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyCombatGearPacketType : uint
{
    Load = 0,
    View = 1,
    Add = 2,
    Update = 3,
    CrossServer = 10,
}

public enum LegacyAirPowerUpType : uint
{
    Info = 0,
    View = 1,
    Upgrade = 3,
    Practice = 4,
    AddPoints = 5,
}

public sealed record LegacyCombatGearItem(
    uint Id,
    uint Level,
    uint Exp,
    uint Equip,
    uint Transform,
    uint Data1,
    uint Data2,
    uint Data3,
    uint Data4,
    uint Data5,
    uint Data6,
    uint MasteryScore);

public sealed record LegacyCombatGearPacket(
    LegacyCombatGearPacketType Type,
    uint Uid,
    IReadOnlyList<LegacyCombatGearItem> Items)
{
    private const int TypeField = 1;
    private const int UidField = 2;
    private const int ItemsField = 3;
    private const int IdField = 1;
    private const int LevelField = 2;
    private const int ExpField = 3;
    private const int EquipField = 4;
    private const int TransformField = 5;
    private const int Data1Field = 6;
    private const int Data2Field = 7;
    private const int Data3Field = 8;
    private const int Data4Field = 9;
    private const int Data5Field = 10;
    private const int Data6Field = 11;
    private const int MasteryScoreField = 12;
    private const int VarintWireType = 0;
    private const int LengthDelimitedWireType = 2;

    public const ushort Opcode = WorldPacketOpcodes.LegacyCombatGear;

    public static bool TryCreateStartup(
        CharacterSnapshot snapshot,
        CombatGearSnapshot combatGear,
        out LegacyCombatGearPacket packet)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(combatGear);

        packet = default!;
        if (combatGear.Items.Count == 0)
        {
            return false;
        }

        var items = CreateStartupItems(snapshot.Class, combatGear.Items);
        if (items.Count == 0)
        {
            return false;
        }

        packet = new LegacyCombatGearPacket(
            LegacyCombatGearPacketType.Load,
            CheckedUInt32(snapshot.Uid, nameof(snapshot.Uid)),
            items);
        return true;
    }

    public static bool TryParse(PacketFrame frame, out LegacyCombatGearPacket packet)
    {
        packet = default!;
        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new ProtoReader(frame.Payload.ToArray());
        var type = LegacyCombatGearPacketType.Load;
        uint uid = 0;
        var items = new List<LegacyCombatGearItem>();
        var hasType = false;
        var hasUid = false;

        while (!reader.End)
        {
            if (!reader.TryReadTag(out var fieldNumber, out var wireType))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case TypeField:
                    if (!TryReadUInt32(ref reader, wireType, out var rawType) ||
                        !TryToCombatGearType(rawType, out type))
                    {
                        return false;
                    }

                    hasType = true;
                    break;
                case UidField:
                    if (!TryReadUInt32(ref reader, wireType, out uid))
                    {
                        return false;
                    }

                    hasUid = true;
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

        if (!hasType || !hasUid || items.Count == 0)
        {
            return false;
        }

        packet = new LegacyCombatGearPacket(type, uid, items);
        return true;
    }

    public static IReadOnlyList<LegacySpellPacket> CreatePirateStartupSpells(
        CharacterSnapshot snapshot,
        CombatGearSnapshot combatGear,
        IReadOnlyDictionary<(int GearTypeId, int Level), CombatGear>? combatGearByTypeAndLevel,
        IReadOnlyDictionary<int, DaoqiDictType>? daoqiDictTypesById = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(combatGear);

        if (snapshot.Class / 1000 != 7 || combatGear.Items.Count == 0)
        {
            return [];
        }

        var spells = new List<LegacySpellPacket>();
        foreach (var item in combatGear.Items)
        {
            foreach (var blackJadeSpell in CreateBlackJadeSpells(item, daoqiDictTypesById))
            {
                spells.Add(blackJadeSpell);
            }

            if (IsItemForClass(snapshot.Class, item.ItemID) &&
                combatGearByTypeAndLevel?.TryGetValue(((int)item.ItemID, (int)item.Level), out var sourceGear) == true)
            {
                foreach (var skill in ReadLegacySkillValues(sourceGear))
                {
                    if (skill != 0)
                    {
                        spells.Add(CreateSpell((ushort)(skill / 1000), (ushort)(skill % 100)));
                    }
                }
            }

            spells.Add(CreateSpell(21_540, 0));
            spells.Add(CreateSpell(21_920, 0));
        }

        return spells;
    }

    public byte[] ToFrameBytes()
    {
        var payload = new List<byte>();
        WriteRequiredVarintField(payload, TypeField, (uint)Type);
        WriteRequiredVarintField(payload, UidField, Uid);
        foreach (var item in Items)
        {
            var itemPayload = new List<byte>();
            WriteRequiredVarintField(itemPayload, IdField, item.Id);
            WriteRequiredVarintField(itemPayload, LevelField, item.Level);
            WriteRequiredVarintField(itemPayload, ExpField, item.Exp);
            WriteRequiredVarintField(itemPayload, EquipField, item.Equip);
            WriteRequiredVarintField(itemPayload, TransformField, item.Transform);
            WriteRequiredVarintField(itemPayload, Data1Field, item.Data1);
            WriteRequiredVarintField(itemPayload, Data2Field, item.Data2);
            WriteRequiredVarintField(itemPayload, Data3Field, item.Data3);
            WriteRequiredVarintField(itemPayload, Data4Field, item.Data4);
            WriteRequiredVarintField(itemPayload, Data5Field, item.Data5);
            WriteRequiredVarintField(itemPayload, Data6Field, item.Data6);
            WriteRequiredVarintField(itemPayload, MasteryScoreField, item.MasteryScore);
            WriteLengthDelimitedField(payload, ItemsField, itemPayload);
        }

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static IReadOnlyList<LegacyCombatGearItem> CreateStartupItems(
        long classCode,
        IReadOnlyList<CombatGearSnapshotItem> snapshotItems)
    {
        var items = new List<LegacyCombatGearItem>();
        foreach (var item in snapshotItems)
        {
            if (!IsItemForClass(classCode, item.ItemID))
            {
                continue;
            }

            items.Add(new LegacyCombatGearItem(
                item.ItemID,
                item.Level,
                item.Exp,
                item.Equip,
                item.Transform,
                GetBlackJade(item, 0),
                GetBlackJade(item, 1),
                GetBlackJade(item, 2),
                GetBlackJade(item, 3),
                GetBlackJade(item, 4),
                GetBlackJade(item, 5),
                item.MasteryScore));
        }

        return items;
    }

    private static bool IsItemForClass(long classCode, uint itemId)
    {
        return (classCode / 1000) switch
        {
            2 => itemId is >= 201 and <= 203,
            4 => itemId is >= 401 and <= 403,
            7 => itemId is >= 701 and <= 703,
            13 or 14 => itemId is >= 1301 and <= 1305,
            _ => false,
        };
    }

    private static IEnumerable<uint> ReadLegacySkillValues(CombatGear gear)
    {
        const int firstSkillField = 8;
        const int skillCount = 10;
        for (var index = 0; index < skillCount; index++)
        {
            var fieldIndex = firstSkillField + index;
            if (fieldIndex < gear.Fields.Count &&
                uint.TryParse(gear.Fields[fieldIndex], out var value))
            {
                yield return value;
            }
        }
    }

    private static LegacySpellPacket CreateSpell(ushort id, ushort level)
    {
        return new LegacySpellPacket(
            Experience: 0,
            id,
            Unknown: 0,
            level,
            Type: 0,
            UseSoul: 0,
            Soul: 0,
            HashLevelSoul: 0);
    }

    private static IReadOnlyList<LegacySpellPacket> CreateBlackJadeSpells(
        CombatGearSnapshotItem item,
        IReadOnlyDictionary<int, DaoqiDictType>? daoqiDictTypesById)
    {
        if (daoqiDictTypesById is null)
        {
            return [];
        }

        var spells = new List<LegacySpellPacket>();
        foreach (var blackJadeId in item.BlackJades)
        {
            if (blackJadeId == 0 ||
                !daoqiDictTypesById.TryGetValue((int)blackJadeId, out var blackJade))
            {
                continue;
            }

            AddBlackJadeSpell(spells, blackJade, attrTypeFieldIndex: 2, attrValueFieldIndex: 3);
            AddBlackJadeSpell(spells, blackJade, attrTypeFieldIndex: 4, attrValueFieldIndex: 5);
        }

        return spells;
    }

    private static void AddBlackJadeSpell(
        List<LegacySpellPacket> spells,
        DaoqiDictType blackJade,
        int attrTypeFieldIndex,
        int attrValueFieldIndex)
    {
        const int spellAttributeType = 24;

        if (!TryReadDaoqiField(blackJade, attrTypeFieldIndex, out var attrType) ||
            attrType != spellAttributeType ||
            !TryReadDaoqiField(blackJade, attrValueFieldIndex, out var attrValue) ||
            attrValue <= 0)
        {
            return;
        }

        spells.Add(CreateSpell((ushort)(attrValue / 1000), (ushort)(attrValue % 100)));
    }

    private static bool TryReadDaoqiField(DaoqiDictType blackJade, int fieldIndex, out int value)
    {
        value = 0;
        return fieldIndex < blackJade.Fields.Count &&
               int.TryParse(blackJade.Fields[fieldIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static uint GetBlackJade(CombatGearSnapshotItem item, int index)
    {
        return index < item.BlackJades.Count ? item.BlackJades[index] : 0;
    }

    private static bool TryParseItem(ReadOnlySpan<byte> payload, out LegacyCombatGearItem item)
    {
        item = default!;
        var reader = new ProtoReader(payload);
        uint id = 0;
        uint level = 0;
        uint exp = 0;
        uint equip = 0;
        uint transform = 0;
        uint data1 = 0;
        uint data2 = 0;
        uint data3 = 0;
        uint data4 = 0;
        uint data5 = 0;
        uint data6 = 0;
        uint masteryScore = 0;
        var seen = new bool[12];

        while (!reader.End)
        {
            if (!reader.TryReadTag(out var fieldNumber, out var wireType))
            {
                return false;
            }

            if (!TryReadUInt32(ref reader, wireType, out var value))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case IdField:
                    id = value;
                    seen[0] = true;
                    break;
                case LevelField:
                    level = value;
                    seen[1] = true;
                    break;
                case ExpField:
                    exp = value;
                    seen[2] = true;
                    break;
                case EquipField:
                    equip = value;
                    seen[3] = true;
                    break;
                case TransformField:
                    transform = value;
                    seen[4] = true;
                    break;
                case Data1Field:
                    data1 = value;
                    seen[5] = true;
                    break;
                case Data2Field:
                    data2 = value;
                    seen[6] = true;
                    break;
                case Data3Field:
                    data3 = value;
                    seen[7] = true;
                    break;
                case Data4Field:
                    data4 = value;
                    seen[8] = true;
                    break;
                case Data5Field:
                    data5 = value;
                    seen[9] = true;
                    break;
                case Data6Field:
                    data6 = value;
                    seen[10] = true;
                    break;
                case MasteryScoreField:
                    masteryScore = value;
                    seen[11] = true;
                    break;
                default:
                    return false;
            }
        }

        if (seen.Any(value => !value))
        {
            return false;
        }

        item = new LegacyCombatGearItem(
            id,
            level,
            exp,
            equip,
            transform,
            data1,
            data2,
            data3,
            data4,
            data5,
            data6,
            masteryScore);
        return true;
    }

    private static bool TryToCombatGearType(uint value, out LegacyCombatGearPacketType type)
    {
        type = (LegacyCombatGearPacketType)value;
        return type is >= LegacyCombatGearPacketType.Load and <= LegacyCombatGearPacketType.Update ||
            type == LegacyCombatGearPacketType.CrossServer;
    }

    internal static uint CheckedUInt32(long value, string name)
    {
        if (value < 0 || value > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, "Value must fit UInt32.");
        }

        return (uint)value;
    }

    internal static bool TryReadUInt32(ref ProtoReader reader, int wireType, out uint value)
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

    internal static void WriteRequiredVarintField(List<byte> payload, int fieldNumber, uint value)
    {
        WriteVarint(payload, ((ulong)fieldNumber << 3) | VarintWireType);
        WriteVarint(payload, value);
    }

    internal static void WriteLengthDelimitedField(List<byte> payload, int fieldNumber, IReadOnlyList<byte> value)
    {
        WriteVarint(payload, ((ulong)fieldNumber << 3) | LengthDelimitedWireType);
        WriteVarint(payload, (ulong)value.Count);
        payload.AddRange(value);
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

        public bool TryReadTag(out int fieldNumber, out int wireType)
        {
            fieldNumber = 0;
            wireType = 0;
            if (!TryReadVarint(out var tagValue) || tagValue == 0)
            {
                return false;
            }

            fieldNumber = (int)(tagValue >> 3);
            wireType = (int)(tagValue & 0x7);
            return fieldNumber > 0;
        }

        public bool TryReadLengthDelimited(out ReadOnlySpan<byte> value)
        {
            value = default;
            if (!TryReadVarint(out var length) ||
                length > int.MaxValue ||
                _offset > _payload.Length - (int)length)
            {
                return false;
            }

            value = _payload.Slice(_offset, (int)length);
            _offset += (int)length;
            return true;
        }

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

public sealed record LegacyAirPowerUpPower(
    uint Id,
    uint SenseLevel,
    uint SenseAipPower,
    uint ArmedLevel,
    uint ArmedAipPower,
    uint OverLoadLevel,
    uint OverLoadAipPower,
    uint Points);

public sealed record LegacyAirPowerUpPacket(
    LegacyAirPowerUpType Type,
    uint UserId,
    uint PowerType,
    IReadOnlyList<LegacyAirPowerUpPower> Powers)
{
    private const int TypeField = 1;
    private const int UserIdField = 2;
    private const int PowerTypeField = 3;
    private const int PowersField = 4;
    private const int IdField = 1;
    private const int SenseLevelField = 2;
    private const int SenseAipPowerField = 3;
    private const int ArmedLevelField = 4;
    private const int ArmedAipPowerField = 5;
    private const int OverLoadLevelField = 6;
    private const int OverLoadAipPowerField = 7;
    private const int PointsField = 8;
    private const int VarintWireType = 0;
    private const int LengthDelimitedWireType = 2;

    public const ushort Opcode = WorldPacketOpcodes.LegacyAirPowerUp;

    public static LegacyAirPowerUpPacket CreateStartup(CharacterSnapshot snapshot, CombatGearSnapshot combatGear)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(combatGear);

        var airPowers = combatGear.AirPowers;
        var senseLevel = airPowers.SenseLevel == 0 ? 1 : airPowers.SenseLevel;
        return new LegacyAirPowerUpPacket(
            LegacyAirPowerUpType.Info,
            LegacyCombatGearPacket.CheckedUInt32(snapshot.Uid, nameof(snapshot.Uid)),
            PowerType: 0,
            [
                new LegacyAirPowerUpPower(
                    Id: 0,
                    senseLevel,
                    airPowers.SenseAipPower,
                    airPowers.ArmedLevel,
                    airPowers.ArmedAipPower,
                    airPowers.OverLoadLevel,
                    airPowers.OverLoadAipPower,
                    airPowers.Points)
            ]);
    }

    public static bool TryParse(PacketFrame frame, out LegacyAirPowerUpPacket packet)
    {
        packet = default!;
        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new LegacyCombatGearPacket.ProtoReader(frame.Payload.ToArray());
        var type = LegacyAirPowerUpType.Info;
        uint userId = 0;
        uint powerType = 0;
        var powers = new List<LegacyAirPowerUpPower>();
        var hasType = false;
        var hasUserId = false;
        var hasPowerType = false;

        while (!reader.End)
        {
            if (!reader.TryReadTag(out var fieldNumber, out var wireType))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case TypeField:
                    if (!LegacyCombatGearPacket.TryReadUInt32(ref reader, wireType, out var rawType) ||
                        !TryToType(rawType, out type))
                    {
                        return false;
                    }

                    hasType = true;
                    break;
                case UserIdField:
                    if (!LegacyCombatGearPacket.TryReadUInt32(ref reader, wireType, out userId))
                    {
                        return false;
                    }

                    hasUserId = true;
                    break;
                case PowerTypeField:
                    if (!LegacyCombatGearPacket.TryReadUInt32(ref reader, wireType, out powerType))
                    {
                        return false;
                    }

                    hasPowerType = true;
                    break;
                case PowersField:
                    if (wireType != LengthDelimitedWireType ||
                        !reader.TryReadLengthDelimited(out var powerPayload) ||
                        !TryParsePower(powerPayload, out var power))
                    {
                        return false;
                    }

                    powers.Add(power);
                    break;
                default:
                    return false;
            }
        }

        if (!hasType || !hasUserId || !hasPowerType || powers.Count == 0)
        {
            return false;
        }

        packet = new LegacyAirPowerUpPacket(type, userId, powerType, powers);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var payload = new List<byte>();
        LegacyCombatGearPacket.WriteRequiredVarintField(payload, TypeField, (uint)Type);
        LegacyCombatGearPacket.WriteRequiredVarintField(payload, UserIdField, UserId);
        LegacyCombatGearPacket.WriteRequiredVarintField(payload, PowerTypeField, PowerType);
        foreach (var power in Powers)
        {
            var powerPayload = new List<byte>();
            LegacyCombatGearPacket.WriteRequiredVarintField(powerPayload, IdField, power.Id);
            LegacyCombatGearPacket.WriteRequiredVarintField(powerPayload, SenseLevelField, power.SenseLevel);
            LegacyCombatGearPacket.WriteRequiredVarintField(powerPayload, SenseAipPowerField, power.SenseAipPower);
            LegacyCombatGearPacket.WriteRequiredVarintField(powerPayload, ArmedLevelField, power.ArmedLevel);
            LegacyCombatGearPacket.WriteRequiredVarintField(powerPayload, ArmedAipPowerField, power.ArmedAipPower);
            LegacyCombatGearPacket.WriteRequiredVarintField(powerPayload, OverLoadLevelField, power.OverLoadLevel);
            LegacyCombatGearPacket.WriteRequiredVarintField(
                powerPayload,
                OverLoadAipPowerField,
                power.OverLoadAipPower);
            LegacyCombatGearPacket.WriteRequiredVarintField(powerPayload, PointsField, power.Points);
            LegacyCombatGearPacket.WriteLengthDelimitedField(payload, PowersField, powerPayload);
        }

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static bool TryParsePower(ReadOnlySpan<byte> payload, out LegacyAirPowerUpPower power)
    {
        power = default!;
        var reader = new LegacyCombatGearPacket.ProtoReader(payload);
        uint id = 0;
        uint senseLevel = 0;
        uint senseAipPower = 0;
        uint armedLevel = 0;
        uint armedAipPower = 0;
        uint overLoadLevel = 0;
        uint overLoadAipPower = 0;
        uint points = 0;
        var seen = new bool[8];

        while (!reader.End)
        {
            if (!reader.TryReadTag(out var fieldNumber, out var wireType) ||
                !LegacyCombatGearPacket.TryReadUInt32(ref reader, wireType, out var value))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case IdField:
                    id = value;
                    seen[0] = true;
                    break;
                case SenseLevelField:
                    senseLevel = value;
                    seen[1] = true;
                    break;
                case SenseAipPowerField:
                    senseAipPower = value;
                    seen[2] = true;
                    break;
                case ArmedLevelField:
                    armedLevel = value;
                    seen[3] = true;
                    break;
                case ArmedAipPowerField:
                    armedAipPower = value;
                    seen[4] = true;
                    break;
                case OverLoadLevelField:
                    overLoadLevel = value;
                    seen[5] = true;
                    break;
                case OverLoadAipPowerField:
                    overLoadAipPower = value;
                    seen[6] = true;
                    break;
                case PointsField:
                    points = value;
                    seen[7] = true;
                    break;
                default:
                    return false;
            }
        }

        if (seen.Any(value => !value))
        {
            return false;
        }

        power = new LegacyAirPowerUpPower(
            id,
            senseLevel,
            senseAipPower,
            armedLevel,
            armedAipPower,
            overLoadLevel,
            overLoadAipPower,
            points);
        return true;
    }

    private static bool TryToType(uint value, out LegacyAirPowerUpType type)
    {
        type = (LegacyAirPowerUpType)value;
        return type is LegacyAirPowerUpType.Info or
            LegacyAirPowerUpType.View or
            LegacyAirPowerUpType.Upgrade or
            LegacyAirPowerUpType.Practice or
            LegacyAirPowerUpType.AddPoints;
    }
}
