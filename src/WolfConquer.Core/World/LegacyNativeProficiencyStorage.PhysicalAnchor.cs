using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeProficiencyStorage
{
    private PhysicalMapAnchor? ordinaryMapAnchor;
    private sealed record PhysicalMapAnchor(
        ConcurrentDictionary<uint, LegacyNativeMsgProficiency> Map, Row[] Rows);

    private void RequireOrdinaryAnchorCurrent()
    {
        if (Owner.OrdinarySpellBinding is null) return;
        var anchor = ordinaryMapAnchor ?? throw UnavailableProficiencies();
        if (!ReferenceEquals(ClientProf, anchor.Map)) throw UnavailableProficiencies();
        using var iterator = anchor.Map.GetEnumerator();
        foreach (var saved in anchor.Rows)
        {
            if (!iterator.MoveNext()) throw UnavailableProficiencies();
            var pair = iterator.Current;
            if (pair.Key != saved.Key || !ReferenceEquals(pair.Value, saved.Physical) || pair.Key != pair.Value.ID)
                throw UnavailableProficiencies();
        }
        if (iterator.MoveNext()) throw UnavailableProficiencies();
    }

    private void PublishOrdinaryAnchor(PhysicalMapAnchor value) =>
        Volatile.Write(ref ordinaryMapAnchor, value);
}
