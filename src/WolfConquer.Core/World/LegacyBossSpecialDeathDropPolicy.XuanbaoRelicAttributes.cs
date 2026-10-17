using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class LegacyBossSpecialDeathDropPolicy
{
    public static bool TryCreateDeityLandRandomRelicItem(
        LegacyBossSpecialDeathDropCatalog catalog,
        LegacyBossSpecialDeathDropRolls rolls,
        out LegacyBossSpecialGeneratedDropItem relicItem,
        out string missingReason)
    {
        return TryCreateDeityLandRandomRelicDrop(catalog, rolls, out relicItem, out missingReason);
    }

    public static bool TryCreateMaxRandomRelicItem(
        LegacyBossSpecialDeathDropCatalog catalog,
        LegacyBossSpecialDeathDropRolls rolls,
        out LegacyBossSpecialGeneratedDropItem relicItem,
        out string missingReason)
    {
        var itemIndex = SelectPoolIndex(DeityLandRelicItemPool.Length, rolls.DeityLandRelicItemIndex);
        var itemId = DeityLandRelicItemPool[itemIndex];
        if (!catalog.RelicRandomAttributesByItemTypeId.TryGetValue(checked((int)itemId), out var randomAttributes) ||
            randomAttributes.Count == 0)
        {
            relicItem = default;
            missingReason =
                "Inventory.AddRandomRelic(Max:true) needs xuanbao_rand_attr.txt epic relic attributes for item pool 4100001..4100005, but no matching rows were loaded.";
            return false;
        }

        var encodedAttributes = new uint[5];
        var usedAttributeTypes = new HashSet<int>();
        for (var attributeIndex = 0; attributeIndex < encodedAttributes.Length; attributeIndex++)
        {
            var candidates = randomAttributes
                .Where(attribute =>
                    !usedAttributeTypes.Contains(attribute.AttributeType) &&
                    !IsSourceExcludedDeityLandRelicAttribute(itemId, attribute.AttributeType) &&
                    GetNormalizedXuanbaoRandomAttributeMax(attribute, out var epic) > 0 &&
                    epic)
                .ToArray();
            if (candidates.Length == 0)
            {
                relicItem = default;
                missingReason =
                    $"Inventory.AddRandomRelic(Max:true) could not select five unique epic allowed attributes for relic item {itemId}.";
                return false;
            }

            var poolIndex = SelectPoolIndex(
                candidates.Length,
                attributeIndex,
                rolls.DeityLandRelicAttributeIndexes);
            var selected = candidates[poolIndex];
            usedAttributeTypes.Add(selected.AttributeType);
            var maximum = GetNormalizedXuanbaoRandomAttributeMax(selected, out _);
            encodedAttributes[attributeIndex] = EncodeDeityLandRelicAttribute(selected, maximum);
        }

        relicItem = new LegacyBossSpecialGeneratedDropItem(
            itemId,
            Durability: 10_099,
            MaximumDurability: 10_099,
            ScatterRange: 7,
            encodedAttributes[0],
            encodedAttributes[1],
            encodedAttributes[2],
            encodedAttributes[3],
            encodedAttributes[4],
            $"Inventory.AddRandomRelic(Max:true); itemPoolIndex={itemIndex} attributeCount=5.");
        missingReason = string.Empty;
        return true;
    }

    private static bool TryCreateDeityLandRandomRelicDrop(
        LegacyBossSpecialDeathDropCatalog catalog,
        LegacyBossSpecialDeathDropRolls rolls,
        out LegacyBossSpecialGeneratedDropItem relicDrop,
        out string missingReason)
    {
        var itemIndex = SelectPoolIndex(DeityLandRelicItemPool.Length, rolls.DeityLandRelicItemIndex);
        var itemId = DeityLandRelicItemPool[itemIndex];
        if (!catalog.RelicRandomAttributesByItemTypeId.TryGetValue(checked((int)itemId), out var randomAttributes) ||
            randomAttributes.Count == 0)
        {
            relicDrop = default;
            missingReason = "Inventory.AddRandomRelic() needs xuanbao_rand_attr.txt random relic attributes for item pool 4100001..4100005, but no matching rows were loaded.";
            return false;
        }

        var attributeCount = GetDeityLandRelicAttributeCount(rolls);
        var encodedAttributes = new uint[5];
        var usedAttributeTypes = new HashSet<int>();
        for (var attributeIndex = 0; attributeIndex < attributeCount; attributeIndex++)
        {
            var candidates = randomAttributes
                .Where(attribute =>
                    !usedAttributeTypes.Contains(attribute.AttributeType) &&
                    !IsSourceExcludedDeityLandRelicAttribute(itemId, attribute.AttributeType) &&
                    GetNormalizedXuanbaoRandomAttributeMax(attribute, out _) > 0)
                .ToArray();
            if (candidates.Length == 0)
            {
                relicDrop = default;
                missingReason = $"Inventory.AddRandomRelic() could not select {attributeCount} unique allowed attributes for relic item {itemId}.";
                return false;
            }

            var poolIndex = SelectPoolIndex(
                candidates.Length,
                attributeIndex,
                rolls.DeityLandRelicAttributeIndexes);
            var selected = candidates[poolIndex];
            usedAttributeTypes.Add(selected.AttributeType);
            encodedAttributes[attributeIndex] = EncodeDeityLandRelicAttribute(
                selected,
                GetDeityLandRelicAttributeValue(selected, attributeIndex, rolls));
        }

        var durabilityRoll = rolls.DeityLandRelicDurabilityRoll is { } forcedRoll
            ? Math.Clamp(forcedRoll, 10, 98)
            : Random.Shared.Next(10, 99);
        var durability = checked((ushort)((durabilityRoll * 100) + 99));
        relicDrop = new LegacyBossSpecialGeneratedDropItem(
            itemId,
            durability,
            durability,
            ScatterRange: 7,
            encodedAttributes[0],
            encodedAttributes[1],
            encodedAttributes[2],
            encodedAttributes[3],
            encodedAttributes[4],
            $"MonsterRole.cs Inventory.AddRandomRelic(); itemPoolIndex={itemIndex} attributeCount={attributeCount}.");
        missingReason = "";
        return true;
    }

    private static int GetDeityLandRelicAttributeCount(LegacyBossSpecialDeathDropRolls rolls)
    {
        if (rolls.DeityLandRelicAttributeCount is { } forcedCount)
        {
            return Math.Clamp(forcedCount, 0, 5);
        }

        return Math.Min(Random.Shared.Next(3, 7), 5);
    }

    private static uint EncodeDeityLandRelicAttribute(
        XuanbaoRandomAttribute attribute,
        int value)
    {
        _ = GetNormalizedXuanbaoRandomAttributeMax(attribute, out var epic);
        return checked(((uint)value * 1_000U) + (uint)attribute.AttributeType + (epic ? 100U : 0U));
    }

    private static int GetDeityLandRelicAttributeValue(
        XuanbaoRandomAttribute attribute,
        int attributeIndex,
        LegacyBossSpecialDeathDropRolls rolls)
    {
        var max = GetNormalizedXuanbaoRandomAttributeMax(attribute, out _);
        var min = Math.Min(attribute.MinValue, max);
        if (rolls.DeityLandRelicAttributeValues is not null &&
            attributeIndex < rolls.DeityLandRelicAttributeValues.Count)
        {
            return Math.Clamp(rolls.DeityLandRelicAttributeValues[attributeIndex], min, max);
        }

        int value;
        do
        {
            value = Random.Shared.Next(min, checked(max + 1));
        }
        while (max > 0 &&
            (double)value / max * 100.0 < 60.0 &&
            Random.Shared.Next(0, 100) < 55);

        return Math.Min(value, max);
    }

    private static int GetNormalizedXuanbaoRandomAttributeMax(
        XuanbaoRandomAttribute attribute,
        out bool epic)
    {
        epic = attribute.MaxValue % 10 == 1;
        return epic ? attribute.MaxValue - 1 : attribute.MaxValue;
    }

    private static bool IsSourceExcludedDeityLandRelicAttribute(uint itemId, int attributeType)
    {
        return itemId switch
        {
            4_100_001 => attributeType is
                (int)LegacyChiAttributeType.SkillCriticalStrike or
                (int)LegacyChiAttributeType.CriticalStrike or
                (int)LegacyChiAttributeType.HPAdd or
                (int)LegacyChiAttributeType.AddAttack,
            4_100_002 => attributeType is
                (int)LegacyChiAttributeType.SkillCriticalStrike or
                (int)LegacyChiAttributeType.CriticalStrike or
                (int)LegacyChiAttributeType.AddMagicAttack or
                (int)LegacyChiAttributeType.AddAttack,
            4_100_003 => attributeType is
                (int)LegacyChiAttributeType.HPAdd or
                (int)LegacyChiAttributeType.CriticalStrike or
                (int)LegacyChiAttributeType.AddMagicAttack or
                (int)LegacyChiAttributeType.AddAttack,
            4_100_004 => attributeType is
                (int)LegacyChiAttributeType.HPAdd or
                (int)LegacyChiAttributeType.CriticalStrike or
                (int)LegacyChiAttributeType.AddMagicAttack or
                (int)LegacyChiAttributeType.SkillCriticalStrike,
            4_100_005 => attributeType is
                (int)LegacyChiAttributeType.HPAdd or
                (int)LegacyChiAttributeType.AddAttack or
                (int)LegacyChiAttributeType.AddMagicAttack or
                (int)LegacyChiAttributeType.SkillCriticalStrike,
            _ => false,
        };
    }
}
