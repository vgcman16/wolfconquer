using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Strict transaction admission; the public tolerant codec/startup parser is unchanged.
internal sealed class LegacyDragonSkinBook
{
    internal const string Key = "AnimaSkin";
    private readonly Dictionary<string, string> source;
    private readonly KeyValuePair<string, string>[] fields;
    internal LegacyDragonSkinState State { get; }
    internal Dictionary<string, string> CopyFields() => new(fields, StringComparer.OrdinalIgnoreCase);

    private LegacyDragonSkinBook(Dictionary<string, string> source,
        KeyValuePair<string, string>[] fields, LegacyDragonSkinState state)
    { this.source = source; this.fields = fields; State = state; }

    internal static bool TryCapture(IReadOnlyDictionary<string, string> attributes,
        out LegacyDragonSkinBook book, out string reason)
    {
        book = null!;
        reason = "dragonSkinAttributesUnavailable";
        // The real parser and participating writers use ordinary dictionaries.
        // Do not execute arbitrary user collection callbacks inside final commit.
        if (attributes is not Dictionary<string, string> dictionary ||
            attributes.GetType() != typeof(Dictionary<string, string>)) return false;
        var copy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var captured = new List<KeyValuePair<string, string>>();
        try
        {
            foreach (var pair in dictionary)
            {
                if (string.IsNullOrEmpty(pair.Key) || pair.Value is null || !copy.TryAdd(pair.Key, pair.Value))
                { reason = "dragonSkinAttributesAmbiguous"; return false; }
                captured.Add(pair);
            }
        }
        catch (InvalidOperationException)
        { reason = "dragonSkinAttributesChanged"; return false; }
        if (!TryRead(copy, out var state, out reason)) return false;
        // The fingerprint and parsed state must come from the same enumeration,
        // not a second independently captured live dictionary image.
        book = new(dictionary, captured.ToArray(), state);
        if (book.IsCurrent(attributes)) return true;
        reason = "dragonSkinAttributesChanged";
        book = null!;
        return false;
    }

    internal bool IsCurrent(IReadOnlyDictionary<string, string> attributes)
    {
        if (!ReferenceEquals(source, attributes) || source.Count != fields.Length) return false;
        var index = 0;
        try
        {
            foreach (var pair in source)
            {
                if (index == fields.Length) return false;
                var expected = fields[index++];
                if (!string.Equals(pair.Key, expected.Key, StringComparison.Ordinal) ||
                    !string.Equals(pair.Value, expected.Value, StringComparison.Ordinal)) return false;
            }
        }
        catch (InvalidOperationException) { return false; }
        return index == fields.Length;
    }

    internal static bool TryRead(IReadOnlyDictionary<string, string> fields,
        out LegacyDragonSkinState state, out string reason)
    {
        state = LegacyDragonSkinState.Empty;
        reason = "dragonSkinStateInvalid";
        if (!fields.TryGetValue(Key, out var text)) { reason = string.Empty; return true; }
        if (text is null || text.Length > CharacterSnapshotFileBaseline.MaximumBytes) return false;
        var tokens = text.Split('/');
        var length = tokens.Length;
        if (length > 0 && tokens[^1].Length == 0) length--;
        if (length < 2 || !uint.TryParse(tokens[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var equipped) ||
            !int.TryParse(tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ||
            count < 0 || count != length - 2) return false;
        var owned = new uint[count];
        var unique = new HashSet<uint>();
        for (var index = 0; index < count; index++)
            if (!uint.TryParse(tokens[index + 2], NumberStyles.Integer, CultureInfo.InvariantCulture, out owned[index]) ||
                owned[index] == 0 || !unique.Add(owned[index])) return false;
        if (equipped != 0 && !unique.Contains(equipped)) return false;
        state = new(equipped, owned);
        reason = string.Empty;
        return true;
    }

    internal static bool SameUnrelated(IReadOnlyDictionary<string, string> before,
        IReadOnlyDictionary<string, string> after)
    {
        var first = before.Where(pair => !pair.Key.Equals(Key, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        var second = after.Where(pair => !pair.Key.Equals(Key, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        return first.Count == second.Count && first.All(pair => second.TryGetValue(pair.Key, out var value) &&
            string.Equals(pair.Value, value, StringComparison.Ordinal));
    }
}
