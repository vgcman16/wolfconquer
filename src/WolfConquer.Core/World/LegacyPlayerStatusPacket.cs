using System.Buffers;
using System.Buffers.Binary;
using System.Collections.ObjectModel;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed class LegacyPlayerStatusPacket
{
    public const ushort Opcode = WorldPacketOpcodes.LegacyPlayerStatus;
    public const int FieldCount = 40;
    public const int PayloadLength = FieldCount * sizeof(uint);
    public const int UidIndex = 0;
    public const int MaxHPIndex = 1;
    public const int MaxManaIndex = 2;
    public const int MaxAttackIndex = 3;
    public const int MinAttackIndex = 4;
    public const int DefenceIndex = 5;
    public const int MagicAttackIndex = 6;
    public const int MagicDefenceIndex = 7;
    public const int DodgeIndex = 8;
    public const int AgilityAttackIndex = 9;
    public const int AccuracyIndex = 10;
    public const int PhysicalPercentIndex = 11;
    public const int MagicPercentIndex = 12;
    public const int MDefenceIndex = 13;
    public const int DamageIndex = 14;
    public const int ItemBlessIndex = 15;
    public const int CriticalStrikeIndex = 16;
    public const int SkillCStrikeIndex = 17;
    public const int ImmunityIndex = 18;
    public const int PenetrationIndex = 19;
    public const int BlockIndex = 20;
    public const int BreakthroughIndex = 21;
    public const int CounteractionIndex = 22;
    public const int DetoxicationIndex = 23;
    public const int PhysicalDamageIncreaseIndex = 24;
    public const int MagicDamageIncreaseIndex = 25;
    public const int PhysicalDamageDecreaseIndex = 26;
    public const int MagicDamageDecreaseIndex = 27;
    public const int MetalResistanceIndex = 28;
    public const int WoodResistanceIndex = 29;
    public const int WaterResistanceIndex = 30;
    public const int FireResistanceIndex = 31;
    public const int EarthResistanceIndex = 32;
    public const int PrestigeLevelIndex = 33;
    public const int RuneScoreIndex = 34;
    public const int DashRateIndex = 35;
    public const int LuckyStrikePercentIndex = 36;
    public const int StrikeLockPercentIndex = 37;
    public const int ReservedZeroIndex = 38;
    public const int DodgeRateIndex = 39;

    public LegacyPlayerStatusPacket(IReadOnlyList<uint> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        if (fields.Count != FieldCount)
        {
            throw new ArgumentOutOfRangeException(nameof(fields), fields.Count, "Legacy player status requires exactly 40 UInt32 fields.");
        }

        var values = new uint[FieldCount];
        for (var index = 0; index < FieldCount; index++)
        {
            values[index] = fields[index];
        }

        Fields = Array.AsReadOnly(values);
    }

    public ReadOnlyCollection<uint> Fields { get; }

    public uint Uid => Fields[UidIndex];

    public uint MaxHP => Fields[MaxHPIndex];

    public uint MaxMana => Fields[MaxManaIndex];

    public uint MaxAttack => Fields[MaxAttackIndex];

    public uint MinAttack => Fields[MinAttackIndex];

    public uint Defence => Fields[DefenceIndex];

    public uint MagicAttack => Fields[MagicAttackIndex];

    public uint MagicDefence => Fields[MagicDefenceIndex];

    public uint Dodge => Fields[DodgeIndex];

    public uint AgilityAttack => Fields[AgilityAttackIndex];

    public uint Accuracy => Fields[AccuracyIndex];

    public uint PhysicalPercent => Fields[PhysicalPercentIndex];

    public uint MagicPercent => Fields[MagicPercentIndex];

    public uint MDefence => Fields[MDefenceIndex];

    public uint Damage => Fields[DamageIndex];

    public uint ItemBless => Fields[ItemBlessIndex];

    public uint CriticalStrike => Fields[CriticalStrikeIndex];

    public uint SkillCStrike => Fields[SkillCStrikeIndex];

    public uint Immunity => Fields[ImmunityIndex];

    public uint Penetration => Fields[PenetrationIndex];

    public uint Block => Fields[BlockIndex];

    public uint Breakthrough => Fields[BreakthroughIndex];

    public uint Counteraction => Fields[CounteractionIndex];

    public uint Detoxication => Fields[DetoxicationIndex];

    public uint PhysicalDamageIncrease => Fields[PhysicalDamageIncreaseIndex];

    public uint MagicDamageIncrease => Fields[MagicDamageIncreaseIndex];

    public uint PhysicalDamageDecrease => Fields[PhysicalDamageDecreaseIndex];

    public uint MagicDamageDecrease => Fields[MagicDamageDecreaseIndex];

    public uint MetalResistance => Fields[MetalResistanceIndex];

    public uint WoodResistance => Fields[WoodResistanceIndex];

    public uint WaterResistance => Fields[WaterResistanceIndex];

    public uint FireResistance => Fields[FireResistanceIndex];

    public uint EarthResistance => Fields[EarthResistanceIndex];

    public uint PrestigeLevel => Fields[PrestigeLevelIndex];

    public uint RuneScore => Fields[RuneScoreIndex];

    public uint DashRate => Fields[DashRateIndex];

    public uint LuckyStrikePercent => Fields[LuckyStrikePercentIndex];

    public uint StrikeLockPercent => Fields[StrikeLockPercentIndex];

    public uint ReservedZero => Fields[ReservedZeroIndex];

    public uint DodgeRate => Fields[DodgeRateIndex];

    public static LegacyPlayerStatusPacket Create(
        uint uid,
        uint maxHp,
        uint maxMana,
        uint maxAttack,
        uint minAttack,
        uint defence,
        uint magicAttack,
        uint magicDefence,
        uint dodge,
        uint agilityAttack,
        uint accuracy,
        uint physicalPercent,
        uint magicPercent,
        uint mDefence,
        uint damage,
        uint itemBless,
        uint criticalStrike,
        uint skillCStrike,
        uint immunity,
        uint penetration,
        uint block,
        uint breakthrough,
        uint counteraction,
        uint detoxication,
        uint physicalDamageIncrease,
        uint magicDamageIncrease,
        uint physicalDamageDecrease,
        uint magicDamageDecrease,
        uint metalResistance,
        uint woodResistance,
        uint waterResistance,
        uint fireResistance,
        uint earthResistance,
        uint prestigeLevel,
        uint runeScore,
        uint dashRate,
        uint luckyStrikePercent,
        uint strikeLockPercent,
        uint dodgeRate)
    {
        var fields = new uint[FieldCount];
        fields[UidIndex] = uid;
        fields[MaxHPIndex] = maxHp;
        fields[MaxManaIndex] = maxMana;
        fields[MaxAttackIndex] = maxAttack;
        fields[MinAttackIndex] = minAttack;
        fields[DefenceIndex] = defence;
        fields[MagicAttackIndex] = magicAttack;
        fields[MagicDefenceIndex] = magicDefence;
        fields[DodgeIndex] = dodge;
        fields[AgilityAttackIndex] = agilityAttack;
        fields[AccuracyIndex] = accuracy;
        fields[PhysicalPercentIndex] = physicalPercent;
        fields[MagicPercentIndex] = magicPercent;
        fields[MDefenceIndex] = mDefence;
        fields[DamageIndex] = damage;
        fields[ItemBlessIndex] = itemBless;
        fields[CriticalStrikeIndex] = criticalStrike;
        fields[SkillCStrikeIndex] = skillCStrike;
        fields[ImmunityIndex] = immunity;
        fields[PenetrationIndex] = penetration;
        fields[BlockIndex] = block;
        fields[BreakthroughIndex] = breakthrough;
        fields[CounteractionIndex] = counteraction;
        fields[DetoxicationIndex] = detoxication;
        fields[PhysicalDamageIncreaseIndex] = physicalDamageIncrease;
        fields[MagicDamageIncreaseIndex] = magicDamageIncrease;
        fields[PhysicalDamageDecreaseIndex] = physicalDamageDecrease;
        fields[MagicDamageDecreaseIndex] = magicDamageDecrease;
        fields[MetalResistanceIndex] = metalResistance;
        fields[WoodResistanceIndex] = woodResistance;
        fields[WaterResistanceIndex] = waterResistance;
        fields[FireResistanceIndex] = fireResistance;
        fields[EarthResistanceIndex] = earthResistance;
        fields[PrestigeLevelIndex] = prestigeLevel;
        fields[RuneScoreIndex] = runeScore;
        fields[DashRateIndex] = dashRate;
        fields[LuckyStrikePercentIndex] = luckyStrikePercent;
        fields[StrikeLockPercentIndex] = strikeLockPercent;
        fields[ReservedZeroIndex] = 0;
        fields[DodgeRateIndex] = dodgeRate;

        return new LegacyPlayerStatusPacket(fields);
    }

    public static bool TryParse(PacketFrame frame, out LegacyPlayerStatusPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != PayloadLength)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        var fields = new uint[FieldCount];
        for (var index = 0; index < FieldCount; index++)
        {
            var offset = index * sizeof(uint);
            fields[index] = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(offset, sizeof(uint)));
        }

        packet = new LegacyPlayerStatusPacket(fields);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        for (var index = 0; index < FieldCount; index++)
        {
            writer.WriteUInt32(index * sizeof(uint), Fields[index]);
        }

        return writer.ToArray();
    }
}

public sealed record LegacyPlayerStatusQueryPacket(uint Uid)
{
    public const ushort Opcode = WorldPacketOpcodes.LegacyPlayerStatus;
    public const int PayloadLength = sizeof(uint);

    public static bool TryParse(PacketFrame frame, out LegacyPlayerStatusQueryPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < PayloadLength)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        packet = new LegacyPlayerStatusQueryPacket(BinaryPrimitives.ReadUInt32LittleEndian(payload));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(0, Uid);
        return writer.ToArray();
    }

    public uint ResolveTargetUid(WorldSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return Uid == 0
            ? checked((uint)session.Character.Uid)
            : Uid;
    }
}
