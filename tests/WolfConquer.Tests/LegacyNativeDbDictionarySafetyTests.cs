using System.Collections;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeDbDictionarySafetyTests
{
    [Fact]
    public void EveryDictionaryInterfaceMutatesTheSameEntriesRevisionAndCache()
    {
        var owner = new LegacyNativeDbSafeDictionary<string, int>(4, StringComparer.OrdinalIgnoreCase);
        IDictionary<string, int> generic = owner; IDictionary legacy = owner;
        ICollection<KeyValuePair<string, int>> pairs = owner;
        generic.Add("First", 1); legacy.Add("Second", 2); pairs.Add(new("Third", 3));
        Assert.Equal(new[] { 1, 2, 3 }, owner.GetValues()); Assert.Equal(3, owner.Revision);
        generic["FIRST"] = 10; legacy["SECOND"] = 20;
        Assert.True(pairs.Remove(new("third", 3))); Assert.False(pairs.Remove(new("First", -1)));
        Assert.Equal(new[] { 10, 20 }, owner.GetValues()); Assert.Equal(6, owner.Revision);
        legacy.Remove("first"); generic.Remove("second"); Assert.Empty(owner.GetValues());
        Assert.Equal(8, owner.Revision); Assert.True(((ICollection)owner).IsSynchronized);
        Assert.Same(StringComparer.OrdinalIgnoreCase, owner.Comparer);
    }

    [Fact]
    public void DuplicateAndWrongTypedInterfaceWritesRejectWithoutPartialChanges()
    {
        var owner = new LegacyNativeDbSafeDictionary<string, int>(); owner.Add("key", 7);
        owner.GetValues(); long revision = owner.Revision; IDictionary legacy = owner;
        Assert.Throws<ArgumentException>(() => legacy.Add("key", 8));
        Assert.Throws<ArgumentException>(() => legacy.Add(9, 8));
        Assert.Throws<ArgumentException>(() => legacy["other"] = "wrong");
        Assert.Throws<ArgumentNullException>(() => legacy.Add(null!, 8));
        Assert.Throws<ArgumentNullException>(() => legacy["other"] = null);
        Assert.Equal(revision, owner.Revision); Assert.False(owner.Update);
        Assert.Equal(new[] { 7 }, owner.GetValues()); Assert.Single(owner);
        Assert.Null(legacy[9]); Assert.False(legacy.Contains(9)); legacy.Remove(9);
        Assert.Equal(revision, owner.Revision);
    }

    [Fact]
    public void KeysValuesAndEnumeratorsAreOrderedIndependentSnapshots()
    {
        var owner = new LegacyNativeDbSafeDictionary<int, string>(); owner.Add(3, "three"); owner.Add(1, "one");
        var keys = owner.Keys; var values = owner.Values;
        using var enumerator = owner.GetEnumerator(); IDictionaryEnumerator legacy = ((IDictionary)owner).GetEnumerator();
        Assert.Throws<NotSupportedException>(() => keys.Add(4));
        Assert.Throws<NotSupportedException>(() => values.Remove("three"));
        Assert.Throws<NotSupportedException>(() => ((IList<string>)values)[0] = "poison");
        owner[3] = "new-three"; owner.Remove(1); owner.Add(7, "seven");
        Assert.Equal(new[] { 3, 1 }, keys); Assert.Equal(new[] { "three", "one" }, values);
        var retained = new List<KeyValuePair<int, string>>(); while (enumerator.MoveNext()) retained.Add(enumerator.Current);
        Assert.Equal(new[] { new KeyValuePair<int, string>(3, "three"), new KeyValuePair<int, string>(1, "one") }, retained);
        Assert.True(legacy.MoveNext()); Assert.Equal(new DictionaryEntry(3, "three"), legacy.Entry);
        Assert.True(legacy.MoveNext()); Assert.Equal(new DictionaryEntry(1, "one"), legacy.Entry);
        Assert.False(legacy.MoveNext()); Assert.Throws<InvalidOperationException>(() => _ = legacy.Current);
        legacy.Reset(); Assert.True(legacy.MoveNext()); Assert.Equal(3, legacy.Key);
        var entries = new KeyValuePair<int, string>[2]; ((ICollection<KeyValuePair<int, string>>)owner).CopyTo(entries, 0);
        var nongeneric = new DictionaryEntry[2]; ((ICollection)owner).CopyTo(nongeneric, 0);
        Assert.Equal(owner.ToArray(), entries); Assert.Equal(new DictionaryEntry(3, "new-three"), nongeneric[0]);
        Assert.Equal(new[] { "new-three", "seven" }, owner.GetValues());
    }

    [Fact]
    public void ReentrantComparerMutationRejectsBeforeAnyEntryRevisionOrCacheChange()
    {
        var comparer = new ReenteringComparer(); var owner = new LegacyNativeDbSafeDictionary<string, string>(4, comparer);
        owner.Add("kept", "one"); long revision = owner.Revision;
        foreach (Action mutation in new Action[] { () => owner.Clear(), () => owner["injected"] = "bad",
                     () => ((IDictionary<string, string>)owner).Add("injected", "bad"), () => owner.Remove("kept") })
        {
            comparer.Attempt = () => { Assert.Equal(new[] { "one" }, owner.GetValues()); mutation(); };
            Assert.Throws<InvalidOperationException>(() => owner.Add("trigger", "rejected"));
            comparer.Attempt = null;
            Assert.True(owner.Update); Assert.Equal(revision, owner.Revision);
            Assert.Equal("one", owner["kept"]); Assert.False(owner.ContainsKey("trigger")); Assert.False(owner.ContainsKey("injected"));
        }
        Assert.Equal(new[] { "one" }, owner.GetValues()); Assert.False(owner.Update);
        owner.Add("after", "valid"); Assert.Equal(revision + 1, owner.Revision);
        Assert.Equal(new[] { "one", "valid" }, owner.GetValues());
    }

    [Fact]
    public void ExhaustedRevisionRejectsActualMutationsBeforeChangingEntriesOrCache()
    {
        var owner = new LegacyNativeDbSafeDictionary<string, string>();
        owner.Add("kept", "value"); owner.GetValues();
        // Force the real owner's finite revision boundary; no replacement owner or callback is used.
        typeof(LegacyNativeDbSafeDictionary<string, string>)
            .GetField("revision", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(owner, long.MaxValue);
        foreach (Action mutate in new Action[] { () => owner["kept"] = "changed", () => owner.Add("new", "value"),
                     () => owner.Remove("kept"), () => owner.Clear(),
                     () => ((IDictionary<string, string>)owner).Add("new", "value") })
        {
            Assert.Throws<OverflowException>(mutate);
            Assert.Equal(long.MaxValue, owner.Revision); Assert.False(owner.Update);
            Assert.Equal("value", owner["kept"]); Assert.Single(owner); Assert.Equal(new[] { "value" }, owner.GetValues());
            Assert.False(System.Threading.Monitor.IsEntered(owner.SyncRoot));
        }
    }

    private sealed class ReenteringComparer : IEqualityComparer<string>
    {
        internal Action? Attempt;
        public bool Equals(string? left, string? right) => StringComparer.Ordinal.Equals(left, right);
        public int GetHashCode(string key)
        {
            Action? attempt = Attempt; Attempt = null; attempt?.Invoke();
            return StringComparer.Ordinal.GetHashCode(key);
        }
    }
}