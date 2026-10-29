#nullable disable
using System.Collections;
using System.Collections.Generic;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeDbSafeDictionary<T1, T2>
{
    // Values and Keys are independent read-only projections, including through casts.
    // They do not materialize/clean the optional native GetValues cache.
    public ICollection<T1> Keys { get { lock (syncRoot) return Array.AsReadOnly(entries.Keys.ToArray()); } }
    public ICollection<T2> Values { get { lock (syncRoot) return Array.AsReadOnly(entries.Values.ToArray()); } }
    IEnumerable<T1> IReadOnlyDictionary<T1, T2>.Keys => Keys;
    IEnumerable<T2> IReadOnlyDictionary<T1, T2>.Values => Values;

    public T2[] GetValues()
    {
        lock (syncRoot)
        {
            // A comparer may inspect values before its surrounding operation commits.
            // Such a read must not change cache metadata if that operation rejects.
            if (dirty && entryOperationDepth != 0) return entries.Values.ToArray();
            if (dirty)
            {
                T2[] prepared = entries.Values.ToArray();
                cachedValues = prepared;
                dirty = false;
            }
            return (T2[])cachedValues.Clone();
        }
    }

    // A concrete atomic snapshot avoids LINQ reading ICollection.Count and CopyTo
    // across two separately locked operations while other threads can mutate.
    public KeyValuePair<T1, T2>[] ToArray() => CaptureEntries();

    private KeyValuePair<T1, T2>[] CaptureEntries()
    {
        lock (syncRoot) return entries.ToArray();
    }

    public IEnumerator<KeyValuePair<T1, T2>> GetEnumerator()
        => ((IEnumerable<KeyValuePair<T1, T2>>)CaptureEntries()).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private sealed class SnapshotEnumerator(KeyValuePair<T1, T2>[] rows) : IDictionaryEnumerator
    {
        private int index = -1;
        public DictionaryEntry Entry
        {
            get
            {
                if (index < 0 || index >= rows.Length) throw new InvalidOperationException("No current dictionary entry.");
                return new DictionaryEntry(rows[index].Key, rows[index].Value);
            }
        }
        public object Key => Entry.Key;
        public object Value => Entry.Value;
        public object Current => Entry;
        public bool MoveNext() { if (index < rows.Length) index++; return index < rows.Length; }
        public void Reset() => index = -1;
    }
}