#nullable disable
using System.Collections;
using System.Collections.Generic;

namespace WolfConquer.Core.Data;

// One physical collection owner. No inherited Dictionary path may bypass its gate.
internal partial class LegacyNativeDbSafeDictionary<T1, T2> : IDictionary<T1, T2>,
    IReadOnlyDictionary<T1, T2>, IDictionary
{
    private readonly object syncRoot = new();
    private Dictionary<T1, T2> entries;
    private T2[] cachedValues = [];
    private bool dirty;
    private long revision;
    private int entryOperationDepth;

    public LegacyNativeDbSafeDictionary() : this(0, null) { }
    public LegacyNativeDbSafeDictionary(int cap) : this(cap, null) { }
    internal LegacyNativeDbSafeDictionary(int cap, IEqualityComparer<T1> comparer)
        => entries = new Dictionary<T1, T2>(cap, comparer);

    public object SyncRoot => syncRoot;
    public IEqualityComparer<T1> Comparer { get { lock (syncRoot) return entries.Comparer; } }
    public int Count { get { lock (syncRoot) return entries.Count; } }
    public long Revision { get { lock (syncRoot) return revision; } }
    public bool Update { get { lock (syncRoot) return dirty; } }
    public T2[] MyArray { get { lock (syncRoot) return (T2[])cachedValues.Clone(); } }

    // Native callers deliberately use default-on-absence and Add as an upsert.
    public T2 this[T1 key]
    {
        get { lock (syncRoot) { using var access = EnterEntries(); return entries.TryGetValue(key, out T2 value) ? value : default; } }
        set { lock (syncRoot) { using var access = EnterEntries(mutation: true); long next = NextRevision(); entries[key] = value; Changed(next); } }
    }

    public void Add(T1 key, T2 value) => this[key] = value;

    public bool TryAdd(T1 key, T2 value)
    {
        lock (syncRoot)
        {
            using var access = EnterEntries(mutation: true);
            long next = NextRevision();
            if (!entries.TryAdd(key, value)) return false;
            Changed(next);
            return true;
        }
    }

    public bool Remove(T1 key)
    {
        lock (syncRoot)
        {
            using var access = EnterEntries(mutation: true);
            long next = NextRevision();
            if (!entries.Remove(key)) return false;
            Changed(next);
            return true;
        }
    }

    public bool Remove(T1 key, out T2 value)
    {
        lock (syncRoot)
        {
            using var access = EnterEntries(mutation: true);
            long next = NextRevision();
            if (!entries.Remove(key, out value)) return false;
            Changed(next);
            return true;
        }
    }

    public void Clear()
    {
        lock (syncRoot)
        {
            using var access = EnterEntries(mutation: true);
            if (entries.Count == 0) return;
            long next = NextRevision();
            entries.Clear();
            Changed(next);
        }
    }

    public bool ContainsKey(T1 key) { lock (syncRoot) { using var access = EnterEntries(); return entries.ContainsKey(key); } }
    public bool ContainsValue(T2 value) { lock (syncRoot) { using var access = EnterEntries(); return entries.ContainsValue(value); } }
    public bool TryGetValue(T1 key, out T2 value) { lock (syncRoot) { using var access = EnterEntries(); return entries.TryGetValue(key, out value); } }

    private long NextRevision() => checked(revision + 1);
    private void Changed(long next) { revision = next; dirty = true; }

    private void AddUnique(T1 key, T2 value)
    {
        lock (syncRoot)
        {
            using var access = EnterEntries(mutation: true);
            long next = NextRevision();
            entries.Add(key, value);
            Changed(next);
        }
    }

    private T2 ReadRequired(T1 key) { lock (syncRoot) { using var access = EnterEntries(); return entries[key]; } }

    private EntryOperation EnterEntries(bool mutation = false) => new(this, mutation);

    // Comparers/equality may run application code under a reentrant monitor.
    // A nested write must fail before it can invalidate the outer dictionary operation.
    private readonly struct EntryOperation : IDisposable
    {
        private readonly LegacyNativeDbSafeDictionary<T1, T2> owner;
        internal EntryOperation(LegacyNativeDbSafeDictionary<T1, T2> owner, bool mutation)
        {
            if (mutation && owner.entryOperationDepth != 0)
                throw new InvalidOperationException("Reentrant collection mutation is not permitted.");
            this.owner = owner;
            owner.entryOperationDepth++;
        }
        public void Dispose() => owner.entryOperationDepth--;
    }
}
