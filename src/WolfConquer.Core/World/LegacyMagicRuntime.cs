using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyMagicSort
{
    Attack = 1,
    Recruit = 2,
    Sector = 4,
    Bomb = 5,
    AttachStatus = 6,
    DetachStatus = 7,
    DispatchXp = 11,
    AddMana = 20,
    LayTrap = 21,
    Line = 14,
    AttackStatus = 16,
    Transform = 19,
    CruelShade = 26,
    Toxic = 27,
    ShurikenVortex = 28,
    CounterKill = 29,
    Spook = 30,
    WarCry = 31,
    Riding = 32,
    Shock = 34,
    ChainBolt = 37,
    StarArrow = 38,
    DragonWhirl = 40,
    RemoveBuffers = 46,
    Tranquility = 47,
    DirectAttack = 48,
    Compasion = 50,
    Auras = 51,
    ShieldBlock = 52,
    Oblivion = 53,
    WhirlwindKick = 54,
    PhysicalSpells = 55,
    ScurvyBomb = 56,
    CannonBarrage = 57,
    BlackSpot = 58,
    BombPirte = 60,
    MoveLine = 61,
    AddBlackSpot = 62,
    PirateXpSkill = 63,
    ChargingVortex = 64,
    MortalDrag = 65,
    KineticSpark = 67,
    BladeFlurry = 68,
    SectorBack = 69,
    BreathFocus = 70,
    FatalCross = 71,
    FatalSpin = 73,
    DragonCyclone = 75,
    StraightFist = 76,
    Perimeter = 78,
    AirKick = 79,
    Strike = 81,
    SectorPassive = 82,
    ManiacDance = 83,
    Pounce = 84,
    Omnipotence = 85,
    BurntFrost = 86,
    Rectangle = 87,
    RemoveStamin = 88,
    PetAttachStatus = 89,
    Wildwind = 90,
    SwirlingStorm = 91,
    Pitching = 92,
    ThunderRampage = 94,
    HeavensWrath = 95,
    ManualLine = 96,
    HellVortex = 98,
    TripleAttack = 99,
}

public sealed record LegacyMagicRuntimeSpell(
    ushort Id,
    LegacyMagicSort Sort,
    string Name,
    byte Level,
    ushort UseMana,
    uint Damage,
    uint Experience,
    byte MaxTargets,
    byte Range,
    ushort NeedLevel,
    uint WeaponType,
    byte UseStamina,
    ushort CoolDown,
    int ColdTime,
    uint Duration = 0,
    uint DamageOnHuman = 0,
    uint Damage2 = 0,
    uint Damage3 = 0,
    uint DamageOnMonster = 0,
    bool AttackInFly = false,
    uint GDamage = 0,
    uint RawDamage = 0);

public readonly record struct LegacyMagicCooldownResult(
    bool Ready,
    DateTimeOffset ReadyAtUtc,
    int RemainingMilliseconds);

public readonly record struct LegacySpellExperienceResult(
    PlayerSpellSnapshotEntry Spell,
    LegacySpellPacket Packet,
    int AppliedExperience,
    bool Updated,
    bool LevelChanged);

public readonly record struct LegacyPlayerMonsterMagicDamageResult(
    uint Damage,
    MsgAttackEffect Effect);

public static partial class LegacyMagicRuntime
{
    private const int TypeIdField = 1;
    private const int SortField = 2;
    private const int NameField = 3;
    private const int AttackInFlyField = 5;
    private const int LevelField = 8;
    private const int UseManaField = 9;
    private const int DamageField = 10;
    private const int GDamageField = 12;
    private const int DurationField = 13;
    private const int ExperienceField = 18;
    private const int NeedLevelField = 20;
    private const int WeaponTypeField = 22;
    private const int MaxTargetsField = 14;
    private const int RangeField = 15;
    private const int UseStaminaField = 29;
    private const int CoolDownField = 33;
    private const int DamageOnHumanField = 35;
    private const int Damage2Field = 36;
    private const int Damage3Field = 37;
    private const int DamageOnMonsterField = 44;
    private const int ColdTimeField = 47;
    private const int MinimumFieldCount = ColdTimeField + 1;

