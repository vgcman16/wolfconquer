using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyAccountManagerFullPromotionPersistence
{
    internal static bool TryProject(CharacterSnapshotFileBaseline.Snapshot baseline, string path,
        Values after, out Prepared? prepared, out string reason)
    {
        prepared = null; reason = Prefix + "ProjectionInvalid";
        var text = CharacterSnapshotFileEncoding.Decode(baseline.Bytes, out var encoding);
        var parts = Regex.Split(text, "(\r\n|\r|\n)");
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DonatePoints"] = after.Donation.ToString(CultureInfo.InvariantCulture),
            ["Class"] = after.Class.ToString(CultureInfo.InvariantCulture),
            ["ChargePromotion"] = after.ChargePromotion.ToString(CultureInfo.InvariantCulture),
        };
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var newline = parts.Where((_, index) => (index & 1) != 0).FirstOrDefault() ?? Environment.NewLine;
        var result = new StringBuilder(text.Length + 128);
        var inCharacter = false;
        for (var index = 0; index < parts.Length; index += 2)
        {
            var line = parts[index];
            var trimmed = line.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                if (inCharacter) AppendMissing(result, values, found, newline);
                inCharacter = trimmed[1..^1].Trim().Equals("Character", StringComparison.OrdinalIgnoreCase);
            }
            if (inCharacter && !trimmed.StartsWith(';') && !trimmed.StartsWith('#'))
            {
                var equals = line.IndexOf('=');
                if (equals > 0 && values.TryGetValue(line[..equals].Trim(), out var value))
                {
                    var key = line[..equals].Trim();
                    if (!found.Add(key)) return false;
                    line = line[..(equals + 1)] + value;
                }
            }
            result.Append(line);
            if (index + 1 < parts.Length) result.Append(parts[index + 1]);
        }
        if (inCharacter) AppendMissing(result, values, found, newline);
        var projected = result.ToString();
        if (found.Count != values.Count || encoding.GetString(encoding.GetBytes(projected)) != projected) return false;
        var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(projected)).ToArray();
        if (bytes.Length is 0 or > CharacterSnapshotFileBaseline.MaximumBytes) return false;
        var parsed = CharacterSnapshotParser.ParseText(projected);
        var beforeParsed = CharacterSnapshotParser.ParseText(text);
        if (parsed.Uid != beforeParsed.Uid || !TryReadValues(parsed.Attributes, out var actual) || actual != after) return false;
        prepared = new(Path.GetDirectoryName(path) ?? throw new ArgumentException("Character image parent is required.", nameof(path)),
            path, baseline.Bytes.ToArray(), bytes);
        reason = string.Empty;
        return true;
    }

    private static void AppendMissing(StringBuilder result, Dictionary<string, string> values,
        HashSet<string> found, string newline)
    {
        foreach (var pair in values)
        {
            if (!found.Add(pair.Key)) continue;
            if (result.Length != 0 && result[^1] is not '\r' and not '\n') result.Append(newline);
            result.Append(pair.Key).Append('=').Append(pair.Value).Append(newline);
        }
    }
}
