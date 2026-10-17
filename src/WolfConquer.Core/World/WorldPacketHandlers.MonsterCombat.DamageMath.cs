using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static uint GetLegacyDeterministicAttack(uint maxAttack, uint minAttack)
    {
        var normalizedMaxAttack = maxAttack;
        if (normalizedMaxAttack <= minAttack)
        {
            normalizedMaxAttack = minAttack == uint.MaxValue ? uint.MaxValue : minAttack + 1;
        }

        var range = normalizedMaxAttack - minAttack;
        var deterministicRoll = range == 0 ? 0 : (range - 1) / 2;
        return SelectLegacyPlayerMonsterPhysicalAttack(minAttack, maxAttack, deterministicRoll);
    }

    private static uint SelectLegacyPlayerMonsterPhysicalAttack(
        uint minAttack,
        uint maxAttack,
        uint? deterministicRoll = null)
    {
        if (maxAttack <= minAttack)
        {
            maxAttack = minAttack == uint.MaxValue ? uint.MaxValue : minAttack + 1;
        }

        var range = maxAttack - minAttack;
        if (range == 0)
        {
            return minAttack;
        }

        var roll = deterministicRoll ?? checked((uint)Random.Shared.NextInt64(range));
        roll = Math.Min(roll, range - 1);
        return AddLegacySaturating(minAttack, roll / 2);
    }

    private static uint ApplyLegacyPlayerMonsterAjustAttack(
        uint damage,
        IReadOnlyList<uint> fields,
        WorldSession session)
    {
        var bonus = CalculateLegacyPlayerAjustAttackBonus(damage, fields, session.Character);
        var runeExtraAttackLevel = ReadLegacyPlayerMonsterRuneExtraAttackLevel(session);
        if (runeExtraAttackLevel > 0)
        {
            var runeExtraAttackPercent = runeExtraAttackLevel == 9 ? 10 : runeExtraAttackLevel;
            runeExtraAttackPercent = AddLegacySaturating(runeExtraAttackPercent, 6);
            bonus += ((ulong)damage * runeExtraAttackPercent) / 100UL;
        }

        return AddLegacySaturating(damage, bonus > uint.MaxValue ? uint.MaxValue : (uint)bonus);
    }

    private static uint ApplyLegacyPlayerAjustAttack(
        uint damage,
        IReadOnlyList<uint> fields,
        CharacterSnapshot character)
    {
        var bonus = CalculateLegacyPlayerAjustAttackBonus(damage, fields, character);
        return AddLegacySaturating(damage, bonus > uint.MaxValue ? uint.MaxValue : (uint)bonus);
    }

    private static ulong CalculateLegacyPlayerAjustAttackBonus(
        uint damage,
        IReadOnlyList<uint> fields,
        CharacterSnapshot character)
    {
        var bonus = 0UL;
        if (HasLegacyStatusFlag(character, LegacyStigmaFlag) ||
            ReadAttributeBool(character.Attributes, "OnAttackPotion", "AttackPotion"))
        {
            bonus += CalculateLegacyPercentDelta(damage, 130);
        }

        if (fields.Count > LegacyPlayerStatusPacket.PhysicalPercentIndex)
        {
            bonus += ((ulong)damage * fields[LegacyPlayerStatusPacket.PhysicalPercentIndex]) / 100UL;
        }

        var focusDamagePercent = ReadLegacyPlayerMonsterFocusDamagePercent(character);
        if (focusDamagePercent > 0)
        {
            bonus += CalculateLegacyPercentDelta(damage, focusDamagePercent);
        }

        return bonus;
    }

    private static uint ApplyLegacyPlayerMonsterAjustMaxAttack(uint damage, CharacterSnapshot character)
    {
        var extraAttack = ReadAttributeUInt32(
            character.Attributes,
            "IncreasePAttack",
            "ExtraStatusIncreasePAttack",
            "ExtraIncreasePAttack",
            "IncreasePAttackPower");
        return AddLegacySaturating(damage, extraAttack);
    }

    private static uint ReadLegacyPlayerMonsterFocusDamagePercent(CharacterSnapshot character)
    {
        if (!HasLegacyStatusFlag(character, LegacyFocusedFlag) &&
            !ReadAttributeBool(character.Attributes, "doFocus", "DoFocus", "Focus"))
        {
            return 0;
        }

        return ReadAttributeUInt32(
            character.Attributes,
            "IntensifyDamage",
            "FocusDamage",
            "FocusDamagePercent",
            "IntensifyDamagePercent");
    }

    private static uint ReadLegacyPlayerMonsterRuneExtraAttackLevel(WorldSession session)
    {
        if (!TryGetCurrentRightWeapon(session, out var rightWeapon))
        {
            return 0;
        }

        var attributes = session.Character.Attributes;
        if (ItemType.IsLegacyTwoHandWeapon(rightWeapon.ItemId))
        {
            return ReadAttributeUInt32(
                attributes,
                "WideSwipeLevel",
                "LegacyWideSwipeLevel",
                "LegacyPlayerMonsterRuneExtraAttackLevel",
                "LegacyRuneExtraAttackLevel");
        }

        return ReadAttributeUInt32(
            attributes,
            "SharpnessLevel",
            "LegacySharpnessLevel",
            "LegacyPlayerMonsterRuneExtraAttackLevel",
            "LegacyRuneExtraAttackLevel");
    }

    private static ulong CalculateLegacyPercentDelta(uint damage, uint percent)
    {
        var adjusted = ((ulong)damage * percent) / 100UL;
        return adjusted <= damage ? 0 : adjusted - damage;
    }

    private static uint AddLegacyPercentBonus(uint value, uint percent)
    {
        if (value == 0 || percent == 0)
        {
            return value;
        }

        return AddLegacySaturating(value, checked((uint)(((ulong)value * percent) / 100)));
    }

    private static uint AddLegacySaturating(uint left, uint right)
    {
        var sum = (ulong)left + right;
        return sum > uint.MaxValue ? uint.MaxValue : (uint)sum;
    }

    private static uint DecreaseLegacyBless(uint damage, uint bless)
    {
        return LegacyCombatDamageCalculator.DecreaseLegacyBless(damage, bless);
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

    private static uint CalculateLegacyMinimumUserMonsterDamage(
        CharacterSnapshot character,
        PlayerItemSnapshot playerItems,
        bool alternateEquipmentActive = false)
    {
        return LegacyCombatDamageCalculator.CalculateMinimumUserMonsterDamage(
            CheckedUInt32(character.Level, nameof(character.Level)),
            LegacyEquipmentSelector.SelectCurrentEquipment(playerItems, alternateEquipmentActive));
    }

    private static uint CalculateLegacyUserMonsterLevelDamage(
        uint attack,
        uint defense,
        int attackerLevel,
        int defenderLevel,
        bool range)
    {
        if (range && attackerLevel >= 117)
        {
            attackerLevel = 117;
        }

        if (GetLegacyMonsterNameType(attackerLevel, defenderLevel) > 2)
        {
            return attack <= defense ? 0 : attack - defense;
        }

        var levelDelta = attackerLevel - defenderLevel;
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

    private static uint MulDivToUInt32(uint number, uint numerator, uint denominator)
    {
        if (denominator == 0)
        {
            return uint.MaxValue;
        }

        var result = ((ulong)number * numerator) / denominator;
        return result > uint.MaxValue ? uint.MaxValue : (uint)result;
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
}
