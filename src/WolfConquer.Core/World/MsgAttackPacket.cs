using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum MsgAttackType : uint
{
    None = 0,
    Physical = 2,
    Magic = 24,
    Archer = 28,
    RequestMarriage = 8,
    AcceptMarriage = 9,
    Death = 14,
    Reflect = 26,
    Dash = 27,
    UpdateHunterJar = 36,
    CounterKillSwitch = 44,
    Scapegoat = 43,
    FatalStrike = 45,
    InteractionRequest = 46,
    InteractionAccept = 47,
    InteractionRefuse = 48,
    InteractionEffect = 49,
    InteractionStopEffect = 50,
    InMoveSpell = 53,
    BlueDamage = 55,
    BackFire = 57
}

[Flags]
public enum MsgAttackEffect : uint
{
    None = 0,
    Block = 1,
    Penetration = 2,
    CriticalStrike = 4,
    Imunity = 8,
    Break = 0xA,
    ResistMetal = 0x10,
    ResistWood = 0x20,
    ResistWater = 0x40,
    ResistFire = 0x80,
    ResistEarth = 0x100,
    AddStudyPoint = 0x200,
    LuckyStrike = 0x400,
    NatureShield = 0x2000,
    Assasssin = 0x4000,
    BurningSky = 0x8000,
    FireCurse = 0x10000,
    TailedBeast = 0x40000,
    Destroy = 0x1000000,
    BonePulseDestroy = 0x4000000,
    Dash = 0x8000000
}

public readonly record struct MsgAttackQuery(
    uint Uid,
    uint OpponentUid,
    ushort X,
    ushort Y,
    MsgAttackType AttackType,
    int Damage,
    int KilledMonster,
    uint ResponseDamage,
    MsgAttackEffect Effect,
    uint Unknown,
    ulong CombatGear = 0,
    ushort SpellLevel = 0,
    ushort SpellId = 0);

