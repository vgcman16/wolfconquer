using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace WolfConquer.Core.Client;

public sealed record LegacyRuneDescTranslationResult(
    int SourceDescriptionsScanned,
    int SourceDescriptions,
    IReadOnlyList<int> ConflictingSourceIds,
    int RowCount,
    int TranslatedRows,
    int UnmatchedRows,
    int UniqueRuneIds,
    int MatchedUniqueRuneIds,
    IReadOnlyList<int> UnmatchedRuneIds,
    int ChangedRows,
    bool Changed);

public static class LegacyRuneDescTranslator
{
    private static readonly Regex ServerKeyItemDescPattern = new(
        "^\\s*\\[\"STR_ITEM_ITEMDESC_(?<id>\\d+)\"\\]\\s*=\\s*\"(?<value>(?:\\\\.|[^\"\\\\])*)\"\\s*,?.*$",
        RegexOptions.CultureInvariant);

    public static LegacyRuneDescTranslationResult PatchFile(string runeDescPath, IEnumerable<string> sourcePaths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runeDescPath);
        ArgumentNullException.ThrowIfNull(sourcePaths);

        var runeDescBytes = File.ReadAllBytes(runeDescPath);
        var runeDescText = DecodeUtf8(runeDescBytes, out var hadUtf8Bom);
        var sourceTexts = sourcePaths
            .Select(ReadSourceText)
            .ToArray();

        var result = Translate(runeDescText, sourceTexts, out var updatedText);
        if (result.Changed)
        {
            File.WriteAllBytes(runeDescPath, EncodeUtf8(updatedText, hadUtf8Bom));
        }

