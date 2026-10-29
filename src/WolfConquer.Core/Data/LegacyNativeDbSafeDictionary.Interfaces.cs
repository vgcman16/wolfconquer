#nullable disable
using System.Collections;
using System.Collections.Generic;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeDbSafeDictionary<T1, T2>
{
    bool ICollection<KeyValuePair<T1, T2>>.IsReadOnly => false;
    T2 IDictionary<T1, T2>.this[T1 key] { get => ReadRequired(key); set => this[key] = value; }
    T2 IReadOnlyDictionary<T1, T2>.this[T1 key] => ReadRequired(key);
    void IDictionary<T1, T2>.Add(T1 key, T2 value) => AddUnique(key, value);
    void ICollection<KeyValuePair<T1, T2>>.Add(KeyValuePair<T1, T2> item) => AddUnique(item.Key, item.Value);

    bool ICollection<KeyValuePair<T1, T2>>.Contains(KeyValuePair<T1, T2> item)
    {
        lock (syncRoot) { using var access = EnterEntries(); return ((ICollection<KeyValuePair<T1, T2>>)entries).Contains(item); }
    }

    bool ICollection<KeyValuePair<T1, T2>>.Remove(KeyValuePair<T1, T2> item)
    {
        lock (syncRoot)
        {
            using var access = EnterEntries(mutation: true);
            long next = NextRevision();
            if (!((ICollection<KeyValuePair<T1, T2>>)entries).Remove(item)) return false;
            Changed(next);
            return true;
        }
    }

    void ICollection<KeyValuePair<T1, T2>>.CopyTo(KeyValuePair<T1, T2>[] array, int arrayIndex)
    {
        lock (syncRoot) ((ICollection<KeyValuePair<T1, T2>>)entries).CopyTo(array, arrayIndex);
    }

    bool IDictionary.IsReadOnly => false;
    bool IDictionary.IsFixedSize => false;
    bool ICollection.IsSynchronized => true;
    object ICollection.SyncRoot => syncRoot;
    ICollection IDictionary.Keys => (ICollection)Keys;
    ICollection IDictionary.Values => (ICollection)Values;
    object IDictionary.this[object key]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(key);
            return key is T1 typed && TryGetValue(typed, out T2 value) ? value : null;
        }
        set => this[RequireKey(key)] = RequireValue(value);
    }
    void IDictionary.Add(object key, object value) => AddUnique(RequireKey(key), RequireValue(value));
    bool IDictionary.Contains(object key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return key is T1 typed && ContainsKey(typed);
    }
    void IDictionary.Remove(object key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key is T1 typed) Remove(typed);
    }
    IDictionaryEnumerator IDictionary.GetEnumerator() => new SnapshotEnumerator(CaptureEntries());
    void ICollection.CopyTo(Array array, int index)
    {
        lock (syncRoot) ((ICollection)entries).CopyTo(array, index);
    }

    private static T1 RequireKey(object key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return key is T1 typed ? typed : throw new ArgumentException("Incorrect dictionary key type.", nameof(key));
    }
    private static T2 RequireValue(object value)
    {
        if (value is T2 typed) return typed;
        if (value is null && default(T2) is null) return default;
        if (value is null) throw new ArgumentNullException(nameof(value));
        throw new ArgumentException("Incorrect dictionary value type.", nameof(value));
    }
}