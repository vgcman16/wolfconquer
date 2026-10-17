using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyHundredWeaponsInfoAction : uint
{
    None = 0,
    Update = 1,
    RequestInfo = 2,
    Add = 3,
}

public sealed record LegacyHundredWeaponsInfoWeapon(
    uint SubType,
    uint AppearancePosition,
    uint PositionFlags,
    byte Level,
    uint Progress,
    int Hitpoints,
    int PhysicalAttack,
    int PhysicalDefense,
    int MagicAttack,
    int MagicDefense,
    uint Score);

public sealed record LegacyHundredWeaponsInfoPacket(
    LegacyHundredWeaponsInfoAction Type,
    uint OwnerUid,
    IReadOnlyList<LegacyHundredWeaponsInfoWeapon> Weapons)
{
    private const int TypeField = 1;
    private const int OwnerUidField = 2;
    private const int WeaponsField = 3;
    private const int WeaponSubTypeField = 1;
    private const int WeaponAppearancePositionField = 2;
    private const int WeaponPositionFlagsField = 3;
    private const int WeaponLevelField = 4;
    private const int WeaponProgressField = 5;
    private const int WeaponHitpointsField = 6;
    private const int WeaponPhysicalAttackField = 7;
    private const int WeaponPhysicalDefenseField = 8;
    private const int WeaponMagicAttackField = 9;
    private const int WeaponMagicDefenseField = 10;
    private const int WeaponScoreField = 12;
    private const int VarintWireType = 0;
    private const int LengthDelimitedWireType = 2;

    public const ushort Opcode = WorldPacketOpcodes.LegacyHundredWeaponsInfo;

    public static LegacyHundredWeaponsInfoPacket Request(uint ownerUid)
    {
        return new LegacyHundredWeaponsInfoPacket(LegacyHundredWeaponsInfoAction.RequestInfo, ownerUid, []);
    }

    public static bool TryParse(PacketFrame frame, out LegacyHundredWeaponsInfoPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new ProtoReader(frame.Payload.ToArray());
        var seenType = false;
        var type = LegacyHundredWeaponsInfoAction.None;
        var ownerUid = 0U;
        var weapons = new List<LegacyHundredWeaponsInfoWeapon>();

        while (!reader.End)
        {
            if (!reader.TryReadTag(out var fieldNumber, out var wireType))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case TypeField:
                    if (wireType != VarintWireType ||
                        !reader.TryReadUInt32(out var typeValue))
                    {
                        return false;
                    }

                    type = (LegacyHundredWeaponsInfoAction)typeValue;
                    seenType = true;
                    break;

                case OwnerUidField:
                    if (wireType != VarintWireType ||
                        !reader.TryReadUInt32(out ownerUid))
                    {
                        return false;
                    }

                    break;

                case WeaponsField:
                    if (wireType != LengthDelimitedWireType ||
                        !reader.TryReadLengthDelimited(out var weaponPayload) ||
                        !TryParseWeapon(weaponPayload, out var weapon))
                    {
                        return false;
                    }

                    weapons.Add(weapon);
                    break;

                default:
                    return false;
            }
        }

        if (!seenType)
        {
            return false;
        }

        packet = new LegacyHundredWeaponsInfoPacket(type, ownerUid, weapons);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        ArgumentNullException.ThrowIfNull(Weapons);

        var payload = new List<byte>();
        WriteVarintField(payload, TypeField, (uint)Type);
        if (OwnerUid != 0)
        {
            WriteVarintField(payload, OwnerUidField, OwnerUid);
        }

        foreach (var weapon in Weapons)
        {
            var weaponPayload = new List<byte>();
            WriteVarintField(weaponPayload, WeaponSubTypeField, weapon.SubType);
            WriteVarintField(weaponPayload, WeaponAppearancePositionField, weapon.AppearancePosition);
            WriteVarintField(weaponPayload, WeaponPositionFlagsField, weapon.PositionFlags);
            WriteVarintField(weaponPayload, WeaponLevelField, weapon.Level);
            WriteVarintField(weaponPayload, WeaponProgressField, weapon.Progress);
            WriteInt32Field(weaponPayload, WeaponHitpointsField, weapon.Hitpoints);
            WriteInt32Field(weaponPayload, WeaponPhysicalAttackField, weapon.PhysicalAttack);
            WriteInt32Field(weaponPayload, WeaponPhysicalDefenseField, weapon.PhysicalDefense);
            WriteInt32Field(weaponPayload, WeaponMagicAttackField, weapon.MagicAttack);
            WriteInt32Field(weaponPayload, WeaponMagicDefenseField, weapon.MagicDefense);
            WriteVarintField(weaponPayload, WeaponScoreField, weapon.Score);
            WriteBytes(payload, WeaponsField, weaponPayload);
        }

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static bool TryParseWeapon(
        ReadOnlySpan<byte> payload,
        out LegacyHundredWeaponsInfoWeapon weapon)
    {
        weapon = default!;

        var reader = new ProtoReader(payload);
        var seen = 0;
        var values = new ulong[12];
        while (!reader.End)
        {
            if (!reader.TryReadTag(out var fieldNumber, out var wireType) ||
                fieldNumber is < 1 or > 12 ||
                fieldNumber == 11 ||
                wireType != VarintWireType ||
                !reader.TryReadVarint(out var value))
            {
                return false;
            }

            values[fieldNumber - 1] = value;
            seen |= 1 << fieldNumber;
        }

        var required =
            (1 << WeaponSubTypeField) |
            (1 << WeaponAppearancePositionField) |
            (1 << WeaponPositionFlagsField) |
            (1 << WeaponLevelField) |
            (1 << WeaponProgressField) |
            (1 << WeaponHitpointsField) |
            (1 << WeaponPhysicalAttackField) |
            (1 << WeaponPhysicalDefenseField) |
            (1 << WeaponMagicAttackField) |
            (1 << WeaponMagicDefenseField) |
            (1 << WeaponScoreField);
        if ((seen & required) != required ||
            values[WeaponSubTypeField - 1] > uint.MaxValue ||
            values[WeaponAppearancePositionField - 1] > uint.MaxValue ||
            values[WeaponPositionFlagsField - 1] > uint.MaxValue ||
            values[WeaponLevelField - 1] > byte.MaxValue ||
            values[WeaponProgressField - 1] > uint.MaxValue ||
            values[WeaponScoreField - 1] > uint.MaxValue)
        {
            return false;
        }

        weapon = new LegacyHundredWeaponsInfoWeapon(
            (uint)values[WeaponSubTypeField - 1],
            (uint)values[WeaponAppearancePositionField - 1],
            (uint)values[WeaponPositionFlagsField - 1],
            (byte)values[WeaponLevelField - 1],
            (uint)values[WeaponProgressField - 1],
            ToInt32(values[WeaponHitpointsField - 1]),
            ToInt32(values[WeaponPhysicalAttackField - 1]),
            ToInt32(values[WeaponPhysicalDefenseField - 1]),
            ToInt32(values[WeaponMagicAttackField - 1]),
            ToInt32(values[WeaponMagicDefenseField - 1]),
            (uint)values[WeaponScoreField - 1]);
        return true;
    }

    private static int ToInt32(ulong value)
    {
        return value <= int.MaxValue ? (int)value : unchecked((int)value);
    }

    private static void WriteVarintField(List<byte> payload, int fieldNumber, ulong value)
    {
        WriteVarint(payload, ((ulong)fieldNumber << 3) | VarintWireType);
        WriteVarint(payload, value);
    }

    private static void WriteInt32Field(List<byte> payload, int fieldNumber, int value)
    {
        WriteVarintField(payload, fieldNumber, value < 0 ? unchecked((ulong)value) : (uint)value);
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

        public bool TryReadUInt32(out uint value)
        {
            value = 0;
            if (!TryReadVarint(out var raw) || raw > uint.MaxValue)
            {
                return false;
            }

            value = (uint)raw;
            return true;
        }

        public bool TryReadLengthDelimited(out ReadOnlySpan<byte> value)
        {
            value = default;
            if (!TryReadVarint(out var length) ||
                length > int.MaxValue ||
                _payload.Length - _offset < (int)length)
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
