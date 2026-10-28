using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeSpellStorage
{
    private PhysicalMapAnchor? ordinaryMapAnchor;
    private sealed record PhysicalMapAnchor(
        ConcurrentDictionary<ushort, LegacyNativeMsgSpell> Map, SpellRow[] Rows);

    private void RequireOrdinaryAnchorCurrent()
    {
        if (OrdinaryBinding is null) return;
        var anchor = ordinaryMapAnchor ?? throw UnavailableNinjaSpells();
        if (!ReferenceEquals(ClientSpells, anchor.Map)) throw UnavailableNinjaSpells();
        using var iterator = anchor.Map.GetEnumerator();
        foreach (var row in anchor.Rows)
        {
            if (!iterator.MoveNext()) throw UnavailableNinjaSpells();
            var pair = iterator.Current;
            if (pair.Key != row.Key || !ReferenceEquals(pair.Value, row.Row) || pair.Key != pair.Value.ID)
                throw UnavailableNinjaSpells();
        }
        if (iterator.MoveNext()) throw UnavailableNinjaSpells();
    }

    private void PublishOrdinaryAnchor(PhysicalMapAnchor anchor) =>
        Volatile.Write(ref ordinaryMapAnchor, anchor);
}
