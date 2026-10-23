using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Classic titles are bytes, distinct from opcode2313 special titles/wings.
internal static class LegacyClassicTitleState
{
    internal const string SelectedKey = "MyTitle";
    internal const string OwnedKey = "Titles";

    internal static bool TryRead(IReadOnlyDictionary<string, string>? attributes,
        out byte selected, out byte[] owned)
    {
        selected = 0;
        owned = [];
        if (attributes is null || !TryCaptureClassicTitleTexts(attributes, out var selectedText, out var ownedText)) return false;
        if (!TryReadClassicTitleSelection(selectedText, out selected)) return false;
        return TryReadClassicTitleOwnership(ownedText, out owned);
    }

    private static bool TryCaptureClassicTitleTexts(IReadOnlyDictionary<string, string> attributes,
        out string? selectedText, out string? ownedText)
    {
        selectedText = null;
        ownedText = null;
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in attributes)
        {
            if (!keys.Add(pair.Key) || pair.Value is null) return false;
            if (pair.Key.Equals(SelectedKey, StringComparison.OrdinalIgnoreCase)) selectedText = pair.Value;
            if (pair.Key.Equals(OwnedKey, StringComparison.OrdinalIgnoreCase)) ownedText = pair.Value;
        }
        return true;
    }

    private static bool TryReadClassicTitleSelection(string? text, out byte selected)
    {
        selected = 0;
        return text is null || (IsSingleLine(text) &&
            byte.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out selected));
    }

    private static bool TryReadClassicTitleOwnership(string? text, out byte[] owned)
    {
        owned = [];
        if (text is null) return true;
        if (!IsSingleLine(text)) return false;
        var titles = new List<byte>();
        var seen = new HashSet<byte>();
        var remaining = text.AsSpan();
        while (!remaining.IsEmpty)
        {
            var end = remaining.IndexOfAny(",/;| ");
            var token = (end < 0 ? remaining : remaining[..end]).Trim();
            remaining = end < 0 ? [] : remaining[(end + 1)..];
            if (token.IsEmpty) continue;
            // Reject the whole invalid owner, never grant a valid prefix followed
            // by malformed or overflowing saved authorization text.
            if (!byte.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var title)) return false;
            if (title != 0 && seen.Add(title)) titles.Add(title);
        }
        owned = titles.ToArray();
        return true;
    }

    internal static bool SameExceptSelection(IReadOnlyDictionary<string, string> first,
        IReadOnlyDictionary<string, string> second)
    {
        var left = WithoutSelection(first);
        var right = WithoutSelection(second);
        return left is not null && right is not null && left.Count == right.Count &&
            left.All(pair => right.TryGetValue(pair.Key, out var value) &&
                string.Equals(pair.Value, value, StringComparison.Ordinal));
    }

    private static Dictionary<string, string>? WithoutSelection(IReadOnlyDictionary<string, string> fields)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in fields)
            if (!pair.Key.Equals(SelectedKey, StringComparison.OrdinalIgnoreCase) && !result.TryAdd(pair.Key, pair.Value))
                return null;
        return result;
    }

    private static bool IsSingleLine(string text) => text.Length <= CharacterSnapshotFileBaseline.MaximumBytes &&
        !text.Contains('\r') && !text.Contains('\n');
}
