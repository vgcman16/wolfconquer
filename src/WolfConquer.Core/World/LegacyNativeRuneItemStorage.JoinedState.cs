namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeRuneItemStorage
{
    internal sealed class JoinedState
    {
        private readonly LegacyNativeRuneItemStorage owner;
        private readonly StorageSnapshot state;
        internal JoinedState(LegacyNativeRuneItemStorage owner, Dictionary<uint, LegacyNativeGameItemRuntime> map, LegacyNativeGameItemRuntime[] cache)
        {
            this.owner = owner;
            var rows = cache.Select(item => new StorageRow(item, item.Uid, item.Position, item.ITEM_ID)).ToArray();
            state = new StorageSnapshot(map, cache, rows);
        }
        internal bool ReferencesCurrentPlain() =>
            ReferenceEquals(owner.items, state.Dictionary) && ReferenceEquals(owner.objects, state.Cache);
        internal bool ContentsCurrentPlain() => ReferencesCurrentPlain() && PreparedContentsCurrentPlain();
        internal bool PreparedContentsCurrentPlain()
        {
            if (state.Dictionary.Count != state.Rows.Length || state.Cache.Length != state.Rows.Length)
                return false;
            int index = 0;
            foreach (var entry in state.Dictionary)
            {
                if (index >= state.Rows.Length)
                    return false;
                var row = state.Rows[index];
                if (entry.Key != row.Uid ||
                    !ReferenceEquals(entry.Value, row.Item) ||
                    !ReferenceEquals(state.Cache[index], row.Item) ||
                    !IsCurrentRow(row))
                    return false;
                index++;
            }
            return index == state.Rows.Length;
        }
        internal void PublishUnchecked()
        {
            owner.items = state.Dictionary;
            owner.objects = state.Cache;
        }
    }

    internal JoinedState CaptureJoinedItems(LegacyNativeOwnedItemGraph graph)
    {
        if (!Monitor.IsEntered(StorageSyncRoot) || !ReferenceEquals(JoinedItemGraph, graph) || storagePreparationDepth != 0)
            throw new InvalidOperationException("Actual joined Rune gate is required.");
        _ = RequireStorageSnapshot();
        return new(this, items, objects);
    }

    internal JoinedState PrepareJoinedItems(LegacyNativeOwnedItemGraph graph, LegacyNativeGameItemRuntime[] rows)
    {
        if (!Monitor.IsEntered(StorageSyncRoot) || !ReferenceEquals(JoinedItemGraph, graph) || storagePreparationDepth != 0)
            throw new InvalidOperationException("Actual joined Rune gate is required.");
        using var preparation = BeginStoragePreparation();
        bool same = objects.Length == rows.Length;
        for (int index = 0; same && index < rows.Length; index++)
            same = ReferenceEquals(objects[index], rows[index]);
        if (same)
            return new(this, items, objects);
        var map = new Dictionary<uint, LegacyNativeGameItemRuntime>(rows.Length, items.Comparer);
        foreach (var item in rows)
        {
            if (item is null || item.Uid == 0)
                throw new InvalidDataException("Invalid canonical Rune identity.");
            map.Add(item.Uid, item);
        }
        return new(this, map, map.Values.ToArray());
    }

}
