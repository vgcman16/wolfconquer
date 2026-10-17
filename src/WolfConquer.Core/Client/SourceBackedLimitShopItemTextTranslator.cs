using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace WolfConquer.Core.Client;

public sealed record SourceBackedLimitShopItemTextSource(
    int ItemId,
    string Text,
    string Source);

public sealed record SourceBackedLimitShopItemTextTranslationResult(
    int RowCount,
    int RowsNeedingTranslation,
    int SourceMatchedRows,
    int SkippedRowsWithoutSource,
    int UniqueItemIdsNeedingTranslation,
    int MatchedUniqueItemIds,
    IReadOnlyList<int> MissingItemIds,
    IReadOnlyList<int> ConflictedItemIds,
    int ChangedRows,
    bool Changed);

public static class SourceBackedLimitShopItemTextTranslator
{
    private static readonly Regex LimitShopRowPattern = new(
        "^(?<prefix>\\s*\\[(?<id>\\d+)\\]\\s*=\\s*\\{\\s*\")(?<text>(?:\\\\.|[^\"\\\\])*)(?<suffix>\"\\s*\\}.*)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex ServerKeyItemNamePattern = new(
        "^\\s*\\[\"STR_ITEM_TYPE_(?<id>\\d+)\"\\]\\s*=\\s*\"(?<value>(?:\\\\.|[^\"\\\\])*)\"\\s*,?.*$",
        RegexOptions.CultureInvariant);

    private static readonly Regex ServerKeyItemDescriptionPattern = new(
        "^\\s*\\[\"STR_ITEM_ITEMDESC_(?<id>\\d+)\"\\]\\s*=\\s*\"(?<value>(?:\\\\.|[^\"\\\\])*)\"\\s*,?.*$",
        RegexOptions.CultureInvariant);

    private static readonly Regex LuaStringAssignmentPattern = new(
        "^\\s*\\[\"(?<key>(?:\\\\.|[^\"\\\\])*)\"\\]\\s*=\\s*\"(?<value>(?:\\\\.|[^\"\\\\])*)\"\\s*,?.*$",
        RegexOptions.CultureInvariant);

    private static readonly Regex BracketedItemIdPattern = new(
        "\\[(?<id>\\d+)\\]",
        RegexOptions.CultureInvariant);

    private static readonly Regex PrintfPlaceholderPattern = new(
        "%(?:[-+ #0]*(?:\\d+|\\*)?(?:\\.(?:\\d+|\\*))?[hlLzjt]*)?[diuoxXfFeEgGaAcspn]",
        RegexOptions.CultureInvariant);

    private static readonly Regex IndexedPlaceholderPattern = new(
        "\\{\\d+\\}",
        RegexOptions.CultureInvariant);

