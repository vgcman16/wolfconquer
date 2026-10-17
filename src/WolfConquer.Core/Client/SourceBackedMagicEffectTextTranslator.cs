using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace WolfConquer.Core.Client;

public sealed record SourceBackedMagicEffectTextSource(
    long SectionId,
    string Key,
    string Value,
    string SourceName);

public sealed record SourceBackedMagicEffectSkillNameSource(
    long SkillId,
    string Name,
    string SourceName);

public sealed record SourceBackedMagicEffectTextTranslationResult(
    int SourceRows,
    int SourceKeys,
    IReadOnlyList<string> ConflictingSourceKeys,
    int SkillNameSourceRows,
    int SkillNameSourceIds,
    IReadOnlyList<long> ConflictingSkillNameIds,
    int TargetRows,
    int RowsNeedingTranslation,
    int SameKeyMatchedRows,
    int SkillNameFallbackRows,
    int PlaceholderMismatchRows,
    int ConflictRows,
    int UnmatchedRows,
    int AlreadyTranslatedRows,
    int ChangedRows,
    int RemainingCjkRows,
    bool Changed);

public static class SourceBackedMagicEffectTextTranslator
{
    private static readonly Regex FormatTokenPattern = new(
        "%(?:%|[-+ #0]*(?:\\d+|\\*)?(?:\\.(?:\\d+|\\*))?[hlL]?[A-Za-z])",
        RegexOptions.CultureInvariant);

    private static readonly Regex BracePlaceholderPattern = new(
        "\\{\\d+(?:[^{}]*)\\}",
        RegexOptions.CultureInvariant);

