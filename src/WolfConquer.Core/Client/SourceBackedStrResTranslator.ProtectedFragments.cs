using System.Text.RegularExpressions;

namespace WolfConquer.Core.Client;

public static partial class SourceBackedStrResTranslator
{
    private const string QuotedResourcePathPattern = "\"[^\"\\r\\n]*[\\\\/][^\"\\r\\n]*\"";

    private static readonly Regex ReviewedResourceEscape = new(@"\\.", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly Regex QuotedResourcePath = new(
        QuotedResourcePathPattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly Regex AmbiguousResourcePath = new(
        "(?:[A-Za-z]:[\\\\/]|\\\\\\\\)[^\\s<>\\\"]+[ \\t]+[^\\s<>\\\"]*[\\\\/][^\\s<>\\\"]+",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly Regex ResourceFragmentMarker = new(
        "[A-Za-z][A-Za-z0-9+.-]*://|www\\.|[A-Za-z]:[\\\\/]|\\\\\\\\",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));

    private static readonly Regex RelativeResourcePathMarker = new(
        "[^\\s<>\\\"|:/\\\\]+(?:[\\\\/][^\\s<>\\\"|:/\\\\]+)+\\.[A-Za-z0-9]{1,8}",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly Regex ProtectedResourceFragment = new(
        "<[^<>\\r\\n]+>" +
        "|" + QuotedResourcePathPattern +
        "|[A-Za-z][A-Za-z0-9+.-]*://[^\\s<>\\\"|]+" +
        "|www\\.[A-Za-z0-9.-]+(?::[0-9]+)?(?:[/?#][^\\s<>\\\"|]*)?" +
        "|[A-Za-z]:[\\\\/][^\\s<>\\\"|]+" +
        "|\\\\\\\\[^\\s<>\\\"|]+" +
        "|[A-Za-z0-9_.-]+(?:[\\\\/][^\\s<>\\\"|:/\\\\]+)+\\.[A-Za-z0-9]{1,8}[^\\s<>\\\"|]*" +
        "|(?:[0-9]{1,3}\\.){3}[0-9]{1,3}(?::[0-9]+)?(?:[\\\\/?#][^\\s<>\\\"|]*)?",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(1));

    private static void ValidateProtectedResourceFragments(string original, string replacement)
    {
        if (AmbiguousResourcePath.IsMatch(QuotedResourcePath.Replace(original, string.Empty)) ||
            AmbiguousResourcePath.IsMatch(QuotedResourcePath.Replace(replacement, string.Empty)))
        {
            throw new InvalidDataException("Ambiguous unquoted paths are outside reviewed human-text edits.");
        }

        var trimmed = original.TrimStart();
        if (trimmed.StartsWith("SOFTWARE\\", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("HKEY_", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Registry/configuration values are outside reviewed human-text edits.");
        }

        var beforeMatches = ProtectedResourceFragment.Matches(original).Cast<Match>().ToArray();
        var afterMatches = ProtectedResourceFragment.Matches(replacement).Cast<Match>().ToArray();
        RequireFragmentMarkerCoverage(original, beforeMatches);
        RequireFragmentMarkerCoverage(replacement, afterMatches);
        var before = beforeMatches.Select(match => match.Value);
        var after = afterMatches.Select(match => match.Value);
        if (!before.SequenceEqual(after, StringComparer.Ordinal))
        {
            throw new InvalidDataException("Reviewed resource edit changed an embedded link, route or path.");
        }

        var beforeEscapes = ReviewedResourceEscape.Matches(original).Cast<Match>().Select(match => match.Value);
        var afterEscapes = ReviewedResourceEscape.Matches(replacement).Cast<Match>().Select(match => match.Value);
        if (!beforeEscapes.SequenceEqual(afterEscapes, StringComparer.Ordinal))
        {
            throw new InvalidDataException("Reviewed resource edit changed literal escape sequences.");
        }

        // Non-English text inside immutable fragments is retained byte-for-byte.
        // All editable human text supplied by the reviewer must be English.
        var editable = ProtectedResourceFragment.Replace(replacement, string.Empty);
        var originalEditable = ProtectedResourceFragment.Replace(original, string.Empty);
        if (!NeedsTranslation(originalEditable) || NeedsTranslation(editable))
        {
            throw new InvalidDataException("Reviewed resource edit still contains untranslated human text.");
        }
    }

    private static void RequireFragmentMarkerCoverage(string value, IReadOnlyList<Match> fragments)
    {
        bool Covered(int start, int length) => fragments.Any(fragment =>
            fragment.Index <= start && fragment.Index + fragment.Length >= start + length);

        foreach (Match marker in ResourceFragmentMarker.Matches(value))
        {
            if (!Covered(marker.Index, marker.Length))
            {
                throw new InvalidDataException("Unrecognized route/path forms require separate review.");
            }
        }

        foreach (Match marker in RelativeResourcePathMarker.Matches(value))
        {
            for (var index = marker.Index; index < marker.Index + marker.Length; index++)
            {
                // Literal UI line escapes are checked independently and are not
                // separators of a relative-file token across two display lines.
                if (value[index] == '\\' && index + 1 < value.Length && (value[index + 1] is 'n' or 'r' or 't'))
                {
                    continue;
                }

                if ((value[index] is '/' or '\\') && !Covered(index, 1))
                {
                    throw new InvalidDataException("Unrecognized relative-file paths require separate review.");
                }
            }
        }
    }
}