public readonly record struct MsgAttackPacket(MsgAttackQuery Query)
{
    public const ushort Opcode = WorldPacketOpcodes.MsgAttack;

    public static bool TryParse(PacketFrame frame, out MsgAttackPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new ProtoVarintReader(frame.Payload.ToArray());
        var builder = new MsgAttackQueryBuilder();

        while (!reader.End)
        {
            if (!reader.TryReadVarint(out var tagValue) || tagValue == 0)
            {
                return false;
            }

            var fieldNumber = (int)(tagValue >> 3);
            var wireType = (int)(tagValue & 0x7);
            if (!TryReadField(ref reader, ref builder, fieldNumber, wireType))
            {
                return false;
            }
        }

        if (!builder.TryBuild(out var query))
        {
            return false;
        }

        packet = new MsgAttackPacket(query);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var payload = new List<byte>();

        WriteUInt32(payload, 4, Query.Uid);
        WriteUInt32(payload, 5, Query.OpponentUid);
        WriteUInt32(payload, 7, Query.X);
        WriteUInt32(payload, 8, Query.Y);
        WriteUInt32(payload, 13, (uint)(ushort)Query.AttackType);
        WriteInt32(payload, 15, Query.Damage);
        if (Query.CombatGear != 0)
        {
            WriteUInt64(payload, 16, Query.CombatGear);
        }

        WriteInt32(payload, 17, Query.KilledMonster);
        if (Query.SpellLevel != 0)
        {
            WriteUInt32(payload, 18, Query.SpellLevel);
        }

        if (Query.SpellId != 0)
        {
            WriteUInt32(payload, 19, Query.SpellId);
        }

        WriteUInt32(payload, 21, Query.ResponseDamage);
        WriteUInt32(payload, 25, (uint)Query.Effect);
        WriteUInt32(payload, 26, Query.Unknown);

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    public static MsgAttackQuery EncodeMagicAttack(MsgAttackQuery query)
    {
        BitUnfold32(query.Damage, out var lower, out var upper);

        lower = (ushort)(ExchangeShortBits(unchecked((uint)(lower - 5310)), 3) ^ query.Uid ^ 0x915D);
        upper ^= 0x3721;

        return query with
        {
            Damage = BitFold32(lower, upper),
            OpponentUid = ExchangeLongBits(unchecked((uint)((int)query.OpponentUid - -1953450726)) ^ query.Uid ^ 0x5F2D2463, 19),
            X = (ushort)(ExchangeShortBits(unchecked((uint)(query.X - 56594)), 1) ^ query.Uid ^ 0x2ED6),
            Y = (ushort)(ExchangeShortBits(unchecked((uint)(query.Y - 30430)), 5) ^ query.Uid ^ 0xB99B)
        };
    }

    public static MsgAttackQuery DecodeMagicAttack(MsgAttackQuery query)
    {
        BitUnfold32(query.SpellId, out var lower, out var upper);

        lower = (ushort)(ExchangeShortBits((ushort)lower ^ query.Uid ^ 0x915D, 13) + 5310);
        upper = (ushort)((byte)upper ^ 0x21);

        return query with
        {
            SpellId = (ushort)BitFold32(lower, upper),
            OpponentUid = unchecked((ExchangeLongBits(query.OpponentUid, 13) ^ query.Uid ^ 0x5F2D2463) + 2341516570u),
            X = (ushort)(ExchangeShortBits(query.X ^ query.Uid ^ 0x2ED6, 15) + 56594),
            Y = (ushort)(ExchangeShortBits(query.Y ^ query.Uid ^ 0xB99B, 11) + 30430)
        };
    }

    public static int BitFold32(int lower16, int higher16)
    {
        return lower16 | (higher16 << 16);
    }

    public static void BitUnfold32(int bits32, out int lower16, out int upper16)
    {
        lower16 = bits32 & 0xFFFF;
        upper16 = bits32 >> 16;
    }

    public static uint ExchangeShortBits(uint data, int bits)
    {
        data &= 0xFFFF;
        return ((data >> bits) | (data << (16 - bits))) & 0xFFFF;
    }

    public static uint ExchangeLongBits(uint data, int bits)
    {
        return (data >> bits) | (data << (32 - bits));
    }

    private static bool TryReadField(ref ProtoVarintReader reader, ref MsgAttackQueryBuilder builder, int fieldNumber, int wireType)
    {
        if (wireType != 0)
        {
            return reader.TrySkipField(wireType);
        }

        if (!reader.TryReadVarint(out var value))
        {
            return false;
        }

        switch (fieldNumber)
        {
            case 4:
                return TryToUInt32(value, out builder.Uid, out builder.HasUid);
            case 5:
                return TryToUInt32(value, out builder.OpponentUid, out builder.HasOpponentUid);
            case 7:
                return TryToUInt16(value, out builder.X, out builder.HasX);
            case 8:
                return TryToUInt16(value, out builder.Y, out builder.HasY);
            case 13:
                if (!TryToUInt16(value, out var attackType, out builder.HasAttackType))
                {
                    return false;
                }

                builder.AttackType = (MsgAttackType)attackType;
                return true;
            case 15:
                builder.Damage = unchecked((int)(uint)value);
                builder.HasDamage = true;
                return true;
            case 16:
                builder.CombatGear = value;
                return true;
            case 17:
                builder.KilledMonster = unchecked((int)(uint)value);
                builder.HasKilledMonster = true;
                return true;
            case 18:
                return TryToUInt16(value, out builder.SpellLevel);
            case 19:
                return TryToUInt16(value, out builder.SpellId);
            case 21:
                return TryToUInt32(value, out builder.ResponseDamage, out builder.HasResponseDamage);
            case 25:
                if (!TryToUInt32(value, out var effect, out builder.HasEffect))
                {
                    return false;
                }

                builder.Effect = (MsgAttackEffect)effect;
                return true;
            case 26:
                return TryToUInt32(value, out builder.Unknown, out builder.HasUnknown);
            default:
                return true;
        }
    }

    private static bool TryToUInt16(ulong value, out ushort result)
    {
        if (value > ushort.MaxValue)
        {
            result = 0;
            return false;
        }

        result = (ushort)value;
        return true;
    }

    private static bool TryToUInt16(ulong value, out ushort result, out bool hasValue)
    {
        hasValue = TryToUInt16(value, out result);
        return hasValue;
    }

    private static bool TryToUInt32(ulong value, out uint result, out bool hasValue)
    {
        if (value > uint.MaxValue)
        {
            result = 0;
            hasValue = false;
            return false;
        }

        result = (uint)value;
        hasValue = true;
        return true;
    }

    private static void WriteUInt32(List<byte> payload, int fieldNumber, uint value)
    {
        WriteTag(payload, fieldNumber);
        WriteVarint(payload, value);
    }

    private static void WriteUInt64(List<byte> payload, int fieldNumber, ulong value)
    {
        WriteTag(payload, fieldNumber);
        WriteVarint(payload, value);
    }

    private static void WriteInt32(List<byte> payload, int fieldNumber, int value)
    {
        WriteTag(payload, fieldNumber);
        WriteVarint(payload, value < 0 ? unchecked((ulong)value) : (uint)value);
    }

    private static void WriteTag(List<byte> payload, int fieldNumber)
    {
        WriteVarint(payload, (uint)(fieldNumber << 3));
    }

    private static void WriteVarint(List<byte> payload, ulong value)
    {
        while (value > 0x7F)
        {
            payload.Add((byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }

        payload.Add((byte)value);
    }

    private struct MsgAttackQueryBuilder
    {
        public uint Uid;
        public uint OpponentUid;
        public ushort X;
        public ushort Y;
        public MsgAttackType AttackType;
        public int Damage;
        public int KilledMonster;
        public uint ResponseDamage;
        public MsgAttackEffect Effect;
        public uint Unknown;
        public ulong CombatGear;
        public ushort SpellLevel;
        public ushort SpellId;

        public bool HasUid;
        public bool HasOpponentUid;
        public bool HasX;
        public bool HasY;
        public bool HasAttackType;
        public bool HasDamage;
        public bool HasKilledMonster;
        public bool HasResponseDamage;
        public bool HasEffect;
        public bool HasUnknown;

        public bool TryBuild(out MsgAttackQuery query)
        {
            if (!HasUid ||
                !HasOpponentUid ||
                !HasX ||
                !HasY ||
                !HasAttackType)
            {
                query = default;
                return false;
            }

            query = new MsgAttackQuery(
                Uid,
                OpponentUid,
                X,
                Y,
                AttackType,
                Damage,
                KilledMonster,
                ResponseDamage,
                Effect,
                Unknown,
                CombatGear,
                SpellLevel,
                SpellId);
            return true;
        }
    }

    private ref struct ProtoVarintReader
    {
        private readonly ReadOnlySpan<byte> _payload;
        private int _offset;

        public ProtoVarintReader(ReadOnlySpan<byte> payload)
        {
            _payload = payload;
            _offset = 0;
        }

        public bool End => _offset == _payload.Length;

        public bool TryReadVarint(out ulong value)
        {
            value = 0;

            for (var index = 0; index < 10; index++)
            {
                if (_offset >= _payload.Length)
                {
                    return false;
                }

                var next = _payload[_offset++];
                if (index == 9 && (next & 0xFE) != 0)
                {
                    return false;
                }

                value |= (ulong)(next & 0x7F) << (index * 7);
                if ((next & 0x80) == 0)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TrySkipField(int wireType)
        {
            switch (wireType)
            {
                case 0:
                    return TryReadVarint(out _);
                case 1:
                    return TrySkipBytes(sizeof(ulong));
                case 2:
                    return TryReadVarint(out var length) && length <= int.MaxValue && TrySkipBytes((int)length);
                case 5:
                    return TrySkipBytes(sizeof(uint));
                default:
                    return false;
            }
        }

        private bool TrySkipBytes(int length)
        {
            if (length < 0 || _offset > _payload.Length - length)
            {
                return false;
            }

            _offset += length;
            return true;
        }
    }
}
