using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed class LegacyDragonSkinInventory
{
    private sealed record Collection(IReadOnlyList<PlayerItemSnapshotItem> Source, Row[] Rows);
    private sealed record Row(PlayerItemSnapshotItem Source, uint Level, uint Progress, uint Owner,
        string Name, string Signature, Collection Deposits);
    private readonly PlayerItemSnapshot source;
    private readonly Collection[] stores;
    internal byte[] Bytes { get; }

    private LegacyDragonSkinInventory(PlayerItemSnapshot source, Collection[] stores, byte[] bytes)
    { this.source = source; this.stores = stores; Bytes = bytes; }

    internal static bool TryCapture(PlayerItemSnapshot items, out LegacyDragonSkinInventory capture, out string reason)
    {
        capture = null!;
        reason = "dragonSkinInventoryInvalid";
        if (items is null) return false;
        var seen = new HashSet<uint>();
        long size = 8;
        var groups = Stores(items);
        var captured = new Collection[groups.Length];
        for (var index = 0; index < groups.Length; index++)
            if (!TryCollection(groups[index], depth: 0, seen, ref size, out captured[index]) ||
                captured[index].Rows.Any(row => !ValidPlacement(index, row.Source))) return false;
        var bytes = PlayerItemSnapshotLoader.ToLegacyBytes(items);
        if (bytes.Length != size || bytes.Length > CharacterSnapshotFileBaseline.MaximumBytes) return false;
        capture = new(items, captured, bytes);
        if (!capture.IsCurrent(items)) { capture = null!; return false; }
        reason = string.Empty;
        return true;
    }

    internal bool IsCurrent(PlayerItemSnapshot items)
    {
        if (!ReferenceEquals(source, items)) return false;
        var groups = Stores(items);
        for (var index = 0; index < groups.Length; index++)
            if (!Matches(stores[index], groups[index])) return false;
        return true;
    }

    internal static IReadOnlyList<PlayerItemSnapshotItem>[] Stores(PlayerItemSnapshot items) =>
    [items.InventoryItems, items.RelicFuseItems, items.MythsoulBagItems, items.EquipmentItems,
        items.RuneItems, items.WardrobeItems, items.WarehouseItems];

    private static bool Ordinary(IReadOnlyList<PlayerItemSnapshotItem>? rows) => rows is not null &&
        (rows.GetType() == typeof(PlayerItemSnapshotItem[]) || rows.GetType() == typeof(List<PlayerItemSnapshotItem>));

    private static bool TryCollection(IReadOnlyList<PlayerItemSnapshotItem> rows, int depth,
        HashSet<uint> seen, ref long size, out Collection collection)
    {
        collection = null!;
        if (!Ordinary(rows) || rows.Count > CharacterSnapshotFileBaseline.MaximumBytes / PlayerItemSnapshotLoader.LegacyDbItemSize)
            return false;
        var count = rows.Count;
        var captured = new Row[count];
        for (var index = 0; index < count; index++)
        {
            var row = rows[index];
            if (row is null || row.Uid == 0 || row.ItemId == 0 || !seen.Add(row.Uid) || !Ordinary(row.DepositedItems) ||
                depth != 0 && row.DepositedItems.Count != 0 || !ValidText(row.OwnerName) || !ValidText(row.Signature)) return false;
            size += PlayerItemSnapshotLoader.LegacyDbItemSize;
            if (row.PerfectionLevel != 0 || row.PerfectionProgress != 0 || row.OwnerUid != 0 ||
                row.OwnerName.Length != 0 || row.Signature.Length != 0)
                size += PlayerItemSnapshotLoader.LegacyPerfectionSize;
            if (size > CharacterSnapshotFileBaseline.MaximumBytes) return false;
            Collection deposits;
            if (depth != 0) deposits = new(row.DepositedItems, []);
            else if (!TryCollection(row.DepositedItems, depth + 1, seen, ref size, out deposits)) return false;
            captured[index] = new(row, row.PerfectionLevel, row.PerfectionProgress, row.OwnerUid,
                row.OwnerName, row.Signature, deposits);
        }
        if (rows.Count != count) return false;
        collection = new(rows, captured);
        return true;
    }

    private static bool ValidText(string text) => text is not null && !text.Contains('\0') && Encoding.UTF8.GetByteCount(text) <= 32 &&
        string.Equals(Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(text)), text, StringComparison.Ordinal);

    private static bool ValidPlacement(int group, PlayerItemSnapshotItem row) => group switch
    {
        0 => row.WarehouseId == 0 && row.Position == 0,
        1 => row.WarehouseId == 0 && row.Position is >= 213 and <= 215,
        2 => row.WarehouseId == 0 && row.Position == 217,
        3 => row.WarehouseId == 0 && row.Position is > 0 and <= 63 ||
            row.WarehouseId == 100 && row.Position is > 0 and <= 59,
        4 => row.WarehouseId == 0 && (row.Position is 211 or 212 or >= 101 and <= 135),
        5 => row.WarehouseId == 100 && row.Position == 0,
        6 => row.WarehouseId != 0 && row.WarehouseId != 100,
        _ => false,
    };

    private static bool Matches(Collection expected, IReadOnlyList<PlayerItemSnapshotItem> current)
    {
        if (!ReferenceEquals(expected.Source, current) || !Ordinary(current) || expected.Rows.Length != current.Count) return false;
        try
        {
            for (var index = 0; index < expected.Rows.Length; index++)
            {
                var row = expected.Rows[index];
                var actual = current[index];
                if (!ReferenceEquals(row.Source, actual) || actual.PerfectionLevel != row.Level ||
                    actual.PerfectionProgress != row.Progress || actual.OwnerUid != row.Owner ||
                    !string.Equals(actual.OwnerName, row.Name, StringComparison.Ordinal) ||
                    !string.Equals(actual.Signature, row.Signature, StringComparison.Ordinal) ||
                    !Matches(row.Deposits, actual.DepositedItems)) return false;
            }
        }
        catch (ArgumentOutOfRangeException) { return false; }
        return expected.Rows.Length == current.Count;
    }
}