        return result;
    }

    public static LegacyRuneDescTranslationResult Translate(
        string runeDescText,
        IEnumerable<string> sourceTexts,
        out string updatedText)
    {
        ArgumentNullException.ThrowIfNull(runeDescText);
        ArgumentNullException.ThrowIfNull(sourceTexts);

        var source = BuildSourceDescriptions(sourceTexts);
        var builder = new StringBuilder(runeDescText.Length);
        var uniqueRuneIds = new HashSet<int>();
        var matchedUniqueRuneIds = new HashSet<int>();
        var unmatchedRuneIds = new HashSet<int>();
        int? currentRuneId = null;
        var rowCount = 0;
        var translatedRows = 0;
        var unmatchedRows = 0;
        var changedRows = 0;

        foreach (var line in SplitLines(runeDescText))
        {
            if (TryParseSectionId(line.Text, out var sectionId))
            {
                currentRuneId = sectionId;
                AppendLine(builder, line);
                continue;
            }

            if (currentRuneId is not { } runeId ||
                !TryParseDescLine(line.Text, out var prefix, out _))
            {
                AppendLine(builder, line);
                continue;
            }

            rowCount++;
            uniqueRuneIds.Add(runeId);
            if (!source.Descriptions.TryGetValue(runeId, out var description))
            {
                unmatchedRows++;
                unmatchedRuneIds.Add(runeId);
                AppendLine(builder, line);
                continue;
            }

            translatedRows++;
            matchedUniqueRuneIds.Add(runeId);
            var translatedLine = prefix + description;
            if (!StringComparer.Ordinal.Equals(line.Text, translatedLine))
            {
                changedRows++;
            }

            builder.Append(translatedLine);
            builder.Append(line.LineEnding);
        }

        updatedText = builder.ToString();
        return new LegacyRuneDescTranslationResult(
            source.Scanned,
            source.Descriptions.Count,
            source.Conflicts.Order().ToArray(),
            rowCount,
            translatedRows,
            unmatchedRows,
            uniqueRuneIds.Count,
            matchedUniqueRuneIds.Count,
            unmatchedRuneIds.Order().ToArray(),
            changedRows,
            !StringComparer.Ordinal.Equals(runeDescText, updatedText));
    }

    private static RuneDescSourceCatalog BuildSourceDescriptions(IEnumerable<string> sourceTexts)
    {
        var descriptions = new Dictionary<int, string>();
        var conflicts = new HashSet<int>();
        var scanned = 0;

        foreach (var sourceText in sourceTexts)
        {
            AddServerKeySourceDescriptions(sourceText, descriptions, conflicts, ref scanned);
            AddRuneDescSourceDescriptions(sourceText, descriptions, conflicts, ref scanned);
            AddItemTypeSourceDescriptions(sourceText, descriptions, conflicts, ref scanned);
        }

        return new RuneDescSourceCatalog(descriptions, conflicts, scanned);
    }

    private static void AddServerKeySourceDescriptions(
        string sourceText,
        Dictionary<int, string> descriptions,
        HashSet<int> conflicts,
        ref int scanned)
    {
        foreach (var line in SplitLines(sourceText))
        {
            var match = ServerKeyItemDescPattern.Match(line.Text);
            if (!match.Success ||
                !int.TryParse(match.Groups["id"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var itemId))
            {
                continue;
            }

            scanned++;
            AddSourceDescription(
                descriptions,
                conflicts,
                itemId,
                NormalizeDescription(UnescapeLuaString(match.Groups["value"].Value)));
        }
    }

    private static void AddRuneDescSourceDescriptions(
        string sourceText,
        Dictionary<int, string> descriptions,
        HashSet<int> conflicts,
        ref int scanned)
    {
        int? currentRuneId = null;
        foreach (var line in SplitLines(sourceText))
        {
            if (TryParseSectionId(line.Text, out var sectionId))
            {
                currentRuneId = sectionId;
                continue;
            }

            if (currentRuneId is not { } runeId ||
                !TryParseDescLine(line.Text, out _, out var description))
            {
                continue;
            }

            scanned++;
            AddSourceDescription(descriptions, conflicts, runeId, NormalizeDescription(description));
        }
    }

    private static void AddItemTypeSourceDescriptions(
        string sourceText,
        Dictionary<int, string> descriptions,
        HashSet<int> conflicts,
        ref int scanned)
    {
        foreach (var line in SplitLines(sourceText))
        {
            var fields = line.Text.Split("@@");
            if (fields.Length <= 53 ||
                !int.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out var itemId))
            {
                continue;
            }

            scanned++;
            AddSourceDescription(descriptions, conflicts, itemId, NormalizeDescription(fields[53]));
        }
    }

    private static void AddSourceDescription(
        Dictionary<int, string> descriptions,
        HashSet<int> conflicts,
        int itemId,
        string description)
    {
        if (itemId <= 0 ||
            conflicts.Contains(itemId) ||
            !IsUsefulEnglishValue(description))
        {
            return;
        }

        if (descriptions.TryGetValue(itemId, out var existing))
        {
            if (!StringComparer.Ordinal.Equals(existing, description))
            {
                descriptions.Remove(itemId);
                conflicts.Add(itemId);
            }

            return;
        }

        descriptions.Add(itemId, description);
    }

    private static bool TryParseSectionId(string line, out int id)
    {
        var trimmed = line.Trim();
        if (trimmed.Length > 0 && trimmed[0] == '\uFEFF')
        {
            trimmed = trimmed[1..].Trim();
        }

        if (trimmed.Length > 2 &&
            trimmed[0] == '[' &&
            trimmed[^1] == ']' &&
            int.TryParse(trimmed[1..^1], NumberStyles.None, CultureInfo.InvariantCulture, out id))
        {
            return true;
        }

        id = 0;
        return false;
    }

    private static bool TryParseDescLine(string line, out string prefix, out string value)
    {
        prefix = string.Empty;
        value = string.Empty;
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

        if (!line[keyStart..keyEnd].Equals("Desc", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var valueStart = equalsIndex + 1;
        while (valueStart < line.Length && char.IsWhiteSpace(line[valueStart]))
        {
            valueStart++;
        }

        prefix = line[..valueStart];
        value = line[valueStart..].Trim();
        return true;
    }

    private static string UnescapeLuaString(string value)
    {
        var builder = new StringBuilder(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character != '\\' || index + 1 >= value.Length)
            {
                builder.Append(character);
                continue;
            }

            var escaped = value[++index];
            switch (escaped)
            {
                case '\\':
                case '"':
                    builder.Append(escaped);
                    break;
                case 'n':
                case 'r':
                case 't':
                    builder.Append(' ');
                    break;
                default:
                    builder.Append('\\');
                    builder.Append(escaped);
                    break;
            }
        }

        return builder.ToString();
    }

    private static string NormalizeDescription(string value)
    {
        return value
            .Replace('~', ' ')
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
    }

    private static bool IsUsefulEnglishValue(string value)
    {
        return value.Length > 0 &&
            ContainsAsciiLetter(value) &&
            !ContainsCjkOrFullwidth(value) &&
            !value.Contains('\uFFFD', StringComparison.Ordinal);
    }

    private static bool ContainsAsciiLetter(string value)
    {
        foreach (var character in value)
        {
            if ((character >= 'A' && character <= 'Z') ||
                (character >= 'a' && character <= 'z'))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsCjkOrFullwidth(string value)
    {
        foreach (var character in value)
        {
            if ((character >= '\u2E80' && character <= '\u9FFF') ||
                (character >= '\uF900' && character <= '\uFAFF') ||
                (character >= '\uFF00' && character <= '\uFFEF'))
            {
                return true;
            }
        }

        return false;
    }

    private static string DecodeUtf8(byte[] bytes, out bool hadUtf8Bom)
    {
        var offset = HasUtf8Bom(bytes) ? 3 : 0;
        hadUtf8Bom = offset > 0;
        return ClientTextEncodings.GetEncoding(ClientTextEncodingKind.Utf8)
            .GetString(bytes, offset, bytes.Length - offset);
    }

    private static byte[] EncodeUtf8(string text, bool emitUtf8Bom)
    {
        var textBytes = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.Utf8).GetBytes(text);
        if (!emitUtf8Bom)
        {
            return textBytes;
        }

        var bytes = new byte[textBytes.Length + 3];
        bytes[0] = 0xEF;
        bytes[1] = 0xBB;
        bytes[2] = 0xBF;
        Buffer.BlockCopy(textBytes, 0, bytes, 3, textBytes.Length);
        return bytes;
    }

    private static bool HasUtf8Bom(byte[] bytes)
    {
        return bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
    }

    private static string ReadSourceText(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return Path.GetFileName(path).Equals("RuneDesc.ini", StringComparison.OrdinalIgnoreCase)
            ? DecodeRuneDescSourceText(bytes)
            : ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936).GetString(bytes);
    }

    private static string DecodeRuneDescSourceText(byte[] bytes)
    {
        try
        {
            return DecodeUtf8(bytes, out _);
        }
        catch (DecoderFallbackException)
        {
            return ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936).GetString(bytes);
        }
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

    private sealed record RuneDescSourceCatalog(
        Dictionary<int, string> Descriptions,
        HashSet<int> Conflicts,
        int Scanned);
}
