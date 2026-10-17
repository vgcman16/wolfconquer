using System.Runtime.CompilerServices;

namespace WolfConquer.Core.Data;

public sealed partial class WorldDataCatalog
{
    // Later reentrant casts use the catalog's load culture, not their current thread culture.
    [System.Text.Json.Serialization.JsonIgnore]
    private readonly System.Globalization.CultureInfo nativeMagicCulture =
        System.Globalization.CultureInfo.ReadOnly((System.Globalization.CultureInfo)System.Globalization.CultureInfo.CurrentCulture.Clone());

    // Per loaded catalog, keyed by exact raw ROW reference. No static/session/save/equality authority.
    [System.Text.Json.Serialization.JsonIgnore]
    private readonly ConditionalWeakTable<MagicType, LegacyNativeMagicRow> nativeMagicRows = new();

    internal LegacyNativeMagicRow GetNativeMagicRow(MagicType row) =>
        nativeMagicRows.GetValue(row, item => LegacyNativeMagicRow.ParseMutableFields(item, nativeMagicCulture));

    internal System.Globalization.CultureInfo NativeMagicParseCulture => nativeMagicCulture;

    internal LegacyNativeMagicRow? FindNativeMagicRow(ushort id, ushort level)
    {
        // Native MagicType.Load finishes with Add6009, base6010: the SAME dictionary/row objects.
        var key = id == 6_009 ? (ushort)6_010 : id;
        if (!MagicByTypeId.TryGetValue(key, out var rows)) return null;
        foreach (var row in rows)
            if (byte.Parse(row.Fields[8], nativeMagicCulture) == level)
                return GetNativeMagicRow(row); // First duplicate level wins, as native Load.
        return null;
    }
}
