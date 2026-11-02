using System.Collections.Frozen;
using WolfConquer.Core.Client;

namespace WolfConquer.Core.Data;

/// <summary>Once-bound immutable selected metadata, with captured server candidate order preserved by exact key join.</summary>
public sealed class Chinese7937XuanbaoRuntimeRules
{
    private readonly FrozenDictionary<int, XuanbaoComposeAttributeLimit> composeById;
    private readonly FrozenDictionary<int, IReadOnlyList<XuanbaoRandomAttribute>> randomByItem;

    private Chinese7937XuanbaoRuntimeRules(Chinese7937XuanbaoRuleCatalogs source,
        Dictionary<int, IReadOnlyList<XuanbaoRandomAttribute>> orderedRandom)
    {
        Source = source;
        composeById = source.ComposeLimits.ToFrozenDictionary(row => row.Id);
        randomByItem = orderedRandom.ToFrozenDictionary();
    }

    public Chinese7937XuanbaoRuleCatalogs Source { get; }
    public IReadOnlyDictionary<int, IReadOnlyList<XuanbaoRandomAttribute>> RandomAttributesByItemTypeId => randomByItem;
    public bool TryGetComposeLimit(int id, out XuanbaoComposeAttributeLimit? row) => composeById.TryGetValue(id, out row);

    internal static bool TryPrepare(WorldDataCatalog owner, Chinese7937XuanbaoRuleCatalogs source,
        out Chinese7937XuanbaoRuntimeRules? rules, out string reason)
    {
        rules = null;
        reason = string.Empty;
        if (source is null || source.ClientVersion != 7937)
        { reason = "selected7937RuleCatalogRequired"; return false; }
        if (!ComposeMatches(owner, source))
        { reason = "selected7937ComposeOwnerOrValuesConflict"; return false; }
        if (!TryJoinRandom(owner, source, out var ordered))
        { reason = "selected7937RandomOwnerOrValuesConflict"; return false; }
        rules = new(source, ordered);
        return true;
    }

    private static bool ComposeMatches(WorldDataCatalog owner, Chinese7937XuanbaoRuleCatalogs source)
    {
        var raw = owner.XuanbaoComposeAttributeLimits.ToArray();
        var captured = owner.XuanbaoComposeAttributeLimitsById.ToArray();
        if (raw.Length != source.ComposeLimits.Count || captured.Length != raw.Length) return false;
        HashSet<int> seen = [];
        foreach (var row in raw)
            if (row is null || !seen.Add(row.Id) || !ComposeRowMatches(row, source)) return false;
        foreach (var pair in captured)
        {
            var row = pair.Value;
            if (row is null || row.Id != pair.Key || !seen.Contains(row.Id) || !ComposeRowMatches(row, source)) return false;
        }
        return true;
    }

    private static bool ComposeRowMatches(XuanbaoComposeAttributeLimit row, Chinese7937XuanbaoRuleCatalogs source)
    {
        if (row.Values is null) return false;
        var values = row.Values.ToArray();
        return values.Length == 3 && source.TryGetComposeLimit(row.Id, out var selected) && selected is not null &&
            values.AsSpan().SequenceEqual(selected.Values.ToArray()) &&
            // LukyStrike has a fixed 100/epic formula. Retain exact row data,
            // but do not turn its unused final cell into a required cap.
            (row.Id == 14 || selected.Values[^1] is >= 30 and <= ushort.MaxValue);
    }

    private static bool TryJoinRandom(WorldDataCatalog owner, Chinese7937XuanbaoRuleCatalogs source,
        out Dictionary<int, IReadOnlyList<XuanbaoRandomAttribute>> ordered)
    {
        ordered = [];
        if (!CompleteRawRandomMatches(owner, source)) return false;
        var capturedGroups = owner.XuanbaoRandomAttributesByItemTypeId.Select(pair =>
            (Item: pair.Key, Rows: pair.Value?.Select(row => row is null ? (RandomValues?)null :
                new RandomValues(row.ItemTypeId, row.AttributeType, row.MinValue, row.MaxValue)).ToArray())).ToArray();
        HashSet<(int Item, int Attribute)> seen = [];
        foreach (var group in capturedGroups)
        {
            if (group.Rows is null || group.Rows.Length == 0 || !owner.ItemsById.TryGetValue(group.Item, out var items) ||
                items.Count == 0 || items.Any(item => item is null || item.Id != group.Item)) return false;
            List<XuanbaoRandomAttribute> selectedRows = [];
            foreach (var captured in group.Rows)
            {
                if (captured is not { } row || row.Item != group.Item || !seen.Add((row.Item, row.Attribute)) ||
                    !source.TryGetRandomAttribute(row.Item, row.Attribute, out var selected) || selected is null ||
                    selected.MinValue != row.Minimum || selected.MaxValue != row.Maximum || !ValidRandomRange(selected)) return false;
                // Source records are deep immutable. Their order here is the
                // captured existing SERVER order, never a claimed client order.
                selectedRows.Add(selected);
            }
            ordered.Add(group.Item, Array.AsReadOnly(selectedRows.ToArray()));
        }
        return seen.Count == source.RandomAttributes.Count;
    }

    private static bool CompleteRawRandomMatches(WorldDataCatalog owner, Chinese7937XuanbaoRuleCatalogs source)
    {
        var rows = owner.XuanbaoRandomAttributes.ToArray();
        if (rows.Length != source.RandomAttributes.Count) return false;
        HashSet<(int Item, int Attribute)> seen = [];
        foreach (var row in rows)
            if (row is null || !seen.Add((row.ItemTypeId, row.AttributeType)) ||
                !source.TryGetRandomAttribute(row.ItemTypeId, row.AttributeType, out var selected) || selected is null ||
                row.MinValue != selected.MinValue || row.MaxValue != selected.MaxValue) return false;
        return true;
    }

    private readonly record struct RandomValues(int Item, int Attribute, int Minimum, int Maximum);

    private static bool ValidRandomRange(XuanbaoRandomAttribute row)
    {
        var maximum = row.MaxValue % 10 == 1 ? row.MaxValue - 1 : row.MaxValue;
        return row.ItemTypeId > 0 && row.AttributeType is >= 1 and <= 14 && row.MinValue >= 0 &&
            maximum is > 0 and <= ushort.MaxValue && row.MinValue <= maximum;
    }
}
