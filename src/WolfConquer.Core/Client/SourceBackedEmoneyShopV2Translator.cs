using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace WolfConquer.Core.Client;

public sealed record SourceBackedEmoneyShopV2TextSource(
    int ItemId,
    string Text,
    string Source);

public sealed record SourceBackedEmoneyShopV2TranslationResult(
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

public static class SourceBackedEmoneyShopV2Translator
{
    private const string ShopMarkerPattern =
        "(?:other|present|item|equip|normal|null|hot|new|rare|discount)(?:\\|(?:other|present|item|equip|normal|null|hot|new|rare|discount))*";

    private static readonly Regex ShopItemRowPattern = new(
        @"^(?<prefix>\s*(?:(?:[Ii]tem\d+)\s*=\s*)?(?<id>\d+)\s+(?:" + ShopMarkerPattern + @")\s*)(?<text>.*)$",
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

    public static SourceBackedEmoneyShopV2TranslationResult PatchFile(
        string shopPath,
        IEnumerable<string> sourcePaths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shopPath);
        ArgumentNullException.ThrowIfNull(sourcePaths);

        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        var originalBytes = File.ReadAllBytes(shopPath);
        var shopText = encoding.GetString(originalBytes);
        var expandedSourcePaths = ExpandSourcePaths(shopPath, sourcePaths);
        var sources = expandedSourcePaths.SelectMany(ReadSourceTexts);
        var result = Translate(shopText, sources, out var updatedText);
        if (result.Changed)
        {
            ClientTextPatchWriter.WritePatchedTextPreservingUnchangedBytes(
                shopPath,
                originalBytes,
                shopText,
                updatedText,
                encoding);
        }

        return result;
    }

    public static SourceBackedEmoneyShopV2TranslationResult Translate(
        string shopText,
        IEnumerable<SourceBackedEmoneyShopV2TextSource> sources,
        out string updatedText)
    {
        ArgumentNullException.ThrowIfNull(shopText);
        ArgumentNullException.ThrowIfNull(sources);

        var sourceCatalog = BuildSourceCatalog(sources);
        var missingIds = new HashSet<int>();
        var neededIds = new HashSet<int>();
        var matchedIds = new HashSet<int>();
        var builder = new StringBuilder(shopText.Length);
        var rowCount = 0;
        var rowsNeedingTranslation = 0;
        var sourceMatchedRows = 0;
        var skippedRowsWithoutSource = 0;
        var changedRows = 0;

        foreach (var line in SplitLines(shopText))
        {
            if (!TryParseShopItemRow(line.Text, out var row))
            {
                builder.Append(line.Text);
                builder.Append(line.LineEnding);
                continue;
            }

            rowCount++;
            var currentText = row.Text;
            if (!NeedsTranslation(currentText))
            {
                builder.Append(line.Text);
                builder.Append(line.LineEnding);
                continue;
            }

            rowsNeedingTranslation++;
            neededIds.Add(row.ItemId);
            if (!TryFindReplacement(row, sourceCatalog, out var replacementText))
            {
                skippedRowsWithoutSource++;
                missingIds.Add(row.ItemId);
                builder.Append(line.Text);
                builder.Append(line.LineEnding);
                continue;
            }

            sourceMatchedRows++;
            matchedIds.Add(row.ItemId);
            var translatedLine = row.Prefix + replacementText;
            if (!StringComparer.Ordinal.Equals(line.Text, translatedLine))
            {
                changedRows++;
            }

            builder.Append(translatedLine);
            builder.Append(line.LineEnding);
        }

        updatedText = builder.ToString();
        return new SourceBackedEmoneyShopV2TranslationResult(
            rowCount,
            rowsNeedingTranslation,
            sourceMatchedRows,
            skippedRowsWithoutSource,
            neededIds.Count,
            matchedIds.Count,
            missingIds.Order().ToArray(),
            sourceCatalog.ConflictedItemIds.Order().ToArray(),
            changedRows,
            !StringComparer.Ordinal.Equals(shopText, updatedText));
    }

    public static IReadOnlyList<SourceBackedEmoneyShopV2TextSource> ReadSourceTexts(string path)
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
        var sources = new List<SourceBackedEmoneyShopV2TextSource>();
        foreach (var line in SplitLines(text))
        {
            AddItemTypeDescriptionSource(path, line.Text, sources);
            AddServerKeyNameSource(path, line.Text, sources);
            AddServerKeyDescriptionSource(path, line.Text, sources);
            AddServerKeyPackageItemReferenceSource(path, line.Text, sources);
            AddEmoneyShopRowSource(path, line.Text, sources);
        }

        return sources;
    }

    private static IReadOnlyList<string> ExpandSourceDirectory(string path)
    {
        var paths = new List<string>();
        AddDistinctExistingPath(paths, Path.Combine(path, "emoneyShopV2.ini"));
        AddDistinctExistingPath(paths, Path.Combine(path, "emoneyshop.ini"));
        AddDistinctExistingPath(paths, Path.Combine(path, "ini", "emoneyShopV2.ini"));
        AddDistinctExistingPath(paths, Path.Combine(path, "ini", "emoneyshop.ini"));
        AddDistinctExistingPath(paths, Path.Combine(path, "shops", "emoneyShopV2.ini"));
        AddDistinctExistingPath(paths, Path.Combine(path, "shops", "emoneyshop.ini"));
        AddDistinctExistingPath(paths, Path.Combine(path, "itemtype.txt"));
        AddDistinctExistingPath(paths, Path.Combine(path, "Server_Key.lua"));
        AddDistinctExistingPath(paths, Path.Combine(path, "Script", "Server_Key.lua"));
        AddDistinctExistingPath(paths, Path.Combine(path, "ini", "Server_Key.lua"));
        AddDistinctExistingPath(paths, Path.Combine(path, "cqclient", "ini", "emoneyShopV2.ini"));
        AddDistinctExistingPath(paths, Path.Combine(path, "cqclient", "ini", "emoneyshop.ini"));
        AddDistinctExistingPath(paths, Path.Combine(path, "cqclient", "ini", "Server_Key.lua"));
        AddDistinctExistingPath(paths, Path.Combine(path, "cqclient", "Script", "Server_Key.lua"));
        AddDistinctExistingPath(paths, Path.Combine(path, "GameServer", "DBServer", "itemtype.txt"));
        AddDistinctExistingPath(paths, Path.Combine(path, "GameServer", "DBServer", "shops", "emoneyShopV2.ini"));
        AddDistinctExistingPath(paths, Path.Combine(path, "GameServer", "DBServer", "shops", "emoneyshop.ini"));
        AddDistinctExistingPath(paths, Path.Combine(path, "lqtxfwd", "GameServer", "DBServer", "itemtype.txt"));
        AddDistinctExistingPath(paths, Path.Combine(path, "lqtxfwd", "GameServer", "DBServer", "shops", "emoneyShopV2.ini"));
        AddDistinctExistingPath(paths, Path.Combine(path, "lqtxfwd", "GameServer", "DBServer", "shops", "emoneyshop.ini"));
        return paths;
    }

    private static IReadOnlyList<string> ExpandSourcePaths(
        string shopPath,
        IEnumerable<string> sourcePaths)
    {
        var paths = sourcePaths
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .ToList();

        if (!ContainsPath(paths, shopPath))
        {
            return paths;
        }

        var directory = Path.GetDirectoryName(shopPath);
        if (string.IsNullOrEmpty(directory))
        {
            return paths;
        }

        AddDistinctExistingPath(paths, Path.Combine(directory, "emoneyshop.ini"));
        return paths;
    }

    private static bool ContainsPath(IEnumerable<string> paths, string candidatePath)
    {
        var fullCandidatePath = Path.GetFullPath(candidatePath);
        return paths.Any(path =>
            StringComparer.OrdinalIgnoreCase.Equals(Path.GetFullPath(path), fullCandidatePath));
    }

    private static void AddDistinctExistingPath(List<string> paths, string path)
    {
        if (!File.Exists(path) || ContainsPath(paths, path))
        {
            return;
        }

        paths.Add(path);
    }

    private static SourceCatalog BuildSourceCatalog(IEnumerable<SourceBackedEmoneyShopV2TextSource> sources)
    {
        var textByKey = new Dictionary<SourceKey, string>();
        var rowTextByKey = new Dictionary<RowSourceKey, string>();
        var conflicts = new HashSet<SourceKey>();
        var rowConflicts = new HashSet<RowSourceKey>();
        foreach (var source in sources)
        {
            var key = new SourceKey(source.ItemId, GetSourceKind(source));
            if (source.ItemId <= 0)
            {
                continue;
            }

            var text = NormalizeVisibleText(source.Text);
            if (!IsUsableEnglishSource(text))
            {
                continue;
            }

            if (TryGetEmoneyShopRowContext(source.Source, out var rowContext))
            {
                AddCandidate(
                    rowTextByKey,
                    rowConflicts,
                    new RowSourceKey(source.ItemId, key.Kind, rowContext),
                    text);
            }

            if (conflicts.Contains(key))
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

        return new SourceCatalog(textByKey, rowTextByKey, conflicts, rowConflicts);
    }

    private static bool TryFindReplacement(
        ShopItemRow row,
        SourceCatalog sourceCatalog,
        out string replacementText)
    {
        var kind = LooksLikeTitleText(row.Text) ? SourceTextKind.Title : SourceTextKind.Description;
        var rowKey = new RowSourceKey(row.ItemId, kind, NormalizeRowContext(row.Prefix));
        if (sourceCatalog.RowTextByKey.TryGetValue(rowKey, out replacementText!) &&
            IsSafeReplacement(row.Text, replacementText))
        {
            return true;
        }

        var key = new SourceKey(row.ItemId, kind);
        if (!sourceCatalog.TextByKey.TryGetValue(key, out replacementText!) ||
            !IsSafeReplacement(row.Text, replacementText))
        {
            replacementText = string.Empty;
            return false;
        }

        return true;
    }

    private static SourceTextKind GetSourceKind(SourceBackedEmoneyShopV2TextSource source)
    {
        return source.Source.Contains("STR_ITEM_TYPE", StringComparison.OrdinalIgnoreCase) ||
            source.Source.Contains("itemtype.name", StringComparison.OrdinalIgnoreCase)
                ? SourceTextKind.Title
                : SourceTextKind.Description;
    }

    private static bool TryParseShopItemRow(string line, out ShopItemRow row)
    {
        row = default;
        var match = ShopItemRowPattern.Match(line);
        if (!match.Success ||
            !int.TryParse(match.Groups["id"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var itemId))
        {
            return false;
        }

        row = new ShopItemRow(itemId, match.Groups["prefix"].Value, match.Groups["text"].Value);
        return true;
    }

    private static void AddItemTypeDescriptionSource(
        string path,
        string line,
        List<SourceBackedEmoneyShopV2TextSource> sources)
    {
        var fields = line.Split("@@", StringSplitOptions.None);
        if (fields.Length <= 53 ||
            !int.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out var itemId))
        {
            return;
        }

        sources.Add(new SourceBackedEmoneyShopV2TextSource(
            itemId,
            fields[53],
            path + ":itemtype.description"));
    }

    private static void AddServerKeyNameSource(
        string path,
        string line,
        List<SourceBackedEmoneyShopV2TextSource> sources)
    {
        var match = ServerKeyItemNamePattern.Match(line);
        if (!match.Success ||
            !int.TryParse(match.Groups["id"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var itemId))
        {
            return;
        }

        sources.Add(new SourceBackedEmoneyShopV2TextSource(
            itemId,
            UnescapeLuaString(match.Groups["value"].Value),
            path + ":Server_Key.STR_ITEM_TYPE"));
    }

    private static void AddServerKeyDescriptionSource(
        string path,
        string line,
        List<SourceBackedEmoneyShopV2TextSource> sources)
    {
        var match = ServerKeyItemDescriptionPattern.Match(line);
        if (!match.Success ||
            !int.TryParse(match.Groups["id"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var itemId))
        {
            return;
        }

        sources.Add(new SourceBackedEmoneyShopV2TextSource(
            itemId,
            UnescapeLuaString(match.Groups["value"].Value),
            path + ":Server_Key.STR_ITEM_ITEMDESC"));
    }

    private static void AddServerKeyPackageItemReferenceSource(
        string path,
        string line,
        List<SourceBackedEmoneyShopV2TextSource> sources)
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

        sources.Add(new SourceBackedEmoneyShopV2TextSource(
            itemId,
            UnescapeLuaString(match.Groups["value"].Value),
            path + ":Server_Key.package-item-reference"));
    }

    private static void AddEmoneyShopRowSource(
        string path,
        string line,
        List<SourceBackedEmoneyShopV2TextSource> sources)
    {
        if (!TryParseShopItemRow(line, out var row))
        {
            return;
        }

        sources.Add(new SourceBackedEmoneyShopV2TextSource(
            row.ItemId,
            row.Text,
            path + ":emoneyShopV2.row:" + NormalizeRowContext(row.Prefix)));
    }

    private static void AddCandidate<TKey>(
        Dictionary<TKey, string> candidates,
        HashSet<TKey> conflicts,
        TKey key,
        string text)
        where TKey : notnull
    {
        if (conflicts.Contains(key))
        {
            return;
        }

        if (candidates.TryGetValue(key, out var existing))
        {
            if (!StringComparer.Ordinal.Equals(existing, text))
            {
                candidates.Remove(key);
                conflicts.Add(key);
            }

            return;
        }

        candidates.Add(key, text);
    }

    private static bool TryGetEmoneyShopRowContext(string source, out string rowContext)
    {
        const string marker = ":emoneyShopV2.row:";
        var index = source.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            rowContext = string.Empty;
            return false;
        }

        rowContext = source[(index + marker.Length)..];
        return rowContext.Length > 0;
    }

    private static string NormalizeRowContext(string prefix)
    {
        return Regex.Replace(prefix.Trim(), "\\s+", " ");
    }

    private static string NormalizeVisibleText(string value)
    {
        return value
            .Replace('~', ' ')
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
    }

    private static bool IsSafeReplacement(string currentText, string replacementText)
    {
        return PlaceholderSignature(currentText).SequenceEqual(PlaceholderSignature(replacementText), StringComparer.Ordinal) &&
            ColorSignature(currentText).SequenceEqual(ColorSignature(replacementText), StringComparer.OrdinalIgnoreCase) &&
            EscapedControlSignature(currentText).SequenceEqual(EscapedControlSignature(replacementText), StringComparer.Ordinal);
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

    private static bool LooksLikeTitleText(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length is > 0 and <= 40 &&
            !trimmed.Any(static character => character is
                '.' or ',' or ';' or ':' or '!' or '?' or
                '\u3002' or '\uff0c' or '\uff1b' or '\uff1a' or '\uff01' or '\uff1f');
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
        if (value.Length == 0 ||
            !value.Any(static character => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z'))
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

    private readonly record struct RowSourceKey(int ItemId, SourceTextKind Kind, string RowContext);

    private readonly record struct ShopItemRow(int ItemId, string Prefix, string Text);

    private sealed record SourceCatalog(
        IReadOnlyDictionary<SourceKey, string> TextByKey,
        IReadOnlyDictionary<RowSourceKey, string> RowTextByKey,
        IReadOnlySet<SourceKey> Conflicts,
        IReadOnlySet<RowSourceKey> RowConflicts)
    {
        public IReadOnlySet<int> ConflictedItemIds { get; } = Conflicts
            .Select(static conflict => conflict.ItemId)
            .Concat(RowConflicts.Select(static conflict => conflict.ItemId))
            .ToHashSet();
    }
}
