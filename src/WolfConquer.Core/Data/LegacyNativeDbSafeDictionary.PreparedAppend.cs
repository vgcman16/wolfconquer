#nullable disable
namespace WolfConquer.Core.Data;

internal partial class LegacyNativeDbSafeDictionary<T1, T2>
{
    internal PreparedAppend CapturePreparedAppend()
    {
        if (!Monitor.IsEntered(syncRoot)) throw new InvalidOperationException("Actual collection gate is required.");
        using var access = EnterEntries(mutation: true);
        return new PreparedAppend(this);
    }

    // Bound to this actual collection, never a replacement bag/authorization owner.
    internal sealed class PreparedAppend
    {
        private readonly LegacyNativeDbSafeDictionary<T1, T2> owner;
        private readonly Dictionary<T1, T2> before;
        private readonly T2[] cache;
        private readonly bool wasDirty;
        private readonly long revision;
        internal KeyValuePair<T1, T2>[] Rows { get; }
        private Dictionary<T1, T2> prepared;
        private long next;

        internal PreparedAppend(LegacyNativeDbSafeDictionary<T1, T2> owner)
        {
            this.owner = owner; before = owner.entries; cache = owner.cachedValues;
            wasDirty = owner.dirty; revision = owner.revision; Rows = before.ToArray();
        }

        internal void Prepare(KeyValuePair<T1, T2>[] additions)
        {
            if (!IsCurrent()) throw new InvalidOperationException("Collection changed before load preparation.");
            using var access = owner.EnterEntries(mutation: true);
            long prospective = checked(revision + 1);
            var image = new Dictionary<T1, T2>(checked(Rows.Length + additions.Length), before.Comparer);
            foreach (var row in Rows) image.Add(row.Key, row.Value);
            foreach (var row in additions) image.Add(row.Key, row.Value);
            prepared = image; next = prospective;
        }

        internal bool IsCurrent() => Monitor.IsEntered(owner.syncRoot) && owner.entryOperationDepth == 0 &&
            ReferenceEquals(owner.entries, before) && ReferenceEquals(owner.cachedValues, cache) &&
            owner.dirty == wasDirty && owner.revision == revision;

        // The coordinating owner validates every state before the first publication.
        // No lookup/comparer/allocation/gate acquisition occurs in this commit leaf.
        internal void PublishUnchecked()
        {
            if (prepared is null) return;
            owner.entries = prepared;
            owner.revision = next;
            owner.dirty = true; // Retain the native deferred-cache refresh convention.
        }
    }
}