    public static bool TryResolveLearnedSpell(
        PlayerSpellSnapshot playerSpells,
        WorldDataCatalog worldData,
        ushort spellId,
        out PlayerSpellSnapshotEntry playerSpell,
        out LegacyMagicRuntimeSpell runtimeSpell)
    {
        ArgumentNullException.ThrowIfNull(playerSpells);
        ArgumentNullException.ThrowIfNull(worldData);

        foreach (var candidate in playerSpells.Spells)
        {
            if (candidate.Id != spellId)
            {
                continue;
            }

            if (TryResolveSpell(worldData, spellId, candidate.Level, out runtimeSpell))
            {
                playerSpell = candidate;
                return true;
            }

            break;
        }

        playerSpell = default!;
        runtimeSpell = default!;
        return false;
    }

    public static bool TryResolveSpell(
        WorldDataCatalog worldData,
        ushort spellId,
        ushort spellLevel,
        out LegacyMagicRuntimeSpell runtimeSpell)
    {
        ArgumentNullException.ThrowIfNull(worldData);

        runtimeSpell = default!;
        if (!worldData.MagicByTypeId.TryGetValue(spellId, out var rows))
        {
            return false;
        }

        foreach (var row in rows)
        {
            if (TryReadByte(row.Fields, LevelField, out var rowLevel) && rowLevel == spellLevel)
            {
                return TryCreate(row, out runtimeSpell);
            }
        }

        return false;
    }

    public static bool TryResolveSpellByRowId(
        WorldDataCatalog worldData,
        ushort rowId,
        out LegacyMagicRuntimeSpell runtimeSpell)
    {
        ArgumentNullException.ThrowIfNull(worldData);

        runtimeSpell = default!;
        return worldData.MagicById.TryGetValue(rowId, out var row) &&
            TryCreate(row, out runtimeSpell);
    }

    public static bool IsSupportedVisibleMonsterAttack(LegacyMagicRuntimeSpell spell)
    {
        return LegacyMagicSortCapabilities.IsSupportedPerimeterVisibleMonsterAttackSpell(spell.Sort, spell.Id) ||
            LegacyMagicSortCapabilities.IsSupportedStraightFistVisibleMonsterAttackSpell(spell.Sort, spell.Id) ||
            spell.Sort is
            LegacyMagicSort.Attack or
            LegacyMagicSort.DirectAttack or
            LegacyMagicSort.Bomb or
            LegacyMagicSort.Sector or
            LegacyMagicSort.Line or
            LegacyMagicSort.ManualLine;
    }

    public static bool IsDirectMonsterAttack(LegacyMagicRuntimeSpell spell)
    {
        return spell.Sort is LegacyMagicSort.Attack or LegacyMagicSort.DirectAttack;
    }

    public static bool UsesLegacyCooldown(LegacyMagicRuntimeSpell spell)
    {
        return spell.ColdTime > 0 && spell.Id is not 15_710 and not 20_280;
    }

    public static DateTimeOffset GetLegacyCooldownReadyAtUtc(
        LegacyMagicRuntimeSpell spell,
        DateTimeOffset nowUtc)
    {
        var normalizedNow = nowUtc.ToUniversalTime();
        return UsesLegacyCooldown(spell)
            ? normalizedNow.AddMilliseconds(spell.ColdTime)
            : normalizedNow;
    }

    public static int GetLegacyCooldownRemainingMilliseconds(
        DateTimeOffset readyAtUtc,
        DateTimeOffset nowUtc)
    {
        var remaining = readyAtUtc.ToUniversalTime() - nowUtc.ToUniversalTime();
        if (remaining <= TimeSpan.Zero)
        {
            return 0;
        }

        return remaining.TotalMilliseconds >= int.MaxValue
            ? int.MaxValue
            : (int)Math.Ceiling(remaining.TotalMilliseconds);
    }

