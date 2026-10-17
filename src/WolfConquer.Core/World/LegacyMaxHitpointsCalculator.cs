using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed record LegacyMaxHitpointsExternalBonuses(
    uint Chi = 0,
    uint InnerPower = 0,
    uint Rune = 0,
    uint JiangHu = 0,
    uint HundredWeapons = 0,
    uint Beasts = 0,
    uint Profession = 0,
    uint Vip = 0,
    uint ExtraStatus = 0)
{
    public uint Total =>
        Chi +
        InnerPower +
        Rune +
        JiangHu +
        HundredWeapons +
        Beasts +
        Profession +
        Vip +
        ExtraStatus;
}

public sealed record LegacyMaxHitpointsBreakdown(
    uint AttributeHitpoints,
    uint EquipmentHitpoints,
    uint CombatGearHitpoints,
    uint ExternalHitpoints,
    uint Total,
    IReadOnlyList<string> MissingInputs);

public static partial class LegacyMaxHitpointsCalculator
{
    private const int ItemTypeItemHitpointsFieldIndex = 18;
    private const int ItemAddHitpointsValueIndex = 0;
    private const int DaoqiMaxHitpointsAttributeType = 1;
    private const int DaoqiFirstAttributeTypeFieldIndex = 2;
    private const int DaoqiFirstAttributeValueFieldIndex = 3;
    private const int DaoqiSecondAttributeTypeFieldIndex = 4;
    private const int DaoqiSecondAttributeValueFieldIndex = 5;
    private const int DaoqiScoreFieldIndex = 6;
    private const int DaoqiTypeRuneFieldIndex = 9;
    private const int DaoqiTypeCombatGearFieldIndex = 14;
    private const int CombatGearNutBookOverallCollectionId = 700;
    public static uint CalculateRuntimeMaxHitpoints(
        CharacterSnapshot snapshot,
        uint currentHitpoints,
        PlayerItemSnapshot playerItems,
        CombatGearSnapshot combatGear,
        WorldDataCatalog? catalog,
        bool alternateEquipmentActive = false)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(playerItems);
        ArgumentNullException.ThrowIfNull(combatGear);

        if (LegacyTransformState.TryGetActiveMaxHitpoints(snapshot, out var transformMaxHitpoints))
        {
            return transformMaxHitpoints;
        }

