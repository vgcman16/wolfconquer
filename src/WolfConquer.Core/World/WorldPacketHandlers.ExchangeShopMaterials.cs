using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryPrepareLegacyExchangeMaterials(
        PlayerItemSnapshot original, IReadOnlyList<LegacyExchangeShopMaterial> definitions, uint buyCount,
        out PlayerItemSnapshot updated, out List<byte[]> frames, out int requirementCount, out string reason)
    {
        updated = original; frames = []; requirementCount = 0;
        reason = "materialRequirementOverflow";
        var ordered = new List<LegacyExchangeShopMaterial>(definitions.Count);
        var totals = new Dictionary<uint, uint>();
        try
        {
            foreach (var definition in definitions)
            {
                if (definition.ItemTypeId == 0 || definition.Count == 0) continue;
                uint required = checked(definition.Count * buyCount);
                ordered.Add(new(definition.ItemTypeId, required));
                totals.TryGetValue(definition.ItemTypeId, out uint previous);
                totals[definition.ItemTypeId] = checked(previous + required);
            }
        }
        catch (OverflowException) { return false; }

        // Capture the real inventory order once. Repeated definitions must share
        // this same stock, not independently authorize reuse of its full amount.
        var stock = original.InventoryItems.ToArray();
        if (stock.Any(row => row is null)) { reason = "materialInventoryUnavailable"; return false; }
        foreach (var (itemId, required) in totals)
        {
            ulong available = 0;
            foreach (var row in stock)
                if (row.ItemId == itemId)
                    available = checked(available + (row.StackSize == 0 ? 1UL : row.StackSize));
            if (available < required)
            { reason = "materialAggregateShortage"; return false; }
        }

        var inventory = stock.ToList();
        var preparedFrames = new List<byte[]>();
        foreach (var requirement in ordered)
            if (!TryConsumeLegacyExchangeMaterial(inventory, requirement.ItemTypeId, requirement.Count, preparedFrames))
            { reason = "materialDebitIncomplete"; return false; }

        updated = original with { InventoryItems = inventory.ToArray() };
        frames = preparedFrames;
        requirementCount = ordered.Count;
        reason = string.Empty;
        return true;
    }
}
