using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace WolfConquer.Core.Client;

public sealed record LegacyServerKeyItemNameTranslationResult(
    int KeyCount,
    int TranslatedKeys,
    int UnmatchedKeys,
    int UniqueItemIds,
    int MatchedUniqueItemIds,
    IReadOnlyList<int> UnmatchedItemIds,
    IReadOnlyList<int> ConflictingItemIds,
    int ChangedKeys,
    bool Changed);

public static class LegacyServerKeyItemNameTranslator
{
    private static readonly Regex ServerKeyItemTextPattern = new(
        "^(?<prefix>\\s*\\[\"(?<key>STR_[A-Z0-9_]+)\"\\]\\s*=\\s*\")(?<name>(?:\\\\.|[^\"\\\\])*)(?<suffix>\"\\s*,.*)$",
        RegexOptions.CultureInvariant);

    public static LegacyServerKeyItemNameTranslationResult PatchFile(string serverKeyPath, string itemTypePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemTypePath);
        return PatchFile(serverKeyPath, [itemTypePath]);
    }

    public static LegacyServerKeyItemNameTranslationResult PatchFile(
        string serverKeyPath,
        IEnumerable<string> sourcePaths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverKeyPath);
        ArgumentNullException.ThrowIfNull(sourcePaths);

        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        var originalBytes = File.ReadAllBytes(serverKeyPath);
        var serverKeyText = encoding.GetString(originalBytes);
        var itemTexts = BuildItemTextsFromSourcePaths(sourcePaths);
        var result = Translate(serverKeyText, itemTexts, out var updatedText);
        if (result.Changed)
        {
            ClientTextPatchWriter.WritePatchedTextPreservingUnchangedBytes(
                serverKeyPath,
                originalBytes,
                serverKeyText,
                updatedText,
                encoding);
        }

        return result;
    }

    public static LegacyServerKeyItemNameTranslationResult Translate(
        string serverKeyText,
        IEnumerable<string> itemTypeLines,
        out string updatedText)
    {
        ArgumentNullException.ThrowIfNull(serverKeyText);
        ArgumentNullException.ThrowIfNull(itemTypeLines);

        var itemTexts = BuildItemTexts(itemTypeLines);
        return Translate(serverKeyText, itemTexts, out updatedText);
    }

    private static LegacyServerKeyItemNameTranslationResult Translate(
        string serverKeyText,
        LegacyServerKeyItemTexts itemTexts,
        out string updatedText)
    {
        var uniqueIds = new HashSet<int>();
        var matchedUniqueIds = new HashSet<int>();
        var unmatchedIds = new HashSet<int>();
        var builder = new StringBuilder(serverKeyText.Length);
        var keyCount = 0;
        var translatedKeys = 0;
        var unmatchedKeys = 0;
        var changedKeys = 0;

        foreach (var line in SplitLines(serverKeyText))
        {
            var match = ServerKeyItemTextPattern.Match(line.Text);
            if (!match.Success ||
                !TryGetItemTextReference(match.Groups["key"].Value, out var itemId, out var textKind))
            {
                builder.Append(line.Text);
                builder.Append(line.LineEnding);
                continue;
            }

            keyCount++;
            uniqueIds.Add(itemId);
            var currentValue = UnescapeLuaString(match.Groups["name"].Value);
            if (itemTexts.TryGetValue(itemId, textKind, out var value))
            {
                translatedKeys++;
                matchedUniqueIds.Add(itemId);
                if (!NeedsTranslation(currentValue))
                {
                    builder.Append(line.Text);
                    builder.Append(line.LineEnding);
                    continue;
                }

                var translatedLine = match.Groups["prefix"].Value + EscapeLuaString(value) + match.Groups["suffix"].Value;
                if (!StringComparer.Ordinal.Equals(line.Text, translatedLine))
                {
                    changedKeys++;
                }

                builder.Append(translatedLine);
                builder.Append(line.LineEnding);
                continue;
            }

            unmatchedKeys++;
            unmatchedIds.Add(itemId);
            builder.Append(line.Text);
            builder.Append(line.LineEnding);
        }

        updatedText = builder.ToString();
        var missingItemIds = unmatchedIds.Order().ToArray();
        return new LegacyServerKeyItemNameTranslationResult(
            keyCount,
            translatedKeys,
            unmatchedKeys,
            uniqueIds.Count,
            matchedUniqueIds.Count,
            missingItemIds,
            itemTexts.ConflictingItemIds.Order().ToArray(),
            changedKeys,
            !StringComparer.Ordinal.Equals(serverKeyText, updatedText));
    }

    private static LegacyServerKeyItemTexts BuildItemTexts(IEnumerable<string> itemTypeLines)
    {
        var builder = new LegacyServerKeyItemTextCatalogBuilder();
        foreach (var line in itemTypeLines)
        {
            AddItemTypeSourceLine(builder, line);
        }

        return builder.Build();
    }

    private static LegacyServerKeyItemTexts BuildItemTextsFromSourcePaths(IEnumerable<string> sourcePaths)
    {
        var builder = new LegacyServerKeyItemTextCatalogBuilder();
        foreach (var sourcePath in sourcePaths)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                continue;
            }

            var sourceText = DecodeLenientCodePage936(File.ReadAllBytes(sourcePath));
            foreach (var line in SplitLines(sourceText))
            {
                AddItemTypeSourceLine(builder, line.Text);
                AddServerKeySourceLine(builder, line.Text);
            }

            if (Path.GetFileName(sourcePath).Equals("RuneDesc.ini", StringComparison.OrdinalIgnoreCase))
            {
                AddRuneDescSourceText(builder, DecodeRuneDescText(File.ReadAllBytes(sourcePath)));
            }
        }

        return builder.Build();
    }

    private static void AddItemTypeSourceLine(LegacyServerKeyItemTextCatalogBuilder builder, string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        var fields = line.Split("@@");
        if (fields.Length <= 1 ||
            !int.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out var itemId))
        {
            return;
        }

        var name = NormalizeName(fields[1]);
        builder.Add(itemId, LegacyServerKeyItemTextKind.Name, name);

        if (fields.Length <= 53)
        {
            return;
        }

        var description = NormalizeDescription(fields[53]);
        if (description.Length == 0)
        {
            description = name;
        }

        builder.Add(itemId, LegacyServerKeyItemTextKind.Description, description);
    }

    private static void AddServerKeySourceLine(LegacyServerKeyItemTextCatalogBuilder builder, string line)
    {
        var match = ServerKeyItemTextPattern.Match(line);
        if (!match.Success ||
            !TryGetItemTextReference(match.Groups["key"].Value, out var itemId, out var textKind))
        {
            return;
        }

        var value = UnescapeLuaString(match.Groups["name"].Value);
        builder.Add(
            itemId,
            textKind,
            textKind == LegacyServerKeyItemTextKind.Description
                ? NormalizeDescription(value)
                : NormalizeName(value));
    }

    private static void AddRuneDescSourceText(LegacyServerKeyItemTextCatalogBuilder builder, string text)
    {
        int? currentRuneId = null;
        foreach (var line in SplitLines(text))
        {
            if (TryParseBracketSectionId(line.Text, out var sectionId))
            {
                currentRuneId = sectionId;
                continue;
            }

            if (currentRuneId is { } runeId &&
                TryParseIniValue(line.Text, "Desc", out var description))
            {
                builder.Add(runeId, LegacyServerKeyItemTextKind.Description, NormalizeDescription(description));
            }
        }
    }

    private static bool TryGetItemTextReference(string key, out int itemId, out LegacyServerKeyItemTextKind textKind)
    {
        if (TryParseKeyId(key, "STR_ITEM_TYPE_", out itemId))
        {
            textKind = LegacyServerKeyItemTextKind.Name;
            return true;
        }

        if (TryParseKeyId(key, "STR_ITEM_ITEMDESC_", out itemId))
        {
            textKind = LegacyServerKeyItemTextKind.Description;
            return true;
        }

        if (key.StartsWith("STR_SHOOT_", StringComparison.Ordinal))
        {
            var suffixStart = "STR_SHOOT_".Length;
            var separatorIndex = key.IndexOf('_', suffixStart);
            if (separatorIndex > suffixStart &&
                int.TryParse(key[suffixStart..separatorIndex], NumberStyles.None, CultureInfo.InvariantCulture, out itemId))
            {
                textKind = LegacyServerKeyItemTextKind.Name;
                return true;
            }
        }

        itemId = 0;
        textKind = LegacyServerKeyItemTextKind.Name;
        return false;
    }

    private static bool TryParseBracketSectionId(string line, out int id)
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

    private static bool TryParseIniValue(string line, string key, out string value)
    {
        var trimmed = line.TrimStart();
        if (!trimmed.StartsWith(key, StringComparison.OrdinalIgnoreCase))
        {
            value = string.Empty;
            return false;
        }

        var equalsIndex = trimmed.IndexOf('=');
        if (equalsIndex < 0 ||
            !trimmed[..equalsIndex].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
        {
            value = string.Empty;
            return false;
        }

        value = trimmed[(equalsIndex + 1)..].Trim();
        return true;
    }

    private static bool TryParseKeyId(string key, string prefix, out int id)
    {
        if (key.StartsWith(prefix, StringComparison.Ordinal) &&
            int.TryParse(key[prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out id))
        {
            return true;
        }

        id = 0;
        return false;
    }

    private static string NormalizeName(string value)
    {
        return value
            .Replace("\uFFFD\uFFFDs", "'s", StringComparison.Ordinal)
            .Replace("\uFFFD\uB863", " sec.", StringComparison.Ordinal)
            .Replace("\uFFFD", string.Empty, StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
    }

    private static string NormalizeDescription(string value)
    {
        return NormalizeName(value)
            .Replace('~', ' ')
            .Trim();
    }

    private static bool IsUsableText(string value)
    {
        return value.Length > 0 &&
            ContainsAsciiLetter(value) &&
            !NeedsTranslation(value) &&
            CanEncodeClientText(value);
    }

    private static bool NeedsTranslation(string value)
    {
        foreach (var character in value)
        {
            if (IsBlockedClientTextCharacter(character))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsAsciiLetter(string value)
    {
        foreach (var character in value)
        {
            if (character is >= 'A' and <= 'Z' or >= 'a' and <= 'z')
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsBlockedClientTextCharacter(char character)
    {
        return character is '\uFFFD' or >= '\u3400' and <= '\u9fff' or >= '\uf900' and <= '\ufaff' or >= '\uff00' and <= '\uffef';
    }

    private static bool CanEncodeClientText(string value)
    {
        try
        {
            ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936).GetByteCount(value);
            return true;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    private static string EscapeLuaString(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
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
            builder.Append(escaped switch
            {
                'n' => ' ',
                'r' => ' ',
                't' => ' ',
                _ => escaped,
            });
        }

        return builder.ToString();
    }

    private static string DecodeLenientCodePage936(byte[] bytes)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(
                936,
                EncoderFallback.ExceptionFallback,
                DecoderFallback.ReplacementFallback)
            .GetString(bytes);
    }

    private static string DecodeRuneDescText(byte[] bytes)
    {
        try
        {
            var offset = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF
                ? 3
                : 0;
            return ClientTextEncodings.GetEncoding(ClientTextEncodingKind.Utf8)
                .GetString(bytes, offset, bytes.Length - offset);
        }
        catch (DecoderFallbackException)
        {
            return DecodeLenientCodePage936(bytes);
        }
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

    private sealed record LegacyServerKeyItemTexts(
        IReadOnlyDictionary<int, string> Names,
        IReadOnlyDictionary<int, string> Descriptions,
        IReadOnlySet<int> ConflictingItemIds)
    {
        public bool TryGetValue(int itemId, LegacyServerKeyItemTextKind textKind, out string value)
        {
            var source = textKind == LegacyServerKeyItemTextKind.Description ? Descriptions : Names;
            if (source.TryGetValue(itemId, out var match))
            {
                value = match;
                return true;
            }

            value = string.Empty;
            return false;
        }
    }

    private sealed class LegacyServerKeyItemTextCatalogBuilder
    {
        private readonly Dictionary<ItemTextKey, string> texts = [];
        private readonly HashSet<ItemTextKey> conflicts = [];

        public void Add(int itemId, LegacyServerKeyItemTextKind textKind, string value)
        {
            if (itemId <= 0 || conflicts.Contains(new ItemTextKey(itemId, textKind)))
            {
                return;
            }

            var normalizedValue = NormalizeCatalogValue(value);
            if (!IsUsableText(normalizedValue))
            {
                return;
            }

            var key = new ItemTextKey(itemId, textKind);
            if (texts.TryGetValue(key, out var existingValue))
            {
                if (!StringComparer.Ordinal.Equals(existingValue, normalizedValue))
                {
                    texts.Remove(key);
                    conflicts.Add(key);
                }

                return;
            }

            texts.Add(key, normalizedValue);
        }

        private static string NormalizeCatalogValue(string value)
        {
            return value
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();
        }

        public LegacyServerKeyItemTexts Build()
        {
            var names = new Dictionary<int, string>();
            var descriptions = new Dictionary<int, string>();
            foreach (var entry in texts)
            {
                if (entry.Key.TextKind == LegacyServerKeyItemTextKind.Description)
                {
                    descriptions.Add(entry.Key.ItemId, entry.Value);
                    continue;
                }

                names.Add(entry.Key.ItemId, entry.Value);
            }

            return new LegacyServerKeyItemTexts(
                names,
                descriptions,
                conflicts.Select(static key => key.ItemId).Distinct().ToHashSet());
        }
    }

    private readonly record struct ItemTextKey(int ItemId, LegacyServerKeyItemTextKind TextKind);

    private enum LegacyServerKeyItemTextKind
    {
        Name,
        Description,
    }
}
