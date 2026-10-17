using System.Globalization;
using System.Text;

namespace WolfConquer.Core.Client;

public sealed record LegacyMagicEffectTranslationResult(
    int SectionsScanned,
    int MatchedSections,
    int TranslatedRows,
    int ChangedRows,
    int UnmatchedSections,
    int UnmatchedRows)
{
    public bool Changed => ChangedRows > 0;
}

public static class LegacyMagicEffectTranslator
{
    private static readonly HashSet<string> VisibleTextKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Name",
        "Desc",
        "DescEx"
    };

    private static readonly Encoding MagicEffectEncoding =
        ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);

    public static LegacyMagicEffectTranslationResult Translate(
        string clientMagicEffectText,
        string referenceMagicEffectText,
        out string translatedText)
    {
        ArgumentNullException.ThrowIfNull(clientMagicEffectText);
        ArgumentNullException.ThrowIfNull(referenceMagicEffectText);

        var referenceSections = BuildReferenceSections(referenceMagicEffectText);
        var occurrenceById = new Dictionary<long, int>();
        var builder = new StringBuilder(clientMagicEffectText.Length);
        ReferenceMagicEffectSection? currentReferenceSection = null;
        var isInsideSection = false;

        var sectionsScanned = 0;
        var matchedSections = 0;
        var unmatchedSections = 0;
        var translatedRows = 0;
        var changedRows = 0;
        var unmatchedRows = 0;

        foreach (var line in SplitLines(clientMagicEffectText))
        {
            if (TryParseSection(line.Text, out var rawSection))
            {
                sectionsScanned++;
                isInsideSection = true;
                currentReferenceSection = null;

                if (TryParseSectionId(rawSection, out var sectionId))
                {
                    var occurrence = NextOccurrence(occurrenceById, sectionId);
                    var key = new MagicEffectSectionKey(sectionId, occurrence);
                    if (referenceSections.TryGetValue(key, out currentReferenceSection))
                    {
                        matchedSections++;
                    }
                    else
                    {
                        unmatchedSections++;
                    }
                }
                else
                {
                    unmatchedSections++;
                }

                AppendLine(builder, line);
                continue;
            }

            if (!TryParseKeyValue(line.Text, out var parsedKey, out _, out var prefix, out var separator) ||
                !VisibleTextKeys.Contains(parsedKey))
            {
                AppendLine(builder, line);
                continue;
            }

            if (currentReferenceSection is null)
            {
                if (isInsideSection)
                {
                    unmatchedRows++;
                }

                AppendLine(builder, line);
                continue;
            }

            if (!currentReferenceSection.VisibleTextValues.TryGetValue(parsedKey, out var referenceValue))
            {
                unmatchedRows++;
                AppendLine(builder, line);
                continue;
            }

            translatedRows++;
            var translatedLine = prefix + parsedKey + separator + referenceValue;
            if (!StringComparer.Ordinal.Equals(line.Text, translatedLine))
            {
                changedRows++;
            }

            builder.Append(translatedLine);
            builder.Append(line.LineEnding);
        }

        translatedText = builder.ToString();
        EnsureCp936Encodable(translatedText);

        return new LegacyMagicEffectTranslationResult(
            sectionsScanned,
            matchedSections,
            translatedRows,
            changedRows,
            unmatchedSections,
            unmatchedRows);
    }

    private static Dictionary<MagicEffectSectionKey, ReferenceMagicEffectSection> BuildReferenceSections(string text)
    {
        var occurrenceById = new Dictionary<long, int>();
        var sections = new Dictionary<MagicEffectSectionKey, ReferenceMagicEffectSection>();
        ReferenceMagicEffectSection? currentSection = null;

        foreach (var line in SplitLines(text))
        {
            if (TryParseSection(line.Text, out var rawSection))
            {
                currentSection = null;
                if (!TryParseSectionId(rawSection, out var sectionId))
                {
                    continue;
                }

                var occurrence = NextOccurrence(occurrenceById, sectionId);
                var sectionKey = new MagicEffectSectionKey(sectionId, occurrence);
                currentSection = new ReferenceMagicEffectSection(
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
                sections[sectionKey] = currentSection;
                continue;
            }

            if (currentSection is null ||
                !TryParseKeyValue(line.Text, out var parsedKey, out var value, out _, out _) ||
                !VisibleTextKeys.Contains(parsedKey) ||
                !IsUsefulEnglishValue(value))
            {
                continue;
            }

            currentSection.VisibleTextValues[parsedKey] = value;
        }

        return sections;
    }

    private static bool TryParseSection(string line, out string section)
    {
        var trimmed = line.Trim();
        if (trimmed.Length > 0 && trimmed[0] == '\uFEFF')
        {
            trimmed = trimmed[1..].Trim();
        }

        if (trimmed.Length > 2 && trimmed[0] == '[' && trimmed[^1] == ']')
        {
            section = trimmed[1..^1].Trim();
            return section.Length > 0;
        }

        section = string.Empty;
        return false;
    }

    private static bool TryParseSectionId(string rawSection, out long id)
    {
        return long.TryParse(rawSection, NumberStyles.None, CultureInfo.InvariantCulture, out id);
    }

    private static int NextOccurrence(Dictionary<long, int> occurrenceById, long id)
    {
        var occurrence = occurrenceById.TryGetValue(id, out var previousOccurrence)
            ? previousOccurrence + 1
            : 1;
        occurrenceById[id] = occurrence;
        return occurrence;
    }

    private static bool TryParseKeyValue(
        string line,
        out string key,
        out string value,
        out string prefix,
        out string separator)
    {
        key = string.Empty;
        value = string.Empty;
        prefix = string.Empty;
        separator = string.Empty;

        var trimmed = line.TrimStart();
        if (trimmed.Length == 0 ||
            trimmed.StartsWith('[') ||
            trimmed.StartsWith("//", StringComparison.Ordinal) ||
            trimmed.StartsWith(';') ||
            trimmed.StartsWith('#'))
        {
            return false;
        }

        var keyStart = 0;
        while (keyStart < line.Length && char.IsWhiteSpace(line[keyStart]))
        {
            keyStart++;
        }

        var equalsIndex = line.IndexOf('=', keyStart);
        if (equalsIndex < 0)
        {
            return false;
        }

        var keyEnd = equalsIndex;
        while (keyEnd > keyStart && char.IsWhiteSpace(line[keyEnd - 1]))
        {
            keyEnd--;
        }

        if (keyEnd <= keyStart)
        {
            return false;
        }

        var valueStart = equalsIndex + 1;
        while (valueStart < line.Length && char.IsWhiteSpace(line[valueStart]))
        {
            valueStart++;
        }

        prefix = line[..keyStart];
        key = line[keyStart..keyEnd];
        separator = line[keyEnd..valueStart];
        value = line[valueStart..].Trim();
        return true;
    }

    private static bool IsUsefulEnglishValue(string value)
    {
        if (value.Length == 0 ||
            value.Contains('\uFFFD', StringComparison.Ordinal) ||
            value.Contains("\u951f\u65a4\u62f7", StringComparison.Ordinal) ||
            ContainsCjkOrFullwidth(value) ||
            !ContainsAsciiLetter(value) ||
            !CanEncodeCp936(value))
        {
            return false;
        }

        return true;
    }

    private static bool ContainsAsciiLetter(string value)
    {
        foreach (var c in value)
        {
            if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsCjkOrFullwidth(string value)
    {
        foreach (var c in value)
        {
            if ((c >= '\u2E80' && c <= '\u9FFF') ||
                (c >= '\uF900' && c <= '\uFAFF') ||
                (c >= '\uFF00' && c <= '\uFFEF'))
            {
                return true;
            }
        }

        return false;
    }

    private static bool CanEncodeCp936(string value)
    {
        try
        {
            _ = MagicEffectEncoding.GetByteCount(value);
            return true;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    private static void EnsureCp936Encodable(string value)
    {
        _ = MagicEffectEncoding.GetBytes(value);
    }

    private static void AppendLine(StringBuilder builder, ClientTextLine line)
    {
        builder.Append(line.Text);
        builder.Append(line.LineEnding);
    }

    private static IEnumerable<ClientTextLine> SplitLines(string text)
    {
        var start = 0;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '\r' && text[index] != '\n')
            {
                continue;
            }

            var endingLength = text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n'
                ? 2
                : 1;
            yield return new ClientTextLine(text[start..index], text.Substring(index, endingLength));
            index += endingLength - 1;
            start = index + 1;
        }

        if (start < text.Length)
        {
            yield return new ClientTextLine(text[start..], string.Empty);
        }
    }

    private sealed record MagicEffectSectionKey(long Id, int Occurrence);

    private sealed record ReferenceMagicEffectSection(Dictionary<string, string> VisibleTextValues);
}
