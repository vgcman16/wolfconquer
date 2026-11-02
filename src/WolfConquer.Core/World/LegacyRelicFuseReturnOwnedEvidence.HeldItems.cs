using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyRelicFuseReturnOwnedEvidence
{
    // Closed DATA check shared by live evidence and actual v26 recovery.
    // Passing it never establishes saved-file, native or allocation ownership.
    internal static bool HeldItemsAreEligible(PlayerItemSnapshot items)
    {
        if (items?.RelicFuseItems is not { } held || held.Count != 3) return false;
        HashSet<ushort> positions = []; HashSet<uint> identities = [];
        PlayerItemSnapshotItem? first = null, second = null, result = null;
        foreach (var row in held)
        {
            if (row is null || row.Uid == 0 || row.ItemId is < 4_100_001 or > 4_100_005 || row.WarehouseId != 0 ||
                row.Position is < 213 or > 215 || !positions.Add(row.Position) || !identities.Add(row.Uid) ||
                row.StackSize > 1 || row.Locked != 0 || row.Suspicious != 0 || row.Bound > 1 ||
                row.DepositedItems is null || row.DepositedItems.Count != 0) return false;
            if (row.Position == 213) first = row;
            else if (row.Position == 214) second = row;
            else result = row;
        }
        // Derived from the actual compose projection. A saved unsafe legacy
        // result must not convert a bound input into a tradable choice.
        return first is not null && second is not null && result is not null &&
            result.ItemId == first.ItemId && result.Bound >= Math.Max(first.Bound, second.Bound);
    }
}
