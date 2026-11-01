using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class LegacyMagicRuntime
{
    // The existing row decoder, without constructing a catalog or claiming a
    // current loaded row/provider from historical journal data.
    internal static bool IsRecordedDerivedSpellRow(MagicType row, ushort id, ushort level) =>
        id is 20_000 or 21_930 && row.TypeId == id && row.Fields.Count >= MinimumFieldCount &&
        int.TryParse(row.Fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rowId) && rowId == row.Id &&
        int.TryParse(row.Fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var typeId) && typeId == id &&
        string.Equals(row.Fields[3], row.Name, StringComparison.Ordinal) && TryCreate(row, out var spell) &&
        spell.Id == id && spell.Level == level;
}
