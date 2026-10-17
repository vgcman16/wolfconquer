using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace WolfConquer.Core.Client;

public sealed record SourceBackedClientStatusTextSource(
    string Section,
    string Key,
    string Value,
    string SourceName);

public sealed record SourceBackedClientStatusTextTranslationResult(
    int SourceRows,
    int SourceKeys,
    IReadOnlyList<string> ConflictingSourceKeys,
    int TargetRows,
    int RowsNeedingTranslation,
    int MatchedRows,
    int PlaceholderMismatchRows,
    int UnmatchedRows,
    int AlreadyTranslatedRows,
    int ChangedRows,
    int RemainingCjkRows,
    bool Changed);

public sealed record SourceBackedClientSkillNameSource(
    int SkillId,
    string Name,
    string SourceName);

public sealed record SourceBackedClientInfoSkillNameTranslationResult(
    int SkillRows,
    int UniqueSkillIds,
    int SourceSkillNames,
    IReadOnlyList<int> ConflictingSourceIds,
    int MatchedSkillRows,
    int MissingSkillRows,
    int TranslatedRows,
    int ChangedRows,
    int RemainingCjkRows,
    bool Changed);

/// <summary>Translates visible status text using only caller-supplied source rows.</summary>
public static class SourceBackedClientStatusTextTranslator
{
    private static readonly Regex IniSectionPattern = new(
        "^\\s*\\[(?<section>[^\\]]+)\\]\\s*$",
        RegexOptions.CultureInvariant);

    private static readonly Regex LuaIdAssignmentPattern = new(
        "^\\s*\\[\"Id\"\\]\\s*=\\s*(?<id>\\d+)\\b",
        RegexOptions.CultureInvariant);

    private static readonly Regex LuaSkillAssignmentPattern = new(
        "^(?<prefix>\\s*\\[\"Skill\"\\]\\s*=\\s*\")(?<value>(?:\\\\.|[^\"\\\\])*)(?<suffix>\".*)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex FormatTokenPattern = new(
        "%(?:%|[-+ #0]*(?:\\d+|\\*)?(?:\\.(?:\\d+|\\*))?[hlL]?[A-Za-z])",
        RegexOptions.CultureInvariant);