        var computed = Calculate(
            snapshot,
            playerItems,
            combatGear,
            catalog,
            alternateEquipmentActive: alternateEquipmentActive).Total;
        var stored = ReadStoredMaxHitpoints(snapshot);
        var max = Math.Max((ulong)currentHitpoints, (ulong)computed);
        max = Math.Max(max, stored);
        return max > uint.MaxValue ? uint.MaxValue : (uint)max;
    }

    public static LegacyMaxHitpointsBreakdown Calculate(
        CharacterSnapshot snapshot,
        PlayerItemSnapshot playerItems,
        CombatGearSnapshot combatGear,
        WorldDataCatalog? catalog,
        LegacyMaxHitpointsExternalBonuses? externalBonuses = null,
        bool alternateEquipmentActive = false)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(playerItems);
        ArgumentNullException.ThrowIfNull(combatGear);

        var breakdown = Calculate(
            snapshot.Class,
            snapshot.Strength,
            snapshot.Agility,
            snapshot.Vitality,
            snapshot.Spirit,
            playerItems,
            combatGear,
            catalog?.ItemsById,
            catalog?.ItemAddBonusByItemTypeIdAndLevel,
            catalog?.CombatGearByGearTypeAndLevel,
            catalog?.DaoqiDictTypesById,
            externalBonuses,
            catalog?.CollectionRewardTiersByCollectionId,
            alternateEquipmentActive);
        var chiContribution = LegacyChiHitpointsCalculator.Calculate(snapshot, catalog);
        var innerPowerContribution = LegacyInnerPowerHitpointsCalculator.Calculate(snapshot, catalog);
        var runeContribution = LegacyRuneHitpointsCalculator.Calculate(playerItems, catalog);
        var circleOfLifeEnchantContribution = CalculateCircleOfLifeEnchantHitpoints(
            snapshot,
            playerItems,
            catalog?.ItemsById,
            alternateEquipmentActive);
        var itemSecondaryContribution = LegacyItemSecondaryHitpointsCalculator.Calculate(
            playerItems,
            catalog,
            alternateEquipmentActive: alternateEquipmentActive);
        var subClassContribution = LegacySubClassHitpointsCalculator.Calculate(snapshot, catalog);
        var combatGearStatusHitpoints = CalculateCombatGearStatusHitpoints(
            snapshot.Attributes,
            catalog?.MagicByTypeId);
        var professionHitpoints = CalculateProfessionHitpoints(
            snapshot.Class,
            catalog?.ProfessionLevelBenefitsByProfessionCode);
        var beastHitpoints = CalculateBeastHitpoints(snapshot, catalog?.BeastAttributesByLevel);
        var sourcedHitpoints = checked(chiContribution.Total +
            innerPowerContribution.Hitpoints +
            runeContribution.Total +
            circleOfLifeEnchantContribution +
            itemSecondaryContribution.Total +
            subClassContribution.Total +
            combatGearStatusHitpoints +
            professionHitpoints +
            beastHitpoints);
        if (sourcedHitpoints == 0)
        {
            return breakdown;
        }

        var missingInputs = breakdown.MissingInputs;
        if (HasChiState(snapshot))
        {
            missingInputs = RemoveMissingInput(missingInputs, "Chi.MaxLife");
            missingInputs = AddMissingInputs(missingInputs, chiContribution.MissingInputs);
        }

        if (innerPowerContribution.MissingInputs.Count == 0)
        {
            missingInputs = RemoveMissingInput(missingInputs, "InnerPower.MaxLife");
        }

        if (!runeContribution.MissingInputs.Contains("PlayerItemSnapshot.RuneItems", StringComparer.Ordinal) &&
            !runeContribution.MissingInputs.Contains("WorldDataCatalog.RuneStorageAttributes", StringComparer.Ordinal))
        {
            missingInputs = RemoveMissingInput(missingInputs, "Rune HP attributes and active rune flags");
            missingInputs = AddMissingInputs(missingInputs, runeContribution.MissingInputs);
        }

        if (beastHitpoints != 0)
        {
            missingInputs = RemoveMissingInput(missingInputs, "Beast HP bonuses");
        }

        if (catalog?.ProfessionLevelBenefitsByProfessionCode is not null)
        {
            missingInputs = RemoveMissingInput(missingInputs, "Profession HP bonuses");
        }

        if (itemSecondaryContribution.MissingInputs.Count == 0)
        {
            missingInputs = RemoveMissingInput(missingInputs, "Item secondary HP bonuses");
        }
        else
        {
            missingInputs = AddMissingInputs(missingInputs, itemSecondaryContribution.MissingInputs);
        }

        if (subClassContribution.MissingInputs.Count == 0 ||
            subClassContribution.MissingInputs.Contains("SubProfInfo Wrangler subclass entry", StringComparer.Ordinal))
        {
            missingInputs = RemoveMissingInput(missingInputs, "SubClass Wrangler HP bonuses");
        }
        else
        {
            missingInputs = AddMissingInputs(missingInputs, subClassContribution.MissingInputs);
        }

        if (catalog?.CollectionRewardTiersByCollectionId is not null &&
            catalog?.DaoqiDictTypesById is not null)
        {
            missingInputs = RemoveMissingInput(missingInputs, "CombatGear rank/nut-book HP bonuses");
        }

        if (combatGearStatusHitpoints != 0)
        {
            missingInputs = RemoveMissingInput(missingInputs, "VIP/status-effect HP bonuses");
        }

        return breakdown with
        {
            ExternalHitpoints = breakdown.ExternalHitpoints + sourcedHitpoints,
            Total = breakdown.Total + sourcedHitpoints,
            MissingInputs = missingInputs,
        };
    }

    public static LegacyMaxHitpointsBreakdown Calculate(
        long classCode,
        long strength,
        long agility,
        long vitality,
        long spirit,
        PlayerItemSnapshot playerItems,
        CombatGearSnapshot combatGear,
        IReadOnlyDictionary<int, IReadOnlyList<ItemType>>? itemsById,
        IReadOnlyDictionary<(int ItemTypeId, int Level), ItemAddBonus>? itemAddBonusByItemTypeIdAndLevel,
        IReadOnlyDictionary<(int GearTypeId, int Level), CombatGear>? combatGearByGearTypeAndLevel,
        IReadOnlyDictionary<int, DaoqiDictType>? daoqiDictTypesById = null,
        LegacyMaxHitpointsExternalBonuses? externalBonuses = null,
        IReadOnlyDictionary<int, IReadOnlyList<CollectionRewardTier>>? collectionRewardTiersByCollectionId = null,
        bool alternateEquipmentActive = false)
    {
        ArgumentNullException.ThrowIfNull(playerItems);
        ArgumentNullException.ThrowIfNull(combatGear);

        var attributeHitpoints = CalculateAttributeHitpoints(classCode, strength, agility, vitality, spirit);
        var equipmentHitpoints = CalculateEquipmentHitpoints(
            playerItems,
            itemsById,
            itemAddBonusByItemTypeIdAndLevel,
            alternateEquipmentActive);
        var combatGearHitpoints = CalculateCombatGearHitpoints(
            classCode,
            combatGear,
            combatGearByGearTypeAndLevel,
            daoqiDictTypesById,
            collectionRewardTiersByCollectionId);
        var externalHitpoints = externalBonuses?.Total ?? 0;
        var missingInputs = CreateMissingInputs(externalBonuses);

        return new LegacyMaxHitpointsBreakdown(
            attributeHitpoints,
            equipmentHitpoints,
            combatGearHitpoints,
            externalHitpoints,
            attributeHitpoints + equipmentHitpoints + combatGearHitpoints + externalHitpoints,
            missingInputs);
    }

    public static uint CalculateAttributeHitpoints(
        long classCode,
        long strength,
        long agility,
        long vitality,
        long spirit)
    {
        var str = Math.Max(0, strength);
        var agi = Math.Max(0, agility);
        var vit = Math.Max(0, vitality);
        var spi = Math.Max(0, spirit);

        if (classCode is >= 1005 and <= 1058)
        {
            return TruncateLegacyUShort((agi * 3.45) + (spi * 3.45) + (str * 3.45) + (vit * 27.6));
        }

        return classCode switch
        {
            1001 => TruncateLegacyUShort((agi * 3.15) + (spi * 3.15) + (str * 3.15) + (vit * 25.2)),
            1002 => TruncateLegacyUShort((agi * 3.24) + (spi * 3.24) + (str * 3.24) + (vit * 25.9)),
            1003 => TruncateLegacyUShort((agi * 3.3) + (spi * 3.3) + (str * 3.3) + (vit * 26.4)),
            1004 => TruncateLegacyUShort((agi * 3.36) + (spi * 3.36) + (str * 3.36) + (vit * 26.8)),
            _ => TruncateLegacyUShort((agi * 3) + (spi * 3) + (str * 3) + (vit * 24)),
        };
    }

    public static uint CalculateBeastHitpoints(
        CharacterSnapshot snapshot,
        IReadOnlyDictionary<int, BeastAttribute>? beastAttributesByLevel)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (beastAttributesByLevel is null ||
            !ReadBooleanAttribute(snapshot, "TailedBeastActivated") ||
            !TryReadUIntAttribute(snapshot, "TailedBeastTotalPoints", out var totalPoints) ||
            beastAttributesByLevel.Count == 0)
        {
            return 0;
        }

        var level = CalculateLegacyBeastLevel(totalPoints, beastAttributesByLevel);
        if (!beastAttributesByLevel.TryGetValue(level, out var attribute) ||
            attribute.Parameters.Count <= 1 ||
            attribute.Parameters[1] <= 0)
        {
            return 0;
        }

        return checked((uint)attribute.Parameters[1]);
    }

    public static uint CalculateChiHitpoints(CharacterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return LegacyChiHitpointsCalculator.Calculate(snapshot).Total;
    }

    public static uint CalculateChiHitpoints(LegacyChiState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return LegacyChiHitpointsCalculator.Calculate(state).Total;
    }

    public static uint CalculateProfessionHitpoints(
        long classCode,
        IReadOnlyDictionary<int, ProfessionLevelBenefit>? professionLevelBenefitsByProfessionCode)
    {
        if (classCode is < int.MinValue or > int.MaxValue ||
            professionLevelBenefitsByProfessionCode is null ||
            !professionLevelBenefitsByProfessionCode.TryGetValue((int)classCode, out var benefit))
        {
            return 0;
        }

        var total = 0u;
        foreach (var encoded in benefit.BonusValues)
        {
            if (encoded <= 0)
            {
                continue;
            }

            var attributeType = encoded / 10_000;
            var attributeValue = encoded % 10_000;
            if ((LegacyChiAttributeType)attributeType == LegacyChiAttributeType.HPAdd)
            {
                total += checked((uint)attributeValue);
            }
        }

        return total;
    }

    public static uint CalculateEquipmentHitpoints(
        PlayerItemSnapshot playerItems,
        IReadOnlyDictionary<int, IReadOnlyList<ItemType>>? itemsById,
        IReadOnlyDictionary<(int ItemTypeId, int Level), ItemAddBonus>? itemAddBonusByItemTypeIdAndLevel,
        bool alternateEquipmentActive = false)
    {
        ArgumentNullException.ThrowIfNull(playerItems);

        var total = 0u;
        foreach (var item in LegacyEquipmentSelector.SelectCurrentEquipment(playerItems, alternateEquipmentActive))
        {
            if (item.Durability == 0)
            {
                continue;
            }

            var position = item.Position % 50;
            var itemHitpoints = TryGetItemHitpoints(itemsById, item.ItemId, out var rawItemHp) ? rawItemHp : 0;
            total += itemHitpoints;

            if (item.PurificationItemId != 0 &&
                TryGetItemHitpoints(itemsById, item.PurificationItemId, out var purificationHitpoints))
            {
                total += purificationHitpoints;
            }

            var enchant = IsLegacyEnchantClearedPosition(position) ? 0u : (uint)item.Enchant;
            if (position != 12)
            {
                total += enchant;
            }

            var plus = IsLegacyPlusClearedPosition(position) ? 0 : Math.Min((int)item.Plus, 12);
            if (plus > 0 &&
                TryGetItemAddBonus(itemAddBonusByItemTypeIdAndLevel, item.ItemId, plus, out var itemAdd) &&
                itemAdd.Values.Count > ItemAddHitpointsValueIndex)
            {
                var plusHitpoints = checked((uint)Math.Max(0, itemAdd.Values[ItemAddHitpointsValueIndex]));
                var animaPercent = TryGetItemHitpoints(itemsById, item.AnimaItemId, out var animaHitpoints)
                    ? animaHitpoints / 100
                    : 0;
                total += plusHitpoints + (plusHitpoints * animaPercent / 100);
            }
        }

        return total;
    }

    public static uint CalculateCircleOfLifeEnchantHitpoints(
        CharacterSnapshot snapshot,
        PlayerItemSnapshot playerItems,
        IReadOnlyDictionary<int, IReadOnlyList<ItemType>>? itemsById,
        bool alternateEquipmentActive = false)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(playerItems);

        return TryGetCircleOfLifeRuneLevel(snapshot, playerItems, itemsById, out var runeLevel)
            ? CalculateCircleOfLifeEnchantHitpoints(playerItems, runeLevel, alternateEquipmentActive)
            : 0;
    }

    public static uint CalculateCircleOfLifeEnchantHitpoints(
        PlayerItemSnapshot playerItems,
        uint runeLevel,
        bool alternateEquipmentActive = false)
    {
        ArgumentNullException.ThrowIfNull(playerItems);

        var total = 0u;
        foreach (var item in LegacyEquipmentSelector.SelectCurrentEquipment(playerItems, alternateEquipmentActive))
        {
            if (item.Durability == 0)
            {
                continue;
            }

            var position = item.Position % 50;
            if (IsLegacyEnchantClearedPosition(position))
            {
                continue;
            }

            total = checked(total + CalculateCircleOfLifeEnchantBonus(item.Enchant, runeLevel));
        }

        return total;
    }

    public static uint CalculateCombatGearHitpoints(
        long classCode,
        CombatGearSnapshot combatGear,
        IReadOnlyDictionary<(int GearTypeId, int Level), CombatGear>? combatGearByGearTypeAndLevel,
        IReadOnlyDictionary<int, DaoqiDictType>? daoqiDictTypesById = null,
        IReadOnlyDictionary<int, IReadOnlyList<CollectionRewardTier>>? collectionRewardTiersByCollectionId = null)
    {
        ArgumentNullException.ThrowIfNull(combatGear);

        var total = 0u;
        CombatGearSnapshotItem? equipped = null;
        foreach (var item in combatGear.Items)
        {
            if (item.Equip != 0 && IsCombatGearForClass(classCode, item.ItemID))
            {
                equipped = item;
                break;
            }
        }

        if (equipped is null)
        {
            return total;
        }

        total += GetCombatGearLife(equipped, combatGearByGearTypeAndLevel);
        total += GetBlackJadeMaxHitpoints(equipped, daoqiDictTypesById);
        total += GetCombatGearNutBookMaxHitpoints(
            classCode,
            combatGear,
            combatGearByGearTypeAndLevel,
            daoqiDictTypesById,
            collectionRewardTiersByCollectionId);

        foreach (var item in combatGear.Items)
        {
            if (item.ItemID == equipped.ItemID || !IsCombatGearForClass(classCode, item.ItemID))
            {
                continue;
            }

            total += GetCombatGearLife(item, combatGearByGearTypeAndLevel) * 5 / 100;
        }

        return total;
    }

    private static IReadOnlyList<string> CreateMissingInputs(LegacyMaxHitpointsExternalBonuses? externalBonuses)
    {
        if (externalBonuses is not null)
        {
            return [];
        }

        // TODO: Wire modern loaders for these legacy QueryEquipment contributors so UID 1293600 can be proven exactly.
        return
        [
            "Chi.MaxLife",
            "InnerPower.MaxLife",
            "Rune HP attributes and active rune flags",
            "JiangHu MaxLife",
            "HundredWeapons hitpoints",
            "Profession HP bonuses",
            "VIP/status-effect HP bonuses",
            "Beast HP bonuses",
            "Item secondary HP bonuses",
            "SubClass Wrangler HP bonuses",
            "CombatGear rank/nut-book HP bonuses"
        ];
    }

    private static int CalculateLegacyBeastLevel(
        uint totalPoints,
        IReadOnlyDictionary<int, BeastAttribute> beastAttributesByLevel)
    {
        var maxLevel = beastAttributesByLevel.Keys.Max();
        var level = 1;
        var points = totalPoints;

        foreach (var attribute in beastAttributesByLevel.Values.OrderBy(attribute => attribute.Level))
        {
            var requiredPoints = (uint)Math.Max(0, attribute.Parameters[0]);
            if (points < requiredPoints)
            {
                break;
            }

            points -= requiredPoints;
            level = Math.Min(level + 1, maxLevel);
        }

        return level;
    }

    private static IReadOnlyList<string> RemoveMissingInput(IReadOnlyList<string> inputs, string value)
    {
        return inputs.Where(input => !string.Equals(input, value, StringComparison.Ordinal)).ToArray();
    }

    private static IReadOnlyList<string> AddMissingInputs(
        IReadOnlyList<string> inputs,
        IReadOnlyList<string> additions)
    {
        if (additions.Count == 0)
        {
            return inputs;
        }

        var merged = inputs.ToList();
        foreach (var addition in additions)
        {
            if (!merged.Contains(addition, StringComparer.Ordinal))
            {
                merged.Add(addition);
            }
        }

        return merged;
    }

    private static bool HasChiState(CharacterSnapshot snapshot)
    {
        return snapshot.Attributes.ContainsKey("Dragon") ||
            snapshot.Attributes.ContainsKey("Phoenix") ||
            snapshot.Attributes.ContainsKey("Pheonix") ||
            snapshot.Attributes.ContainsKey("Tiger") ||
            snapshot.Attributes.ContainsKey("Turtle");
    }

    private static uint GetCombatGearLife(
        CombatGearSnapshotItem item,
        IReadOnlyDictionary<(int GearTypeId, int Level), CombatGear>? combatGearByGearTypeAndLevel)
    {
        if (combatGearByGearTypeAndLevel is null ||
            !combatGearByGearTypeAndLevel.TryGetValue(((int)item.ItemID, (int)item.Level), out var gear))
        {
            return 0;
        }

        return checked((uint)Math.Max(0, gear.Life));
    }

    private static uint GetBlackJadeMaxHitpoints(
        CombatGearSnapshotItem item,
        IReadOnlyDictionary<int, DaoqiDictType>? daoqiDictTypesById)
    {
        if (daoqiDictTypesById is null)
        {
            return 0;
        }

        var total = 0u;
        foreach (var blackJadeId in item.BlackJades)
        {
            if (blackJadeId == 0 ||
                !daoqiDictTypesById.TryGetValue((int)blackJadeId, out var blackJade))
            {
                continue;
            }

            total += ReadMaxHitpointsAttribute(
                blackJade,
                DaoqiFirstAttributeTypeFieldIndex,
                DaoqiFirstAttributeValueFieldIndex);
            total += ReadMaxHitpointsAttribute(
                blackJade,
                DaoqiSecondAttributeTypeFieldIndex,
                DaoqiSecondAttributeValueFieldIndex);
        }

        return total;
    }

    private static uint GetCombatGearNutBookMaxHitpoints(
        long classCode,
        CombatGearSnapshot combatGear,
        IReadOnlyDictionary<(int GearTypeId, int Level), CombatGear>? combatGearByGearTypeAndLevel,
        IReadOnlyDictionary<int, DaoqiDictType>? daoqiDictTypesById,
        IReadOnlyDictionary<int, IReadOnlyList<CollectionRewardTier>>? collectionRewardTiersByCollectionId)
    {
        if (classCode / 1000 != 7 ||
            daoqiDictTypesById is null ||
            collectionRewardTiersByCollectionId is null)
        {
            return 0;
        }

        var total = 0u;
        for (var collectionId = CombatGearNutBookOverallCollectionId; collectionId <= 703; collectionId++)
        {
            var score = CalculateCombatGearNutBookScore(
                collectionId,
                combatGear,
                combatGearByGearTypeAndLevel,
                daoqiDictTypesById);
            if (score == 0 ||
                !collectionRewardTiersByCollectionId.TryGetValue(collectionId, out var tiers))
            {
                continue;
            }

            foreach (var tier in tiers)
            {
                if (score >= tier.MinInclusive &&
                    score <= tier.MaxInclusive &&
                    tier.RewardType == DaoqiMaxHitpointsAttributeType &&
                    tier.RewardValue > 0)
                {
                    total = checked(total + (uint)tier.RewardValue);
                    break;
                }
            }
        }

        return total;
    }

    private static int CalculateCombatGearNutBookScore(
        int collectionId,
        CombatGearSnapshot combatGear,
        IReadOnlyDictionary<(int GearTypeId, int Level), CombatGear>? combatGearByGearTypeAndLevel,
        IReadOnlyDictionary<int, DaoqiDictType> daoqiDictTypesById)
    {
        var score = 0;
        if (collectionId != CombatGearNutBookOverallCollectionId)
        {
            var item = combatGear.Items.FirstOrDefault(candidate => candidate.ItemID == collectionId);
            if (item is not null)
            {
                score = checked(score + GetCombatGearScore(item, combatGearByGearTypeAndLevel) + (int)item.MasteryScore);
            }
        }

        foreach (var blackJadeId in combatGear.BlackJadeBag.BlackJades)
        {
            if (blackJadeId == 0 ||
                !daoqiDictTypesById.TryGetValue((int)blackJadeId, out var blackJade) ||
                !TryReadIntField(blackJade.Fields, DaoqiScoreFieldIndex, out var blackJadeScore) ||
                blackJadeScore <= 0)
            {
                continue;
            }

            if (collectionId == CombatGearNutBookOverallCollectionId)
            {
                if (TryReadIntField(blackJade.Fields, DaoqiTypeCombatGearFieldIndex, out var typeCombatGear) &&
                    typeCombatGear == 0 &&
                    TryReadIntField(blackJade.Fields, DaoqiTypeRuneFieldIndex, out var typeRune) &&
                    typeRune % 100 == 7)
                {
                    score = checked(score + blackJadeScore);
                }

                continue;
            }

            if (TryReadIntField(blackJade.Fields, DaoqiTypeCombatGearFieldIndex, out var itemTypeCombatGear) &&
                itemTypeCombatGear == collectionId)
            {
                score = checked(score + blackJadeScore);
            }
        }

        return score;
    }

    private static int GetCombatGearScore(
        CombatGearSnapshotItem item,
        IReadOnlyDictionary<(int GearTypeId, int Level), CombatGear>? combatGearByGearTypeAndLevel)
    {
        if (combatGearByGearTypeAndLevel is null ||
            !combatGearByGearTypeAndLevel.TryGetValue(((int)item.ItemID, (int)item.Level), out var gear))
        {
            return 0;
        }

        return Math.Max(0, gear.Score);
    }

    private static uint ReadMaxHitpointsAttribute(DaoqiDictType blackJade, int typeFieldIndex, int valueFieldIndex)
    {
        return TryReadIntField(blackJade.Fields, typeFieldIndex, out var attrType) &&
            attrType == DaoqiMaxHitpointsAttributeType &&
            TryReadIntField(blackJade.Fields, valueFieldIndex, out var attrValue) &&
            attrValue > 0
                ? checked((uint)attrValue)
                : 0;
    }

    private static uint CalculateCircleOfLifeEnchantBonus(uint enchant, uint runeLevel)
    {
        return runeLevel switch
        {
            1 or 2 => enchant / 2,
            3 => checked(enchant * 2 / 3),
            4 => enchant,
            5 => checked(enchant * 12 / 10),
            6 => checked(enchant * 14 / 10),
            7 => checked(enchant * 16 / 10),
            8 => checked(enchant * 18 / 10),
            9 => checked(enchant * 2),
            _ => 0,
        };
    }

    private static bool TryGetCircleOfLifeRuneLevel(
        CharacterSnapshot snapshot,
        PlayerItemSnapshot playerItems,
        IReadOnlyDictionary<int, IReadOnlyList<ItemType>>? itemsById,
        out uint runeLevel)
    {
        if (TryReadUIntAttribute(
            snapshot,
            out runeLevel,
            "CircleOfLifeRuneLevel",
            "CircleOfLifeLevel",
            "RuneCircleOfLifeLevel"))
        {
            return runeLevel > 0;
        }

        runeLevel = 0;
        if (itemsById is null)
        {
            return false;
        }

        foreach (var rune in playerItems.RuneItems)
        {
            if (!IsLegacyWornRunePosition(rune.Position) ||
                !TryGetItemType(itemsById, rune.ItemId, out var itemType) ||
                !itemType.Name.Contains("Circle of Life", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            runeLevel = rune.ItemId % 100;
            return runeLevel > 0;
        }

        return false;
    }

    private static bool TryGetItemAddBonus(
        IReadOnlyDictionary<(int ItemTypeId, int Level), ItemAddBonus>? itemAddBonusByItemTypeIdAndLevel,
        uint itemId,
        int plus,
        out ItemAddBonus itemAdd)
    {
        itemAdd = default!;
        if (itemAddBonusByItemTypeIdAndLevel is null)
        {
            return false;
        }

        var baseId = GetLegacyItemAddBaseId(itemId);
        if (itemAddBonusByItemTypeIdAndLevel.TryGetValue(((int)baseId, plus), out var exact) ||
            itemAddBonusByItemTypeIdAndLevel.TryGetValue(((int)baseId + 10, plus), out exact) ||
            itemAddBonusByItemTypeIdAndLevel.TryGetValue(((int)baseId + 20, plus), out exact))
        {
            itemAdd = exact;
            return true;
        }

        return false;
    }

    private static bool TryGetItemHitpoints(
        IReadOnlyDictionary<int, IReadOnlyList<ItemType>>? itemsById,
        uint itemId,
        out uint hitpoints)
    {
        hitpoints = 0;
        if (itemId > int.MaxValue ||
            itemsById is null ||
            !itemsById.TryGetValue((int)itemId, out var rows) ||
            rows.Count == 0)
        {
            return false;
        }

        return TryReadUIntField(rows[0].Fields, ItemTypeItemHitpointsFieldIndex, out hitpoints);
    }

    private static bool TryGetItemType(
        IReadOnlyDictionary<int, IReadOnlyList<ItemType>>? itemsById,
        uint itemId,
        out ItemType itemType)
    {
        itemType = default!;
        if (itemId > int.MaxValue ||
            itemsById is null ||
            !itemsById.TryGetValue((int)itemId, out var rows) ||
            rows.Count == 0)
        {
            return false;
        }

        itemType = rows[0];
        return true;
    }

    private static ulong ReadStoredMaxHitpoints(CharacterSnapshot snapshot)
    {
        foreach (var key in new[] { "MaxHitpoints", "MaxHitPoints", "MaxLife" })
        {
            if (snapshot.Attributes.TryGetValue(key, out var raw) &&
                ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return 0;
    }

    private static bool IsLegacyEnchantClearedPosition(int position)
    {
        return position is 0 or 7 or 10 or 11 or 12 or 13 or 15 or 16 or 17 or 18 or 19;
    }

    private static bool IsLegacyPlusClearedPosition(int position)
    {
        return position is 0 or 7 or 13 or 15 or 16 or 17;
    }

    private static bool IsLegacyWornRunePosition(ushort position)
    {
        return position is >= 101 and <= 131;
    }

    private static bool IsCombatGearForClass(long classCode, uint itemId)
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

    private static uint GetLegacyItemAddBaseId(uint itemId)
    {
        var itemTypeFamily = itemId / 1000;
        if (itemId == 300000)
        {
            return itemId;
        }

        var firstTwoDigits = itemId / 10000;
        if ((firstTwoDigits is >= 11 and <= 16) ||
            firstTwoDigits is 20 or 42 or 90 ||
            firstTwoDigits is >= 60 and <= 62 ||
            firstTwoDigits is 67 or 68)
        {
            if (itemTypeFamily != 420)
            {
                return itemId - (itemId % 10);
            }
        }

        var firstDigit = itemId / 100000;
        return (firstDigit * 100000) + (firstDigit * 10000) + (firstDigit * 1000) + (itemId % 1000 - itemId % 10);
    }

    private static uint TruncateLegacyUShort(double value)
    {
        return unchecked((uint)(ushort)value);
    }

    private static bool TryReadUIntField(IReadOnlyList<string> fields, int fieldIndex, out uint value)
    {
        value = 0;
        return fields.Count > fieldIndex &&
            uint.TryParse(fields[fieldIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadUIntAttribute(CharacterSnapshot snapshot, string key, out uint value)
    {
        value = 0;
        return snapshot.Attributes.TryGetValue(key, out var raw) &&
            uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadUIntAttribute(CharacterSnapshot snapshot, out uint value, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (TryReadUIntAttribute(snapshot, key, out value))
            {
                return true;
            }
        }

        value = 0;
        return false;
    }

    private static bool ReadBooleanAttribute(CharacterSnapshot snapshot, string key)
    {
        return snapshot.Attributes.TryGetValue(key, out var raw) &&
            bool.TryParse(raw, out var value) &&
            value;
    }

    private static bool TryReadIntField(IReadOnlyList<string> fields, int fieldIndex, out int value)
    {
        value = 0;
        return fields.Count > fieldIndex &&
            int.TryParse(fields[fieldIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

}