    public static LegacyMagicCooldownResult TryStartLegacyCooldown(
        LegacyMagicRuntimeSpell spell,
        DateTimeOffset nowUtc,
        DateTimeOffset? currentReadyAtUtc)
    {
        var normalizedNow = nowUtc.ToUniversalTime();
        if (!UsesLegacyCooldown(spell))
        {
            return new LegacyMagicCooldownResult(
                Ready: true,
                ReadyAtUtc: normalizedNow,
                RemainingMilliseconds: 0);
        }

        if (currentReadyAtUtc is { } readyAtUtc)
        {
            var remainingMilliseconds = GetLegacyCooldownRemainingMilliseconds(readyAtUtc, normalizedNow);
            if (remainingMilliseconds > 0)
            {
                return new LegacyMagicCooldownResult(
                    Ready: false,
                    ReadyAtUtc: readyAtUtc.ToUniversalTime(),
                    remainingMilliseconds);
            }
        }

        return new LegacyMagicCooldownResult(
            Ready: true,
            ReadyAtUtc: GetLegacyCooldownReadyAtUtc(spell, normalizedNow),
            RemainingMilliseconds: 0);
    }

    public static LegacySpellExperienceResult ApplySpellExperience(
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell runtimeSpell,
        uint damage,
        uint experienceRate,
        int availableLevelCount,
        int playerLevel,
        bool isRuneSkill = false)
    {
        if (damage == 0 ||
            experienceRate == 0 ||
            isRuneSkill ||
            availableLevelCount <= 1 ||
            playerSpell.Level >= availableLevelCount - 1 ||
            playerLevel < runtimeSpell.NeedLevel)
        {
            return CreateSpellExperienceResult(playerSpell, 0, updated: false, levelChanged: false);
        }

        var appliedExperience = MultiplySaturatingToInt32(damage, experienceRate);
        var experience = AddSaturatingToInt32(playerSpell.Experience, appliedExperience);
        var level = playerSpell.Level;
        var levelChanged = false;

        if (runtimeSpell.Experience == 0 || (uint)Math.Max(0, experience) > runtimeSpell.Experience)
        {
            level = checked((ushort)Math.Min(ushort.MaxValue, level + 1));
            experience = 0;
            levelChanged = true;
        }

        if (playerSpell.PreviousLevel != 0 &&
            level >= playerSpell.PreviousLevel / 2 &&
            level < playerSpell.PreviousLevel)
        {
            levelChanged = levelChanged || level != playerSpell.PreviousLevel;
            level = playerSpell.PreviousLevel;
            experience = 0;
        }

        var updatedSpell = playerSpell with
        {
            Level = level,
            Experience = experience,
        };

        return CreateSpellExperienceResult(updatedSpell, appliedExperience, updated: true, levelChanged);
    }

    public static byte GetLegacyLineWidth(LegacyMagicRuntimeSpell spell)
    {
        return spell.Id switch
        {
            1045 or 1046 or 1260 or 15930 => 2,
            11005 or 12930 or 15810 => 1,
            _ => 0,
        };
    }

    public static byte GetLegacyBombRadius(LegacyMagicRuntimeSpell spell)
    {
        if (spell.MaxTargets > 0)
        {
            return spell.MaxTargets;
        }

        return spell.Range;
    }

    public static int GetLegacySectorSize(LegacyMagicRuntimeSpell spell)
    {
        return Math.Clamp(spell.Range * 20, 0, 360);
    }

    public static byte GetLegacySectorDistance(LegacyMagicRuntimeSpell spell)
    {
        return spell.Id == 8001
            ? spell.Range
            : (byte)Math.Min((int)spell.Range, 4);
    }

