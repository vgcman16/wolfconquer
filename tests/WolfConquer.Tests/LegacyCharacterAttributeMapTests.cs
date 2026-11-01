using System.Collections;
using System.Collections.ObjectModel;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyCharacterAttributeMapTests
{
    [Theory]
    [InlineData(0, false)] [InlineData(0, true)]
    [InlineData(1, false)] [InlineData(1, true)]
    [InlineData(2, false)] [InlineData(2, true)]
    public void RealComparersAndUniqueOwnedSpellingPreserveEveryUnrelatedField(int kind, bool wrapped)
    {
        var comparer = Comparer(kind);
        var source = new Dictionary<string, string>(comparer)
            { ["Other"] = "upper", ["action"] = "250", ["activepick"] = "yes", ["Unrelated"] = "unchanged" };
        if (kind != 1) source.Add("other", "lower");
        var before = source.ToArray();
        IReadOnlyDictionary<string, string> input = wrapped ? new ReadOnlyDictionary<string, string>(source) : source;
        var copied = LegacyCharacterAttributeMap.Copy(input);
        Assert.Same(source.Comparer, copied.Comparer);
        Assert.True(LegacyCharacterAttributeMap.TryRead(copied, "ActivePick", out var active)); Assert.Equal("yes", active);
        LegacyCharacterAttributeMap.Set(copied, "Action", "0");
        Assert.True(LegacyCharacterAttributeMap.Remove(copied, "ActivePick"));
        LegacyCharacterAttributeMap.Set(copied, "AttackHit", "true");
        Assert.Equal("0", copied["action"]);
        Assert.Contains("action", copied.Keys); Assert.DoesNotContain("Action", copied.Keys, StringComparer.Ordinal);
        Assert.DoesNotContain("activepick", copied.Keys);
        Assert.Equal("upper", copied["Other"]); Assert.Equal("unchanged", copied["Unrelated"]);
        if (kind != 1) Assert.Equal("lower", copied["other"]);
        Assert.Equal("true", copied["AttackHit"]);
        Assert.False(LegacyCharacterAttributeMap.Remove(copied, "Absent"));
        Assert.Equal(before, source.ToArray());
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void AmbiguousOwnedSpellingsAreRefusedWithoutChangingTheProposedOrSourceData(int operation)
    {
        var source = new Dictionary<string, string>(StringComparer.Ordinal)
            { ["Action"] = "250", ["action"] = "250", ["Other"] = "upper", ["other"] = "lower" };
        var original = source.ToArray(); var copy = LegacyCharacterAttributeMap.Copy(source);
        var error = Assert.Throws<InvalidDataException>(() =>
        {
            if (operation == 3) LegacyCharacterAttributeMap.Validate(source, "ACTION");
            else if (operation == 2) LegacyCharacterAttributeMap.TryRead(copy, "ACTION", out _);
            else if (operation == 1) LegacyCharacterAttributeMap.Remove(copy, "ACTION");
            else LegacyCharacterAttributeMap.Set(copy, "ACTION", "0");
        });
        Assert.Equal("characterAttributeOwnedFieldAmbiguous", error.Message);
        Assert.Equal(original, source.ToArray()); Assert.Equal(original, copy.ToArray());
    }

    [Fact]
    public void UnsupportedMapAndComparerCallbacksCannotRunDuringCopyOrEdit()
    {
        var callbackMap = new CallbackMap();
        Assert.Throws<InvalidOperationException>(() => LegacyCharacterAttributeMap.Copy(callbackMap));
        Assert.Equal(0, callbackMap.Calls);
        var comparer = new CallbackComparer();
        var source = new Dictionary<string, string>(comparer) { ["Action"] = "250" };
        comparer.Calls = 0;
        Assert.Throws<InvalidOperationException>(() => LegacyCharacterAttributeMap.Copy(source));
        Assert.Throws<InvalidOperationException>(() => LegacyCharacterAttributeMap.Set(source, "Action", "0"));
        Assert.Throws<InvalidOperationException>(() => LegacyCharacterAttributeMap.Remove(source, "Action"));
        Assert.Throws<InvalidOperationException>(() => LegacyCharacterAttributeMap.TryRead(source, "Action", out _));
        Assert.Throws<InvalidOperationException>(() => LegacyCharacterAttributeMap.Validate(source, "Action"));
        Assert.Equal(0, comparer.Calls);
        Assert.Equal("250", Assert.Single(source).Value);
    }

    private static IEqualityComparer<string> Comparer(int kind) => kind switch
    {
        0 => StringComparer.Ordinal,
        1 => StringComparer.OrdinalIgnoreCase,
        2 => EqualityComparer<string>.Default,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private sealed class CallbackComparer : IEqualityComparer<string>
    {
        internal int Calls;
        public bool Equals(string? first, string? second) { Calls++; return StringComparer.Ordinal.Equals(first, second); }
        public int GetHashCode(string value) { Calls++; return StringComparer.Ordinal.GetHashCode(value); }
    }

    private sealed class CallbackMap : IReadOnlyDictionary<string, string>
    {
        internal int Calls;
        private T Invoke<T>() { Calls++; throw new InvalidOperationException("Unexpected map callback."); }
        public string this[string key] => Invoke<string>();
        public IEnumerable<string> Keys => Invoke<IEnumerable<string>>();
        public IEnumerable<string> Values => Invoke<IEnumerable<string>>();
        public int Count => Invoke<int>();
        public bool ContainsKey(string key) => Invoke<bool>();
        public bool TryGetValue(string key, out string value) { value = null!; return Invoke<bool>(); }
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => Invoke<IEnumerator<KeyValuePair<string, string>>>();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
