using System.Globalization;
using System.Text;

namespace WolfConquer.Core.Client;

public sealed record LegacyDbVisibleTextTranslationResult(
    int ReplacementRows,
    int VisibleTextRowsScanned,
    int MatchedSourceRows,
    int AlreadyTranslatedRows,
    int ChangedRows,
    int UnmatchedCjkRows)
{
    public int TranslatedRows => MatchedSourceRows + AlreadyTranslatedRows;

    public bool Changed => ChangedRows > 0;
}

public static class LegacyDbVisibleTextTranslator
{
    private static readonly HashSet<string> VisibleTextKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Name",
        "Desc",
        "DescEx"
    };

    public static LegacyDbVisibleTextTranslationResult Translate(
        string targetText,
        IEnumerable<LegacyDbVisibleTextReplacement> replacements,
        out string updatedText)
    {
        ArgumentNullException.ThrowIfNull(targetText);
        ArgumentNullException.ThrowIfNull(replacements);

        var replacementRows = replacements.ToArray();
        var replacementsByField = replacementRows
            .GroupBy(
                row => new FieldKey(row.Section, row.Key),
                row => row,
                FieldKeyComparer.Instance)
            .ToDictionary(
                group => group.Key,
                group => group.ToArray(),
                FieldKeyComparer.Instance);
        var lines = SplitLines(targetText).ToArray();
        var visibleTextRowsScanned = 0;
        var matchedSourceRows = 0;
        var alreadyTranslatedRows = 0;
        var changedRows = 0;
        var unmatchedCjkRows = 0;
        string? currentSection = null;

        for (var index = 0; index < lines.Length; index++)
        {
            if (TryApplyDelimitedRowReplacements(
                    lines[index].Text,
                    replacementsByField,
                    out var updatedDelimitedLine,
                    out var delimitedRowsScanned,
                    out var delimitedMatchedRows,
                    out var delimitedAlreadyTranslatedRows,
                    out var delimitedUnmatchedCjkRows))
            {
                visibleTextRowsScanned += delimitedRowsScanned;
                matchedSourceRows += delimitedMatchedRows;
                alreadyTranslatedRows += delimitedAlreadyTranslatedRows;
                unmatchedCjkRows += delimitedUnmatchedCjkRows;
                if (!StringComparer.Ordinal.Equals(lines[index].Text, updatedDelimitedLine))
                {
                    lines[index] = lines[index] with
                    {
                        Text = updatedDelimitedLine,
                    };
                    changedRows++;
                }

                continue;
            }

            if (TryParseSection(lines[index].Text, out var section))
            {
                currentSection = section;
                continue;
            }

            if (currentSection is null ||
                !TryParseKeyValue(lines[index].Text, out var prefix, out var key, out var separator, out var value) ||
                !VisibleTextKeys.Contains(key))
            {
                continue;
            }

            visibleTextRowsScanned++;
            var fieldKey = new FieldKey(currentSection, key);
            if (replacementsByField.TryGetValue(fieldKey, out var fieldReplacements) &&
                TryApplyReplacement(lines[index].Text, fieldReplacements, value, prefix, key, separator, out var updatedLine, out var alreadyTranslated))
            {
                if (alreadyTranslated)
                {
                    alreadyTranslatedRows++;
                    continue;
                }

                matchedSourceRows++;
                if (!StringComparer.Ordinal.Equals(lines[index].Text, updatedLine))
                {
                    lines[index] = lines[index] with
                    {
                        Text = updatedLine,
                    };
                    changedRows++;
                }

                continue;
            }

            if (ContainsCjkOrReplacement(value))
            {
                unmatchedCjkRows++;
            }
        }

        var builder = new StringBuilder(targetText.Length);
        foreach (var line in lines)
        {
            builder.Append(line.Text);
            builder.Append(line.LineEnding);
        }

        updatedText = builder.ToString();
        return new LegacyDbVisibleTextTranslationResult(
            replacementRows.Length,
            visibleTextRowsScanned,
            matchedSourceRows,
            alreadyTranslatedRows,
            changedRows,
            unmatchedCjkRows);
    }

    public static LegacyDbVisibleTextTranslationResult PatchFile(
        string targetPath,
        IEnumerable<LegacyDbVisibleTextReplacement> replacements)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentNullException.ThrowIfNull(replacements);

        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        var originalBytes = File.ReadAllBytes(targetPath);
        var originalText = encoding.GetString(originalBytes);
        var result = Translate(originalText, replacements, out var updatedText);
        if (result.Changed)
        {
            ClientTextPatchWriter.WritePatchedTextPreservingUnchangedBytes(
                targetPath,
                originalBytes,
                originalText,
                updatedText,
                encoding);
        }

        return result;
    }

    private static bool TryApplyReplacement(
        string originalLine,
        IReadOnlyList<LegacyDbVisibleTextReplacement> replacements,
        string value,
        string prefix,
        string key,
        string separator,
        out string updatedLine,
        out bool alreadyTranslated)
    {
        foreach (var replacement in replacements)
        {
            if (StringComparer.Ordinal.Equals(value, replacement.ReplacementValue))
            {
                updatedLine = originalLine;
                alreadyTranslated = true;
                return true;
            }

            if (StringComparer.Ordinal.Equals(value, replacement.SourceValue))
            {
                updatedLine = prefix + key + separator + replacement.ReplacementValue;
                alreadyTranslated = false;
                return true;
            }
        }

        updatedLine = originalLine;
        alreadyTranslated = false;
        return false;
    }

    private static bool TryApplyDelimitedRowReplacements(
        string line,
        IReadOnlyDictionary<FieldKey, LegacyDbVisibleTextReplacement[]> replacementsByField,
        out string updatedLine,
        out int visibleTextRowsScanned,
        out int matchedSourceRows,
        out int alreadyTranslatedRows,
        out int unmatchedCjkRows)
    {
        updatedLine = line;
        visibleTextRowsScanned = 0;
        matchedSourceRows = 0;
        alreadyTranslatedRows = 0;
        unmatchedCjkRows = 0;

        if (!line.Contains("@@", StringComparison.Ordinal))
        {
            return false;
        }

        var fields = line.Split("@@", StringSplitOptions.None);
        if (fields.Length < 2 || fields[0].Length == 0)
        {
            return false;
        }

        var handledAnyField = false;
        foreach (var fieldKey in replacementsByField.Keys.Where(key => StringComparer.Ordinal.Equals(key.Section, fields[0])))
        {
            if (!TryParseDelimitedFieldKey(fieldKey.Key, out var fieldIndex) || fieldIndex >= fields.Length)
            {
                continue;
            }

            handledAnyField = true;
            visibleTextRowsScanned++;
            var value = fields[fieldIndex];
            var replacements = replacementsByField[fieldKey];
            if (TryGetDelimitedReplacement(replacements, value, out var replacementValue, out var alreadyTranslated))
            {
                if (alreadyTranslated)
                {
                    alreadyTranslatedRows++;
                    continue;
                }

                fields[fieldIndex] = replacementValue;
                matchedSourceRows++;
                continue;
            }

            if (ContainsCjkOrReplacement(value))
            {
                unmatchedCjkRows++;
            }
        }

        if (!handledAnyField)
        {
            return false;
        }

        updatedLine = string.Join("@@", fields);
        return true;
    }

    private static bool TryGetDelimitedReplacement(
        IReadOnlyList<LegacyDbVisibleTextReplacement> replacements,
        string value,
        out string replacementValue,
        out bool alreadyTranslated)
    {
        foreach (var replacement in replacements)
        {
            if (StringComparer.Ordinal.Equals(value, replacement.ReplacementValue))
            {
                replacementValue = value;
                alreadyTranslated = true;
                return true;
            }

            if (StringComparer.Ordinal.Equals(value, replacement.SourceValue))
            {
                replacementValue = replacement.ReplacementValue;
                alreadyTranslated = false;
                return true;
            }
        }

        replacementValue = value;
        alreadyTranslated = false;
        return false;
    }

    private static bool TryParseDelimitedFieldKey(string key, out int fieldIndex)
    {
        fieldIndex = 0;
        return key.StartsWith("Field", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(key[5..], NumberStyles.None, CultureInfo.InvariantCulture, out fieldIndex) &&
            fieldIndex >= 0;
    }

    private static bool TryParseSection(string line, out string section)
    {
        var trimmed = line.Trim();
        if (trimmed.Length > 2 && trimmed[0] == '[' && trimmed[^1] == ']')
        {
            section = trimmed[1..^1].Trim();
            return section.Length > 0;
        }

        section = string.Empty;
        return false;
    }

    private static bool TryParseKeyValue(
        string line,
        out string prefix,
        out string key,
        out string separator,
        out string value)
    {
        prefix = string.Empty;
        key = string.Empty;
        separator = string.Empty;
        value = string.Empty;

        var keyStart = 0;
        while (keyStart < line.Length && char.IsWhiteSpace(line[keyStart]))
        {
            keyStart++;
        }

        var equalsIndex = line.IndexOf('=', keyStart);
        if (equalsIndex <= keyStart)
        {
            return false;
        }

        var keyEnd = equalsIndex;
        while (keyEnd > keyStart && char.IsWhiteSpace(line[keyEnd - 1]))
        {
            keyEnd--;
        }

        var valueStart = equalsIndex + 1;
        while (valueStart < line.Length && char.IsWhiteSpace(line[valueStart]))
        {
            valueStart++;
        }

        prefix = line[..keyStart];
        key = line[keyStart..keyEnd];
        separator = line[keyEnd..valueStart];
        value = line[valueStart..];
        return key.Length > 0;
    }

    private static bool ContainsCjkOrReplacement(string value)
    {
        foreach (var character in value)
        {
            if (character is '\uFFFD' or >= '\u3400' and <= '\u9FFF' or >= '\uFF00' and <= '\uFFEF')
            {
                return true;
            }
        }

        return false;
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

    private sealed record FieldKey(string Section, string Key);

    private sealed class FieldKeyComparer : IEqualityComparer<FieldKey>
    {
        public static readonly FieldKeyComparer Instance = new();

        public bool Equals(FieldKey? x, FieldKey? y)
        {
            return x is not null &&
                y is not null &&
                StringComparer.Ordinal.Equals(x.Section, y.Section) &&
                StringComparer.OrdinalIgnoreCase.Equals(x.Key, y.Key);
        }

        public int GetHashCode(FieldKey obj)
        {
            return HashCode.Combine(
                StringComparer.Ordinal.GetHashCode(obj.Section),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Key));
        }
    }
}
