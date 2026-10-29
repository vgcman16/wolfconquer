using System.Text.Json;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Runtime;

public static partial class LocalRuntimeInspectionProtocol
{
    private static void RequireNativeRowsAndComparison(LegacyRuntimeInspectionSnapshot snapshot)
    {
        if (snapshot.NativeItems is not { } native)
        {
            if (snapshot.SelectedValuesEqual is not null || snapshot.NativeItemsUnavailableReason is not
                ("nativeInventoryAndEquipmentNotInitialized" or "nativeInventoryNotInitialized" or "nativeEquipmentNotInitialized"))
                throw new JsonException("nativeRowsAvailabilityInvalid");
            return;
        }
        if (snapshot.NativeItemsUnavailableReason is not null || snapshot.SelectedValuesEqual is null)
            throw new JsonException("nativeRowsAvailabilityInvalid");
        RequireCanonicalRows(native);
        var equal = snapshot.ManagedCharacter.Hitpoints == snapshot.NativeVitals.Hitpoints &&
            snapshot.ManagedCharacter.MinMana == snapshot.NativeVitals.Mana && snapshot.ManagedItems.SequenceEqual(native);
        if (equal != snapshot.SelectedValuesEqual) throw new JsonException("selectedComparisonInvalid");
    }

    private static void RequireCanonicalRows(LegacyOwnedPlayerItem[] rows)
    {
        if (rows is null || rows.Length > LegacyRuntimeInspectionSnapshot.MaximumRows)
            throw new JsonException("snapshotRowsInvalid");
        uint previousUid = 0;
        var slots = new HashSet<ushort>();
        foreach (var row in rows)
        {
            // The producer emits each selected projection in ascending UID order.
            // This checks detached shape only, never ownership of a live alias.
            if (row is null || row.Uid <= previousUid || row.ItemId == 0)
                throw new JsonException("snapshotRowsInvalid");
            previousUid = row.Uid;
            var valid = row.Container switch
            {
                "inventory" => row.Position == 0 && row.WarehouseId == 0,
                "equipment" => slots.Add(row.Position) &&
                    (row.WarehouseId == 0 && row.Position is > 0 and <= 63 ||
                     row.WarehouseId == 100 && row.Position is > 0 and <= 59 &&
                     LegacyNativeItemPosition.ItemPosition(row.ItemId) is 9 or 17),
                _ => false,
            };
            if (!valid) throw new JsonException("snapshotRowsInvalid");
        }
    }
}
