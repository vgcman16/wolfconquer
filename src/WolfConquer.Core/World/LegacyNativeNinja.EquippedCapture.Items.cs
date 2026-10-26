using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    private const int MaximumEquippedItemRows = 4096;

    private readonly record struct EquippedItemRow(Item Row, uint ItemId, uint Level, uint Position, uint Exp, uint UK)
    {
        internal bool IsCurrent(Item row) => ReferenceEquals(Row, row) && row is not null &&
            row.ItemID == ItemId && row.Level == Level && row.Position == Position && row.Exp == Exp && row.UK == UK;
    }

    private static EquippedItemRow[] CaptureEquippedItems(ConcurrentDictionary<uint, Item> items)
    {
        if (items.Count > MaximumEquippedItemRows) throw UnavailableEquippedSigil();
        // Retain the source's actual Values ordering, including first match within a slot.
        var values = items.Values;
        if (values.Count > MaximumEquippedItemRows) throw UnavailableEquippedSigil();
        var rows = new List<EquippedItemRow>(values.Count);
        foreach (var item in values)
        {
            if (item is null || rows.Count >= MaximumEquippedItemRows) throw UnavailableEquippedSigil();
            rows.Add(new(item, item.ItemID, item.Level, item.Position, item.Exp, item.UK));
        }
        return rows.ToArray();
    }

    private static bool EquippedItemsCurrent(ConcurrentDictionary<uint, Item> items, EquippedItemRow[] rows)
    {
        if (items.Count != rows.Length || rows.Length > MaximumEquippedItemRows) return false;
        var values = items.Values;
        if (values.Count != rows.Length) return false;
        var index = 0;
        foreach (var row in values)
        {
            if (index >= rows.Length || !rows[index++].IsCurrent(row)) return false;
        }
        return index == rows.Length;
    }

    private static EquippedItemRow? SelectEquippedItem(EquippedItemRow[] rows, uint itemId, bool alternate)
    {
        EquippedItemRow? selected = null;
        for (uint slot = 1; slot <= 8; slot++)
        {
            EquippedItemRow? current = null;
            if (alternate)
                current = FirstEquippedMatch(rows, itemId, slot + 8);
            current ??= FirstEquippedMatch(rows, itemId, slot);
            if (current.HasValue) selected = current;
        }
        return selected;
    }

    private static EquippedItemRow? FirstEquippedMatch(EquippedItemRow[] rows, uint itemId, uint position)
    {
        foreach (var row in rows)
            if (row.Position == position && row.ItemId == itemId) return row;
        return null;
    }
}
