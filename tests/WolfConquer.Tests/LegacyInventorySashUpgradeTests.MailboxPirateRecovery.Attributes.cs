using System.Collections;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    private sealed class PirateRecoveryAttributes(IReadOnlyDictionary<string, string> values) : IReadOnlyDictionary<string, string>
    {
        internal Action? OnExperienceRead { get; set; }
        internal bool Fired { get; private set; }
        public bool TryGetValue(string key, out string value)
        {
            if (key == "ClassExperience" && !Fired && OnExperienceRead is { } action)
            { Fired = true; action(); }
            return values.TryGetValue(key, out value!);
        }
        public string this[string key] => values[key];
        public IEnumerable<string> Keys => values.Keys;
        public IEnumerable<string> Values => values.Values;
        public int Count => values.Count;
        public bool ContainsKey(string key) => values.ContainsKey(key);
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => values.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
