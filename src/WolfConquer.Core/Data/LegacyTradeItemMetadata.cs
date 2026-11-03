using System.Collections.Frozen;
using System.Globalization;

namespace WolfConquer.Core.Data;

// Constructor-owned server66 stack bounds. This DATA owner grants no item custody.
internal sealed class LegacyTradeItemMetadata
{
    private readonly WorldDataCatalog owner;
    private readonly FrozenDictionary<uint, ushort> caps;

    private LegacyTradeItemMetadata(WorldDataCatalog owner, Dictionary<uint, ushort> caps, bool emptySource)
    { this.owner = owner; this.caps = caps.ToFrozenDictionary(); HasEmptySource = emptySource; }

    internal bool HasEmptySource { get; }
    internal bool IsCurrent(WorldDataCatalog actual) => ReferenceEquals(owner, actual) &&
        ReferenceEquals(actual.TradeItemMetadata, this);

    internal bool Allows(PlayerItemSnapshotItem row) => caps.TryGetValue(row.ItemId, out var cap) &&
        (row.StackSize <= 1 || row.StackSize <= cap);

    internal static LegacyTradeItemMetadata Capture(WorldDataCatalog owner)
    {
        Dictionary<uint, ushort> values = [];
        foreach (var group in owner.Items.Where(row => row is not null && row.Id > 0).GroupBy(row => row.Id))
            if (TryCap(owner, group.Key, group.ToArray(), out var cap)) values.Add(checked((uint)group.Key), cap);
        return new(owner, values, owner.Items.Count == 0);
    }

    private static bool TryCap(WorldDataCatalog owner, int id, ItemType[] raw, out ushort cap)
    {
        cap = 0;
        if (!owner.ItemsById.TryGetValue(id, out var indexed) || indexed is null || indexed.Count != raw.Length)
            return false;
        ushort? selected = null;
        for (var index = 0; index < raw.Length; index++)
        {
            var row = raw[index];
            if (!ReferenceEquals(indexed[index], row) || row.Fields is null || row.Fields.Count != 66 ||
                !int.TryParse(row.Fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out var fieldId) || fieldId != id ||
                !TryStackCap(row, out var value) || selected is { } prior && prior != value) return false;
            selected = value;
        }
        if (selected is not { } actual) return false;
        cap = actual;
        return true;
    }

    private static bool TryStackCap(ItemType row, out ushort cap)
    {
        cap = 1;
        // The selected ItemType formula genuinely ignores column46 for accessories.
        return row.IsLegacyAccessory || ushort.TryParse(row.Fields[ItemType.StackSizeFieldIndex],
            NumberStyles.Integer, CultureInfo.InvariantCulture, out cap);
    }
}
