using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyOwnedPhysicalMetadataWitness
{
    internal void RequireItemNumber(uint id, int field, uint maximum, bool optionalColumn = false)
    {
        if (id == 0) return;
        UseItem(id, required: true);
        var row = catalog!.ItemsById[checked((int)id)][0];
        if (row.Fields.Count <= field)
        {
            if (!optionalColumn) throw Invalid();
            return; // The native loader explicitly gives absent optional extras0.
        }
        if (!uint.TryParse(row.Fields[field], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value > maximum)
            throw Invalid();
    }

    internal void RequireItemAddNumbers(ItemAddBonus row, params int[] used)
    {
        UseRow(row);
        var raw = row.RawLine.Split(new[] { "@@", " ", "@" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (raw.Length != 10 || !uint.TryParse(raw[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ||
            id != row.ItemTypeId || !byte.TryParse(raw[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var plus) || plus != row.Level)
            throw Invalid();
        foreach (int at in used)
        {
            uint maximum = at is 1 or 2 ? uint.MaxValue : at == 7 ? byte.MaxValue : ushort.MaxValue;
            if (at is < 0 or > 7 || row.Values.Count <= at || row.Values[at] < 0 ||
                !uint.TryParse(raw[at + 2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value > maximum ||
                value != row.Values[at]) throw Invalid();
        }
    }

    internal uint RequireRawUnsigned(IReadOnlyList<string> fields, int at, uint maximum, bool optional = false)
    {
        // The caller is one of the two closed collectors and has already
        // retained this actual adopted row. This is DATA validation only.
        if (fields.Count <= at) { if (!optional) throw Invalid(); return 0; }
        if (!uint.TryParse(fields[at], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value > maximum)
            throw Invalid();
        return value;
    }

    internal int RequireRawInt32(IReadOnlyList<string> fields, int at)
    {
        if (at < 0 || fields.Count <= at ||
            !int.TryParse(fields[at], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) throw Invalid();
        return value;
    }

    internal void RequireRawInt32Equals(IReadOnlyList<string> fields, int at, int expected)
    {
        if (RequireRawInt32(fields, at) != expected) throw Invalid();
    }

    private void RequireCombatGearRankNumbers()
    {
        foreach (var row in catalog!.CombatGearRanks)
        {
            if (row.CharacterId < 0) continue; // The actual rank reader skips this row before parsing columns.
            var kind = RequireRawUnsigned(row.Fields, 2, byte.MaxValue);
            if (kind is 40 or 42) _ = RequireRawUnsigned(row.Fields, 3, uint.MaxValue);
        }
    }
}