    public static bool TryGetLegacyBossPhysicalSpellEffect(ushort spellId, out MsgAttackEffect effect)
    {
        effect = spellId switch
        {
            7_013 or 7_014 or 7_015 or 7_017 or 8_003 or 10_000 or 10_001 or 10_362 or 10_500 or
                10_502 or 10_503 or 10_504 or 10_506 or 10_531 or 10_364 or 11_322 or 13_190 or
                30_013 or 30_014 =>
                MsgAttackEffect.None,
            11_311 => MsgAttackEffect.ResistEarth,
            _ => MsgAttackEffect.None,
        };

        return spellId is
            7_013 or 7_014 or 7_015 or 7_017 or 8_003 or 10_000 or 10_001 or 10_362 or 10_500 or
            10_502 or 10_503 or 10_504 or 10_506 or 10_531 or 10_364 or 11_311 or 11_322 or 13_190 or
            30_013 or 30_014;
    }

    public static uint CalculatePlayerMonsterDamage(
        WorldSession session,
        WorldDataCatalog? worldData,
        Monster monsterFamily,
        LegacyMagicRuntimeSpell spell,
        IReadOnlyList<uint> statusFields,
        bool hasInfinity = false,
        uint infinityMonsterDamagePercent = 0,
        uint infinitySpellDamagePercent = 0,
        bool hasSlayer = false,
        uint slayerPercent = 0,
        uint slayerTargetHitpoints = uint.MaxValue,
        uint monsterDamageBonusPercent = 0)
    {
        return CalculatePlayerMonsterDamageWithEffects(
                session,
                worldData,
                monsterFamily,
                spell,
                statusFields,
                hasInfinity,
                infinityMonsterDamagePercent,
                infinitySpellDamagePercent,
                hasSlayer,
                slayerPercent,
                slayerTargetHitpoints,
                monsterDamageBonusPercent: monsterDamageBonusPercent)
            .Damage;
    }

    public static LegacyPlayerMonsterMagicDamageResult CalculatePlayerMonsterDamageWithEffects(
        WorldSession session,
        WorldDataCatalog? worldData,
        Monster monsterFamily,
        LegacyMagicRuntimeSpell spell,
        IReadOnlyList<uint> statusFields,
        bool hasInfinity = false,
        uint infinityMonsterDamagePercent = 0,
        uint infinitySpellDamagePercent = 0,
        bool hasSlayer = false,
        uint slayerPercent = 0,
        uint slayerTargetHitpoints = uint.MaxValue,
        uint luckyStrikeRoll = 99,
        uint magicCriticalStrikeRoll = 99,
        uint monsterDamageBonusPercent = 0)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(monsterFamily);
        ArgumentNullException.ThrowIfNull(statusFields);

        if (LegacyMonsterDamageRules.TakesOnePlayerDamage(monsterFamily))
        {
            return new LegacyPlayerMonsterMagicDamageResult(1, MsgAttackEffect.None);
        }

        var effect = MsgAttackEffect.None;
        var damage = statusFields.Count > LegacyPlayerStatusPacket.MagicAttackIndex
            ? statusFields[LegacyPlayerStatusPacket.MagicAttackIndex]
            : 0;

        if (statusFields.Count > LegacyPlayerStatusPacket.MagicPercentIndex)
        {
            damage = AddPercentBonus(damage, statusFields[LegacyPlayerStatusPacket.MagicPercentIndex]);
        }

        damage = AddSaturating(damage, spell.Damage);
        if (monsterDamageBonusPercent != 0)
        {
            damage = MulDivToUInt32(damage, monsterDamageBonusPercent, 100);
        }

        var attackerLevel = CheckedInt32(session.Character.Level, nameof(session.Character.Level));
        var monsterLevel = ReadMonsterLevel(monsterFamily);
        if (attackerLevel >= monsterLevel)
        {
            damage = MulDivToUInt32(damage, 18, 10);
        }