    private static readonly HashSet<string> DefaultIniVisibleTextKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Name",
        "Num",
        "Note"
    };

    private static readonly Encoding CodePage936Encoding =
        ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);

    public static SourceBackedClientStatusTextTranslationResult PatchStatusTipsFile(
        string statusTipsPath,
        IEnumerable<SourceBackedClientStatusTextSource> sources)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statusTipsPath);
        ArgumentNullException.ThrowIfNull(sources);

        var originalBytes = File.ReadAllBytes(statusTipsPath);
        var originalText = CodePage936Encoding.GetString(originalBytes);
        var result = TranslateStatusTips(originalText, sources, out var updatedText);
        if (result.Changed)
        {
            ClientTextPatchWriter.WritePatchedTextPreservingUnchangedBytes(
                statusTipsPath,
                originalBytes,
                originalText,
                updatedText,
                CodePage936Encoding);
        }

        return result;
    }

    public static SourceBackedClientStatusTextTranslationResult TranslateStatusTips(
        string statusTipsText,
        IEnumerable<SourceBackedClientStatusTextSource> sources,
        out string updatedText)
    {
        ArgumentNullException.ThrowIfNull(statusTipsText);
        ArgumentNullException.ThrowIfNull(sources);

        var sourceCatalog = BuildStatusSourceCatalog(sources);
        var builder = new StringBuilder(statusTipsText.Length);
        var targetRows = 0;
        var rowsNeedingTranslation = 0;
        var matchedRows = 0;
        var placeholderMismatchRows = 0;
        var unmatchedRows = 0;
        var alreadyTranslatedRows = 0;
        var changedRows = 0;
        var remainingCjkRows = 0;
        var currentSection = string.Empty;

        foreach (var line in SplitLines(statusTipsText))
        {
            if (TryParseIniSection(line.Text, out var section))
            {
                currentSection = section;
                AppendLine(builder, line);
                continue;
            }

            if (currentSection.Length == 0 ||
                !TryParseIniKeyValue(line.Text, out var key, out var value, out var prefix, out var separator) ||
                !DefaultIniVisibleTextKeys.Contains(key))
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
            var sourceKey = MakeSourceKey(currentSection, key);
            if (!sourceCatalog.Values.TryGetValue(sourceKey, out var replacement))
            {
                unmatchedRows++;
                remainingCjkRows++;
                AppendLine(builder, line);
                continue;
            }

            if (!HaveCompatibleFormatTokens(value, replacement))
            {
                placeholderMismatchRows++;
                remainingCjkRows++;
                AppendLine(builder, line);
                continue;
            }

            matchedRows++;
            var translatedLine = prefix + key + separator + replacement;
            if (!StringComparer.Ordinal.Equals(translatedLine, line.Text))
            {
                changedRows++;
            }

            if (ContainsCjkOrFullwidth(replacement))
            {
                remainingCjkRows++;
            }

            builder.Append(translatedLine);
            builder.Append(line.LineEnding);
        }

        updatedText = builder.ToString();
        EnsureCp936Encodable(updatedText);
        return new SourceBackedClientStatusTextTranslationResult(
            sourceCatalog.SourceRows,
            sourceCatalog.Values.Count,
            sourceCatalog.ConflictingKeys.Select(FormatSourceKey).Order(StringComparer.Ordinal).ToArray(),
            targetRows,
            rowsNeedingTranslation,
            matchedRows,
            placeholderMismatchRows,
            unmatchedRows,
            alreadyTranslatedRows,
            changedRows,
            remainingCjkRows,
            !StringComparer.Ordinal.Equals(statusTipsText, updatedText));
    }

    public static IReadOnlyList<SourceBackedClientStatusTextSource> ReadIniStatusTextSources(
        string path,
        string? sourceName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var text = CodePage936Encoding.GetString(File.ReadAllBytes(path));
        return ReadIniStatusTextSourcesFromText(text, sourceName ?? path);
    }

    public static IReadOnlyList<SourceBackedClientStatusTextSource> ReadIniStatusTextSourcesFromText(
        string text,
        string sourceName)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        var sources = new List<SourceBackedClientStatusTextSource>();
        var currentSection = string.Empty;
        var currentSectionValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in SplitLines(text))
        {
            if (TryParseIniSection(line.Text, out var section))
            {
                AddMagicEffectStatusNameSourceIfUseful(sources, currentSection, currentSectionValues, sourceName);
                currentSection = section;
                currentSectionValues.Clear();
                continue;
            }

            if (currentSection.Length == 0 ||
                !TryParseIniKeyValue(line.Text, out var key, out var value, out _, out _))
            {
                continue;
            }

            currentSectionValues.TryAdd(key, value);
            if (DefaultIniVisibleTextKeys.Contains(key) && IsUsefulEnglishValue(value))
            {
                sources.Add(new SourceBackedClientStatusTextSource(currentSection, key, value, sourceName));
            }
        }

        AddMagicEffectStatusNameSourceIfUseful(sources, currentSection, currentSectionValues, sourceName);
        return sources;
    }

    public static SourceBackedClientInfoSkillNameTranslationResult PatchInfoLuaFile(
        string infoLuaPath,
        IEnumerable<string> magicTypePaths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(infoLuaPath);
        ArgumentNullException.ThrowIfNull(magicTypePaths);

        var originalBytes = File.ReadAllBytes(infoLuaPath);
        var originalText = CodePage936Encoding.GetString(originalBytes);
        var skillSources = ReadMagicTypeSkillNameSources(magicTypePaths);
        var result = TranslateInfoLuaSkillNames(originalText, skillSources, out var updatedText);
        if (result.Changed)
        {
            ClientTextPatchWriter.WritePatchedTextPreservingUnchangedBytes(
                infoLuaPath,
                originalBytes,
                originalText,
                updatedText,
                CodePage936Encoding);
        }

        return result;
    }

    public static SourceBackedClientInfoSkillNameTranslationResult TranslateInfoLuaSkillNames(
        string infoLuaText,
        IEnumerable<SourceBackedClientSkillNameSource> skillSources,
        out string updatedText)
    {
        ArgumentNullException.ThrowIfNull(infoLuaText);
        ArgumentNullException.ThrowIfNull(skillSources);

        var sourceCatalog = BuildSkillNameSourceCatalog(skillSources);
        var uniqueSkillIds = new HashSet<int>();
        var builder = new StringBuilder(infoLuaText.Length);
        int? currentSkillId = null;
        var skillRows = 0;
        var matchedSkillRows = 0;
        var missingSkillRows = 0;
        var translatedRows = 0;
        var changedRows = 0;
        var remainingCjkRows = 0;

        foreach (var line in SplitLines(infoLuaText))
        {
            var idMatch = LuaIdAssignmentPattern.Match(line.Text);
            if (idMatch.Success &&
                int.TryParse(idMatch.Groups["id"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedSkillId))
            {
                currentSkillId = parsedSkillId;
            }

            var skillMatch = LuaSkillAssignmentPattern.Match(line.Text);
            if (!skillMatch.Success || currentSkillId is not { } skillId)
            {
                AppendLine(builder, line);
                if (IsLuaTableClose(line.Text))
                {
                    currentSkillId = null;
                }

                continue;
            }

            skillRows++;
            uniqueSkillIds.Add(skillId);
            var value = skillMatch.Groups["value"].Value;
            if (!ContainsCjkOrFullwidth(value))
            {
                AppendLine(builder, line);
                if (IsLuaTableClose(line.Text))
                {
                    currentSkillId = null;
                }

                continue;
            }

            if (!sourceCatalog.Names.TryGetValue(skillId, out var sourceName))
            {
                missingSkillRows++;
                remainingCjkRows++;
                AppendLine(builder, line);
                if (IsLuaTableClose(line.Text))
                {
                    currentSkillId = null;
                }

                continue;
            }

            matchedSkillRows++;
            translatedRows++;
            var translatedValue = EscapeLuaString(sourceName);
            var translatedLine = skillMatch.Groups["prefix"].Value + translatedValue + skillMatch.Groups["suffix"].Value;
            if (!StringComparer.Ordinal.Equals(translatedLine, line.Text))
            {
                changedRows++;
            }

            builder.Append(translatedLine);
            builder.Append(line.LineEnding);
            if (IsLuaTableClose(line.Text))
            {
                currentSkillId = null;
            }
        }

        updatedText = builder.ToString();
        EnsureCp936Encodable(updatedText);
        return new SourceBackedClientInfoSkillNameTranslationResult(
            skillRows,
            uniqueSkillIds.Count,
            sourceCatalog.Names.Count,
            sourceCatalog.Conflicts.Order().ToArray(),
            matchedSkillRows,
            missingSkillRows,
            translatedRows,
            changedRows,
            remainingCjkRows,
            !StringComparer.Ordinal.Equals(infoLuaText, updatedText));
    }

    public static IReadOnlyList<SourceBackedClientSkillNameSource> ReadMagicTypeSkillNameSources(
        IEnumerable<string> magicTypePaths)
    {
        ArgumentNullException.ThrowIfNull(magicTypePaths);

        var sources = new List<SourceBackedClientSkillNameSource>();
        foreach (var path in magicTypePaths)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            var text = CodePage936Encoding.GetString(File.ReadAllBytes(path));
            sources.AddRange(ReadMagicTypeSkillNameSourcesFromText(text, path));
        }

        return sources;
    }

    public static IReadOnlyList<SourceBackedClientSkillNameSource> ReadMagicTypeSkillNameSourcesFromText(
        string magicTypeText,
        string sourceName)
    {
        ArgumentNullException.ThrowIfNull(magicTypeText);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        var sources = new List<SourceBackedClientSkillNameSource>();
        foreach (var line in SplitLines(magicTypeText))
        {
            var columns = line.Text.Split("@@", StringSplitOptions.None);
            if (columns.Length < 4 ||
                !int.TryParse(columns[1], NumberStyles.None, CultureInfo.InvariantCulture, out var skillId))
            {
                continue;
            }

            var name = columns[3].Trim();
            if (IsUsefulEnglishValue(name))
            {
                sources.Add(new SourceBackedClientSkillNameSource(skillId, name, sourceName));
            }
        }

        return sources;
    }

    private static StatusSourceCatalog BuildStatusSourceCatalog(IEnumerable<SourceBackedClientStatusTextSource> sources)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var conflicts = new HashSet<string>(StringComparer.Ordinal);
        var sourceRows = 0;

        foreach (var source in sources)
        {
            if (string.IsNullOrWhiteSpace(source.Section) ||
                string.IsNullOrWhiteSpace(source.Key) ||
                !IsUsefulEnglishValue(source.Value))
            {
                continue;
            }

            sourceRows++;
            var key = MakeSourceKey(source.Section, source.Key);
            if (conflicts.Contains(key))
            {
                continue;
            }

            if (values.TryGetValue(key, out var existingValue))
            {
                if (!StringComparer.Ordinal.Equals(existingValue, source.Value))
                {
                    values.Remove(key);
                    conflicts.Add(key);
                }

                continue;
            }

            values.Add(key, source.Value);
        }

        return new StatusSourceCatalog(sourceRows, values, conflicts);
    }

    private static SkillNameSourceCatalog BuildSkillNameSourceCatalog(
        IEnumerable<SourceBackedClientSkillNameSource> sources)
    {
        var names = new Dictionary<int, string>();
        var conflicts = new HashSet<int>();

        foreach (var source in sources)
        {
            if (source.SkillId <= 0 || conflicts.Contains(source.SkillId) || !IsUsefulEnglishValue(source.Name))
            {
                continue;
            }

            if (names.TryGetValue(source.SkillId, out var existing))
            {
                if (!StringComparer.Ordinal.Equals(existing, source.Name))
                {
                    names.Remove(source.SkillId);
                    conflicts.Add(source.SkillId);
                }

                continue;
            }

            names.Add(source.SkillId, source.Name);
        }

        return new SkillNameSourceCatalog(names, conflicts);
    }

    private static void AddMagicEffectStatusNameSourceIfUseful(
        List<SourceBackedClientStatusTextSource> sources,
        string section,
        Dictionary<string, string> sectionValues,
        string sourceName)
    {
        if (section.Length == 0 ||
            !long.TryParse(section, NumberStyles.None, CultureInfo.InvariantCulture, out _) ||
            !sectionValues.TryGetValue("Name", out var name) ||
            !sectionValues.TryGetValue("Status", out var status) ||
            !sectionValues.TryGetValue("ClientRepresent", out var clientRepresent) ||
            !sectionValues.TryGetValue("CoolDown", out var coolDown) ||
            !IsUsefulEnglishValue(name) ||
            !IsNonNegativeInteger(status) ||
            !StringComparer.Ordinal.Equals(clientRepresent.Trim(), "1") ||
            !IsPositiveInteger(coolDown))
        {
            return;
        }

        sources.Add(new SourceBackedClientStatusTextSource(
            status.Trim(),
            "Name",
            name,
            sourceName + ":[" + section.Trim() + "] Status"));
    }

    private static bool IsNonNegativeInteger(string value)
    {
        return long.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) &&
            parsed >= 0;
    }

    private static bool IsPositiveInteger(string value)
    {
        return long.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) &&
            parsed > 0;
    }

    private static string MakeSourceKey(string section, string key)
    {
        return section.Trim() + "\u001f" + key.Trim().ToUpperInvariant();
    }

    private static string FormatSourceKey(string sourceKey)
    {
        var separatorIndex = sourceKey.IndexOf('\u001f', StringComparison.Ordinal);
        return separatorIndex < 0
            ? sourceKey
            : sourceKey[..separatorIndex] + ":" + sourceKey[(separatorIndex + 1)..];
    }

    private static bool TryParseIniSection(string line, out string section)
    {
        var match = IniSectionPattern.Match(line);
        if (!match.Success)
        {
            section = string.Empty;
            return false;
        }

        section = match.Groups["section"].Value.Trim();
        return section.Length > 0;
    }

    private static bool TryParseIniKeyValue(
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
        value = line[valueStart..];
        return key.Length > 0;
    }

    private static bool HaveCompatibleFormatTokens(string targetValue, string sourceValue)
    {
        var targetTokens = ReadFormatTokens(targetValue);
        var sourceTokens = ReadFormatTokens(sourceValue);
        return targetTokens.SequenceEqual(sourceTokens, StringComparer.Ordinal);
    }

    private static IReadOnlyList<string> ReadFormatTokens(string value)
    {
        return FormatTokenPattern
            .Matches(value)
            .Select(match => match.Value)
            .ToArray();
    }

    private static bool IsLuaTableClose(string line)
    {
        return line.TrimStart().StartsWith('}');
    }

    private static string EscapeLuaString(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static bool IsUsefulEnglishValue(string value)
    {
        return value.Length > 0 &&
            ContainsAsciiLetter(value) &&
            !ContainsCjkOrFullwidth(value) &&
            !value.Contains('\uFFFD', StringComparison.Ordinal) &&
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
            _ = CodePage936Encoding.GetByteCount(value);
            return true;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    private static void EnsureCp936Encodable(string value)
    {
        _ = CodePage936Encoding.GetBytes(value);
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

    private sealed record StatusSourceCatalog(
        int SourceRows,
        Dictionary<string, string> Values,
        HashSet<string> ConflictingKeys);

    private sealed record SkillNameSourceCatalog(
        Dictionary<int, string> Names,
        HashSet<int> Conflicts);


}
