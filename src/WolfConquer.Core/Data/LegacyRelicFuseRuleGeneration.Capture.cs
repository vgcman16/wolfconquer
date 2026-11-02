using System.Globalization;

namespace WolfConquer.Core.Data;

internal sealed partial class LegacyRelicFuseRuleGeneration
{
    private static bool RealItemGroups(WorldDataCatalog owner)
    {
        for (var itemId = 4_100_001; itemId <= 4_100_005; itemId++)
        {
            if (!owner.ItemsById.TryGetValue(itemId, out var grouped) || grouped.Count == 0) return false;
            var raw = owner.Items.Where(row => row is not null && row.Id == itemId).ToArray();
            if (raw.Length != grouped.Count) return false;
            for (var index = 0; index < raw.Length; index++)
            {
                var row = grouped[index];
                if (!ReferenceEquals(row, raw[index]) || row.Id != itemId || row.Fields is null || row.Fields.Count != 66 ||
                    !int.TryParse(row.Fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out var encoded) || encoded != itemId)
                    return false;
            }
        }
        return true;
    }

    private static bool TryCopyCaps(IReadOnlyList<XuanbaoComposeAttributeLimit> rows,
        IReadOnlyDictionary<int, XuanbaoComposeAttributeLimit> indexed, out int[] caps)
    {
        caps = new int[15];
        if (rows is null || indexed is null) return false;
        var raw = rows.ToArray();
        HashSet<int> seen = [];
        foreach (var row in raw)
        {
            if (row is null || row.Id is < 1 or > 14 || row.Values is null || !seen.Add(row.Id)) return false;
            var values = row.Values.ToArray();
            if (values.Length != 3 || values.Any(value => value < 0) ||
                row.Id != 14 && (values[^1] is < 30 or > ushort.MaxValue) ||
                !indexed.TryGetValue(row.Id, out var resolved) || resolved is null || resolved.Id != row.Id ||
                resolved.Values is null || !values.AsSpan().SequenceEqual(resolved.Values.ToArray())) return false;
            if (row.Id != 14) caps[row.Id] = values[^1];
        }
        return indexed.Count == seen.Count;
    }
}
