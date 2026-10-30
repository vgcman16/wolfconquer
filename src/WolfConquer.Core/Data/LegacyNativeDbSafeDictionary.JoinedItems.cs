#nullable disable
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeDbSafeDictionary<T1, T2>
{
    private LegacyNativeOwnedItemGraph joinedItemGraph;
    internal bool HasJoinedItemGraph(LegacyNativeOwnedItemGraph graph) => ReferenceEquals(joinedItemGraph, graph);
    internal void JoinItemGraph(LegacyNativeOwnedItemGraph graph)
    {
        if (joinedItemGraph is not null || graph is null || this is not LegacyNativeItemBagRuntime)
            throw new InvalidOperationException("Only an actual unjoined item bag can be attached.");
        joinedItemGraph = graph;
    }

    internal sealed class JoinedState
    {
        private readonly LegacyNativeDbSafeDictionary<T1, T2> owner;
        private readonly Dictionary<T1, T2> map;
        private readonly T2[] cache;
        private readonly long version;
        private readonly bool dirty;
        internal readonly KeyValuePair<T1, T2>[] Rows;
        internal JoinedState(LegacyNativeDbSafeDictionary<T1, T2> owner, Dictionary<T1, T2> map, T2[] cache, long version, bool dirty)
        {
            this.owner = owner;
            this.map = map;
            this.cache = cache;
            this.version = version;
            this.dirty = dirty;
            Rows = map.ToArray();
        }
        internal bool ReferencesCurrentPlain() =>
            ReferenceEquals(owner.entries, map) &&
                ReferenceEquals(owner.cachedValues, cache) &&
                owner.revision == version &&
                owner.dirty == dirty;
        internal bool ContentsCurrentPlain()
        {
            int index = 0;
            foreach (var row in map)
            {
                if (index >= Rows.Length ||
                    !EqualityComparer<T1>.Default.Equals(row.Key, Rows[index].Key) ||
                    !ReferenceEquals(row.Value, Rows[index].Value))
                    return false;
                index++;
            }
            return index == Rows.Length;
        }
        internal void PublishUnchecked()
        {
            owner.entries = map;
            owner.cachedValues = cache;
            owner.revision = version;
            owner.dirty = dirty;
        }
    }

    internal JoinedState CaptureJoinedItems(LegacyNativeOwnedItemGraph graph)
    {
        if (!Monitor.IsEntered(syncRoot) || !HasJoinedItemGraph(graph) || entryOperationDepth != 0)
            throw new InvalidOperationException("Actual joined bag gate is required.");
        return new(this, entries, cachedValues, revision, dirty);
    }

    internal JoinedState PrepareJoinedItems(LegacyNativeOwnedItemGraph graph, KeyValuePair<T1, T2>[] rows)
    {
        if (!Monitor.IsEntered(syncRoot) || !HasJoinedItemGraph(graph) || entryOperationDepth != 0)
            throw new InvalidOperationException("Actual joined bag gate is required.");
        using var access = EnterEntries();
        bool same = entries.Count == rows.Length;
        int index = 0;
        foreach (var old in entries)
        {
            if (index >= rows.Length ||
                !EqualityComparer<T1>.Default.Equals(old.Key, rows[index].Key) ||
                !ReferenceEquals(old.Value, rows[index].Value))
                same = false;
            index++;
        }
        if (same)
            return new(this, entries, cachedValues, revision, dirty);
        long next = checked(revision + 1);
        var map = new Dictionary<T1, T2>(rows.Length, entries.Comparer);
        foreach (var row in rows)
            map.Add(row.Key, row.Value);
        var cache = map.Values.ToArray();
        return new(this, map, cache, next, false);
    }

}