    private static readonly Encoding MagicEffectEncoding =
        ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);

    public static SourceBackedMagicEffectTextTranslationResult PatchFile(
        string magicEffectPath,
        string? referenceMagicEffectPath,
        IEnumerable<string>? magicTypePaths = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(magicEffectPath);

        var sources = string.IsNullOrWhiteSpace(referenceMagicEffectPath)
            ? []
            : ReadMagicEffectTextSources(referenceMagicEffectPath);
        var skillNameSources = magicTypePaths is null
            ? []
            : ReadMagicTypeSkillNameSources(magicTypePaths);

        return PatchFile(magicEffectPath, sources, skillNameSources);
    }

    public static SourceBackedMagicEffectTextTranslationResult PatchFile(
        string magicEffectPath,
        IEnumerable<SourceBackedMagicEffectTextSource> sources,
        IEnumerable<SourceBackedMagicEffectSkillNameSource>? skillNameSources = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(magicEffectPath);
        ArgumentNullException.ThrowIfNull(sources);

        var originalBytes = File.ReadAllBytes(magicEffectPath);
        var originalText = MagicEffectEncoding.GetString(originalBytes);
        var result = Translate(originalText, sources, skillNameSources ?? [], out var updatedText);
        if (result.Changed)
        {
            ClientTextPatchWriter.WritePatchedTextPreservingUnchangedBytes(
                magicEffectPath,
                originalBytes,
                originalText,
                updatedText,
                MagicEffectEncoding);
        }

        return result;
    }

    public static SourceBackedMagicEffectTextTranslationResult Translate(
        string magicEffectText,
        IEnumerable<SourceBackedMagicEffectTextSource> sources,
        IEnumerable<SourceBackedMagicEffectSkillNameSource>? skillNameSources,
        out string updatedText)
    {
        ArgumentNullException.ThrowIfNull(magicEffectText);
        ArgumentNullException.ThrowIfNull(sources);

        var sourceCatalog = BuildTextSourceCatalog(sources);
        var skillNameCatalog = BuildSkillNameSourceCatalog(skillNameSources ?? []);
        var builder = new StringBuilder(magicEffectText.Length);
        long? currentSectionId = null;
        var targetRows = 0;
        var rowsNeedingTranslation = 0;
        var sameKeyMatchedRows = 0;
        var skillNameFallbackRows = 0;
        var placeholderMismatchRows = 0;
        var conflictRows = 0;
        var unmatchedRows = 0;
        var alreadyTranslatedRows = 0;
        var changedRows = 0;
        var remainingCjkRows = 0;

        foreach (var line in SplitLines(magicEffectText))
        {
            if (TryParseSectionId(line.Text, out var sectionId))
            {
                currentSectionId = sectionId;
                AppendLine(builder, line);
                continue;
            }

            if (IsSectionHeader(line.Text))
            {
                currentSectionId = null;
                AppendLine(builder, line);
                continue;
            }

            if (currentSectionId is not { } id ||
                !TryParseKeyValue(line.Text, out var key, out var value, out var prefix, out var separator) ||
                !IsVisibleTextKey(key))
            {
                AppendLine(builder, line);
                continue;
            }

            targetRows++;
            if (!ContainsCjkOrFullwidth(value))
            {
                alreadyTranslatedRows++;
                AppendLine(builder, line);
                continue;
            }

            rowsNeedingTranslation++;
            var replacement = ResolveReplacement(id, key, sourceCatalog, skillNameCatalog);
            if (replacement.Kind == MagicEffectReplacementKind.Conflict)
            {
                conflictRows++;
                remainingCjkRows++;
                AppendLine(builder, line);
                continue;
            }

            if (replacement.Kind == MagicEffectReplacementKind.None || replacement.Value is null)
            {
                unmatchedRows++;
                remainingCjkRows++;
                AppendLine(builder, line);
                continue;
            }

            if (IsDescriptionKey(key) && !HaveCompatiblePlaceholders(value, replacement.Value))
            {
                placeholderMismatchRows++;
                remainingCjkRows++;
                AppendLine(builder, line);
                continue;
            }

            if (replacement.Kind == MagicEffectReplacementKind.SameKey)
            {
                sameKeyMatchedRows++;
            }
            else if (replacement.Kind == MagicEffectReplacementKind.SkillName)
            {
                skillNameFallbackRows++;
            }

            var translatedLine = prefix + key + separator + replacement.Value;
            if (!StringComparer.Ordinal.Equals(translatedLine, line.Text))
            {
                changedRows++;
            }

            if (ContainsCjkOrFullwidth(replacement.Value))
            {
                remainingCjkRows++;
            }

            builder.Append(translatedLine);
            builder.Append(line.LineEnding);
        }

        updatedText = builder.ToString();
        EnsureCp936Encodable(updatedText);
        return new SourceBackedMagicEffectTextTranslationResult(
            sourceCatalog.SourceRows,
            sourceCatalog.Values.Count,
            sourceCatalog.ConflictingKeys.Select(FormatSourceKey).Order(StringComparer.Ordinal).ToArray(),
            skillNameCatalog.SourceRows,
            skillNameCatalog.Names.Count,
            skillNameCatalog.Conflicts.Order().ToArray(),
            targetRows,
            rowsNeedingTranslation,
            sameKeyMatchedRows,
            skillNameFallbackRows,
            placeholderMismatchRows,
            conflictRows,
            unmatchedRows,
            alreadyTranslatedRows,
            changedRows,
            remainingCjkRows,
            !StringComparer.Ordinal.Equals(magicEffectText, updatedText));
    }

    public static SourceBackedMagicEffectTextTranslationResult Translate(
        string magicEffectText,
        IEnumerable<SourceBackedMagicEffectTextSource> sources,
        out string updatedText)
    {
        return Translate(magicEffectText, sources, [], out updatedText);
    }

    public static IReadOnlyList<SourceBackedMagicEffectTextSource> ReadMagicEffectTextSources(
        string path,
        string? sourceName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var text = MagicEffectEncoding.GetString(File.ReadAllBytes(path));
        return ReadMagicEffectTextSourcesFromText(text, sourceName ?? path);
    }

    public static IReadOnlyList<SourceBackedMagicEffectTextSource> ReadMagicEffectTextSourcesFromText(
        string text,
        string sourceName)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        var sources = new List<SourceBackedMagicEffectTextSource>();
        long? currentSectionId = null;
        foreach (var line in SplitLines(text))
        {
            if (TryParseSectionId(line.Text, out var sectionId))
            {
                currentSectionId = sectionId;
                continue;
            }

            if (IsSectionHeader(line.Text))
            {
                currentSectionId = null;
                continue;
            }

            if (currentSectionId is not { } id ||
                !TryParseKeyValue(line.Text, out var key, out var value, out _, out _) ||
                !IsVisibleTextKey(key))
            {
                continue;
            }

            var sourceValue = value.Trim();
            if (IsUsefulEnglishValue(sourceValue))
            {
                sources.Add(new SourceBackedMagicEffectTextSource(id, key, sourceValue, sourceName));
            }
        }

        return sources;
    }

    public static IReadOnlyList<SourceBackedMagicEffectSkillNameSource> ReadMagicTypeSkillNameSources(
        IEnumerable<string> magicTypePaths)
    {
        ArgumentNullException.ThrowIfNull(magicTypePaths);

        var sources = new List<SourceBackedMagicEffectSkillNameSource>();
        foreach (var path in magicTypePaths)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            var text = MagicEffectEncoding.GetString(File.ReadAllBytes(path));
            sources.AddRange(ReadMagicTypeSkillNameSourcesFromText(text, path));
        }

        return sources;
    }

    public static IReadOnlyList<SourceBackedMagicEffectSkillNameSource> ReadMagicTypeSkillNameSourcesFromText(
        string magicTypeText,
        string sourceName)
    {
        ArgumentNullException.ThrowIfNull(magicTypeText);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        var sources = new List<SourceBackedMagicEffectSkillNameSource>();
        foreach (var line in SplitLines(magicTypeText))
        {
            var columns = line.Text.Split("@@", StringSplitOptions.None);
            if (columns.Length < 4 ||
                !long.TryParse(columns[1], NumberStyles.None, CultureInfo.InvariantCulture, out var skillId))
            {
                continue;
            }

            var name = columns[3].Trim();
            if (IsUsefulEnglishValue(name))
            {
                sources.Add(new SourceBackedMagicEffectSkillNameSource(skillId, name, sourceName));
            }
        }

        return sources;
    }

    private static MagicEffectReplacement ResolveReplacement(
        long sectionId,
        string key,
        TextSourceCatalog sourceCatalog,
        SkillNameSourceCatalog skillNameCatalog)
    {
        var sourceKey = MakeSourceKey(sectionId, key);
        if (sourceCatalog.ConflictingKeys.Contains(sourceKey))
        {
            return MagicEffectReplacement.Conflict();
        }

        sourceCatalog.Values.TryGetValue(sourceKey, out var sameKeyValue);
        if (!IsNameLikeKey(key))
        {
            return sameKeyValue is null
                ? MagicEffectReplacement.None()
                : MagicEffectReplacement.SameKey(sameKeyValue);
        }

        if (skillNameCatalog.Conflicts.Contains(sectionId) && sameKeyValue is null)
        {
            return MagicEffectReplacement.Conflict();
        }

        var hasSkillName = skillNameCatalog.Names.TryGetValue(sectionId, out var skillNameValue);
        if (sameKeyValue is not null && hasSkillName && !StringComparer.Ordinal.Equals(sameKeyValue, skillNameValue))
        {
            return MagicEffectReplacement.Conflict();
        }

        if (sameKeyValue is not null)
        {
            return MagicEffectReplacement.SameKey(sameKeyValue);
        }

        return hasSkillName
            ? MagicEffectReplacement.SkillName(skillNameValue!)
            : MagicEffectReplacement.None();
    }

    private static TextSourceCatalog BuildTextSourceCatalog(IEnumerable<SourceBackedMagicEffectTextSource> sources)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var conflicts = new HashSet<string>(StringComparer.Ordinal);
        var sourceRows = 0;

        foreach (var source in sources)
        {
            var value = source.Value.Trim();
            if (source.SectionId < 0 ||
                string.IsNullOrWhiteSpace(source.Key) ||
                !IsVisibleTextKey(source.Key) ||
                !IsUsefulEnglishValue(value))
            {
                continue;
            }

            sourceRows++;
            var key = MakeSourceKey(source.SectionId, source.Key);
            if (conflicts.Contains(key))
            {
                continue;
            }

            if (values.TryGetValue(key, out var existing))
            {
                if (!StringComparer.Ordinal.Equals(existing, value))
                {
                    values.Remove(key);
                    conflicts.Add(key);
                }

                continue;
            }

            values.Add(key, value);
        }

        return new TextSourceCatalog(sourceRows, values, conflicts);
    }

    private static SkillNameSourceCatalog BuildSkillNameSourceCatalog(
        IEnumerable<SourceBackedMagicEffectSkillNameSource> sources)
    {
        var names = new Dictionary<long, string>();
        var conflicts = new HashSet<long>();
        var sourceRows = 0;

        foreach (var source in sources)
        {
            var name = source.Name.Trim();
            if (source.SkillId < 0 || !IsUsefulEnglishValue(name))
            {
                continue;
            }

            sourceRows++;
            if (conflicts.Contains(source.SkillId))
            {
                continue;
            }

            if (names.TryGetValue(source.SkillId, out var existing))
            {
                if (!StringComparer.Ordinal.Equals(existing, name))
                {
                    names.Remove(source.SkillId);
                    conflicts.Add(source.SkillId);
                }

                continue;
            }

            names.Add(source.SkillId, name);
        }

        return new SkillNameSourceCatalog(sourceRows, names, conflicts);
    }

    private static string MakeSourceKey(long sectionId, string key)
    {
        return sectionId.ToString(CultureInfo.InvariantCulture) + "\u001f" + key.Trim().ToUpperInvariant();
    }

    private static string FormatSourceKey(string sourceKey)
    {
        var separatorIndex = sourceKey.IndexOf('\u001f', StringComparison.Ordinal);
        return separatorIndex < 0
            ? sourceKey
            : sourceKey[..separatorIndex] + ":" + sourceKey[(separatorIndex + 1)..];
    }

    private static bool TryParseSectionId(string line, out long sectionId)
    {
        sectionId = 0;
        var trimmed = line.Trim();
        if (trimmed.Length > 0 && trimmed[0] == '\uFEFF')
        {
            trimmed = trimmed[1..].Trim();
        }

        if (trimmed.Length <= 2 || trimmed[0] != '[' || trimmed[^1] != ']')
        {
            return false;
        }

        var section = trimmed[1..^1].Trim();
        return long.TryParse(section, NumberStyles.None, CultureInfo.InvariantCulture, out sectionId);
    }

    private static bool IsSectionHeader(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length > 0 && trimmed[0] == '\uFEFF')
        {
            trimmed = trimmed[1..].Trim();
        }

        return trimmed.Length > 2 && trimmed[0] == '[' && trimmed[^1] == ']';
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
        if (equalsIndex <= keyStart)
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
        return key.Length > 0;
    }

    private static bool IsVisibleTextKey(string key)
    {
        return IsNameLikeKey(key) || IsDescriptionKey(key);
    }

    private static bool IsNameLikeKey(string key)
    {
        var trimmed = key.Trim();
        if (!trimmed.StartsWith("Name", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        for (var index = "Name".Length; index < trimmed.Length; index++)
        {
            if (!char.IsDigit(trimmed[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsDescriptionKey(string key)
    {
        return key.Equals("Desc", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("DescEx", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HaveCompatiblePlaceholders(string targetValue, string sourceValue)
    {
        var targetTokens = ReadPlaceholderTokens(targetValue);
        var sourceTokens = ReadPlaceholderTokens(sourceValue);
        return targetTokens.SequenceEqual(sourceTokens, StringComparer.Ordinal);
    }

    private static IReadOnlyList<string> ReadPlaceholderTokens(string value)
    {
        return FormatTokenPattern
            .Matches(value)
            .Cast<Match>()
            .Concat(BracePlaceholderPattern.Matches(value).Cast<Match>())
            .OrderBy(match => match.Index)
            .Select(match => match.Value)
            .ToArray();
    }

    private static bool IsUsefulEnglishValue(string value)
    {
        return value.Length > 0 &&
            ContainsAsciiLetter(value) &&
            !ContainsCjkOrFullwidth(value) &&
            !value.Contains('\uFFFD', StringComparison.Ordinal) &&
            !value.Contains("\u951f\u65a4\u62f7", StringComparison.Ordinal) &&
            CanEncodeCp936(value);
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
                (character >= '\u3000' && character <= '\u303F') ||
                (character >= '\uFF00' && character <= '\uFFEF'))
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

    private enum MagicEffectReplacementKind
    {
        None,
        SameKey,
        SkillName,
        Conflict
    }

    private sealed record MagicEffectReplacement(MagicEffectReplacementKind Kind, string? Value)
    {
        public static MagicEffectReplacement None()
        {
            return new MagicEffectReplacement(MagicEffectReplacementKind.None, null);
        }

        public static MagicEffectReplacement SameKey(string value)
        {
            return new MagicEffectReplacement(MagicEffectReplacementKind.SameKey, value);
        }

        public static MagicEffectReplacement SkillName(string value)
        {
            return new MagicEffectReplacement(MagicEffectReplacementKind.SkillName, value);
        }

        public static MagicEffectReplacement Conflict()
        {
            return new MagicEffectReplacement(MagicEffectReplacementKind.Conflict, null);
        }
    }

    private sealed record TextSourceCatalog(
        int SourceRows,
        Dictionary<string, string> Values,
        HashSet<string> ConflictingKeys);

    private sealed record SkillNameSourceCatalog(
        int SourceRows,
        Dictionary<long, string> Names,
        HashSet<long> Conflicts);
}
