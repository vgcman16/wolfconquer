using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed record LegacyItemSecondaryHitpointsContribution(
    uint RelicAttributeHitpoints,
    uint RefineryHitpoints,
    uint MythsoulBagHitpoints,
    uint Total,
    IReadOnlyList<string> MissingInputs);

public static class LegacyItemSecondaryHitpointsCalculator
{
    private const uint RelicHpAddAttributeType = (byte)LegacyChiAttributeType.HPAdd;
    private const int EquippedRelicPosition = 13;
    private const int DaoqiMaxHitpointsAttributeType = 1;
    private const int SpecialEffectValueIndex = 0;
    private const int RefineryIntensificationType = 10;
    private const int RefinerySecondaryTypeParameterIndex = 2;
    private const int RefinerySecondaryValueParameterIndex = 3;

    public static LegacyItemSecondaryHitpointsContribution Calculate(
        PlayerItemSnapshot playerItems,
        WorldDataCatalog? catalog = null,
        DateTime? now = null,
        bool alternateEquipmentActive = false)
    {
        ArgumentNullException.ThrowIfNull(playerItems);

        return Calculate(
            playerItems,
            catalog?.SpecialEffectAttributesById,
            catalog?.RefineryMaterialsByItemType,
            now,
            alternateEquipmentActive);
    }

    public static LegacyItemSecondaryHitpointsContribution Calculate(
        PlayerItemSnapshot playerItems,
        IReadOnlyDictionary<int, SpecialEffectAttribute>? specialEffectAttributesById,
        IReadOnlyDictionary<int, RefineryMaterial>? refineryMaterialsByItemType,
        DateTime? now = null,
        bool alternateEquipmentActive = false)
    {
        ArgumentNullException.ThrowIfNull(playerItems);

        var missingInputs = new List<string>();
        var currentEquipment = LegacyEquipmentSelector.SelectCurrentEquipment(playerItems, alternateEquipmentActive);
        var relicHitpoints = CalculateRelicAttributeHitpoints(currentEquipment);
        var refineryHitpoints = CalculateRefineryHitpoints(
            currentEquipment,
            refineryMaterialsByItemType,
            missingInputs,
            now);
        var mythsoulBagHitpoints = CalculateMythsoulBagHitpoints(
            playerItems,
            specialEffectAttributesById,
            missingInputs);

        var total = checked(relicHitpoints + refineryHitpoints + mythsoulBagHitpoints);
        return new LegacyItemSecondaryHitpointsContribution(
            relicHitpoints,
            refineryHitpoints,
            mythsoulBagHitpoints,
            total,
            missingInputs);
    }

    private static uint CalculateRelicAttributeHitpoints(IReadOnlyList<PlayerItemSnapshotItem> equipmentItems)
    {
        var total = 0u;
        foreach (var item in equipmentItems)
        {
            if (item.Durability == 0 || item.Position % 50 != EquippedRelicPosition)
            {
                continue;
            }

            foreach (var encodedAttribute in GetRelicAttributes(item))
            {
                var attributeType = encodedAttribute % 100;
                var attributeValue = encodedAttribute / 1000;
                if (attributeType == RelicHpAddAttributeType)
                {
                    total = checked(total + attributeValue);
                }
            }
        }

        return total;
    }

    private static uint CalculateRefineryHitpoints(
        IReadOnlyList<PlayerItemSnapshotItem> equipmentItems,
        IReadOnlyDictionary<int, RefineryMaterial>? refineryMaterialsByItemType,
        ICollection<string> missingInputs,
        DateTime? now)
    {
        var total = 0u;
        foreach (var item in equipmentItems)
        {
            if (item.Durability == 0 || !IsRefineryInLife(item, now))
            {
                continue;
            }

            if (refineryMaterialsByItemType is null)
            {
                AddMissing(missingInputs, "WorldDataCatalog.RefineryMaterials");
                continue;
            }

            if (item.RefineryEffectId > int.MaxValue ||
                !refineryMaterialsByItemType.TryGetValue((int)item.RefineryEffectId, out var refineryMaterial))
            {
                AddMissing(missingInputs, $"Rifinery.txt row for item {item.RefineryEffectId}");
                continue;
            }

            total = checked(total + CalculateRefineryMaterialHitpoints(refineryMaterial));
        }

        return total;
    }

    private static uint CalculateMythsoulBagHitpoints(
        PlayerItemSnapshot playerItems,
        IReadOnlyDictionary<int, SpecialEffectAttribute>? specialEffectAttributesById,
        ICollection<string> missingInputs)
    {
        var total = 0u;
        foreach (var item in playerItems.MythsoulBagItems)
        {
            if (specialEffectAttributesById is null)
            {
                AddMissing(missingInputs, "WorldDataCatalog.SpecialEffectAttributes");
                continue;
            }

            if (item.ItemId > int.MaxValue ||
                !specialEffectAttributesById.TryGetValue((int)item.ItemId, out var attribute))
            {
                AddMissing(missingInputs, $"speffect_attribute.txt row for item {item.ItemId}");
                continue;
            }

            if (attribute.Type == DaoqiMaxHitpointsAttributeType &&
                attribute.Values.Count > SpecialEffectValueIndex &&
                attribute.Values[SpecialEffectValueIndex] > 0)
            {
                total = checked(total + (uint)attribute.Values[SpecialEffectValueIndex]);
            }
        }

        return total;
    }

    private static uint CalculateRefineryMaterialHitpoints(RefineryMaterial refineryMaterial)
    {
        var total = 0u;
        if (refineryMaterial.EffectType == RefineryIntensificationType &&
            refineryMaterial.PrimaryValue > 0)
        {
            total = checked(total + (uint)refineryMaterial.PrimaryValue);
        }

        if (refineryMaterial.Parameters.Count > RefinerySecondaryValueParameterIndex &&
            refineryMaterial.Parameters[RefinerySecondaryTypeParameterIndex] == RefineryIntensificationType &&
            refineryMaterial.Parameters[RefinerySecondaryValueParameterIndex] > 0)
        {
            total = checked(total + (uint)refineryMaterial.Parameters[RefinerySecondaryValueParameterIndex]);
        }

        return total;
    }

    private static bool IsRefineryInLife(PlayerItemSnapshotItem item, DateTime? now)
    {
        if (item.RefineryEffectDuration == 0)
        {
            return item.RefineryEffectId > 1000;
        }

        try
        {
            var addedOn = DateTime.FromBinary(item.RefineryEffectAddedOn);
            return addedOn.AddSeconds(item.RefineryEffectDuration) > (now ?? DateTime.Now);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static IEnumerable<uint> GetRelicAttributes(PlayerItemSnapshotItem item)
    {
        yield return item.Attribute1;
        yield return item.Attribute2;
        yield return item.Attribute3;
        yield return item.Attribute4;
        yield return item.Attribute5;
    }

    private static void AddMissing(ICollection<string> missingInputs, string value)
    {
        if (!missingInputs.Contains(value))
        {
            missingInputs.Add(value);
        }
    }
}
