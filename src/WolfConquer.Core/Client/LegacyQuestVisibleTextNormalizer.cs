using System.Text;

namespace WolfConquer.Core.Client;

internal static class LegacyQuestVisibleTextNormalizer
{
    private static readonly (string Artifact, string Replacement)[] TextReplacements =
    [
        ("\u9225\u6A9A", "'s"),
        ("\u9225\u6A9B", "'t"),
        ("\u9225\u6A99", "'r"),
        ("\u9225\u6A93", "'m"),
        ("\u9225\u6A9D", "'v"),
        ("\u9225\u6A92", "'l"),
        ("\u9225\u6A87", "'d"),
        ("\u9225\u69AE", "'s"),
    ];

    private const char CjkListMarkerArtifact = '\u8DEF';
    private const string CjkIdeographicSpaceArtifact = "\u9286\u20AC";

    public static string NormalizeReferenceValue(string value)
    {
        return NormalizeEnglishLikeMojibake(
            value
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim());
    }

    public static bool TryNormalizeAlreadyEnglishValue(string value, out string normalizedValue)
    {
        normalizedValue = NormalizeEnglishLikeMojibake(value);
        return !StringComparer.Ordinal.Equals(value, normalizedValue);
    }

    public static bool ContainsCjkOrReplacement(string value)
    {
        foreach (var character in value)
        {
            if (character is '\uFFFD' ||
                character is >= '\u3000' and <= '\u303F' ||
                character is >= '\u3040' and <= '\u30FF' ||
                character is >= '\u3400' and <= '\u9FFF' ||
                character is >= '\uAC00' and <= '\uD7AF' ||
                character is >= '\uF900' and <= '\uFAFF')
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeEnglishLikeMojibake(string value)
    {
        if (value.Length == 0 ||
            !ContainsKnownArtifact(value) ||
            !LooksMostlyAsciiEnglish(value))
        {
            return value;
        }

        var normalized = value;
        foreach (var replacement in TextReplacements)
        {
            normalized = normalized.Replace(replacement.Artifact, replacement.Replacement, StringComparison.Ordinal);
        }

        normalized = ReplaceLineLeadingListMarkerArtifacts(normalized);
        normalized = ReplaceQuoteOrDashFragments(normalized, "\u9225?");
        normalized = ReplaceQuoteOrDashFragments(normalized, "\u9225\uFFFD");
        normalized = ReplaceCjkFullStopArtifacts(normalized, "\u9286?");
        normalized = ReplaceCjkFullStopArtifacts(normalized, "\u9286\uFFFD");
        normalized = ReplaceIdeographicSpaceArtifacts(normalized, CjkIdeographicSpaceArtifact);

        return ContainsCjkOrReplacement(normalized) ? value : normalized;
    }

    private static bool ContainsKnownArtifact(string value)
    {
        if (value.Contains("\u9286?", StringComparison.Ordinal) ||
            value.Contains("\u9286\uFFFD", StringComparison.Ordinal) ||
            value.Contains("\u9225?", StringComparison.Ordinal) ||
            value.Contains("\u9225\uFFFD", StringComparison.Ordinal) ||
            value.Contains(CjkIdeographicSpaceArtifact, StringComparison.Ordinal))
        {
            return true;
        }

        if (ContainsLineLeadingListMarkerArtifact(value))
        {
            return true;
        }

        foreach (var replacement in TextReplacements)
        {
            if (value.Contains(replacement.Artifact, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsLineLeadingListMarkerArtifact(string value)
    {
        var markerIndex = value.IndexOf(CjkListMarkerArtifact);
        while (markerIndex >= 0)
        {
            if (IsLogicalLinePrefix(value, markerIndex))
            {
                return true;
            }

            markerIndex = value.IndexOf(CjkListMarkerArtifact, markerIndex + 1);
        }

        return false;
    }

    private static bool LooksMostlyAsciiEnglish(string value)
    {
        var asciiLetters = 0;
        var asciiReadable = 0;
        var nonAscii = 0;

        foreach (var character in value)
        {
            if (character <= '\u007f')
            {
                if (!char.IsControl(character))
                {
                    asciiReadable++;
                }

                if (char.IsAsciiLetter(character))
                {
                    asciiLetters++;
                }
            }
            else
            {
                nonAscii++;
            }
        }

        return asciiLetters >= 2 && asciiReadable >= nonAscii;
    }

    private static string ReplaceQuoteOrDashFragments(string value, string artifact)
    {
        var builder = default(StringBuilder);
        var index = 0;
        while (true)
        {
            var artifactIndex = value.IndexOf(artifact, index, StringComparison.Ordinal);
            if (artifactIndex < 0)
            {
                break;
            }

            builder ??= new StringBuilder(value.Length);
            builder.Append(value, index, artifactIndex - index);

            var previous = FindPreviousNonWhitespace(value, artifactIndex - 1);
            var next = FindNextNonWhitespace(value, artifactIndex + artifact.Length);
            builder.Append(IsWhitespaceBounded(value, artifactIndex, artifact.Length, previous, next) ? '-' : '\'');
            index = artifactIndex + artifact.Length;
        }

        if (builder is null)
        {
            return value;
        }

        builder.Append(value, index, value.Length - index);
        return builder.ToString();
    }

    private static string ReplaceLineLeadingListMarkerArtifacts(string value)
    {
        var builder = default(StringBuilder);
        var index = 0;
        while (index < value.Length)
        {
            var markerIndex = value.IndexOf(CjkListMarkerArtifact, index);
            if (markerIndex < 0)
            {
                break;
            }

            if (!IsLogicalLinePrefix(value, markerIndex))
            {
                index = markerIndex + 1;
                continue;
            }

            var nextIndex = markerIndex + 1;
            while (nextIndex < value.Length && char.IsWhiteSpace(value[nextIndex]) && value[nextIndex] is not '\r' and not '\n')
            {
                nextIndex++;
            }

            if (nextIndex >= value.Length || value[nextIndex] is '\r' or '\n')
            {
                index = markerIndex + 1;
                continue;
            }

            builder ??= new StringBuilder(value.Length + 2);
            builder.Append(value, index, markerIndex - index);
            builder.Append("- ");
            index = nextIndex;
        }

        if (builder is null)
        {
            return value;
        }

        builder.Append(value, index, value.Length - index);
        return builder.ToString();
    }

    private static bool IsLogicalLinePrefix(string value, int markerIndex)
    {
        for (var index = markerIndex - 1; index >= 0; index--)
        {
            if (value[index] is '\r' or '\n')
            {
                return true;
            }

            if (value[index] == 'n' && index > 0 && value[index - 1] == '\\')
            {
                return true;
            }

            if (!char.IsWhiteSpace(value[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static string ReplaceCjkFullStopArtifacts(string value, string artifact)
    {
        var builder = default(StringBuilder);
        var index = 0;
        while (true)
        {
            var artifactIndex = value.IndexOf(artifact, index, StringComparison.Ordinal);
            if (artifactIndex < 0)
            {
                break;
            }

            builder ??= new StringBuilder(value.Length);
            builder.Append(value, index, artifactIndex - index);

            var previous = FindPreviousNonWhitespace(value, artifactIndex - 1);
            var next = FindNextNonWhitespace(value, artifactIndex + artifact.Length);
            if (!IsSentencePunctuation(previous) && !IsSentencePunctuation(next))
            {
                builder.Append('.');
            }

            index = artifactIndex + artifact.Length;
        }

        if (builder is null)
        {
            return value;
        }

        builder.Append(value, index, value.Length - index);
        return builder.ToString();
    }

    private static string ReplaceIdeographicSpaceArtifacts(string value, string artifact)
    {
        var builder = default(StringBuilder);
        var index = 0;
        while (true)
        {
            var artifactIndex = value.IndexOf(artifact, index, StringComparison.Ordinal);
            if (artifactIndex < 0)
            {
                break;
            }

            builder ??= new StringBuilder(value.Length);
            builder.Append(value, index, artifactIndex - index);

            var previous = FindPreviousNonWhitespace(value, artifactIndex - 1);
            var next = FindNextNonWhitespace(value, artifactIndex + artifact.Length);
            if (previous is not null && next is not null)
            {
                builder.Append(' ');
            }

            index = artifactIndex + artifact.Length;
        }

        if (builder is null)
        {
            return value;
        }

        builder.Append(value, index, value.Length - index);
        return builder.ToString().TrimEnd();
    }

    private static bool IsWhitespaceBounded(
        string value,
        int artifactIndex,
        int artifactLength,
        char? previous,
        char? next)
    {
        return previous is not null &&
            next is not null &&
            artifactIndex > 0 &&
            artifactIndex + artifactLength < value.Length &&
            char.IsWhiteSpace(value[artifactIndex - 1]) &&
            char.IsWhiteSpace(value[artifactIndex + artifactLength]);
    }

    private static bool IsSentencePunctuation(char? character)
    {
        return character is '.' or '!' or '?' or ',' or ';' or ':';
    }

    private static char? FindPreviousNonWhitespace(string value, int startIndex)
    {
        for (var index = startIndex; index >= 0; index--)
        {
            if (!char.IsWhiteSpace(value[index]))
            {
                return value[index];
            }
        }

        return null;
    }

    private static char? FindNextNonWhitespace(string value, int startIndex)
    {
        for (var index = startIndex; index < value.Length; index++)
        {
            if (!char.IsWhiteSpace(value[index]))
            {
                return value[index];
            }
        }

        return null;
    }

    private static int? FindNextNonWhitespaceIndex(string value, int startIndex)
    {
        for (var index = startIndex; index < value.Length; index++)
        {
            if (!char.IsWhiteSpace(value[index]))
            {
                return index;
            }
        }

        return null;
    }
}
