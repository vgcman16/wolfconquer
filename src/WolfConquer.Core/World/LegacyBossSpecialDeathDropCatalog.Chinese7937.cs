using WolfConquer.Core.Client;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial record LegacyBossSpecialDeathDropCatalog
{
    /// <summary>Explicit selected7937 rule source; absent for existing/default catalog construction.</summary>
    public Chinese7937XuanbaoAttributeCatalog? Selected7937Attributes { get; private init; }

    /// <summary>Binds parsed7937 gold-limit data to the actual server relic item and random-attribute catalogs.</summary>
    public static LegacyBossSpecialDeathDropCatalog FromWorldData(WorldDataCatalog worldData,
        Chinese7937XuanbaoAttributeCatalog selected7937Attributes)
    {
        ArgumentNullException.ThrowIfNull(worldData);
        ArgumentNullException.ThrowIfNull(selected7937Attributes);
        if (selected7937Attributes.ClientVersion != 7937)
            throw new ArgumentException("Explicit7937 relic attribute catalog required.", nameof(selected7937Attributes));
        foreach (var itemType in LegacyBossSpecialDeathDropPolicy.RequiredRelicItemTypes)
        {
            var key = checked((int)itemType);
            if (!selected7937Attributes.HasGoldLimitItem(itemType) ||
                !worldData.ItemsById.TryGetValue(key, out var items) || items.Count == 0 ||
                items.Any(item => item.Id != key) ||
                !worldData.EffectiveXuanbaoRandomAttributesByItemTypeId.TryGetValue(key, out var attributes) || attributes.Count == 0 ||
                attributes.Any(attribute => attribute.ItemTypeId != key))
                throw new InvalidDataException("Selected7937 relic metadata requires all actual server item and attribute owners.");
        }
        return FromItemsById(worldData.ItemsById, worldData.EffectiveXuanbaoRandomAttributesByItemTypeId)
            with { Selected7937Attributes = selected7937Attributes };
    }
}

public static partial class LegacyBossSpecialDeathDropPolicy
{
    internal static ReadOnlySpan<uint> RequiredRelicItemTypes => DeityLandRelicItemPool;

    private static bool IsExcludedDeityLandRelicAttribute(LegacyBossSpecialDeathDropCatalog catalog,
        uint itemType, int attributeType)
    {
        if (catalog.Selected7937Attributes is not { } selected)
            return IsSourceExcludedDeityLandRelicAttribute(itemType, attributeType);
        // The selected table's20 true pairs independently match the existing
        // source-backed exclusion rule. Other score-limit metadata stays raw.
        return attributeType >= 0 && selected.TryGetGoldLimitFlag(itemType, (uint)attributeType, out var excluded) && excluded;
    }
}
