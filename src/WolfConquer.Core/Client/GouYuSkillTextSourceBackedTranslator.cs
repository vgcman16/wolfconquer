using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace WolfConquer.Core.Client;

public sealed record GouYuSkillTextTranslationResult(
    int TextRowsScanned,
    int UniqueTextIds,
    int SourceSkillNames,
    IReadOnlyList<int> ConflictingSourceIds,
    IReadOnlyList<int> MissingSourceIds,
    int MatchedTextIds,
    int TranslatedRows,
    int ChangedRows,
    int RemainingCjkRows,
    bool Changed);

public static class GouYuSkillTextSourceBackedTranslator
{
    private static readonly Regex GouYuTextAssignmentPattern = new(
        "^(?<prefix>\\s*tGouYuItemTipText\\[(?<id>\\d+)\\]\\[\"(?<key>Text[^\"]*)\"\\]\\s*=\\s*\")(?<value>(?:\\\\.|[^\"\\\\])*)(?<suffix>\".*)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex LuaStringAssignmentPattern = new(
        "^(?<prefix>\\s*(?<key>Text[1-6]|text)\\s*=\\s*\")(?<value>(?:\\\\.|[^\"\\\\])*)(?<suffix>\".*)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex MagicEffectSectionPattern = new(
        "^\\s*\\[(?<id>\\d+)\\]\\s*$",
        RegexOptions.CultureInvariant);

    private static readonly Regex MagicEffectNamePattern = new(
        "^\\s*Name\\s*=\\s*(?<value>.*?)\\s*$",
        RegexOptions.CultureInvariant);

    private static readonly Regex ColorWrappedValuePattern = new(
        "^(?<open><color=0x[0-9A-Fa-f]+>)(?<inner>.*)(?<close></color>)$",
        RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private static readonly Regex ColorTagPattern = new(
        "</?color(?:=0x[0-9A-Fa-f]+)?>",
        RegexOptions.CultureInvariant);

    private static readonly Regex ColorSegmentPattern = new(
        "<color=0x[0-9A-Fa-f]+>(?<inner>.*?)</color>",
        RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private static readonly Regex Text2LevelValuePattern = new(
        "^等级：(?<level>\\d+)级$",
        RegexOptions.CultureInvariant);

    private static readonly Regex Text3CooldownValuePattern = new(
        "^技能cd：(?<seconds>\\d+)秒$",
        RegexOptions.CultureInvariant);

    private static readonly Encoding GouYuEncoding =
        ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);

    public static GouYuSkillTextTranslationResult PatchFile(
        string gouYuPath,
        IEnumerable<string> magicEffectPaths,
        IEnumerable<string> magicTypePaths)
    {
        return PatchFile(gouYuPath, magicEffectPaths, magicTypePaths, []);
    }

    public static GouYuSkillTextTranslationResult PatchFile(
        string gouYuPath,
        IEnumerable<string> magicEffectPaths,
        IEnumerable<string> magicTypePaths,
        IEnumerable<string> gouYuTypePaths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gouYuPath);
        ArgumentNullException.ThrowIfNull(magicEffectPaths);
        ArgumentNullException.ThrowIfNull(magicTypePaths);
        ArgumentNullException.ThrowIfNull(gouYuTypePaths);

        var gouYuText = GouYuEncoding.GetString(File.ReadAllBytes(gouYuPath));
        var magicEffectTexts = magicEffectPaths
            .Select(path => GouYuEncoding.GetString(File.ReadAllBytes(path)))
            .ToArray();
        var magicTypeTexts = magicTypePaths
            .Select(path => GouYuEncoding.GetString(File.ReadAllBytes(path)))
            .ToArray();
        var gouYuTypeTexts = gouYuTypePaths
            .Select(path => GouYuEncoding.GetString(File.ReadAllBytes(path)))
            .ToArray();

        var result = Translate(gouYuText, magicEffectTexts, magicTypeTexts, gouYuTypeTexts, out var updatedText);
        if (result.Changed)
        {
            File.WriteAllBytes(gouYuPath, GouYuEncoding.GetBytes(updatedText));
        }

        return result;
    }

