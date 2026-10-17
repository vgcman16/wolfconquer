using System.Text;

namespace WolfConquer.Core.Client;

public sealed record LegacyMonsterSayTemplateTranslationResult(
    int KeyValueRowsScanned,
    int SupportedKeyRows,
    int AlreadyEnglishRows,
    int ChangedRows,
    int UnsupportedCjkRows)
{
    public int TranslatedRows => ChangedRows;

    public bool Changed => ChangedRows > 0;
}

public static class LegacyMonsterSayTemplateTranslator
{
    private static readonly IReadOnlyDictionary<string, string> Templates =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["battle"] = "Intruders! You dare enter my domain? Come and meet your fate!",
            ["blood10"] = "My patience is gone. None of you will leave this place alive!",
            ["blood50"] = "Struggle all you want. You cannot shake me.",
            ["blood80"] = "You overestimate yourselves. Kneel now, and I may leave you whole.",
            ["die"] = "This is not the end... I will return stronger.",
        };

    public static LegacyMonsterSayTemplateTranslationResult PatchFile(string targetPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.Utf8);
        var originalBytes = File.ReadAllBytes(targetPath);
        var originalText = encoding.GetString(originalBytes);
        var result = Translate(originalText, out var updatedText);
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

    public static LegacyMonsterSayTemplateTranslationResult Translate(string targetText, out string updatedText)
    {
        ArgumentNullException.ThrowIfNull(targetText);

        var lines = SplitLines(targetText).ToArray();
        var keyValueRowsScanned = 0;
        var supportedKeyRows = 0;
        var alreadyEnglishRows = 0;
        var changedRows = 0;
        var unsupportedCjkRows = 0;

        for (var index = 0; index < lines.Length; index++)
        {
            if (IsComment(lines[index].Text) ||
                !TryParseKeyValue(lines[index].Text, out var prefix, out var key, out var separator, out var value))
            {
                continue;
            }

            keyValueRowsScanned++;
            if (!TryGetSupportedPrefix(key, out var supportedPrefix))
            {
                if (ContainsCjk(value))
                {
                    unsupportedCjkRows++;
                }

                continue;
            }

            supportedKeyRows++;
            if (!ContainsCjk(value))
            {
                alreadyEnglishRows++;
                continue;
            }

            var translatedValue = TranslateValue(supportedPrefix, value);
            var updatedLine = prefix + key + separator + translatedValue;
            if (StringComparer.Ordinal.Equals(lines[index].Text, updatedLine))
            {
                continue;
            }

            lines[index] = lines[index] with
            {
                Text = updatedLine,
            };
            changedRows++;
        }

        var builder = new StringBuilder(targetText.Length);
        foreach (var line in lines)
        {
            builder.Append(line.Text);
            builder.Append(line.LineEnding);
        }

        updatedText = builder.ToString();
        return new LegacyMonsterSayTemplateTranslationResult(
            keyValueRowsScanned,
            supportedKeyRows,
            alreadyEnglishRows,
            changedRows,
            unsupportedCjkRows);
    }

    private static string TranslateValue(string supportedPrefix, string sourceValue)
    {
        var placeholders = ExtractPrintfPlaceholders(sourceValue).ToArray();
        var template = supportedPrefix.Equals("kill", StringComparison.OrdinalIgnoreCase)
            ? placeholders.Contains("%s", StringComparer.Ordinal)
                ? "%s has fallen. Who else dares challenge me?"
                : "Another foe has fallen. Who else dares challenge me?"
            : Templates[supportedPrefix];

        return PreserveMissingPlaceholders(template, placeholders);
    }

    private static string PreserveMissingPlaceholders(string template, IReadOnlyList<string> sourcePlaceholders)
    {
        if (sourcePlaceholders.Count == 0)
        {
            return template;
        }

        var missingPlaceholders = sourcePlaceholders.ToList();
        foreach (var templatePlaceholder in ExtractPrintfPlaceholders(template))
        {
            var matchedIndex = missingPlaceholders.FindIndex(
                placeholder => StringComparer.Ordinal.Equals(placeholder, templatePlaceholder));
            if (matchedIndex >= 0)
            {
                missingPlaceholders.RemoveAt(matchedIndex);
            }
        }

        return missingPlaceholders.Count == 0
            ? template
            : template + " " + string.Join(" ", missingPlaceholders);
    }

    private static IEnumerable<string> ExtractPrintfPlaceholders(string value)
    {
        for (var index = 0; index + 1 < value.Length; index++)
        {
            if (value[index] != '%')
            {
                continue;
            }

            var specifier = value[index + 1];
            if (specifier is 's' or 'u' or 'd' or '%')
            {
                yield return value.Substring(index, 2);
                index++;
            }
        }
    }

    private static bool TryGetSupportedPrefix(string key, out string supportedPrefix)
    {
        var underscoreIndex = key.IndexOf('_', StringComparison.Ordinal);
        var keyPrefix = underscoreIndex >= 0 ? key[..underscoreIndex] : key;
        if (Templates.ContainsKey(keyPrefix) ||
            keyPrefix.Equals("kill", StringComparison.OrdinalIgnoreCase))
        {
            supportedPrefix = keyPrefix;
            return true;
        }

        supportedPrefix = string.Empty;
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

    private static bool IsComment(string line)
    {
        foreach (var character in line)
        {
            if (char.IsWhiteSpace(character))
            {
                continue;
            }

            return character is ';' or '#';
        }

        return false;
    }

    private static bool ContainsCjk(string value)
    {
        foreach (var character in value)
        {
            if (character is '\uFFFD' or >= '\u3400' and <= '\u9FFF' or >= '\uF900' and <= '\uFAFF')
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
}