    private static readonly Regex ColorTokenPattern = new(
        @"</?color(?:=[^>]+)?>|0x[0-9a-fA-F]{8}",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex EscapedControlTokenPattern = new(
        @"\\[nrt]",
        RegexOptions.CultureInvariant);

    public static SourceBackedLimitShopItemTextTranslationResult PatchFile(
        string limitShopPath,
        IEnumerable<string> sourcePaths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(limitShopPath);
        ArgumentNullException.ThrowIfNull(sourcePaths);

        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        var originalBytes = File.ReadAllBytes(limitShopPath);
        var limitShopText = encoding.GetString(originalBytes);
        var sources = sourcePaths.SelectMany(ReadSourceTexts);
        var result = Translate(limitShopText, sources, out var updatedText);
        if (result.Changed)
        {
            ClientTextPatchWriter.WritePatchedTextPreservingUnchangedBytes(
                limitShopPath,
                originalBytes,
                limitShopText,
                updatedText,
                encoding);
        }

        return result;
    }

    public static SourceBackedLimitShopItemTextTranslationResult Translate(
        string limitShopText,
        IEnumerable<SourceBackedLimitShopItemTextSource> sources,
        out string updatedText)
    {
        ArgumentNullException.ThrowIfNull(limitShopText);
        ArgumentNullException.ThrowIfNull(sources);

        var sourceCatalog = BuildSourceCatalog(sources);
        var missingIds = new HashSet<int>();
        var neededIds = new HashSet<int>();
        var matchedIds = new HashSet<int>();
        var builder = new StringBuilder(limitShopText.Length);
        var rowCount = 0;
        var rowsNeedingTranslation = 0;
        var sourceMatchedRows = 0;
        var skippedRowsWithoutSource = 0;
        var changedRows = 0;

        foreach (var line in SplitLines(limitShopText))
        {
            var match = LimitShopRowPattern.Match(line.Text);
            if (!match.Success ||
                !int.TryParse(match.Groups["id"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var itemId))
            {
                builder.Append(line.Text);
                builder.Append(line.LineEnding);
                continue;
            }

            rowCount++;
            var currentLuaText = match.Groups["text"].Value;
            var currentText = UnescapeLuaString(currentLuaText);
            if (!NeedsTranslation(currentText))
            {
                builder.Append(line.Text);
                builder.Append(line.LineEnding);
                continue;
            }

            rowsNeedingTranslation++;
            neededIds.Add(itemId);
            if (!TryFindReplacement(itemId, currentText, currentLuaText, sourceCatalog, out var replacementText))
            {
                skippedRowsWithoutSource++;
                missingIds.Add(itemId);
                builder.Append(line.Text);
                builder.Append(line.LineEnding);
                continue;
            }

            sourceMatchedRows++;
            matchedIds.Add(itemId);
            var translatedLine = match.Groups["prefix"].Value + EscapeLuaString(replacementText) + match.Groups["suffix"].Value;
            if (!StringComparer.Ordinal.Equals(line.Text, translatedLine))
            {
                changedRows++;
            }

            builder.Append(translatedLine);
            builder.Append(line.LineEnding);
        }

        updatedText = builder.ToString();
        return new SourceBackedLimitShopItemTextTranslationResult(
            rowCount,
            rowsNeedingTranslation,
            sourceMatchedRows,
            skippedRowsWithoutSource,
            neededIds.Count,
            matchedIds.Count,
            missingIds.Order().ToArray(),
            sourceCatalog.ConflictedItemIds.Order().ToArray(),
            changedRows,
            !StringComparer.Ordinal.Equals(limitShopText, updatedText));
    }

    public static IReadOnlyList<SourceBackedLimitShopItemTextSource> ReadSourceTexts(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (Directory.Exists(path))
        {
            return ExpandSourceDirectory(path)
                .SelectMany(ReadSourceTexts)
                .ToArray();
        }

        if (!File.Exists(path))
        {
            return [];
        }

        var text = DecodeSourceText(File.ReadAllBytes(path));
        var sources = new List<SourceBackedLimitShopItemTextSource>();
        foreach (var line in SplitLines(text))
        {
            AddItemTypeDescriptionSource(path, line.Text, sources);
            AddServerKeyNameSource(path, line.Text, sources);
            AddServerKeyDescriptionSource(path, line.Text, sources);
            AddServerKeyPackageItemReferenceSource(path, line.Text, sources);
            AddLimitShopRowSource(path, line.Text, sources);
        }

        return sources;
    }

    private static IReadOnlyList<string> ExpandSourceDirectory(string path)
    {
        var paths = new List<string>();
        AddDistinctExistingPath(paths, Path.Combine(path, "LimitShop.lua"));
        AddDistinctExistingPath(paths, Path.Combine(path, "ini", "LimitShop.lua"));
        AddDistinctExistingPath(paths, Path.Combine(path, "Server_Key.lua"));
        AddDistinctExistingPath(paths, Path.Combine(path, "Script", "Server_Key.lua"));
        AddDistinctExistingPath(paths, Path.Combine(path, "ini", "Server_Key.lua"));
        AddDistinctExistingPath(paths, Path.Combine(path, "itemtype.txt"));
        AddDistinctExistingPath(paths, Path.Combine(path, "cqclient", "ini", "LimitShop.lua"));
        AddDistinctExistingPath(paths, Path.Combine(path, "cqclient", "ini", "Server_Key.lua"));
        AddDistinctExistingPath(paths, Path.Combine(path, "cqclient", "Script", "Server_Key.lua"));
        AddDistinctExistingPath(paths, Path.Combine(path, "GameServer", "DBServer", "itemtype.txt"));
        AddDistinctExistingPath(paths, Path.Combine(path, "lqtxfwd", "GameServer", "DBServer", "itemtype.txt"));
        return paths;
    }

    private static void AddDistinctExistingPath(List<string> paths, string path)
    {
        if (!File.Exists(path) || ContainsPath(paths, path))
        {
            return;
        }

        paths.Add(path);
    }

    private static bool ContainsPath(IEnumerable<string> paths, string candidatePath)
    {
        var fullCandidatePath = Path.GetFullPath(candidatePath);
        return paths.Any(path =>
            StringComparer.OrdinalIgnoreCase.Equals(Path.GetFullPath(path), fullCandidatePath));
    }

    private static SourceCatalog BuildSourceCatalog(IEnumerable<SourceBackedLimitShopItemTextSource> sources)
    {
        var textByKey = new Dictionary<SourceKey, string>();
        var conflicts = new HashSet<SourceKey>();
        foreach (var source in sources)
        {
            var key = new SourceKey(source.ItemId, GetSourceKind(source));
            if (source.ItemId <= 0 || conflicts.Contains(key))
            {
                continue;
            }

            var text = NormalizeVisibleText(source.Text);
            if (!IsUsableEnglishSource(text))
            {
                continue;
            }

            if (textByKey.TryGetValue(key, out var existing))
            {
                if (!StringComparer.Ordinal.Equals(existing, text))
                {
                    textByKey.Remove(key);
                    conflicts.Add(key);
                }

                continue;
            }

            textByKey.Add(key, text);
        }

        return new SourceCatalog(textByKey, conflicts);
    }

    private static bool TryFindReplacement(
        int itemId,
        string currentText,
        string currentLuaText,
        SourceCatalog sourceCatalog,
        out string replacementText)
    {
        var key = new SourceKey(itemId, SourceTextKind.Description);
        if (!sourceCatalog.TextByKey.TryGetValue(key, out replacementText!) ||
            !IsSafeReplacement(currentText, currentLuaText, replacementText))
        {
            replacementText = string.Empty;
            return false;
        }

        return true;
    }

    private static SourceTextKind GetSourceKind(SourceBackedLimitShopItemTextSource source)
    {
        return source.Source.Contains("STR_ITEM_TYPE", StringComparison.OrdinalIgnoreCase) ||
            source.Source.Contains("itemtype.name", StringComparison.OrdinalIgnoreCase)
                ? SourceTextKind.Title
                : SourceTextKind.Description;
    }

    private static void AddItemTypeDescriptionSource(
        string path,
        string line,
        List<SourceBackedLimitShopItemTextSource> sources)
    {
        var fields = line.Split("@@", StringSplitOptions.None);
        if (fields.Length <= 53 ||
            !int.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out var itemId))
        {
            return;
        }

        sources.Add(new SourceBackedLimitShopItemTextSource(
            itemId,
            fields[53],
            path + ":itemtype.description"));
    }

    private static void AddServerKeyNameSource(
        string path,
        string line,
        List<SourceBackedLimitShopItemTextSource> sources)
    {
        var match = ServerKeyItemNamePattern.Match(line);
        if (!match.Success ||
            !int.TryParse(match.Groups["id"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var itemId))
        {
            return;
        }

        sources.Add(new SourceBackedLimitShopItemTextSource(
            itemId,
            UnescapeLuaString(match.Groups["value"].Value),
            path + ":Server_Key.STR_ITEM_TYPE"));
    }

    private static void AddServerKeyDescriptionSource(
        string path,
        string line,
        List<SourceBackedLimitShopItemTextSource> sources)
    {
        var match = ServerKeyItemDescriptionPattern.Match(line);
        if (!match.Success ||
            !int.TryParse(match.Groups["id"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var itemId))
        {
            return;
        }

        sources.Add(new SourceBackedLimitShopItemTextSource(
            itemId,
            UnescapeLuaString(match.Groups["value"].Value),
            path + ":Server_Key.STR_ITEM_ITEMDESC"));
    }

    private static void AddServerKeyPackageItemReferenceSource(
        string path,
        string line,
        List<SourceBackedLimitShopItemTextSource> sources)
    {
        var match = LuaStringAssignmentPattern.Match(line);
        if (!match.Success)
        {
            return;
        }

        var key = UnescapeLuaString(match.Groups["key"].Value);
        if (key.StartsWith("STR_ITEM_TYPE_", StringComparison.OrdinalIgnoreCase) ||
            key.StartsWith("STR_ITEM_ITEMDESC_", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var idMatches = BracketedItemIdPattern.Matches(key);
        if (idMatches.Count != 1 ||
            !int.TryParse(idMatches[0].Groups["id"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var itemId))
        {
            return;
        }

        sources.Add(new SourceBackedLimitShopItemTextSource(
            itemId,
            UnescapeLuaString(match.Groups["value"].Value),
            path + ":Server_Key.package-item-reference"));
    }

    private static void AddLimitShopRowSource(
        string path,
        string line,
        List<SourceBackedLimitShopItemTextSource> sources)
    {
        var match = LimitShopRowPattern.Match(line);
        if (!match.Success ||
            !int.TryParse(match.Groups["id"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var itemId))
        {
            return;
        }

        sources.Add(new SourceBackedLimitShopItemTextSource(
            itemId,
            UnescapeLuaString(match.Groups["text"].Value),
            path + ":LimitShop.row"));
    }

    private static string NormalizeVisibleText(string value)
    {
        return value
            .Replace('~', ' ')
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
    }

    private static bool IsSafeReplacement(string currentText, string currentLuaText, string replacementText)
    {
        return PlaceholderSignature(currentText).SequenceEqual(PlaceholderSignature(replacementText), StringComparer.Ordinal) &&
            ColorSignature(currentText).SequenceEqual(ColorSignature(replacementText), StringComparer.OrdinalIgnoreCase) &&
            EscapedControlSignature(currentLuaText).SequenceEqual(EscapedControlSignature(EscapeLuaString(replacementText)), StringComparer.Ordinal);
    }

    private static string[] PlaceholderSignature(string value)
    {
        var withoutEscapedPercents = value.Replace("%%", string.Empty, StringComparison.Ordinal);
        return PrintfPlaceholderPattern
            .Matches(withoutEscapedPercents)
            .Select(static match => match.Value)
            .Concat(IndexedPlaceholderPattern.Matches(value).Select(static match => match.Value))
            .ToArray();
    }

    private static string[] ColorSignature(string value)
    {
        return ColorTokenPattern
            .Matches(value)
            .Select(static match => match.Value)
            .ToArray();
    }

    private static string[] EscapedControlSignature(string value)
    {
        return EscapedControlTokenPattern
            .Matches(value)
            .Select(static match => match.Value)
            .ToArray();
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

    private static bool IsUsableEnglishSource(string value)
    {
        if (value.Length == 0 || !value.Any(static character => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z'))
        {
            return false;
        }

        foreach (var character in value)
        {
            if (IsBlockedClientTextCharacter(character) || char.IsControl(character))
            {
                return false;
            }
        }

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

    private static bool IsBlockedClientTextCharacter(char character)
    {
        return character is '\uFFFD' or >= '\u2e80' and <= '\u9fff' or >= '\uf900' and <= '\ufaff' or >= '\uff00' and <= '\uffef';
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
            if (value[index] != '\\' || index + 1 >= value.Length)
            {
                builder.Append(value[index]);
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

    private static string DecodeSourceText(byte[] bytes)
    {
        if (HasUtf8Bom(bytes))
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false)
                .GetString(bytes, 3, bytes.Length - 3);
        }

        try
        {
            return ClientTextEncodings.GetEncoding(ClientTextEncodingKind.Utf8).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return GetLenientCodePage936Encoding().GetString(bytes);
        }
    }

    private static bool HasUtf8Bom(byte[] bytes)
    {
        return bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
    }

    private static Encoding GetLenientCodePage936Encoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(
            936,
            EncoderFallback.ExceptionFallback,
            DecoderFallback.ReplacementFallback);
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

    private enum SourceTextKind
    {
        Description,
        Title,
    }

    private readonly record struct SourceKey(int ItemId, SourceTextKind Kind);

    private sealed record SourceCatalog(
        IReadOnlyDictionary<SourceKey, string> TextByKey,
        IReadOnlySet<SourceKey> Conflicts)
    {
        public IReadOnlySet<int> ConflictedItemIds { get; } = Conflicts
            .Select(static conflict => conflict.ItemId)
            .ToHashSet();
    }
}
