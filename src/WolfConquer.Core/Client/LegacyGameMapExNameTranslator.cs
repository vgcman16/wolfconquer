using System.Globalization;
using System.Text;

namespace WolfConquer.Core.Client;

public sealed record LegacyGameMapExNameTranslationResult(
    int ReferenceMapsScanned,
    int TargetMapsScanned,
    int TranslatableNames,
    int MatchedNames,
    int ChangedNames,
    int UnmatchedNames,
    bool Changed);

public static class LegacyGameMapExNameTranslator
{
    public static LegacyGameMapExNameTranslationResult Translate(
        string targetText,
        string referenceText,
        out string updatedText)
    {
        ArgumentNullException.ThrowIfNull(targetText);
        ArgumentNullException.ThrowIfNull(referenceText);

        var referenceNames = ReadReferenceNames(referenceText);
        var lines = SplitLines(targetText).ToArray();
        var targetMapsScanned = 0;
        var translatableNames = 0;
        var matchedNames = 0;
        var changedNames = 0;
        var unmatchedNames = 0;
        var changed = false;
        int? currentMapId = null;

        for (var index = 0; index < lines.Length; index++)
        {
            if (TryParseSection(lines[index].Text, out var mapId))
            {
                currentMapId = mapId;
                targetMapsScanned++;
                continue;
            }

            if (currentMapId is not { } id ||
                !TryParseKeyValue(lines[index].Text, out var prefix, out var key, out var separator, out var value) ||
                !key.Equals("Name", StringComparison.OrdinalIgnoreCase) ||
                !NeedsTranslation(value))
            {
                continue;
            }

            translatableNames++;
            if (!referenceNames.TryGetValue(id, out var referenceName))
            {
                unmatchedNames++;
                continue;
            }

            matchedNames++;
            var translatedLine = prefix + key + separator + referenceName;
            if (!StringComparer.Ordinal.Equals(lines[index].Text, translatedLine))
            {
                lines[index] = lines[index] with
                {
                    Text = translatedLine,
                };
                changedNames++;
                changed = true;
            }
        }

        var builder = new StringBuilder(targetText.Length);
        foreach (var line in lines)
        {
            builder.Append(line.Text);
            builder.Append(line.LineEnding);
        }

        updatedText = builder.ToString();
        return new LegacyGameMapExNameTranslationResult(
            referenceNames.Count,
            targetMapsScanned,
            translatableNames,
            matchedNames,
            changedNames,
            unmatchedNames,
            changed);
    }

    public static LegacyGameMapExNameTranslationResult PatchFile(string targetPath, string referencePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(referencePath);

        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        var originalBytes = File.ReadAllBytes(targetPath);
        var originalText = encoding.GetString(originalBytes);
        var referenceText = encoding.GetString(File.ReadAllBytes(referencePath));
        var result = Translate(originalText, referenceText, out var updatedText);
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

    private static IReadOnlyDictionary<int, string> ReadReferenceNames(string text)
    {
        var names = new Dictionary<int, string>();
        int? currentMapId = null;

        foreach (var line in SplitLines(text))
        {
            if (TryParseSection(line.Text, out var mapId))
            {
                currentMapId = mapId;
                continue;
            }

            if (currentMapId is { } id &&
                TryParseKeyValue(line.Text, out _, out var key, out _, out var value) &&
                key.Equals("Name", StringComparison.OrdinalIgnoreCase) &&
                IsUsableReferenceName(value))
            {
                names.TryAdd(id, NormalizeName(value));
            }
        }

        return names;
    }

    private static bool TryParseSection(string line, out int mapId)
    {
        var trimmed = line.Trim();
        if (trimmed.Length > 2 &&
            trimmed[0] == '[' &&
            trimmed[^1] == ']' &&
            int.TryParse(trimmed[1..^1], NumberStyles.None, CultureInfo.InvariantCulture, out mapId))
        {
            return true;
        }

        mapId = 0;
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

    private static bool NeedsTranslation(string value)
    {
        foreach (var character in value)
        {
            if (character is '\uFFFD' or >= '\u3400' and <= '\u9fff')
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsUsableReferenceName(string value)
    {
        var normalized = NormalizeName(value);
        if (normalized.Length == 0)
        {
            return false;
        }

        foreach (var character in normalized)
        {
            if (character is '\uFFFD' or >= '\u3400' and <= '\u9fff' or >= '\u0600' and <= '\u06ff')
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizeName(string value)
    {
        return value
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
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
}
