using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TrySelectLegacyMonsterDeathDropItemId(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        WorldDataCatalog worldData,
        Monster monsterFamily,
        int roll,
        out LegacyMonsterDeathDropSelection selection,
        out string noDropReason)
    {
        noDropReason = "";
        if (roll is > 500 and < 600)
        {
            if (TryReadKnownLegacyDropItemId(worldData, monsterFamily, "drop_hp", out var itemId, out noDropReason))
            {
                selection = new LegacyMonsterDeathDropSelection(itemId, FloorItemRuntimePickupKind.Item, Amount: 0);
                return true;
            }

            selection = default;
            return false;
        }

        if (roll is > 600 and < 700)
        {
            if (TryReadKnownLegacyDropItemId(worldData, monsterFamily, "drop_mp", out var itemId, out noDropReason))
            {
                selection = new LegacyMonsterDeathDropSelection(itemId, FloorItemRuntimePickupKind.Item, Amount: 0);
                return true;
            }

            selection = default;
            return false;
        }

        if (roll > 700)
        {
            foreach (var specialDrop in monsterFamily.SpecialDrops)
            {
                if (specialDrop.ItemTypeId > 0 &&
                    TryUseKnownLegacyDropItemId(worldData, checked((uint)specialDrop.ItemTypeId), out var itemId))
                {
                    selection = new LegacyMonsterDeathDropSelection(itemId, FloorItemRuntimePickupKind.Item, Amount: 0);
                    return true;
                }
            }

            if (TryGenerateLegacyMonsterDeathDropItemId(
                    context,
                    character,
                    worldData,
                    monsterFamily,
                    out selection,
                    out noDropReason))
            {
                return true;
            }
        }

        selection = default;
        if (noDropReason.Length == 0)
        {
            noDropReason = $"source roll {roll} did not resolve a concrete monster death drop item";
        }

        return false;
    }

    private static bool TryGenerateLegacyMonsterDeathDropItemId(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        WorldDataCatalog worldData,
        Monster monsterFamily,
        out LegacyMonsterDeathDropSelection selection,
        out string noDropReason)
    {
        noDropReason = "";
        var forcedLevel = TryReadUInt32Attribute(
            character.Attributes,
            out var forcedLevelValue,
            RuntimeLegacyMonsterDropItemLevelAttribute);
        if (!TrySelectLegacyGeneratedItemBase(monsterFamily, character, forcedLevel, out var itemBase, out noDropReason))
        {
            selection = default;
            return false;
        }

        var level = forcedLevel
            ? forcedLevelValue
            : AlterLegacyGeneratedItemLevel(itemBase.Level, itemBase.Sort);
        if (level > 99)
        {
            selection = default;
            noDropReason = $"source generated item level {level} is outside the local item id range";
            return false;
        }

        var quality = ReadLegacyGeneratedItemQuality(context, character, monsterFamily);
        var candidate = checked((uint)itemBase.Sort * 1000U + level * 10U + quality);
        if (!TryUseKnownLegacyDropItemId(worldData, candidate, out var itemId))
        {
            selection = default;
            noDropReason =
                $"source generated candidate item id {candidate} from sort={itemBase.Sort} level={level} quality={quality} " +
                "is absent from itemtype; legacy MobItemGenerator returns 0 and no item drops";
            return false;
        }

        selection = new LegacyMonsterDeathDropSelection(itemId, FloorItemRuntimePickupKind.Item, Amount: 0);
        return true;
    }

    private static bool TrySelectLegacyGeneratedItemBase(
        Monster monsterFamily,
        CharacterSnapshot character,
        bool hasForcedLevel,
        out LegacyMonsterGeneratedItemBase itemBase,
        out string noDropReason)
    {
        noDropReason = "";
        if (TryReadUInt32Attribute(
                character.Attributes,
                out var forcedSort,
                RuntimeLegacyMonsterDropItemSortAttribute) &&
            forcedSort <= ushort.MaxValue)
        {
            var sort = (ushort)forcedSort;
            if (!TryReadLegacyGeneratedItemLevelForSort(monsterFamily, sort, out var level) && !hasForcedLevel)
            {
                itemBase = default;
                noDropReason = $"source generated sort {sort} has no matching monster drop level attribute";
                return false;
            }

            if (!hasForcedLevel && level == 99)
            {
                itemBase = default;
                noDropReason = $"source generated sort {sort} maps to disabled drop level 99";
                return false;
            }

            itemBase = new LegacyMonsterGeneratedItemBase(sort, level);
            return true;
        }

        var categoryRoll = Random.Shared.Next(0, 1200);
        if (categoryRoll < 20)
        {
            return TrySelectLegacyGeneratedItemBase(
                monsterFamily,
                [160],
                out itemBase,
                out noDropReason,
                "drop_shoes",
                "drop_boots");
        }

        if (categoryRoll < 50)
        {
            return TrySelectLegacyGeneratedItemBase(
                monsterFamily,
                LegacyGeneratedNecklaceTypes,
                out itemBase,
                out noDropReason,
                "drop_necklace");
        }

        if (categoryRoll < 100)
        {
            return TrySelectLegacyGeneratedItemBase(
                monsterFamily,
                LegacyGeneratedRingTypes,
                out itemBase,
                out noDropReason,
                "drop_ring");
        }

        if (categoryRoll < 400)
        {
            return TrySelectLegacyGeneratedItemBase(
                monsterFamily,
                LegacyGeneratedArmetTypes,
                out itemBase,
                out noDropReason,
                "drop_armet");
        }

        if (categoryRoll < 700)
        {
            return TrySelectLegacyGeneratedItemBase(
                monsterFamily,
                LegacyGeneratedArmorTypes,
                out itemBase,
                out noDropReason,
                "drop_armor");
        }

        var weaponRoll = Random.Shared.Next(0, 100);
        if (weaponRoll < 20)
        {
            return TrySelectLegacyGeneratedItemBase(
                monsterFamily,
                [900],
                out itemBase,
                out noDropReason,
                "drop_shield");
        }

        if (weaponRoll >= 40 && weaponRoll < 80)
        {
            return TrySelectLegacyGeneratedItemBase(
                monsterFamily,
                LegacyGeneratedOneHanderTypes,
                out itemBase,
                out noDropReason,
                "drop_weapon");
        }

        if (weaponRoll >= 80)
        {
            return TrySelectLegacyGeneratedItemBase(
                monsterFamily,
                LegacyGeneratedTwoHanderTypes,
                out itemBase,
                out noDropReason,
                "drop_weapon");
        }

        itemBase = default;
        noDropReason = "source generated weapon roll 20..39 does not assign a generated equipment sort";
        return false;
    }

    private static bool TrySelectLegacyGeneratedItemBase(
        Monster monsterFamily,
        IReadOnlyList<ushort> sorts,
        out LegacyMonsterGeneratedItemBase itemBase,
        out string noDropReason,
        params string[] levelAttributeNames)
    {
        if (!TryReadUInt32Attribute(monsterFamily.Attributes, out var level, levelAttributeNames))
        {
            itemBase = default;
            noDropReason = $"source generated item branch has no monster level attribute among [{string.Join(", ", levelAttributeNames)}]";
            return false;
        }

        if (level == 99)
        {
            itemBase = default;
            noDropReason = $"source generated item branch maps [{string.Join(", ", levelAttributeNames)}] to disabled drop level 99";
            return false;
        }

        if (sorts.Count == 0)
        {
            itemBase = default;
            noDropReason = "source generated item branch has no sort table entries";
            return false;
        }

        itemBase = new LegacyMonsterGeneratedItemBase(
            sorts[Random.Shared.Next(0, sorts.Count)],
            level);
        noDropReason = "";
        return true;
    }

    private static bool TryReadLegacyGeneratedItemLevelForSort(
        Monster monsterFamily,
        ushort sort,
        out uint level)
    {
        if (sort == 160)
        {
            return TryReadUInt32Attribute(monsterFamily.Attributes, out level, "drop_shoes", "drop_boots");
        }

        if (LegacyGeneratedNecklaceTypes.Contains(sort))
        {
            return TryReadUInt32Attribute(monsterFamily.Attributes, out level, "drop_necklace");
        }

        if (LegacyGeneratedRingTypes.Contains(sort))
        {
            return TryReadUInt32Attribute(monsterFamily.Attributes, out level, "drop_ring");
        }

        if (LegacyGeneratedArmetTypes.Contains(sort))
        {
            return TryReadUInt32Attribute(monsterFamily.Attributes, out level, "drop_armet");
        }

        if (LegacyGeneratedArmorTypes.Contains(sort))
        {
            return TryReadUInt32Attribute(monsterFamily.Attributes, out level, "drop_armor");
        }

        if (sort == 900)
        {
            return TryReadUInt32Attribute(monsterFamily.Attributes, out level, "drop_shield");
        }

        if (LegacyGeneratedOneHanderTypes.Contains(sort) ||
            LegacyGeneratedTwoHanderTypes.Contains(sort))
        {
            return TryReadUInt32Attribute(monsterFamily.Attributes, out level, "drop_weapon");
        }

        level = 0;
        return false;
    }

    private static uint AlterLegacyGeneratedItemLevel(uint level, ushort sort)
    {
        var roll = Random.Shared.Next(0, 100);
        if (roll < 50)
        {
            var maxExclusive = checked((int)Math.Min(int.MaxValue, level / 2U + level / 3U));
            var altered = checked((uint)Random.Shared.Next(0, Math.Max(1, maxExclusive)));
            return altered > 1 ? altered - 1U : altered;
        }

        if (roll > 80)
        {
            return Math.Min(level + 1U, IsLegacyGeneratedShortLevelSort(sort) ? 9U : 23U);
        }

        return level;
    }

    private static bool IsLegacyGeneratedShortLevelSort(ushort sort)
    {
        return sort is >= 110 and <= 114 or >= 130 and <= 134 or >= 900 and <= 999;
    }

    private static byte ReadLegacyGeneratedItemQuality(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        Monster monsterFamily)
    {
        if (TryReadUInt32Attribute(
                character.Attributes,
                out var forcedQuality,
                RuntimeLegacyMonsterDropItemQualityAttribute) &&
            forcedQuality <= 9)
        {
            return (byte)forcedQuality;
        }

        return context.MonsterStates?.NextLegacyGeneratedItemQuality(monsterFamily.Id) ?? (byte)3;
    }

    private static int ReadLegacyMonsterDeathDropRoll(CharacterSnapshot character, out bool forced)
    {
        forced = TryReadInt64Attribute(character.Attributes, RuntimeLegacyMonsterDropRollAttribute, out var forcedRoll) &&
            forcedRoll >= 0;
        return forced
            ? checked((int)(forcedRoll % 1000))
            : Random.Shared.Next(0, 1000);
    }

    private static byte GetLegacyMonsterDeathDropCount(int roll, bool forcedRoll)
    {
        var count = (byte)(roll % 5);
        if (count == 0 ||
            !forcedRoll && Random.Shared.Next(0, 100) < 80)
        {
            return 1;
        }

        return count;
    }

    private static string? GetLegacyMonsterDeathDropKind(int roll)
    {
        return roll switch
        {
            > 40 and < 60 => "money",
            > 500 and < 600 => "hp-item",
            > 600 and < 700 => "mp-item",
            > 700 => "item",
            _ => null,
        };
    }

    private static uint ReadLegacyMonsterDeathDropMoneyAmount(CharacterSnapshot character)
    {
        return TryReadInt64Attribute(character.Attributes, RuntimeLegacyMonsterDropMoneyAmountAttribute, out var forcedAmount) &&
            forcedAmount > 0 &&
            forcedAmount <= uint.MaxValue
            ? checked((uint)forcedAmount)
            : checked((uint)Random.Shared.Next(1, 200_000));
    }

    private static uint GetLegacyMoneyItemId(uint amount)
    {
        if (amount < 100)
        {
            return 1_090_000;
        }

        if (amount < 399)
        {
            return 1_090_010;
        }

        if (amount < 5_099)
        {
            return 1_090_020;
        }

        if (amount < 8_099)
        {
            return 1_091_000;
        }

        if (amount < 12_099)
        {
            return 1_091_010;
        }

        return 1_091_020;
    }

    private static bool TryReadKnownLegacyDropItemId(
        WorldDataCatalog worldData,
        Monster monsterFamily,
        string attributeName,
        out uint itemId,
        out string noDropReason)
    {
        if (!monsterFamily.Attributes.TryGetValue(attributeName, out var raw) ||
            !uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            itemId = 0;
            noDropReason = $"source fixed monster drop attribute {attributeName} is missing or invalid";
            return false;
        }

        if (TryUseKnownLegacyDropItemId(worldData, parsed, out itemId))
        {
            noDropReason = "";
            return true;
        }

        noDropReason = $"source fixed monster drop item id {parsed} from {attributeName} is not present in the local itemtype catalog";
        return false;
    }

    private static bool TryUseKnownLegacyDropItemId(
        WorldDataCatalog worldData,
        uint candidate,
        out uint itemId)
    {
        if (candidate <= 0 ||
            candidate > int.MaxValue ||
            !worldData.ItemsById.TryGetValue((int)candidate, out var items) ||
            items.Count == 0)
        {
            itemId = 0;
            return false;
        }

        itemId = candidate;
        return true;
    }

    private static string FormatLegacyMonsterDropNoDropReason(string reason)
    {
        return string.IsNullOrWhiteSpace(reason)
            ? "source generated item branch returned no item; legacy MobItemGenerator returns 0 and no item drops"
            : reason.Replace('\r', ' ').Replace('\n', ' ');
    }

    private static IReadOnlyList<KeyValuePair<string, object?>> CreateLegacyFixedMonsterDropNoDropFacts(
        WorldDataCatalog worldData,
        Monster monsterFamily,
        string dropKind)
    {
        var attributeName = dropKind switch
        {
            "hp-item" => "drop_hp",
            "mp-item" => "drop_mp",
            _ => null,
        };
        if (attributeName is null)
        {
            return [];
        }

        var sourceField = attributeName == "drop_hp"
            ? "MonsterFamily.DropHPItem"
            : "MonsterFamily.DropMPItem";
        var facts = new List<KeyValuePair<string, object?>>
        {
            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
            LegacyParityGapDiagnostics.Fact("dropAttribute", attributeName),
            LegacyParityGapDiagnostics.Fact("sourceField", sourceField),
        };

        if (!monsterFamily.Attributes.TryGetValue(attributeName, out var raw))
        {
            facts.Add(LegacyParityGapDiagnostics.Fact("dropCatalogStatus", "missing-attribute"));
            return facts;
        }

        facts.Add(LegacyParityGapDiagnostics.Fact("dropAttributeRaw", raw));
        if (!uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var candidate))
        {
            facts.Add(LegacyParityGapDiagnostics.Fact("dropCatalogStatus", "invalid-attribute"));
            return facts;
        }

        facts.Add(LegacyParityGapDiagnostics.Fact("candidateItemId", candidate));
        if (candidate == 0 || candidate > int.MaxValue)
        {
            facts.Add(LegacyParityGapDiagnostics.Fact("dropCatalogStatus", "invalid-item-id"));
            return facts;
        }

        var present = worldData.ItemsById.TryGetValue(checked((int)candidate), out var items) &&
            items.Count > 0;
        facts.Add(LegacyParityGapDiagnostics.Fact(
            "dropCatalogStatus",
            present ? "present" : "missing-itemtype"));
        return facts;
    }

    private static bool IsLegacyMonsterDropBlockedMap(int mapId)
    {
        return mapId is 10_250 or 3_846 or 2_351 or 2_352 or 2_353;
    }
}
