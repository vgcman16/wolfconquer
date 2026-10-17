using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed record LegacyRuneHitpointsBreakdown(
    uint StorageScore,
    uint StorageAttributeHitpoints,
    uint StatusFlagHitpoints,
    uint Total,
    IReadOnlyList<string> MissingInputs);

public static class LegacyRuneHitpointsCalculator
{
    private const int RuneHitpointsAttributeType = 1;

    public static LegacyRuneHitpointsBreakdown Calculate(
        PlayerItemSnapshot? playerItems,
        WorldDataCatalog? catalog)
    {
        return Calculate(playerItems?.RuneItems, catalog?.RuneStorageAttributes);
    }

    public static LegacyRuneHitpointsBreakdown Calculate(
        IReadOnlyList<PlayerItemSnapshotItem>? runeItems,
        IReadOnlyList<RuneStorageAttribute>? storageAttributes)
    {
        var missingInputs = new List<string>();
        if (runeItems is null)
        {
            missingInputs.Add("PlayerItemSnapshot.RuneItems");
        }

        if (storageAttributes is null)
        {
            missingInputs.Add("WorldDataCatalog.RuneStorageAttributes");
        }

        missingInputs.Add("Rune runtime status flag HP inputs (BloodTide, Rampage, Sacrifice, FineRain1, RiseofTaoism)");

        if (runeItems is null || storageAttributes is null)
        {
            return new LegacyRuneHitpointsBreakdown(0, 0, 0, 0, missingInputs);
        }

        var score = CalculateStorageScore(runeItems);
        var storageHitpoints = CalculateStorageAttributeHitpoints(score, storageAttributes);

        return new LegacyRuneHitpointsBreakdown(
            score,
            storageHitpoints,
            StatusFlagHitpoints: 0,
            Total: storageHitpoints,
            missingInputs);
    }

    public static uint CalculateStorageScore(IReadOnlyList<PlayerItemSnapshotItem> runeItems)
    {
        ArgumentNullException.ThrowIfNull(runeItems);

        var score = 0u;
        foreach (var item in runeItems)
        {
            if (item.Position is 0 or 211)
            {
                continue;
            }

            var itemScore = 500u;
            if (item.ItemId / 10000 != 401)
            {
                itemScore = checked(itemScore + (300u * (item.ItemId % 100)));
            }

            score = checked(score + itemScore);
        }

        return score;
    }

    public static uint CalculateStorageAttributeHitpoints(
        uint storageScore,
        IReadOnlyList<RuneStorageAttribute> storageAttributes)
    {
        ArgumentNullException.ThrowIfNull(storageAttributes);

        var hitpoints = 0u;
        foreach (var attribute in storageAttributes)
        {
            if (attribute.RequiredStorageScore < 0 || attribute.RequiredStorageScore > storageScore)
            {
                continue;
            }

            foreach (var bonus in attribute.Bonuses)
            {
                if (bonus.AttributeType == RuneHitpointsAttributeType && bonus.Value > 0)
                {
                    hitpoints = checked(hitpoints + (uint)bonus.Value);
                }
            }
        }

        return hitpoints;
    }
}
