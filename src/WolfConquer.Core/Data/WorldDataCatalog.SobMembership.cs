using System.Collections;
using System.Collections.ObjectModel;

namespace WolfConquer.Core.Data;

public sealed partial class WorldDataCatalog
{
    // Constructor-owned collection. It exposes no mutable array/list alias;
    // each element is the exact original scalar row, not an invented owner.
    private sealed class OwnedSobRows : IReadOnlyList<SobNpc>
    {
        private readonly SobNpc[] rows;
        internal OwnedSobRows(IEnumerable<SobNpc> source) => rows = source.ToArray();
        public int Count => rows.Length;
        public SobNpc this[int index] => rows[index];
        public IEnumerator<SobNpc> GetEnumerator() => ((IEnumerable<SobNpc>)rows).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => rows.GetEnumerator();
    }

    private static IReadOnlyList<SobNpc> CaptureSobMembership(IReadOnlyList<SobNpc> rows) => new OwnedSobRows(rows);

    private static IReadOnlyDictionary<int, IReadOnlyList<SobNpc>> BuildOwnedSobIndex(
        IReadOnlyList<SobNpc> rows, Func<SobNpc, int> key)
    {
        var index = new Dictionary<int, IReadOnlyList<SobNpc>>();
        foreach (var group in rows.GroupBy(key)) index.Add(group.Key, new OwnedSobRows(group));
        return new ReadOnlyDictionary<int, IReadOnlyList<SobNpc>>(index);
    }

    // Exact catalog-owned membership only. This is a structural witness,
    // never a mutable-state or target damage authority.
    internal bool IsOwnedSobMembership(IReadOnlyList<SobNpc>? rows) =>
        ReferenceEquals(rows, SobNpcs) && rows is OwnedSobRows;

    internal bool TryGetOwnedSobRowsById(int id, out IReadOnlyList<SobNpc> rows)
    {
        rows = Array.Empty<SobNpc>();
        if (!IsOwnedSobMembership(SobNpcs) || !SobNpcsById.TryGetValue(id, out var found) || found is not OwnedSobRows)
            return false;
        rows = found;
        return true;
    }
}
