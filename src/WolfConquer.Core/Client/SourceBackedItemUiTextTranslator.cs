using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace WolfConquer.Core.Client;

public sealed record SourceBackedItemUiTextSource(
    int ItemId,
    string Field,
    string Text,
    string Source);

public sealed record SourceBackedItemUiTextBlockedRow(
    string Target,
    int ItemId,
    string Field,
    string Reason);

public sealed record SourceBackedItemUiTextConflict(
    int ItemId,
    string Field);

public sealed record SourceBackedItemUiTextTranslationResult(
    string Target,
    int RowCount,
    int RowsNeedingTranslation,
    int SourceMatchedRows,
    int SkippedRowsWithoutSource,
    int SkippedRowsWithConflictingSource,
    int SkippedRowsWithUnsafeReplacement,
    int SkippedRowsWithUnsupportedField,
    int UniqueItemIdsNeedingTranslation,
    int MatchedUniqueItemIds,
    IReadOnlyList<SourceBackedItemUiTextBlockedRow> BlockedRows,
    IReadOnlyList<SourceBackedItemUiTextConflict> Conflicts,
    int ChangedRows,
    bool Changed);

public static class SourceBackedItemUiTextTranslator
{
    public const string NameField = "Name";
    public const string DescriptionField = "Description";

    private static readonly Regex LuaShowNameStartPattern = new(
        @"^\s*local\s+ShowName\s*=\s*\{",
        RegexOptions.CultureInvariant);

    private static readonly Regex LuaTableStartPattern = new(
        @"^\s*local\s+(?<table>[A-Za-z][A-Za-z0-9_]*)\s*=\s*\{",
        RegexOptions.CultureInvariant);

    private static readonly Regex LuaSimpleStringRowPattern = new(
        "^(?<prefix>\\s*\\[(?<id>\\d+)\\]\\s*=\\s*\")(?<value>(?:\\\\.|[^\"\\\\])*)(?<suffix>\"\\s*,?.*)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex LuaNestedTableStartPattern = new(
        @"^\s*\[(?<id>\d+)\]\s*=\s*\{\s*$",
        RegexOptions.CultureInvariant);

    private static readonly Regex LuaNamedStringFieldPattern = new(
        "\\[\"(?<field>[A-Za-z][A-Za-z0-9_]*)\"\\]\\s*=\\s*\"(?<value>(?:\\\\.|[^\"\\\\])*)\"",
        RegexOptions.CultureInvariant);

    private static readonly Regex ServerKeyItemDescriptionPattern = new(
        "^\\s*\\[\"STR_ITEM_ITEMDESC_(?<id>\\d+)\"\\]\\s*=\\s*\"(?<value>(?:\\\\.|[^\"\\\\])*)\"\\s*,?.*$",
        RegexOptions.CultureInvariant);

    private static readonly Regex ServerKeyItemNamePattern = new(
        "^\\s*\\[\"STR_ITEM_TYPE_(?<id>\\d+)\"\\]\\s*=\\s*\"(?<value>(?:\\\\.|[^\"\\\\])*)\"\\s*,?.*$",
        RegexOptions.CultureInvariant);

    private static readonly Regex IniSectionPattern = new(
        @"^\s*\[(?<section>[^\]]+)\]\s*(?:[;#].*)?$",
        RegexOptions.CultureInvariant);

    private static readonly Regex IniKeyValuePattern = new(
        @"^(?<prefix>\s*(?<key>[A-Za-z][A-Za-z0-9_]*)\s*=\s*)(?<value>.*?)(?<suffix>\s*)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex PrintfPlaceholderPattern = new(
        "%(?:[-+ #0]*(?:\\d+|\\*)?(?:\\.(?:\\d+|\\*))?[hlLzjt]*)?[diuoxXfFeEgGaAcspn]",
        RegexOptions.CultureInvariant);