    public static GouYuSkillTextTranslationResult Translate(
        string gouYuText,
        IEnumerable<string> magicEffectTexts,
        IEnumerable<string> magicTypeTexts,
        out string updatedText)
    {
        return Translate(gouYuText, magicEffectTexts, magicTypeTexts, [], out updatedText);
    }

    public static GouYuSkillTextTranslationResult Translate(
        string gouYuText,
        IEnumerable<string> magicEffectTexts,
        IEnumerable<string> magicTypeTexts,
        IEnumerable<string> gouYuTypeTexts,
        out string updatedText)
    {
        ArgumentNullException.ThrowIfNull(gouYuText);
        ArgumentNullException.ThrowIfNull(magicEffectTexts);
        ArgumentNullException.ThrowIfNull(magicTypeTexts);
        ArgumentNullException.ThrowIfNull(gouYuTypeTexts);

        var source = BuildSourceCatalog(magicEffectTexts, magicTypeTexts, gouYuTypeTexts);
        var targetRows = ReadTargetRows(gouYuText);
        var targetNames = BuildTargetNames(targetRows);
        var sourcePhraseReplacements = BuildSourcePhraseReplacements(targetRows, source.Names);
        var uniqueTextIds = targetRows.Select(row => row.Id).ToHashSet();
        var missingSourceIds = new HashSet<int>();
        var matchedTextIds = new HashSet<int>();
        var builder = new StringBuilder(gouYuText.Length);
        var translatedRows = 0;
        var changedRows = 0;
        var remainingCjkRows = 0;

        foreach (var line in SplitLines(gouYuText))
        {
            var match = GouYuTextAssignmentPattern.Match(line.Text);
            if (!match.Success ||
                !int.TryParse(match.Groups["id"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            {
                var luaStringMatch = LuaStringAssignmentPattern.Match(line.Text);
                if (luaStringMatch.Success)
                {
                    var tableKey = luaStringMatch.Groups["key"].Value;
                    var tableValue = luaStringMatch.Groups["value"].Value;
                    var updatedTableValue = UpdateCommonLuaTableStringValue(tableKey, tableValue);
                    if (ContainsCjkOrFullwidth(updatedTableValue))
                    {
                        remainingCjkRows++;
                    }

                    if (!StringComparer.Ordinal.Equals(tableValue, updatedTableValue))
                    {
                        translatedRows++;
                        var updatedTableLine = luaStringMatch.Groups["prefix"].Value + updatedTableValue + luaStringMatch.Groups["suffix"].Value;
                        if (!StringComparer.Ordinal.Equals(line.Text, updatedTableLine))
                        {
                            changedRows++;
                        }

                        builder.Append(updatedTableLine);
                        builder.Append(line.LineEnding);
                        continue;
                    }
                }

                AppendLine(builder, line);
                continue;
            }

            var key = match.Groups["key"].Value;
            var value = match.Groups["value"].Value;
            var updatedValue = UpdateCommonLuaStringValue(key, value);
            if (targetNames.TryGetValue(id, out var targetName) &&
                source.Names.TryGetValue(id, out var sourceName))
            {
                matchedTextIds.Add(id);
                updatedValue = UpdateLuaStringValue(updatedValue, targetName, sourceName);
            }
            else if (targetNames.ContainsKey(id))
            {
                missingSourceIds.Add(id);
            }

            if (!key.Equals("Text1", StringComparison.OrdinalIgnoreCase))
            {
                updatedValue = UpdateSourceBackedPhrases(updatedValue, sourcePhraseReplacements);
            }

            if (ContainsCjkOrFullwidth(updatedValue))
            {
                remainingCjkRows++;
            }

            if (StringComparer.Ordinal.Equals(value, updatedValue))
            {
                AppendLine(builder, line);
                continue;
            }

            translatedRows++;
            var updatedLine = match.Groups["prefix"].Value + updatedValue + match.Groups["suffix"].Value;
            if (!StringComparer.Ordinal.Equals(line.Text, updatedLine))
            {
                changedRows++;
            }

            builder.Append(updatedLine);
            builder.Append(line.LineEnding);
        }

        updatedText = builder.ToString();
        EnsureCp936Encodable(updatedText);

        return new GouYuSkillTextTranslationResult(
            targetRows.Count,
            uniqueTextIds.Count,
            source.Names.Count,
            source.Conflicts.Order().ToArray(),
            missingSourceIds.Order().ToArray(),
            matchedTextIds.Count,
            translatedRows,
            changedRows,
            remainingCjkRows,
            !StringComparer.Ordinal.Equals(gouYuText, updatedText));
    }

    private static GouYuSourceCatalog BuildSourceCatalog(
        IEnumerable<string> magicEffectTexts,
        IEnumerable<string> magicTypeTexts,
        IEnumerable<string> gouYuTypeTexts)
    {
        var directBuilder = new SourceCatalogBuilder();
        var magicTypeNameBuilder = new SourceCatalogBuilder();

        foreach (var magicEffectText in magicEffectTexts)
        {
            int? currentSourceId = null;
            foreach (var line in SplitLines(magicEffectText))
            {
                var sectionMatch = MagicEffectSectionPattern.Match(line.Text);
                if (sectionMatch.Success)
                {
                    currentSourceId = null;
                    if (int.TryParse(sectionMatch.Groups["id"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var sectionId) &&
                        sectionId > 0 &&
                        sectionId % 100 == 0)
                    {
                        currentSourceId = sectionId / 100;
                    }

                    continue;
                }

                if (currentSourceId is not { } sourceId)
                {
                    continue;
                }

                var nameMatch = MagicEffectNamePattern.Match(line.Text);
                if (nameMatch.Success)
                {
                    directBuilder.Add(sourceId, nameMatch.Groups["value"].Value.Trim());
                }
            }
        }

        foreach (var magicTypeText in magicTypeTexts)
        {
            foreach (var line in SplitLines(magicTypeText))
            {
                var columns = line.Text.Split("@@", StringSplitOptions.None);
                if (columns.Length < 4 ||
                    !int.TryParse(columns[1], NumberStyles.None, CultureInfo.InvariantCulture, out var sourceId))
                {
                    continue;
                }

                var name = columns[3].Trim();
                directBuilder.Add(sourceId, name);
                magicTypeNameBuilder.Add(sourceId, name);
            }
        }

        var direct = directBuilder.Build();
        var magicTypeNames = magicTypeNameBuilder.Build().Names;
        var gouYuTypeBuilder = new SourceCatalogBuilder();
        foreach (var gouYuTypeText in gouYuTypeTexts)
        {
            foreach (var line in SplitLines(gouYuTypeText))
            {
                var columns = line.Text.Split("@@", StringSplitOptions.None);
                if (columns.Length < 8 ||
                    !int.TryParse(columns[1], NumberStyles.None, CultureInfo.InvariantCulture, out var gouYuId))
                {
                    continue;
                }

                foreach (var magicTypeId in ReadGouYuTypeMagicTypeIds(columns))
                {
                    if (magicTypeNames.TryGetValue(magicTypeId, out var sourceName))
                    {
                        gouYuTypeBuilder.Add(gouYuId, sourceName);
                        break;
                    }
                }
            }
        }

        var gouYuType = gouYuTypeBuilder.Build();
        var names = new Dictionary<int, string>(direct.Names);
        foreach (var (id, name) in gouYuType.Names)
        {
            names[id] = name;
        }

        var conflicts = new HashSet<int>(direct.Conflicts);
        conflicts.ExceptWith(gouYuType.Names.Keys);
        conflicts.UnionWith(gouYuType.Conflicts);
        foreach (var id in gouYuType.Conflicts)
        {
            names.Remove(id);
        }

        return new GouYuSourceCatalog(names, conflicts);
    }

    private static List<GouYuTargetRow> ReadTargetRows(string gouYuText)
    {
        var rows = new List<GouYuTargetRow>();
        foreach (var line in SplitLines(gouYuText))
        {
            var match = GouYuTextAssignmentPattern.Match(line.Text);
            if (!match.Success ||
                !int.TryParse(match.Groups["id"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            {
                continue;
            }

            rows.Add(new GouYuTargetRow(id, match.Groups["key"].Value, match.Groups["value"].Value));
        }

        return rows;
    }

    private static Dictionary<int, string> BuildTargetNames(IEnumerable<GouYuTargetRow> rows)
    {
        var targetNames = new Dictionary<int, string>();
        foreach (var row in rows)
        {
            if (!row.Key.Equals("Text1", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var displayName = StripColorTags(row.Value).Trim();
            if (displayName.Length > 0 && ContainsCjkOrFullwidth(displayName))
            {
                targetNames[row.Id] = displayName;
            }
        }

        return targetNames;
    }

    private static IReadOnlyList<SourcePhraseReplacement> BuildSourcePhraseReplacements(
        IEnumerable<GouYuTargetRow> rows,
        IReadOnlyDictionary<int, string> sourceNames)
    {
        var replacements = new Dictionary<string, string>(StringComparer.Ordinal);
        var conflicts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (!row.Key.Equals("Text5", StringComparison.OrdinalIgnoreCase) ||
                !sourceNames.TryGetValue(row.Id, out var sourceName))
            {
                continue;
            }

            foreach (var phrase in ExtractSourceBackedSkillPhrases(row.Value))
            {
                if (conflicts.Contains(phrase))
                {
                    continue;
                }

                if (replacements.TryGetValue(phrase, out var existing))
                {
                    if (!StringComparer.Ordinal.Equals(existing, sourceName))
                    {
                        replacements.Remove(phrase);
                        conflicts.Add(phrase);
                    }

                    continue;
                }

                replacements.Add(phrase, sourceName);
            }
        }

        return replacements
            .Select(pair => new SourcePhraseReplacement(pair.Key, pair.Value))
            .OrderByDescending(replacement => replacement.SourcePhrase.Length)
            .ThenBy(replacement => replacement.SourcePhrase, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<string> ExtractSourceBackedSkillPhrases(string value)
    {
        foreach (Match match in ColorSegmentPattern.Matches(value))
        {
            var inner = match.Groups["inner"].Value.Trim();
            if (IsSourceBackedSkillPhrase(inner))
            {
                yield return inner;
            }
        }
    }

    private static bool IsSourceBackedSkillPhrase(string value)
    {
        return value.Length is >= 2 and <= 16 &&
            ContainsCjkOrFullwidth(value) &&
            !value.Any(char.IsWhiteSpace) &&
            value.IndexOfAny(['。', '，', '、', '；', '：', '（', '）', '(', ')', '%']) < 0;
    }

    private static IEnumerable<int> ReadGouYuTypeMagicTypeIds(string[] columns)
    {
        foreach (var index in new[] { 6, 7 })
        {
            if (index >= columns.Length ||
                !int.TryParse(columns[index], NumberStyles.None, CultureInfo.InvariantCulture, out var magicTypeId) ||
                magicTypeId <= 0)
            {
                continue;
            }

            yield return magicTypeId;
        }
    }

    private static string UpdateCommonLuaStringValue(string key, string value)
    {
        if (key.Equals("Text2", StringComparison.OrdinalIgnoreCase))
        {
            var updatedValue = value
                .Replace("类型：", "Type: ", StringComparison.Ordinal)
                .Replace("火遁法印", "Fire Release Seal", StringComparison.Ordinal)
                .Replace("水遁法印", "Water Release Seal", StringComparison.Ordinal)
                .Replace("土遁法印", "Earth Release Seal", StringComparison.Ordinal)
                .Replace("风遁法印", "Wind Release Seal", StringComparison.Ordinal)
                .Replace("雷遁法印", "Lightning Release Seal", StringComparison.Ordinal)
                .Replace("血继法印", "Bloodline Seal", StringComparison.Ordinal);

            return UpdateWholeValueOrColorWrappedInner(updatedValue, TranslateText2LevelValue);
        }

        if (key.Equals("Text3", StringComparison.OrdinalIgnoreCase))
        {
            return UpdateText3LabelValue(value);
        }

        if (key.Equals("Text4", StringComparison.OrdinalIgnoreCase))
        {
            return UpdateWholeValueOrColorWrappedInner(value, TranslateText4UnlockRequirementValue);
        }

        if (key.Equals("Text6", StringComparison.OrdinalIgnoreCase))
        {
            return UpdateWholeValueOrColorWrappedInner(value, TranslateText6LabelValue);
        }

        return value;
    }

    private static string UpdateCommonLuaTableStringValue(string key, string value)
    {
        if (key.Equals("Text2", StringComparison.OrdinalIgnoreCase))
        {
            return UpdateWholeValueOrColorWrappedInner(value, TranslateText2LevelValue);
        }

        if (key.Equals("Text3", StringComparison.OrdinalIgnoreCase))
        {
            return UpdateWholeValueOrColorWrappedInner(value, TranslateExperienceValue);
        }

        if (key.Equals("Text4", StringComparison.OrdinalIgnoreCase))
        {
            return UpdateWholeValueOrColorWrappedInner(value, TranslateText4UnlockRequirementValue);
        }

        if (key.Equals("Text6", StringComparison.OrdinalIgnoreCase))
        {
            return UpdateWholeValueOrColorWrappedInner(value, TranslateText6LabelValue);
        }

        if (key.Equals("text", StringComparison.Ordinal))
        {
            return TranslateGouYuHandbookRowName(value) ?? value;
        }

        return value;
    }

    private static string UpdateText3LabelValue(string value)
    {
        var updatedWholeValue = TranslateText3LabelSegmentValue(value);
        if (updatedWholeValue is not null)
        {
            return updatedWholeValue;
        }

        var wrappedMatch = ColorWrappedValuePattern.Match(value);
        if (wrappedMatch.Success)
        {
            var updatedWrappedInner = TranslateText3LabelSegmentValue(wrappedMatch.Groups["inner"].Value);
            if (updatedWrappedInner is not null)
            {
                return wrappedMatch.Groups["open"].Value + updatedWrappedInner + wrappedMatch.Groups["close"].Value;
            }
        }

        return ColorSegmentPattern.Replace(
            value,
            match =>
            {
                var innerGroup = match.Groups["inner"];
                var inner = innerGroup.Value;
                var updatedInner = TranslateText3LabelSegmentValue(inner);
                var relativeInnerIndex = innerGroup.Index - match.Index;
                return updatedInner is null
                    ? match.Value
                    : match.Value[..relativeInnerIndex] + updatedInner + match.Value[(relativeInnerIndex + inner.Length)..];
            });
    }

    private static string UpdateWholeValueOrColorWrappedInner(string value, Func<string, string?> translate)
    {
        var wrappedMatch = ColorWrappedValuePattern.Match(value);
        if (wrappedMatch.Success)
        {
            var updatedInner = translate(wrappedMatch.Groups["inner"].Value);
            if (updatedInner is not null)
            {
                return wrappedMatch.Groups["open"].Value + updatedInner + wrappedMatch.Groups["close"].Value;
            }
        }

        return translate(value) ?? value;
    }

    private static string? TranslateText2LevelValue(string value)
    {
        var match = Text2LevelValuePattern.Match(value);
        if (!match.Success)
        {
            return null;
        }

        return "Level: " + match.Groups["level"].Value;
    }

    private static string? TranslateExperienceValue(string value)
    {
        return StringComparer.Ordinal.Equals(value, "经验：%.3f%%")
            ? "EXP: %.3f%%"
            : null;
    }

    private static string? TranslateText6LabelValue(string value)
    {
        if (StringComparer.Ordinal.Equals(value, "不可升级"))
        {
            return "Unupgradable";
        }

        return TranslateExperienceValue(value);
    }

    private static string? TranslateText3LabelSegmentValue(string value)
    {
        if (StringComparer.Ordinal.Equals(value, "等级："))
        {
            return "Level: ";
        }

        if (StringComparer.Ordinal.Equals(value, "%u级"))
        {
            return "%u";
        }

        var cooldownMatch = Text3CooldownValuePattern.Match(value);
        if (cooldownMatch.Success)
        {
            return "Cooldown: " + cooldownMatch.Groups["seconds"].Value + " sec";
        }

        return TranslateExperienceValue(value);
    }

    private static string? TranslateText4UnlockRequirementValue(string value)
    {
        return StringComparer.Ordinal.Equals(value, "解锁条件：开启双枪战装")
            ? "Unlock requirement: activate Dual-Gun Combat Gear"
            : null;
    }

    private static string? TranslateGouYuHandbookRowName(string value)
    {
        return value switch
        {
            "火遁" => "Fire Release",
            "水遁" => "Water Release",
            "土遁" => "Earth Release",
            "风遁" => "Wind Release",
            "雷遁" => "Lightning Release",
            "血继" => "Bloodline",
            _ => null,
        };
    }

    private static string UpdateSourceBackedPhrases(
        string value,
        IReadOnlyList<SourcePhraseReplacement> replacements)
    {
        var updatedValue = value;
        foreach (var replacement in replacements)
        {
            updatedValue = updatedValue.Replace(
                replacement.SourcePhrase,
                EscapeLuaString(replacement.SourceName),
                StringComparison.Ordinal);
        }

        return updatedValue;
    }

    private static string UpdateLuaStringValue(string value, string targetName, string sourceName)
    {
        var escapedSourceName = EscapeLuaString(sourceName);
        var wrappedMatch = ColorWrappedValuePattern.Match(value);
        if (wrappedMatch.Success &&
            StringComparer.Ordinal.Equals(wrappedMatch.Groups["inner"].Value, targetName))
        {
            return wrappedMatch.Groups["open"].Value + escapedSourceName + wrappedMatch.Groups["close"].Value;
        }

        return value.Replace(targetName, escapedSourceName, StringComparison.Ordinal);
    }

    private static string StripColorTags(string value)
    {
        return ColorTagPattern.Replace(value, string.Empty);
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
            _ = GouYuEncoding.GetByteCount(value);
            return true;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    private static void EnsureCp936Encodable(string value)
    {
        _ = GouYuEncoding.GetBytes(value);
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

    private sealed record GouYuTargetRow(int Id, string Key, string Value);

    private sealed record GouYuSourceCatalog(Dictionary<int, string> Names, HashSet<int> Conflicts);

    private sealed record SourcePhraseReplacement(string SourcePhrase, string SourceName);

    private sealed class SourceCatalogBuilder
    {
        private readonly Dictionary<int, string> names = new();
        private readonly HashSet<int> conflicts = [];

        public void Add(int id, string name)
        {
            if (conflicts.Contains(id) || !IsUsefulEnglishValue(name))
            {
                return;
            }

            if (names.TryGetValue(id, out var existing))
            {
                if (!StringComparer.Ordinal.Equals(existing, name))
                {
                    names.Remove(id);
                    conflicts.Add(id);
                }

                return;
            }

            names.Add(id, name);
        }

        public GouYuSourceCatalog Build()
        {
            return new GouYuSourceCatalog(names, conflicts);
        }
    }
}
