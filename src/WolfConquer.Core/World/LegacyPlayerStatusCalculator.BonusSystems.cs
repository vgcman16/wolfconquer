using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class LegacyPlayerStatusCalculator
{
    private static void ApplyMythsoulBagAttributes(
        uint[] fields,
        PlayerItemSnapshot playerItems,
        IReadOnlyDictionary<int, SpecialEffectAttribute>? specialEffectAttributesById)
    {
        if (specialEffectAttributesById is null)
        {
            return;
        }

        foreach (var item in playerItems.MythsoulBagItems)
        {
            if (item.ItemId > int.MaxValue ||
                !specialEffectAttributesById.TryGetValue((int)item.ItemId, out var attribute) ||
                attribute.Values.Count == 0 ||
                attribute.Values[0] <= 0)
            {
                continue;
            }

            ApplyMythsoulBagAttribute(fields, attribute.Type, checked((uint)attribute.Values[0]));
        }
    }

    private static void ApplyMythsoulBagAttribute(uint[] fields, int type, uint value)
    {
        switch (type)
        {
            case 1:
                Add(fields, LegacyPlayerStatusPacket.MaxHPIndex, value);
                break;
            case 2:
                Add(fields, LegacyPlayerStatusPacket.MinAttackIndex, value);
                Add(fields, LegacyPlayerStatusPacket.MaxAttackIndex, value);
                break;
            case 3:
                Add(fields, LegacyPlayerStatusPacket.DefenceIndex, value);
                break;
            case 4:
                Add(fields, LegacyPlayerStatusPacket.MagicAttackIndex, value);
                break;
            case 5:
                Add(fields, LegacyPlayerStatusPacket.MagicDefenceIndex, value);
                break;
            case 6:
                Add(fields, LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex, value);
                break;
            case 7:
                Add(fields, LegacyPlayerStatusPacket.PhysicalDamageDecreaseIndex, value);
                break;
            case 8:
                Add(fields, LegacyPlayerStatusPacket.MagicDamageIncreaseIndex, value);
                break;
            case 9:
                Add(fields, LegacyPlayerStatusPacket.MagicDamageDecreaseIndex, value);
                break;
            case 10:
                Add(fields, LegacyPlayerStatusPacket.CriticalStrikeIndex, value);
                break;
            case 11:
                Add(fields, LegacyPlayerStatusPacket.SkillCStrikeIndex, value);
                break;
            case 12:
                Add(fields, LegacyPlayerStatusPacket.ImmunityIndex, value);
                break;
            case 13:
                Add(fields, LegacyPlayerStatusPacket.BreakthroughIndex, value);
                break;
            case 14:
                Add(fields, LegacyPlayerStatusPacket.CounteractionIndex, value);
                break;
        }
    }

    private static void ApplyBeastAttributes(
        uint[] fields,
        CharacterSnapshot snapshot,
        IReadOnlyDictionary<int, BeastAttribute>? beastAttributesByLevel)
    {
        if (beastAttributesByLevel is null ||
            beastAttributesByLevel.Count == 0 ||
            !ReadBooleanAttribute(snapshot, "TailedBeastActivated") ||
            !TryReadUIntAttribute(snapshot, "TailedBeastTotalPoints", out var totalPoints))
        {
            return;
        }

        var level = CalculateLegacyBeastLevel(totalPoints, beastAttributesByLevel);
        if (!beastAttributesByLevel.TryGetValue(level, out var attribute))
        {
            return;
        }

        Add(fields, LegacyPlayerStatusPacket.MinAttackIndex, ReadBeastParameter(attribute, 2));
        Add(fields, LegacyPlayerStatusPacket.MaxAttackIndex, ReadBeastParameter(attribute, 2));
        Add(fields, LegacyPlayerStatusPacket.MagicAttackIndex, ReadBeastParameter(attribute, 3));
        Add(fields, LegacyPlayerStatusPacket.DefenceIndex, ReadBeastParameter(attribute, 4));
        Add(fields, LegacyPlayerStatusPacket.MagicDefenceIndex, ReadBeastParameter(attribute, 5));
        Add(fields, LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex, ReadBeastParameter(attribute, 6));
        Add(fields, LegacyPlayerStatusPacket.MagicDamageIncreaseIndex, ReadBeastParameter(attribute, 7));
        Add(fields, LegacyPlayerStatusPacket.PhysicalDamageDecreaseIndex, ReadBeastParameter(attribute, 8));
        Add(fields, LegacyPlayerStatusPacket.MagicDamageDecreaseIndex, ReadBeastParameter(attribute, 9));
    }

    private static void ApplyProfessionAttributes(
        uint[] fields,
        CharacterSnapshot snapshot,
        IReadOnlyDictionary<int, ProfessionLevelBenefit>? benefitsByProfessionCode)
    {
        if (benefitsByProfessionCode is null ||
            snapshot.Class > int.MaxValue ||
            !benefitsByProfessionCode.TryGetValue((int)snapshot.Class, out var benefit))
        {
            return;
        }

        foreach (var encoded in benefit.BonusValues.Take(4))
        {
            if (encoded <= 0)
            {
                continue;
            }

            ApplyChiStyleAttribute(fields, (uint)(encoded / 10_000), (uint)(encoded % 10_000));
        }
    }

    private static void ApplyRuneStorageAttributes(
        uint[] fields,
        PlayerItemSnapshot playerItems,
        IReadOnlyList<RuneStorageAttribute>? runeStorageAttributes)
    {
        if (runeStorageAttributes is null)
        {
            return;
        }

        var score = LegacyRuneHitpointsCalculator.CalculateStorageScore(playerItems.RuneItems);
        foreach (var attribute in runeStorageAttributes)
        {
            if (attribute.RequiredStorageScore < 0 ||
                attribute.RequiredStorageScore > score)
            {
                continue;
            }

            foreach (var bonus in attribute.Bonuses)
            {
                if (bonus.Value > 0)
                {
                    ApplyRuneStorageAttribute(fields, bonus.AttributeType, checked((uint)bonus.Value));
                }
            }
        }
    }

    private static void ApplyRuneStorageAttribute(uint[] fields, int type, uint value)
    {
        switch (type)
        {
            case 2:
                Add(fields, LegacyPlayerStatusPacket.MinAttackIndex, value);
                Add(fields, LegacyPlayerStatusPacket.MaxAttackIndex, value);
                break;
            case 3:
                Add(fields, LegacyPlayerStatusPacket.MagicAttackIndex, value);
                break;
            case 10:
                Add(fields, LegacyPlayerStatusPacket.CriticalStrikeIndex, value);
                break;
            case 11:
                Add(fields, LegacyPlayerStatusPacket.SkillCStrikeIndex, value);
                break;
            case 12:
                Add(fields, LegacyPlayerStatusPacket.ImmunityIndex, value);
                break;
            case 13:
                Add(fields, LegacyPlayerStatusPacket.BreakthroughIndex, value / 10);
                break;
            case 14:
                Add(fields, LegacyPlayerStatusPacket.CounteractionIndex, value / 10);
                break;
        }
    }

    private static void ApplyActiveRuneAttackAttributes(
        uint[] fields,
        PlayerItemSnapshot playerItems,
        IReadOnlyDictionary<int, IReadOnlyList<ItemType>>? itemsById,
        bool alternateEquipmentActive)
    {
        if (itemsById is null)
        {
            return;
        }

        if (TryGetEquippedRuneLevel(playerItems, itemsById, "Tempered Glaive", out var temperedGlaiveLevel) &&
            TryGetCurrentLeftWeaponItemType(playerItems, itemsById, alternateEquipmentActive, out var leftWeapon))
        {
            AddTemperedGlaiveAttack(fields, leftWeapon, temperedGlaiveLevel);
        }

        var rightWeaponIsTwoHand = IsEquippedRightWeaponTwoHand(playerItems);
        if (rightWeaponIsTwoHand &&
            TryGetEquippedRuneLevel(playerItems, itemsById, "WideSwipe", out var wideSwipeLevel))
        {
            AddActiveRuneAttack(fields, wideSwipeLevel);
        }

        if (!rightWeaponIsTwoHand &&
            TryGetEquippedRuneLevel(playerItems, itemsById, "Sharpness", out var sharpnessLevel))
        {
            AddActiveRuneAttack(fields, sharpnessLevel);
        }
    }

    private static void AddTemperedGlaiveAttack(uint[] fields, ItemType leftWeapon, uint runeLevel)
    {
        var maxAttack = ReadUIntField(leftWeapon, MaxAttackFieldIndex);
        var minAttack = ReadUIntField(leftWeapon, MinAttackFieldIndex);
        var maxBonus = runeLevel switch
        {
            >= 1 and <= 3 => maxAttack / 4,
            >= 4 and <= 6 => maxAttack / 3,
            7 => maxAttack / 2,
            8 or 9 => checked(maxAttack * 2 / 3),
            _ => 0u,
        };
        var minBonus = runeLevel switch
        {
            >= 1 and <= 3 => minAttack / 4,
            >= 4 and <= 6 => minAttack / 3,
            7 => minAttack / 2,
            8 or 9 => minAttack,
            _ => 0u,
        };

        Add(fields, LegacyPlayerStatusPacket.MaxAttackIndex, maxBonus);
        Add(fields, LegacyPlayerStatusPacket.MinAttackIndex, minBonus);
    }

    private static void AddActiveRuneAttack(uint[] fields, uint runeLevel)
    {
        var attack = checked(runeLevel * (fields[LegacyPlayerStatusPacket.MinAttackIndex] / 50));
        Add(fields, LegacyPlayerStatusPacket.MaxAttackIndex, attack);
        Add(fields, LegacyPlayerStatusPacket.MinAttackIndex, attack);
    }

    private static void ApplyActiveRuneDefenceAttributes(
        uint[] fields,
        PlayerItemSnapshot playerItems,
        IReadOnlyDictionary<int, IReadOnlyList<ItemType>>? itemsById)
    {
        if (itemsById is null)
        {
            return;
        }

        if (TryGetEquippedRuneLevel(playerItems, itemsById, "Wonder", out var wonderLevel))
        {
            Add(fields, LegacyPlayerStatusPacket.BlockIndex, checked(GetLegacyWonderBlockBase(wonderLevel) * 12));
        }
    }

    private static bool TryGetEquippedRuneLevel(
        PlayerItemSnapshot playerItems,
        IReadOnlyDictionary<int, IReadOnlyList<ItemType>> itemsById,
        string runeName,
        out uint level)
    {
        level = 0;
        foreach (var rune in playerItems.RuneItems)
        {
            if (!IsLegacyWornRunePosition(rune.Position) ||
                !TryGetItemType(itemsById, rune.ItemId, out var itemType) ||
                itemType.Fields.Count <= 1 ||
                !itemType.Fields[1].Contains(runeName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            level = rune.ItemId % 100;
            return true;
        }

        return false;
    }

    private static bool TryGetCurrentLeftWeaponItemType(
        PlayerItemSnapshot playerItems,
        IReadOnlyDictionary<int, IReadOnlyList<ItemType>> itemsById,
        bool alternateEquipmentActive,
        out ItemType itemType)
    {
        itemType = default!;
        foreach (var item in LegacyEquipmentSelector.SelectCurrentEquipment(playerItems, alternateEquipmentActive))
        {
            if (item.Durability == 0 ||
                item.Position % 50 != 5 ||
                !TryGetItemType(itemsById, item.ItemId, out itemType))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static uint GetLegacyWonderBlockBase(uint runeLevel)
    {
        return runeLevel switch
        {
            1 => 100,
            2 => 130,
            3 => 150,
            4 => 160,
            5 => 180,
            6 => 200,
            7 => 220,
            8 => 230,
            9 => 250,
            _ => runeLevel,
        };
    }

    private static bool IsEquippedRightWeaponTwoHand(PlayerItemSnapshot playerItems)
    {
        foreach (var item in playerItems.EquipmentItems)
        {
            if (item.Durability > 0 &&
                item.Position % 50 == 4)
            {
                return IsLegacyTwoHandWeapon(item.ItemId);
            }
        }

        return false;
    }

    private static bool IsLegacyTwoHandWeapon(uint itemId)
    {
        return itemId / 100_000 == 5 ||
            itemId is >= 421_003 and <= 421_439;
    }

    private static void ApplyChiAttributes(
        uint[] fields,
        CharacterSnapshot snapshot,
        IReadOnlyDictionary<int, IReadOnlyList<FateExperience>>? fateExperienceByType)
    {
        foreach (var power in LegacyChiState.FromSnapshot(snapshot).Powers)
        {
            if (!power.Unlocked)
            {
                continue;
            }

            foreach (var attribute in power.Attributes)
            {
                ApplyRawChiAttribute(fields, attribute.Type, attribute.Value);
            }

            var fate = FindChiFateAttributes(power, fateExperienceByType);
            if (fate is not null)
            {
                foreach (var encoded in fate.Values)
                {
                    if (encoded <= 0)
                    {
                        continue;
                    }

                    ApplyRawChiAttribute(fields, (uint)(encoded / 10_000), (uint)(encoded % 10_000));
                }
            }

            ApplyChiScoreBonus(fields, power);
        }
    }

    private static void ApplyRawChiAttribute(uint[] fields, uint type, uint value)
    {
        switch ((LegacyChiAttributeType)type)
        {
            case LegacyChiAttributeType.CriticalStrike:
                Add(fields, LegacyPlayerStatusPacket.CriticalStrikeIndex, checked(value * 10));
                break;
            case LegacyChiAttributeType.SkillCriticalStrike:
                Add(fields, LegacyPlayerStatusPacket.SkillCStrikeIndex, checked(value * 10));
                break;
            case LegacyChiAttributeType.Immunity:
                Add(fields, LegacyPlayerStatusPacket.ImmunityIndex, checked(value * 10));
                break;
            case LegacyChiAttributeType.Breakthrough:
                Add(fields, LegacyPlayerStatusPacket.BreakthroughIndex, value);
                break;
            case LegacyChiAttributeType.Counteraction:
                Add(fields, LegacyPlayerStatusPacket.CounteractionIndex, value);
                break;
            case LegacyChiAttributeType.AddAttack:
                Add(fields, LegacyPlayerStatusPacket.MinAttackIndex, value);
                Add(fields, LegacyPlayerStatusPacket.MaxAttackIndex, value);
                break;
            case LegacyChiAttributeType.AddMagicAttack:
                Add(fields, LegacyPlayerStatusPacket.MagicAttackIndex, value);
                break;
            case LegacyChiAttributeType.AddMagicDefense:
                Add(fields, LegacyPlayerStatusPacket.MagicDefenceIndex, value);
                break;
            case LegacyChiAttributeType.PhysicalDamageIncrease:
                Add(fields, LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex, value);
                break;
            case LegacyChiAttributeType.MagicDamageIncrease:
                Add(fields, LegacyPlayerStatusPacket.MagicDamageIncreaseIndex, value);
                break;
            case LegacyChiAttributeType.PhysicalDamageDecrease:
                Add(fields, LegacyPlayerStatusPacket.PhysicalDamageDecreaseIndex, value);
                break;
            case LegacyChiAttributeType.MagicDamageDecrease:
                Add(fields, LegacyPlayerStatusPacket.MagicDamageDecreaseIndex, value);
                break;
        }
    }

    private static void ApplyChiScoreBonus(uint[] fields, LegacyChiPowerState power)
    {
        var score = CalculateChiScore(power);
        var isTopRankOrScore = score == 400;
        if (!isTopRankOrScore)
        {
            return;
        }

        switch (power.Type)
        {
            case LegacyChiPowerType.Dragon:
                Add(fields, LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex, 1_000);
                Add(fields, LegacyPlayerStatusPacket.MagicDamageIncreaseIndex, 300);
                Add(fields, LegacyPlayerStatusPacket.MagicDamageDecreaseIndex, 300);
                break;
            case LegacyChiPowerType.Phoenix:
                Add(fields, LegacyPlayerStatusPacket.MinAttackIndex, 3_000);
                Add(fields, LegacyPlayerStatusPacket.MaxAttackIndex, 3_000);
                Add(fields, LegacyPlayerStatusPacket.MagicAttackIndex, 3_000);
                Add(fields, LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex, 1_000);
                Add(fields, LegacyPlayerStatusPacket.MagicDamageIncreaseIndex, 300);
                break;
            case LegacyChiPowerType.Tiger:
                Add(fields, LegacyPlayerStatusPacket.CriticalStrikeIndex, 1_500);
                Add(fields, LegacyPlayerStatusPacket.SkillCStrikeIndex, 1_500);
                Add(fields, LegacyPlayerStatusPacket.ImmunityIndex, 800);
                break;
            case LegacyChiPowerType.Turtle:
                Add(fields, LegacyPlayerStatusPacket.BreakthroughIndex, 150);
                Add(fields, LegacyPlayerStatusPacket.CounteractionIndex, 150);
                Add(fields, LegacyPlayerStatusPacket.ImmunityIndex, 800);
                break;
        }
    }

    private static FateExperience? FindChiFateAttributes(
        LegacyChiPowerState power,
        IReadOnlyDictionary<int, IReadOnlyList<FateExperience>>? fateExperienceByType)
    {
        if (fateExperienceByType is null ||
            !fateExperienceByType.TryGetValue((int)power.Type, out var candidates))
        {
            return null;
        }

        return candidates
            .Where(entry => power.Experience >= 0 && power.Experience >= (ulong)Math.Max(0, entry.Experience))
            .OrderBy(entry => entry.Experience)
            .LastOrDefault();
    }

    private static int CalculateChiScore(LegacyChiPowerState power)
    {
        var score = 0;
        foreach (var attribute in power.Attributes)
        {
            var max = ChiMaxValue(attribute.Type);
            var min = ChiMinValue(attribute.Type);
            if (max <= min)
            {
                continue;
            }

            var boundedValue = Math.Clamp(attribute.Value, min, max);
            score += (int)((uint)(boundedValue - min) / ((double)(max - min) / 100.0));
        }

        return score;
    }

    private static int ChiMaxValue(byte attributeType)
    {
        return (LegacyChiAttributeType)attributeType switch
        {
            LegacyChiAttributeType.CriticalStrike => 200,
            LegacyChiAttributeType.SkillCriticalStrike => 200,
            LegacyChiAttributeType.Immunity => 200,
            LegacyChiAttributeType.Breakthrough => 200,
            LegacyChiAttributeType.Counteraction => 200,
            LegacyChiAttributeType.HPAdd => 3_500,
            LegacyChiAttributeType.AddAttack => 2_000,
            LegacyChiAttributeType.AddMagicAttack => 2_500,
            LegacyChiAttributeType.AddMagicDefense => 250,
            LegacyChiAttributeType.PhysicalDamageIncrease => 500,
            LegacyChiAttributeType.PhysicalDamageDecrease => 500,
            LegacyChiAttributeType.MagicDamageIncrease => 300,
            LegacyChiAttributeType.MagicDamageDecrease => 300,
            _ => 100,
        };
    }

    private static int ChiMinValue(byte attributeType)
    {
        return (LegacyChiAttributeType)attributeType switch
        {
            LegacyChiAttributeType.CriticalStrike => 10,
            LegacyChiAttributeType.SkillCriticalStrike => 10,
            LegacyChiAttributeType.Immunity => 10,
            LegacyChiAttributeType.Breakthrough => 10,
            LegacyChiAttributeType.Counteraction => 10,
            LegacyChiAttributeType.HPAdd => 1_000,
            LegacyChiAttributeType.AddAttack => 500,
            LegacyChiAttributeType.AddMagicAttack => 500,
            LegacyChiAttributeType.AddMagicDefense => 50,
            LegacyChiAttributeType.PhysicalDamageIncrease => 50,
            LegacyChiAttributeType.PhysicalDamageDecrease => 50,
            LegacyChiAttributeType.MagicDamageIncrease => 50,
            LegacyChiAttributeType.MagicDamageDecrease => 50,
            _ => 10,
        };
    }

    private static void ApplyChiStyleAttribute(uint[] fields, uint type, uint value)
    {
        switch (type)
        {
            case 1:
                Add(fields, LegacyPlayerStatusPacket.CriticalStrikeIndex, value);
                break;
            case 2:
                Add(fields, LegacyPlayerStatusPacket.SkillCStrikeIndex, value);
                break;
            case 3:
                Add(fields, LegacyPlayerStatusPacket.ImmunityIndex, value);
                break;
            case 4:
                Add(fields, LegacyPlayerStatusPacket.BreakthroughIndex, value / 10);
                break;
            case 5:
                Add(fields, LegacyPlayerStatusPacket.CounteractionIndex, value / 10);
                break;
            case 7:
                Add(fields, LegacyPlayerStatusPacket.MinAttackIndex, value);
                Add(fields, LegacyPlayerStatusPacket.MaxAttackIndex, value);
                break;
            case 8:
                Add(fields, LegacyPlayerStatusPacket.MagicAttackIndex, value);
                break;
            case 9:
                Add(fields, LegacyPlayerStatusPacket.MagicDefenceIndex, value);
                break;
            case 10:
                Add(fields, LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex, value);
                break;
            case 11:
                Add(fields, LegacyPlayerStatusPacket.MagicDamageIncreaseIndex, value);
                break;
            case 12:
                Add(fields, LegacyPlayerStatusPacket.PhysicalDamageDecreaseIndex, value);
                break;
            case 13:
                Add(fields, LegacyPlayerStatusPacket.MagicDamageDecreaseIndex, value);
                break;
        }
    }
}