    private static readonly Regex ColorTokenPattern = new(
        @"</?color(?:=[^>]+)?>|0x[0-9a-fA-F]{8}",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ItemQuenchNumPercentPattern = new(
        @"^%d级(?<term>.+?)[:：]\+%d%%(?<suffix>[，,]?)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex ItemQuenchNumFlatPattern = new(
        @"^%d级(?<term>.+?)[:：]\+%d(?<suffix>[，,]?)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex ItemQuenchAppendNumPattern = new(
        @"^%s(?<term>.+?)[:：]\+%d$",
        RegexOptions.CultureInvariant);

    private static readonly ISet<string> ExactIniTextFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        NameField,
        "Desc",
        DescriptionField,
        "Intro",
        "IntroName",
        "GetWayText",
        "GetWayText1",
        "GetWayText2",
        "GetWayText3",
        "DelTip",
        "AddTip",
        "Note",
        "Num",
        "AppendNum",
    };

    private static readonly ISet<string> DescriptionFallbackFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Desc",
        DescriptionField,
        "Intro",
    };

    private static readonly ISet<string> StorageExactTextFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Intro",
        "GetWayText",
        "GetWayText1",
        "GetWayText2",
        "GetWayText3",
        "DelTip",
        "AddTip",
    };

    private static readonly ISet<string> LuaTextFieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        NameField,
        "Desc",
        DescriptionField,
        "Intro",
        "IntroName",
        "Note",
        "Num",
        "Text",
        "Tip",
        "Tips",
    };

    private static readonly IReadOnlyDictionary<string, string> ItemQuenchTermTranslations =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["魔防增加"] = "Magic Defense Increase",
            ["法术穿透"] = "Magic Penetration",
            ["盾牌格挡"] = "Shield Block",
            ["生命增效"] = "Health Boost",
            ["最终物理伤害减免"] = "Final Physical Damage Reduction",
            ["最终物理伤害增效"] = "Final Physical Damage Boost",
            ["最终物伤减免"] = "Final Physical Damage Reduction",
            ["最终物伤增效"] = "Final Physical Damage Boost",
            ["毒伤减免"] = "Poison Damage Reduction",
            ["最终法术伤害增效"] = "Final Magic Damage Boost",
            ["最终法术伤害减免"] = "Final Magic Damage Reduction",
            ["最终法伤增效"] = "Final Magic Damage Boost",
            ["最终法伤减免"] = "Final Magic Damage Reduction",
            ["物理暴击"] = "Physical Critical",
            ["法术暴击"] = "Magic Critical",
            ["免暴机率"] = "Critical Immunity Rate",
            ["粉碎攻击"] = "Smash Attack",
            ["坚固防御"] = "Sturdy Defense",
            ["金属性抗性"] = "Metal Resistance",
            ["木属性抗性"] = "Wood Resistance",
            ["水属性抗性"] = "Water Resistance",
            ["火属性抗性"] = "Fire Resistance",
            ["土属性抗性"] = "Earth Resistance",
        };

    public static SourceBackedItemUiTextTranslationResult PatchFile(
        string targetPath,
        IEnumerable<string> sourcePaths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentNullException.ThrowIfNull(sourcePaths);

        var originalBytes = File.ReadAllBytes(targetPath);
        var decoded = DecodeTargetText(originalBytes);
        var sources = sourcePaths.SelectMany(ReadSourceTexts);
        var target = Path.GetFileName(targetPath);
        var result = Translate(decoded.Text, target, sources, out var updatedText);
        if (result.Changed)
        {
            if (decoded.HadUtf8Bom)
            {
                File.WriteAllBytes(targetPath, EncodeUtf8(updatedText, emitBom: true));
            }
            else
            {
                ClientTextPatchWriter.WritePatchedTextPreservingUnchangedBytes(
                    targetPath,
                    originalBytes,
                    decoded.Text,
                    updatedText,
                    decoded.Encoding);
            }
        }

        return result;
    }

    public static SourceBackedItemUiTextTranslationResult Translate(
        string targetText,
        string target,
        IEnumerable<SourceBackedItemUiTextSource> sources,
        out string updatedText)
    {
        ArgumentNullException.ThrowIfNull(targetText);
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        ArgumentNullException.ThrowIfNull(sources);

        var sourceCatalog = BuildSourceCatalog(sources);
        var targetKind = GetTargetKind(target);
        var neededIds = new HashSet<int>();
        var matchedIds = new HashSet<int>();
        var blockedRows = new List<SourceBackedItemUiTextBlockedRow>();
        var lines = SplitLines(targetText).ToArray();
        var state = new TargetParseState(targetKind);
        var rowCount = 0;
        var rowsNeedingTranslation = 0;
        var sourceMatchedRows = 0;
        var skippedRowsWithoutSource = 0;
        var skippedRowsWithConflictingSource = 0;
        var skippedRowsWithUnsafeReplacement = 0;
        var skippedRowsWithUnsupportedField = 0;
        var changedRows = 0;

        for (var index = 0; index < lines.Length; index++)
        {
            if (!TryParseTargetRows(lines[index].Text, targetKind, state, out var rows))
            {
                continue;
            }

            var replacements = new List<PendingReplacement>();
            foreach (var row in rows)
            {
                rowCount++;
                if (!NeedsTranslation(row.Value))
                {
                    continue;
                }

                rowsNeedingTranslation++;
                neededIds.Add(row.ItemId);
                var replacement = FindReplacement(row, sourceCatalog);
                if (replacement.Status != ReplacementStatus.Found)
                {
                    blockedRows.Add(new SourceBackedItemUiTextBlockedRow(
                        target,
                        row.ItemId,
                        row.Field,
                        replacement.Reason));

                    switch (replacement.Status)
                    {
                        case ReplacementStatus.UnsupportedField:
                            skippedRowsWithUnsupportedField++;
                            break;
                        case ReplacementStatus.ConflictingSource:
                            skippedRowsWithConflictingSource++;
                            break;
                        case ReplacementStatus.UnsafeReplacement:
                            skippedRowsWithUnsafeReplacement++;
                            break;
                        default:
                            skippedRowsWithoutSource++;
                            break;
                    }

                    continue;
                }

                sourceMatchedRows++;
                matchedIds.Add(row.ItemId);
                var formattedReplacement = row.FormatReplacement(replacement.Text);
                if (!StringComparer.Ordinal.Equals(
                    lines[index].Text.Substring(row.ValueStart, row.ValueLength),
                    formattedReplacement))
                {
                    changedRows++;
                    replacements.Add(new PendingReplacement(row.ValueStart, row.ValueLength, formattedReplacement));
                }
            }

            if (replacements.Count > 0)
            {
                var translatedLine = ApplyReplacements(lines[index].Text, replacements);
                lines[index] = lines[index] with { Text = translatedLine };
            }
        }

        var builder = new StringBuilder(targetText.Length);
        foreach (var line in lines)
        {
            builder.Append(line.Text);
            builder.Append(line.LineEnding);
        }

        updatedText = builder.ToString();
        return new SourceBackedItemUiTextTranslationResult(
            target,
            rowCount,
            rowsNeedingTranslation,
            sourceMatchedRows,
            skippedRowsWithoutSource,
            skippedRowsWithConflictingSource,
            skippedRowsWithUnsafeReplacement,
            skippedRowsWithUnsupportedField,
            neededIds.Count,
            matchedIds.Count,
            blockedRows
                .OrderBy(static row => row.Target, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static row => row.ItemId)
                .ThenBy(static row => row.Field, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static row => row.Reason, StringComparer.Ordinal)
                .ToArray(),
            sourceCatalog.Conflicts
                .OrderBy(static conflict => conflict.ItemId)
                .ThenBy(static conflict => conflict.Field, StringComparer.OrdinalIgnoreCase)
                .Select(static conflict => new SourceBackedItemUiTextConflict(conflict.ItemId, conflict.Field))
                .ToArray(),
            changedRows,
            !StringComparer.Ordinal.Equals(targetText, updatedText));
    }

    public static IReadOnlyList<SourceBackedItemUiTextSource> ReadSourceTexts(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            return [];
        }

        var text = DecodeSourceText(File.ReadAllBytes(path));
        var fileName = Path.GetFileName(path);
        var sources = new List<SourceBackedItemUiTextSource>();
        foreach (var line in SplitLines(text))
        {
            AddItemTypeSources(path, line.Text, sources);
            AddServerKeyDescriptionSource(path, line.Text, sources);
        }

        AddSameFormatSources(path, fileName, text, sources);
        return sources;
    }

    private static SourceCatalog BuildSourceCatalog(IEnumerable<SourceBackedItemUiTextSource> sources)
    {
        var values = new Dictionary<SourceKey, string>();
        var conflicts = new HashSet<SourceKey>();
        foreach (var source in sources)
        {
            var field = NormalizeField(source.Field);
            var key = new SourceKey(source.ItemId, field);
            if (source.ItemId <= 0 || field.Length == 0 || conflicts.Contains(key))
            {
                continue;
            }

            var text = NormalizeSourceText(source.Text);
            if (!IsUsableEnglishSource(text))
            {
                continue;
            }

            if (values.TryGetValue(key, out var existing))
            {
                if (!StringComparer.Ordinal.Equals(existing, text))
                {
                    values.Remove(key);
                    conflicts.Add(key);
                }

                continue;
            }

            values.Add(key, text);
        }

        return new SourceCatalog(values, conflicts);
    }

    private static ReplacementCandidate FindReplacement(TargetRow row, SourceCatalog catalog)
    {
        var candidates = GetFieldCandidates(row);
        if (candidates.Count == 0)
        {
            return ReplacementCandidate.UnsupportedField("unsupported-field");
        }

        var sawMissing = false;
        foreach (var field in candidates)
        {
            foreach (var sourceKey in GetSourceKeys(row, field))
            {
                if (catalog.Conflicts.Contains(sourceKey))
                {
                    return ReplacementCandidate.ConflictingSource("conflicting-source");
                }

                if (!catalog.Values.TryGetValue(sourceKey, out var text))
                {
                    sawMissing = true;
                    continue;
                }

                var replacementText = FormatSourceTextForTarget(row, sourceKey, text);
                if (!IsSafeReplacement(row.Value, replacementText))
                {
                    return ReplacementCandidate.UnsafeReplacement("unsafe-replacement");
                }

                return ReplacementCandidate.Found(replacementText);
            }
        }

        if (TryGetBuiltInItemQuenchTipInfoReplacement(row, out var builtInReplacement))
        {
            return IsSafeReplacement(row.Value, builtInReplacement)
                ? ReplacementCandidate.Found(builtInReplacement)
                : ReplacementCandidate.UnsafeReplacement("unsafe-replacement");
        }

        return sawMissing
            ? ReplacementCandidate.MissingSource("missing-source")
            : ReplacementCandidate.UnsupportedField("unsupported-field");
    }

    private static bool TryGetBuiltInItemQuenchTipInfoReplacement(TargetRow row, out string replacement)
    {
        replacement = string.Empty;
        if (row.TargetKind != TargetKind.ItemQuenchTipInfoIni)
        {
            return false;
        }

        var field = NormalizeField(row.Field);
        if (field.Equals(NameField, StringComparison.OrdinalIgnoreCase))
        {
            if (!ItemQuenchTermTranslations.TryGetValue(row.Value, out var translatedTerm) ||
                translatedTerm is null)
            {
                return false;
            }

            replacement = translatedTerm;
            return true;
        }

        if (field.Equals("Note", StringComparison.OrdinalIgnoreCase))
        {
            if (!row.Value.Equals("剩余时间:%02u天%02u小时%02u分%02u秒", StringComparison.Ordinal))
            {
                return false;
            }

            replacement = "Remaining time:%02u days %02u hours %02u minutes %02u seconds";
            return true;
        }

        if (field.Equals("Num", StringComparison.OrdinalIgnoreCase))
        {
            return TryGetBuiltInItemQuenchNumReplacement(row.Value, out replacement);
        }

        return field.Equals("AppendNum", StringComparison.OrdinalIgnoreCase) &&
            TryGetBuiltInItemQuenchAppendNumReplacement(row.Value, out replacement);
    }

    private static bool TryGetBuiltInItemQuenchNumReplacement(string value, out string replacement)
    {
        replacement = string.Empty;
        var percentMatch = ItemQuenchNumPercentPattern.Match(value);
        if (percentMatch.Success &&
            TryGetBuiltInItemQuenchTermTranslation(percentMatch, out var percentTerm))
        {
            replacement = $"Level %d {percentTerm}:+%d%%{NormalizeItemQuenchSuffix(percentMatch.Groups["suffix"].Value)}";
            return true;
        }

        var flatMatch = ItemQuenchNumFlatPattern.Match(value);
        if (!flatMatch.Success ||
            !TryGetBuiltInItemQuenchTermTranslation(flatMatch, out var flatTerm))
        {
            return false;
        }

        replacement = $"Level %d {flatTerm}:+%d{NormalizeItemQuenchSuffix(flatMatch.Groups["suffix"].Value)}";
        return true;
    }

    private static bool TryGetBuiltInItemQuenchAppendNumReplacement(string value, out string replacement)
    {
        replacement = string.Empty;
        var match = ItemQuenchAppendNumPattern.Match(value);
        if (!match.Success ||
            !TryGetBuiltInItemQuenchTermTranslation(match, out var term))
        {
            return false;
        }

        replacement = $"%s{term}:+%d";
        return true;
    }

    private static bool TryGetBuiltInItemQuenchTermTranslation(Match match, out string term)
    {
        if (!ItemQuenchTermTranslations.TryGetValue(match.Groups["term"].Value, out var translatedTerm) ||
            translatedTerm is null)
        {
            term = string.Empty;
            return false;
        }

        term = translatedTerm;
        return true;
    }

    private static string NormalizeItemQuenchSuffix(string suffix)
    {
        return suffix.Length == 0 ? string.Empty : ",";
    }

    private static IEnumerable<SourceKey> GetSourceKeys(TargetRow row, string field)
    {
        yield return new SourceKey(row.ItemId, field);

        if (row.TargetKind == TargetKind.ItemLua &&
            field.Equals(NameField, StringComparison.OrdinalIgnoreCase) &&
            row.ItemId is > 0 and <= (int.MaxValue - 1) / 100)
        {
            yield return new SourceKey((row.ItemId * 100) + 1, field);
        }
    }

    private static string FormatSourceTextForTarget(TargetRow row, SourceKey sourceKey, string text)
    {
        if (row.TargetKind != TargetKind.ItemLua ||
            !sourceKey.Field.Equals(NameField, StringComparison.OrdinalIgnoreCase) ||
            sourceKey.ItemId == row.ItemId)
        {
            return text;
        }

        return StripRuneLevelSuffix(text);
    }

    private static IReadOnlyList<string> GetFieldCandidates(TargetRow row)
    {
        var normalized = NormalizeField(row.Field);
        if (row.TargetKind == TargetKind.StorageIni)
        {
            if (!StorageExactTextFields.Contains(normalized))
            {
                return [];
            }

            return normalized.Equals("Intro", StringComparison.OrdinalIgnoreCase)
                ? [normalized, DescriptionField]
                : [normalized];
        }

        if (row.TargetKind == TargetKind.ItemLua)
        {
            return IsSupportedLuaTextField(normalized)
                ? [normalized]
                : [];
        }

        if (!ExactIniTextFields.Contains(normalized))
        {
            return [];
        }

        if (StringComparer.OrdinalIgnoreCase.Equals(normalized, NameField))
        {
            return [NameField];
        }

        if (DescriptionFallbackFields.Contains(normalized))
        {
            return normalized.Equals(DescriptionField, StringComparison.OrdinalIgnoreCase)
                ? [DescriptionField]
                : [normalized, DescriptionField];
        }

        return [normalized];
    }

    private static bool IsSupportedLuaTextField(string field)
    {
        var lastSegment = field.LastIndexOf('.') >= 0
            ? field[(field.LastIndexOf('.') + 1)..]
            : field;
        return LuaTextFieldNames.Contains(lastSegment);
    }

    private static string StripRuneLevelSuffix(string value)
    {
        return Regex.Replace(
            value,
            @"\(\+\d+\)$",
            string.Empty,
            RegexOptions.CultureInvariant).TrimEnd();
    }

    private static bool TryParseTargetRow(
        string line,
        TargetKind targetKind,
        TargetParseState state,
        out TargetRow row)
    {
        row = default;
        return targetKind switch
        {
            TargetKind.ItemLua => TryParseItemLuaTargetRow(line, state, out row),
            TargetKind.ItemQuenchTipInfoIni => TryParseSectionIniTargetRow(line, state, sectionIdProvidesItemId: true, out row),
            TargetKind.StorageIni => TryParseSectionIniTargetRow(line, state, sectionIdProvidesItemId: false, out row),
            _ => false,
        };
    }

    private static bool TryParseTargetRows(
        string line,
        TargetKind targetKind,
        TargetParseState state,
        out IReadOnlyList<TargetRow> rows)
    {
        if (TryParseTargetRow(line, targetKind, state, out var row))
        {
            rows = [row];
            return true;
        }

        rows = [];
        return false;
    }

    private static bool TryParseItemLuaTargetRow(
        string line,
        TargetParseState state,
        out TargetRow row)
    {
        row = default;
        if (LuaShowNameStartPattern.IsMatch(line))
        {
            state.InLuaShowName = true;
            return false;
        }

        if (!state.InLuaShowName)
        {
            return false;
        }

        if (line.Trim().Equals("}", StringComparison.Ordinal))
        {
            state.InLuaShowName = false;
            return false;
        }

        var match = LuaSimpleStringRowPattern.Match(line);
        if (!match.Success ||
            !int.TryParse(match.Groups["id"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var itemId))
        {
            return false;
        }

        var value = UnescapeLuaString(match.Groups["value"].Value);
        row = new TargetRow(
            TargetKind.ItemLua,
            itemId,
            NameField,
            value,
            match.Groups["prefix"].Length,
            match.Groups["value"].Length,
            EscapeLuaString);
        return true;
    }

    private static bool TryParseSectionIniTargetRow(
        string line,
        TargetParseState state,
        bool sectionIdProvidesItemId,
        out TargetRow row)
    {
        row = default;
        var sectionMatch = IniSectionPattern.Match(line);
        if (sectionMatch.Success)
        {
            var section = sectionMatch.Groups["section"].Value;
            state.CurrentSectionId = TryParseStrictInt(section, out var sectionId) ? sectionId : null;
            state.CurrentStorageItemId = null;
            return false;
        }

        var match = IniKeyValuePattern.Match(line);
        if (!match.Success)
        {
            return false;
        }

        var key = NormalizeField(match.Groups["key"].Value);
        var value = match.Groups["value"].Value;
        if (!sectionIdProvidesItemId &&
            key.Equals("ID", StringComparison.OrdinalIgnoreCase) &&
            TryParseStrictInt(value.Trim(), out var storageItemId))
        {
            state.CurrentStorageItemId = storageItemId;
            return false;
        }

        var itemId = sectionIdProvidesItemId ? state.CurrentSectionId : state.CurrentStorageItemId;
        if (itemId is null)
        {
            return false;
        }

        if (!ExactIniTextFields.Contains(key) && !NeedsTranslation(value))
        {
            return false;
        }

        row = new TargetRow(
            state.TargetKind,
            itemId.Value,
            key,
            value,
            match.Groups["value"].Index,
            match.Groups["value"].Length,
            static replacement => replacement);
        return true;
    }

    private static string ApplyReplacements(string line, IEnumerable<PendingReplacement> replacements)
    {
        var ordered = replacements
            .OrderByDescending(static replacement => replacement.ValueStart)
            .ToArray();
        var builder = new StringBuilder(line);
        foreach (var replacement in ordered)
        {
            builder.Remove(replacement.ValueStart, replacement.ValueLength);
            builder.Insert(replacement.ValueStart, replacement.Text);
        }

        return builder.ToString();
    }

    private static void AddItemTypeSources(
        string path,
        string line,
        List<SourceBackedItemUiTextSource> sources)
    {
        var fields = line.Split("@@", StringSplitOptions.None);
        if (fields.Length <= 1 ||
            !int.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out var itemId))
        {
            return;
        }

        sources.Add(new SourceBackedItemUiTextSource(
            itemId,
            NameField,
            fields[1],
            path + ":itemtype.name"));

        if (fields.Length <= 53)
        {
            return;
        }

        sources.Add(new SourceBackedItemUiTextSource(
            itemId,
            DescriptionField,
            fields[53],
            path + ":itemtype.description"));
    }

    private static void AddServerKeyDescriptionSource(
        string path,
        string line,
        List<SourceBackedItemUiTextSource> sources)
    {
        var match = ServerKeyItemDescriptionPattern.Match(line);
        if (!match.Success ||
            !int.TryParse(match.Groups["id"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var itemId))
        {
            return;
        }

        sources.Add(new SourceBackedItemUiTextSource(
            itemId,
            DescriptionField,
            UnescapeLuaString(match.Groups["value"].Value),
            path + ":Server_Key.STR_ITEM_ITEMDESC"));
    }

    private static void AddSameFormatSources(
        string path,
        string fileName,
        string text,
        List<SourceBackedItemUiTextSource> sources)
    {
        var targetKind = GetTargetKind(fileName);
        if (targetKind == TargetKind.Unknown)
        {
            return;
        }

        var state = new TargetParseState(targetKind);
        foreach (var line in SplitLines(text))
        {
            if (!TryParseTargetRow(line.Text, targetKind, state, out var row) ||
                !IsUsableEnglishSource(row.Value))
            {
                continue;
            }

            sources.Add(new SourceBackedItemUiTextSource(
                row.ItemId,
                row.Field,
                row.Value,
                path + ":" + row.Field));
        }
    }

    private static TargetKind GetTargetKind(string target)
    {
        var fileName = Path.GetFileName(target);
        if (fileName.Equals("Item.lua", StringComparison.OrdinalIgnoreCase))
        {
            return TargetKind.ItemLua;
        }

        if (fileName.Equals("ItemQuenchTipInfo.ini", StringComparison.OrdinalIgnoreCase))
        {
            return TargetKind.ItemQuenchTipInfoIni;
        }

        if (fileName.Equals("Storage.ini", StringComparison.OrdinalIgnoreCase))
        {
            return TargetKind.StorageIni;
        }

        return TargetKind.Unknown;
    }

    private static string NormalizeField(string field)
    {
        return field.Trim();
    }

    private static string NormalizeSourceText(string value)
    {
        return value
            .Replace('~', ' ')
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
    }

    private static bool IsSafeReplacement(string currentValue, string replacement)
    {
        return PlaceholderSignature(currentValue).SequenceEqual(PlaceholderSignature(replacement), StringComparer.Ordinal) &&
            ColorSignature(currentValue).SequenceEqual(ColorSignature(replacement), StringComparer.OrdinalIgnoreCase);
    }

    private static string[] PlaceholderSignature(string value)
    {
        return PrintfPlaceholderPattern
            .Matches(value.Replace("%%", string.Empty, StringComparison.Ordinal))
            .Select(static match => match.Value)
            .ToArray();
    }

    private static string[] ColorSignature(string value)
    {
        return ColorTokenPattern
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
            var character = value[index];
            if (character != '\\' || index + 1 >= value.Length)
            {
                builder.Append(character);
                continue;
            }

            var escaped = value[++index];
            builder.Append(escaped switch
            {
                '\\' => '\\',
                '"' => '"',
                '\'' => '\'',
                'n' => ' ',
                'r' => ' ',
                't' => ' ',
                _ => escaped,
            });
        }

        return builder.ToString();
    }

    private static DecodedTargetText DecodeTargetText(byte[] bytes)
    {
        if (HasUtf8Bom(bytes))
        {
            var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.Utf8);
            return new DecodedTargetText(
                encoding.GetString(bytes, 3, bytes.Length - 3),
                encoding,
                HadUtf8Bom: true);
        }

        var utf8 = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.Utf8);
        try
        {
            return new DecodedTargetText(utf8.GetString(bytes), utf8, HadUtf8Bom: false);
        }
        catch (DecoderFallbackException)
        {
            var codePage936 = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
            return new DecodedTargetText(codePage936.GetString(bytes), codePage936, HadUtf8Bom: false);
        }
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
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(
                    936,
                    EncoderFallback.ExceptionFallback,
                    DecoderFallback.ReplacementFallback)
                .GetString(bytes);
        }
    }

    private static byte[] EncodeUtf8(string text, bool emitBom)
    {
        var textBytes = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.Utf8).GetBytes(text);
        if (!emitBom)
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

    private static bool TryParseStrictInt(string value, out int result)
    {
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result);
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

    private enum TargetKind
    {
        Unknown,
        ItemLua,
        ItemQuenchTipInfoIni,
        StorageIni,
    }

    private enum ReplacementStatus
    {
        Found,
        MissingSource,
        ConflictingSource,
        UnsafeReplacement,
        UnsupportedField,
    }

    private sealed record SourceCatalog(
        IReadOnlyDictionary<SourceKey, string> Values,
        IReadOnlySet<SourceKey> Conflicts);

    private readonly record struct SourceKey(int ItemId, string Field);

    private readonly record struct PendingReplacement(
        int ValueStart,
        int ValueLength,
        string Text);

    private readonly record struct TargetRow(
        TargetKind TargetKind,
        int ItemId,
        string Field,
        string Value,
        int ValueStart,
        int ValueLength,
        Func<string, string> FormatReplacement);

    private sealed class TargetParseState(TargetKind targetKind)
    {
        public TargetKind TargetKind { get; } = targetKind;
        public bool InLuaShowName { get; set; }
        public int? CurrentSectionId { get; set; }
        public int? CurrentStorageItemId { get; set; }
    }

    private sealed record ReplacementCandidate(
        ReplacementStatus Status,
        string Text,
        string Reason)
    {
        public static ReplacementCandidate Found(string text)
        {
            return new ReplacementCandidate(ReplacementStatus.Found, text, string.Empty);
        }

        public static ReplacementCandidate MissingSource(string reason)
        {
            return new ReplacementCandidate(ReplacementStatus.MissingSource, string.Empty, reason);
        }

        public static ReplacementCandidate ConflictingSource(string reason)
        {
            return new ReplacementCandidate(ReplacementStatus.ConflictingSource, string.Empty, reason);
        }

        public static ReplacementCandidate UnsafeReplacement(string reason)
        {
            return new ReplacementCandidate(ReplacementStatus.UnsafeReplacement, string.Empty, reason);
        }

        public static ReplacementCandidate UnsupportedField(string reason)
        {
            return new ReplacementCandidate(ReplacementStatus.UnsupportedField, string.Empty, reason);
        }
    }

    private sealed record DecodedTargetText(
        string Text,
        Encoding Encoding,
        bool HadUtf8Bom);
}
