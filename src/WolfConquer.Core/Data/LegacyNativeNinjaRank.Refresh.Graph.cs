namespace WolfConquer.Core.Data;

internal partial class LegacyNativeNinjaRank
{
    private sealed record RankRow(uint Key, Entry Row, uint Uid, Type Category, uint Points);
    private sealed record RankSource(Rank Owner, Type Category, object Gate, int Limit,
        Dictionary<uint, Entry> Map, RankRow[] Rows);

    private static Rank[] CaptureOwners(LegacyNativeDbSafeDictionary<Type, Rank> registry)
    {
        if (registry.Count != (int)Type.Count) throw UnavailableRefresh();
        var owners = new Rank[(int)Type.Count];
        var seen = new HashSet<Rank>(ReferenceEqualityComparer.Instance);
        foreach (var pair in registry)
        {
            var index = (uint)pair.Key;
            if (index >= (uint)Type.Count || pair.Value is null || owners[index] is not null ||
                pair.Value._Type != pair.Key || !seen.Add(pair.Value)) throw UnavailableRefresh();
            owners[index] = pair.Value;
        }
        if (owners.Any(owner => owner is null)) throw UnavailableRefresh();
        return owners;
    }

    private static RankSource CaptureSource(Rank owner, Type category, object gate)
    {
        if (owner._Type != category || !ReferenceEquals(owner.SynRoot, gate) || owner.MaxItems <= 0 ||
            owner.RankingList is not { } map) throw UnavailableRefresh();
        var rows = new List<RankRow>();
        var uids = new HashSet<uint>();
        foreach (var pair in map)
        {
            var row = pair.Value;
            if (pair.Key == 0 || row is null || row.UID != pair.Key || row.Type != category ||
                !uids.Add(pair.Key)) throw UnavailableRefresh();
            rows.Add(new(pair.Key, row, row.UID, row.Type, row.TotalPoints));
        }
        return new(owner, category, gate, owner.MaxItems, map, rows.ToArray());
    }

    internal sealed partial class PreparedRefresh
    {
        private void RequireCurrentSources()
        {
            if (!ReferenceEquals(Ranks, registry) || !ReferenceEquals(registry.SyncRoot, registryGate))
                throw UnavailableRefresh();
            var owners = CaptureOwners(registry);
            for (var index = 0; index < sources.Length; index++)
            {
                var source = sources[index];
                var owner = owners[index];
                if (!ReferenceEquals(owner, source.Owner) || owner._Type != source.Category ||
                    !ReferenceEquals(owner.SynRoot, source.Gate) || owner.MaxItems != source.Limit ||
                    !ReferenceEquals(owner.RankingList, source.Map) || source.Map.Count != source.Rows.Length)
                    throw UnavailableRefresh();
                var ordinal = 0;
                foreach (var pair in source.Map)
                {
                    if (ordinal >= source.Rows.Length) throw UnavailableRefresh();
                    var row = source.Rows[ordinal++];
                    if (pair.Key != row.Key || !ReferenceEquals(pair.Value, row.Row) ||
                        pair.Value.UID != row.Uid || pair.Value.Type != row.Category ||
                        pair.Value.TotalPoints != row.Points) throw UnavailableRefresh();
                }
                if (ordinal != source.Rows.Length) throw UnavailableRefresh();
            }
        }
    }
}
