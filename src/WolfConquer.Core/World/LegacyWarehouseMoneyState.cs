using System.Collections.ObjectModel;
using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// The existing strict pickup and NPC-sale file contracts: both aliases are
// optional, but every present value must be nonnegative signed-64 and agree.
// This is a scoped wallet projection, not a global Character parser change.
internal static class LegacyWarehouseMoneyState
{
    internal static bool TryRead(IReadOnlyDictionary<string, string> attributes, out long value)
    {
        value = 0;
        if (attributes is null) return false;
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in attributes)
            if (string.IsNullOrEmpty(pair.Key) || pair.Value is null || !fields.TryAdd(pair.Key, pair.Value)) return false;
        var canonical = fields.TryGetValue("WHMoney", out var firstText);
        var alias = fields.TryGetValue("WarehouseMoney", out var secondText);
        long first = 0, second = 0;
        if (canonical && !Nonnegative(firstText!, out first) || alias && !Nonnegative(secondText!, out second) ||
            canonical && alias && first != second) return false;
        value = canonical ? first : alias ? second : 0;
        return true;
    }

    private static bool Nonnegative(string text, out long value) =>
        long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= 0;

    // Only actual BCL dictionary enumeration is admitted to the native final
    // proof. A read-only wrapper may execute code when opened; that happens
    // during preparation, never in IsCurrentPlain or between physical stores.
    internal sealed class NativeValueProof
    {
        private readonly Dictionary<string, string>.Enumerator witness;
        private readonly KeyValuePair<string, string>[] rows;
        internal long Value { get; }
        internal IReadOnlyDictionary<string, string> CapturedFields { get; }

        internal NativeValueProof(CharacterSnapshot character)
        {
            var source = character.Attributes ?? throw Invalid();
            if (source.GetType() == typeof(Dictionary<string, string>))
                witness = ((Dictionary<string, string>)source).GetEnumerator();
            else if (source.GetType() == typeof(ReadOnlyDictionary<string, string>) &&
                source.GetEnumerator() is Dictionary<string, string>.Enumerator actual) witness = actual;
            else throw Invalid();
            var captured = new List<KeyValuePair<string, string>>();
            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var iterator = witness;
            while (iterator.MoveNext())
            {
                var pair = iterator.Current;
                if (string.IsNullOrEmpty(pair.Key) || pair.Value is null || !fields.TryAdd(pair.Key, pair.Value)) throw Invalid();
                captured.Add(pair);
            }
            if (!TryRead(fields, out var value)) throw Invalid();
            rows = captured.ToArray(); Value = value; CapturedFields = fields;
        }

        internal bool IsCurrentPlain()
        {
            try
            {
                var iterator = witness; var index = 0;
                while (iterator.MoveNext())
                {
                    if (index == rows.Length || iterator.Current.Key != rows[index].Key ||
                        iterator.Current.Value != rows[index++].Value) return false;
                }
                return index == rows.Length;
            }
            catch (InvalidOperationException) { return false; }
        }

        private static InvalidOperationException Invalid() => new("ordinaryWarehouseMoneyStateInvalidOrAmbiguous");
    }
}
