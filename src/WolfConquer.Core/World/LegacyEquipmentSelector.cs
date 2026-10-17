using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static class LegacyEquipmentSelector
{
    public static IReadOnlyList<PlayerItemSnapshotItem> SelectCurrentEquipment(
        PlayerItemSnapshot playerItems,
        bool alternateEquipmentActive = false)
    {
        ArgumentNullException.ThrowIfNull(playerItems);

        var occupiedPositions = playerItems.EquipmentItems
            .Select(item => item.Position)
            .ToHashSet();
        var selectedPositions = new HashSet<ushort>();
        var selected = new List<PlayerItemSnapshotItem>();

        foreach (var item in playerItems.EquipmentItems)
        {
            if (!IsCurrentEquipment(item, occupiedPositions, alternateEquipmentActive) ||
                !selectedPositions.Add(item.Position))
            {
                continue;
            }

            selected.Add(item);
        }

        return selected;
    }

    public static bool IsPositionFree(PlayerItemSnapshot playerItems, ushort position)
    {
        ArgumentNullException.ThrowIfNull(playerItems);

        return !playerItems.EquipmentItems.Any(item => item.Position == position);
    }

    private static bool IsCurrentEquipment(
        PlayerItemSnapshotItem item,
        IReadOnlySet<ushort> occupiedPositions,
        bool alternateEquipmentActive)
    {
        if (!alternateEquipmentActive)
        {
            return item.Position <= 50;
        }

        return item.Position >= 50 || !occupiedPositions.Contains((ushort)(item.Position + 50));
    }
}