        var defense = ReadMonsterDefense(monsterFamily);
        damage = damage <= defense ? 1 : damage - defense;
        if (statusFields.Count > LegacyPlayerStatusPacket.LuckyStrikePercentIndex &&
            CheckSourceRate(statusFields[LegacyPlayerStatusPacket.LuckyStrikePercentIndex] / 100, luckyStrikeRoll))
        {
            effect |= MsgAttackEffect.LuckyStrike;
            damage = MulDivToUInt32(damage, 200, 100);
        }

        if (statusFields.Count > LegacyPlayerStatusPacket.SkillCStrikeIndex &&
            statusFields[LegacyPlayerStatusPacket.SkillCStrikeIndex] != 0 &&
            CheckSourceRate(40, magicCriticalStrikeRoll))
        {
            effect |= MsgAttackEffect.CriticalStrike;
            var sourcePercent = statusFields[LegacyPlayerStatusPacket.SkillCStrikeIndex] / 100;
            damage = AddSaturating(damage, MulDivToUInt32(damage, sourcePercent, 100));
        }

        damage = CalculateLegacyUserMonsterLevelDamage(
            damage,
            defense,
            attackerLevel,
            monsterLevel);
        damage = Math.Max(
            damage,
            LegacyCombatDamageCalculator.CalculateMinimumUserMonsterDamage(
                CheckedUInt32(session.Character.Level, nameof(session.Character.Level)),
                LegacyEquipmentSelector.SelectCurrentEquipment(session.PlayerItems, session.AlternateEquipmentActive)));

        if (statusFields.Count > LegacyPlayerStatusPacket.MagicDamageIncreaseIndex)
        {
            damage = AddSaturating(damage, statusFields[LegacyPlayerStatusPacket.MagicDamageIncreaseIndex]);
        }

        var defense2 = ReadAttributeUInt32(monsterFamily.Attributes, "defense2", "defence2", "Defense2", "Defence2");
        if (defense2 == 0)
        {
            damage = 1;
        }

        if (LegacyMonsterDamageRules.IsLegacyGuardFamily(monsterFamily))
        {
            damage /= 10;
        }

        if (hasSlayer)
        {
            damage = MulDivToUInt32(damage, slayerPercent, 100);
            var maximumDamage = slayerTargetHitpoints == 0 ? 0 : slayerTargetHitpoints - 1;
            damage = Math.Min(damage, maximumDamage);
        }

        if (hasInfinity)
        {
            damage = MulDivToUInt32(damage, infinitySpellDamagePercent, 100);
        }

