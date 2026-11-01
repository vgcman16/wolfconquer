using System.Collections;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyTeleportAttributePreservationTests
{
    [Fact]
    public void ReducerRefusesUnknownMapAndComparerWithoutInvokingTheirCallbacks()
    {
        var map = new CallbackMap();
        Assert.Throws<InvalidOperationException>(() => LegacyTeleportDestinationReducer.Reduce(Character(map), new(1000, 50, 51), true));
        Assert.Equal(0, map.Calls);
        var comparer = new CallbackComparer();
        var fields = new Dictionary<string, string>(comparer) { ["DynamicID"] = "44" }; comparer.Calls = 0;
        Assert.Throws<InvalidOperationException>(() => LegacyTeleportDestinationReducer.Reduce(Character(fields), new(1000, 50, 51), true));
        Assert.Equal(0, comparer.Calls);
    }
    private sealed class CallbackComparer : IEqualityComparer<string>
    {
        internal int Calls;
        public bool Equals(string? first, string? second) { Calls++; return StringComparer.Ordinal.Equals(first, second); }
        public int GetHashCode(string value) { Calls++; return StringComparer.Ordinal.GetHashCode(value); }
    }
    private sealed class CallbackMap : IReadOnlyDictionary<string, string>
    {
        internal int Calls;
        private T Called<T>() { Calls++; throw new InvalidOperationException("Unexpected attribute callback."); }
        public string this[string key] => Called<string>();
        public IEnumerable<string> Keys => Called<IEnumerable<string>>();
        public IEnumerable<string> Values => Called<IEnumerable<string>>();
        public int Count => Called<int>();
        public bool ContainsKey(string key) => Called<bool>();
        public bool TryGetValue(string key, out string value) { value = null!; return Called<bool>(); }
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => Called<IEnumerator<KeyValuePair<string, string>>>();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
