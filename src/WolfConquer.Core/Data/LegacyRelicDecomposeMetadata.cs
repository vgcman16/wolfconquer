using System.Collections.Frozen;
using System.Globalization;

namespace WolfConquer.Core.Data;

// Constructor-owned scalar metadata, independent of optional compose/Echo data.
internal sealed class LegacyRelicDecomposeMetadata
{
    private readonly WorldDataCatalog owner;
    private readonly FrozenSet<uint> relics;
    private LegacyRelicDecomposeMetadata(WorldDataCatalog owner, HashSet<uint> relics, ushort durability)
    { this.owner = owner; this.relics = relics.ToFrozenSet(); RewardDurability = durability; }

    internal ushort RewardDurability { get; }
    internal bool ContainsRelic(uint itemId) => relics.Contains(itemId);
    internal bool IsCurrent(WorldDataCatalog actual) => ReferenceEquals(owner, actual) &&
        ReferenceEquals(actual.RelicDecomposeMetadata, this);

    internal static LegacyRelicDecomposeMetadata? Capture(WorldDataCatalog owner)
    {
        if (!TryRows(owner, 3_306_885, out var reward)) return null;
        ushort? durability = null;
        foreach (var row in reward)
        {
            if (!row.TryGetLegacyDurability(out var value) || durability is { } prior && prior != value) return null;
            durability = value;
        }
        if (durability is null) return null;
        HashSet<uint> allowed = [];
        for (var id = 4_100_001; id <= 4_100_005; id++)
            if (TryRows(owner, id, out _)) allowed.Add(checked((uint)id));
        return new(owner, allowed, durability.Value);
    }

    private static bool TryRows(WorldDataCatalog owner, int id, out ItemType[] rows)
    {
        rows = [];
        if (!owner.ItemsById.TryGetValue(id, out var indexed) || indexed is null || indexed.Count == 0) return false;
        var raw = owner.Items.Where(row => row is not null && row.Id == id).ToArray();
        if (raw.Length != indexed.Count) return false;
        for (var index = 0; index < raw.Length; index++)
        {
            var row = indexed[index];
            if (row is null || !ReferenceEquals(raw[index], row) || row.Id != id || row.Fields is null || row.Fields.Count != 66 ||
                !int.TryParse(row.Fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out var fieldId) || fieldId != id)
                return false;
        }
        rows = raw; return true;
    }
}