        return new LegacyPlayerMonsterMagicDamageResult(damage, effect);
    }

    private static bool TryCreate(MagicType row, out LegacyMagicRuntimeSpell runtimeSpell)
    {
        runtimeSpell = default!;
        if (row.Fields.Count < MinimumFieldCount ||
            !TryReadUInt16(row.Fields, TypeIdField, out var id) ||
            !TryReadByte(row.Fields, SortField, out var sort) ||
            !TryReadByte(row.Fields, AttackInFlyField, out var attackInFlyField) ||
            !TryReadByte(row.Fields, LevelField, out var level) ||
            !TryReadUInt16(row.Fields, UseManaField, out var useMana) ||
            !TryReadUInt32(row.Fields, DamageField, out var rawDamage) ||
            !TryReadUInt32(row.Fields, GDamageField, out var gDamage) ||
            !TryReadUInt32(row.Fields, DurationField, out var duration) ||
            !TryReadUInt32(row.Fields, ExperienceField, out var experience) ||
            !TryReadByte(row.Fields, MaxTargetsField, out var maxTargets) ||
            !TryReadByte(row.Fields, RangeField, out var range) ||
            !TryReadUInt16(row.Fields, NeedLevelField, out var needLevel) ||
            !TryReadUInt32(row.Fields, WeaponTypeField, out var weaponType) ||
            !TryReadByte(row.Fields, UseStaminaField, out var useStamina) ||
            !TryReadUInt16(row.Fields, CoolDownField, out var coolDown) ||
            !TryReadInt32(row.Fields, DamageOnHumanField, out var damageOnHuman) ||
            !TryReadInt32(row.Fields, Damage2Field, out var damage2) ||
            !TryReadInt32(row.Fields, Damage3Field, out var damage3) ||
            !TryReadInt32(row.Fields, DamageOnMonsterField, out var damageOnMonster) ||
            !TryReadInt32(row.Fields, ColdTimeField, out var coldTime))
        {
            return false;
        }

        runtimeSpell = new LegacyMagicRuntimeSpell(
            id,
            (LegacyMagicSort)sort,
            row.Name,
            level,
            useMana,
            NormalizeLegacyDamage(id, (LegacyMagicSort)sort, rawDamage),
            experience,
            maxTargets,
            range,
            needLevel,
            weaponType,
            useStamina,
            NormalizeLegacyCoolDown(id, coolDown),
            coldTime,
            duration,
            unchecked((uint)damageOnHuman),
            unchecked((uint)damage2),
            unchecked((uint)damage3),
            unchecked((uint)damageOnMonster),
            IsLegacyAttackInFly(id, attackInFlyField),
            gDamage,
            rawDamage);
        return true;
    }

    private static bool IsLegacyAttackInFly(ushort spellId, byte attackInFlyField)
    {
        return attackInFlyField == 0 &&
            IsLegacyAttackInFlyAllowedSpell(spellId);
    }

    private static bool IsLegacyAttackInFlyAllowedSpell(ushort spellId)
    {
        if ((spellId >= 5_010 && spellId <= 5_050) ||
            (spellId >= 7_000 && spellId <= 7_040))
        {
            return false;
        }

        return spellId is not (
            1_045 or 1_046 or 1_250 or 1_260 or 1_290 or
            6_000 or 6_001 or 6_010 or
            10_315 or 10_381 or 10_415 or 10_490 or
            11_000 or 11_005 or 11_040 or 11_070 or 11_110 or 11_140 or
            11_170 or 11_180 or 11_190 or 11_230);
    }

    private static uint NormalizeLegacyDamage(ushort spellId, LegacyMagicSort sort, uint rawDamage)
    {
        if (spellId == 13_030)
        {
            return rawDamage;
        }

        if (spellId == 11_140)
        {
            return 150;
        }

        if (rawDamage == 0)
        {
            return 100;
        }

        // MagicType.cs245 preserves14500's integer power; only zero becomes100.
        if (spellId == 14_500)
        {
            return rawDamage;
        }

        if (sort is LegacyMagicSort.Attack && rawDamage < 10_000)
        {
            return rawDamage;
        }

        var normalized = Math.Min(rawDamage % 1_000, 500);
        return normalized == 0 ? 100 : normalized;
    }

    private static ushort NormalizeLegacyCoolDown(ushort spellId, ushort rawCoolDown)
    {
        return spellId == 10_415 ? (ushort)800 : rawCoolDown;
    }

    private static bool TryReadUInt16(IReadOnlyList<string> fields, int index, out ushort value)
    {
        value = 0;
        return index < fields.Count &&
            ushort.TryParse(fields[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadUInt32(IReadOnlyList<string> fields, int index, out uint value)
    {
        value = 0;
        return index < fields.Count &&
            uint.TryParse(fields[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadByte(IReadOnlyList<string> fields, int index, out byte value)
    {
        value = 0;
        return index < fields.Count &&
            byte.TryParse(fields[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadInt32(IReadOnlyList<string> fields, int index, out int value)
    {
        value = 0;
        return index < fields.Count &&
            int.TryParse(fields[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static uint AddPercentBonus(uint value, uint percent)
    {
        if (value == 0 || percent == 0)
        {
            return value;
        }

        return AddSaturating(value, checked((uint)(((ulong)value * percent) / 100)));
    }

    private static uint AddSaturating(uint left, uint right)
    {
        var sum = (ulong)left + right;
        return sum > uint.MaxValue ? uint.MaxValue : (uint)sum;
    }

    private static uint ReadMonsterDefense(Monster monsterFamily)
    {
        return ReadAttributeUInt32(
            monsterFamily.Attributes,
            "defense",
            "defence",
            "Defense",
            "Defence");
    }

    private static int ReadMonsterLevel(Monster monsterFamily)
    {
        var level = ReadAttributeUInt32(monsterFamily.Attributes, "level", "Level");
        return level > int.MaxValue ? 0 : (int)level;
    }

    private static uint CalculateLegacyUserMonsterLevelDamage(
        uint attack,
        uint defense,
        int attackerLevel,
        int monsterLevel)
    {
        if (GetLegacyMonsterNameType(attackerLevel, monsterLevel) > 2)
        {
            return attack <= defense ? 0 : attack - defense;
        }

        var levelDelta = attackerLevel - monsterLevel;
        var scaled = attack;
        if (levelDelta >= 0 && levelDelta < 3)
        {
            scaled = MulDivToUInt32(attack, 120, 100);
        }
        else if (levelDelta >= 3 && levelDelta <= 5)
        {
            scaled = MulDivToUInt32(attack, 150, 100);
        }
        else if (levelDelta > 5 && levelDelta <= 10)
        {
            scaled = MulDivToUInt32(attack, 200, 100);
        }
        else if (levelDelta > 10 && levelDelta <= 20)
        {
            scaled = MulDivToUInt32(attack, 250, 100);
        }
        else if (levelDelta > 20)
        {
            scaled = MulDivToUInt32(attack, 300, 100);
        }

        return scaled <= defense ? 0 : scaled - defense;
    }

    private static int GetLegacyMonsterNameType(int attackerLevel, int defenderLevel)
    {
        var levelDelta = attackerLevel - defenderLevel;
        if (levelDelta >= 3)
        {
            return 1;
        }

        if (levelDelta >= 0)
        {
            return 2;
        }

        return levelDelta >= -5 ? 3 : 4;
    }

    private static uint MulDivToUInt32(uint number, uint numerator, uint denominator)
    {
        if (denominator == 0)
        {
            return uint.MaxValue;
        }

        var result = (ulong)number * numerator / denominator;
        return result > uint.MaxValue ? uint.MaxValue : (uint)result;
    }

    private static bool CheckSourceRate(uint percent, uint roll)
    {
        return percent != 0 && roll % 100 < percent;
    }

    private static uint BigMulDivToUInt32(uint number, uint numerator, uint denominator)
    {
        if (denominator == 0)
        {
            return uint.MaxValue;
        }

        var result = (((ulong)number * numerator) + (denominator / 2UL)) / denominator;
        return result > uint.MaxValue ? uint.MaxValue : (uint)result;
    }

    private static uint ReadAttributeUInt32(IReadOnlyDictionary<string, string> attributes, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (attributes.TryGetValue(key, out var value) &&
                uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return 0;
    }

    private static LegacySpellExperienceResult CreateSpellExperienceResult(
        PlayerSpellSnapshotEntry spell,
        int appliedExperience,
        bool updated,
        bool levelChanged)
    {
        return new LegacySpellExperienceResult(
            spell,
            LegacySpellPacket.FromSnapshot(spell),
            appliedExperience,
            updated,
            levelChanged);
    }

    private static int MultiplySaturatingToInt32(uint left, uint right)
    {
        var product = (ulong)left * right;
        return product > int.MaxValue ? int.MaxValue : (int)product;
    }

    private static int AddSaturatingToInt32(int left, int right)
    {
        var sum = (long)left + right;
        if (sum > int.MaxValue)
        {
            return int.MaxValue;
        }

        return sum < int.MinValue ? int.MinValue : (int)sum;
    }

    private static int CheckedInt32(long value, string name)
    {
        if (value is < int.MinValue or > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy magic value cannot be outside int range.");
        }

        return (int)value;
    }

    private static uint CheckedUInt32(long value, string name)
    {
        if (value is < uint.MinValue or > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy magic value cannot be outside uint range.");
        }

        return (uint)value;
    }
}
