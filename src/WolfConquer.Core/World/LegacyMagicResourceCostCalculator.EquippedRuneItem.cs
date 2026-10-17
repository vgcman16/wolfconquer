using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class LegacyMagicResourceCostCalculator
{
    internal static bool TryGetEquippedRuneItem(
        PlayerItemSnapshot playerItems,
        IReadOnlyDictionary<int, IReadOnlyList<ItemType>>? itemsById,
        string runeName,
        bool alternateEquipmentActive,
        IReadOnlyDictionary<string, string>? characterAttributes,
        long? characterClass,
        out PlayerItemSnapshotItem? equipped)
    {
        equipped = null;
        if (itemsById is null) return false;
        foreach (var rune in playerItems.RuneItems)
        {
            if (!IsLegacyWornRunePosition(rune.Position, playerItems.RuneItems, alternateEquipmentActive) ||
                !IsLegacyRuneItem(rune.ItemId) ||
                !TryGetItemType(itemsById, rune.ItemId, out var itemType) ||
                !PassesLegacyRuneJobRequirement(itemType, characterAttributes, characterClass) ||
                !ContainsRuneName(itemType, runeName))
                continue;
            equipped = rune;
            return true;
        }

        return false;
    }
}
