using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.Client;

public sealed record LegacyQuestInfoRandomSourceRow(
    string Key,
    string Text,
    string Source);

public sealed record LegacyQuestInfoRandomTranslationResult(
    int SourceRows,
    int SourceKeys,
    IReadOnlyList<string> ConflictingSourceKeys,
    int TargetSections,
    int TargetRows,
    int RowsNeedingTranslation,
    int SourceMatchedRows,
    int SameFormatMatchedRows,
    int CompoundNpcMatchedRows,
    int AnchorMatchedRows,
    int FallbackMatchedRows,
    int ConflictRows,
    int UnmatchedRows,
    IReadOnlyList<string> MissingSourceKeys,
    int ChangedRows,
    int RemainingCjkRows,
    bool Changed);

public static class LegacyQuestInfoRandomTranslator
{
    private static readonly Regex RandomDailyInfoStartPattern = new(
        @"^\s*local\s+RandomDailyInfo\s*=\s*\{\s*$",
        RegexOptions.CultureInvariant);

    private static readonly Regex LuaSectionStartPattern = new(
        @"^\s*\[(?<id>\d+)\]\s*=\s*\{\s*$",
        RegexOptions.CultureInvariant);

    private static readonly Regex LuaStringFieldPattern = new(
        "^(?<prefix>\\s*(?<key>[A-Za-z][A-Za-z0-9_]*)\\s*=\\s*\")(?<value>(?:\\\\.|[^\"\\\\])*)(?<suffix>\"\\s*,?.*)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex IniSectionPattern = new(
        @"^\s*\[(?<section>[^\]]+)\]\s*(?:[;#].*)?$",
        RegexOptions.CultureInvariant);

    private static readonly Regex IniKeyValuePattern = new(
        @"^(?<key>[A-Za-z][A-Za-z0-9_]*)\s*=\s*(?<value>.*)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex AnchorPattern = new(
        @"\[(?<kind>npc|mob|monster|item_t)\s+(?<name>[^,\]]+),(?<payload>[^\]]+)\]",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex PrintfPlaceholderPattern = new(
        "%(?:[-+ #0]*(?:\\d+|\\*)?(?:\\.(?:\\d+|\\*))?[hlLzjt]*)?[diuoxXfFeEgGaAcspn]",
        RegexOptions.CultureInvariant);

    private static readonly Regex IndexedPlaceholderPattern = new(
        "\\{\\d+\\}",
        RegexOptions.CultureInvariant);

    private static readonly Regex ColorTokenPattern = new(
        @"</?color(?:=[^>]+)?>|0x[0-9a-fA-F]{8}",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static LegacyQuestInfoRandomTranslationResult PatchFile(
        string questInfoRandomPath,
        IEnumerable<string> sourcePaths)
    {
        return PatchFile(questInfoRandomPath, sourcePaths, LegacyQuestInfoNameFallbackCatalog.Empty);
    }

    public static LegacyQuestInfoRandomTranslationResult PatchFile(
        string questInfoRandomPath,
        IEnumerable<string> sourcePaths,
        LegacyQuestInfoNameFallbackCatalog fallbackCatalog)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(questInfoRandomPath);
        ArgumentNullException.ThrowIfNull(sourcePaths);
        ArgumentNullException.ThrowIfNull(fallbackCatalog);

        var originalBytes = File.ReadAllBytes(questInfoRandomPath);
        var decoded = DecodeTargetText(originalBytes);
        var sources = sourcePaths
            .Where(ClientTextSourcePathFence.IsAllowedSourcePath)
            .SelectMany(ReadSourceRows)
            .ToArray();
        var result = Translate(decoded.Text, sources, fallbackCatalog, out var updatedText);
        if (result.Changed)
        {
            if (decoded.HadUtf8Bom)
            {
                File.WriteAllBytes(questInfoRandomPath, EncodeUtf8(updatedText, emitBom: true));
            }
            else
            {
                ClientTextPatchWriter.WritePatchedTextPreservingUnchangedBytes(
                    questInfoRandomPath,
                    originalBytes,
                    decoded.Text,
                    updatedText,
                    decoded.Encoding);
            }
        }

        return result;
    }

    public static LegacyQuestInfoRandomTranslationResult Translate(
        string targetText,
        IEnumerable<LegacyQuestInfoRandomSourceRow> sources,
        out string updatedText)
    {
        return Translate(targetText, sources, LegacyQuestInfoNameFallbackCatalog.Empty, out updatedText);
    }

    public static LegacyQuestInfoRandomTranslationResult Translate(
        string targetText,
        IEnumerable<LegacyQuestInfoRandomSourceRow> sources,
        LegacyQuestInfoNameFallbackCatalog fallbackCatalog,
        out string updatedText)
    {
        ArgumentNullException.ThrowIfNull(targetText);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(fallbackCatalog);

        var sourceCatalog = BuildSourceCatalog(sources);
        var missingSourceKeys = new HashSet<string>(StringComparer.Ordinal);
        var builder = new StringBuilder(targetText.Length);
        var inRandomDailyInfo = false;
        string? currentSectionId = null;
        var targetSections = 0;
        var targetRows = 0;
        var rowsNeedingTranslation = 0;
        var sourceMatchedRows = 0;
        var sameFormatMatchedRows = 0;
        var compoundNpcMatchedRows = 0;
        var anchorMatchedRows = 0;
        var fallbackMatchedRows = 0;
        var conflictRows = 0;
        var unmatchedRows = 0;
        var changedRows = 0;
        var remainingCjkRows = 0;

        foreach (var line in SplitLines(targetText))
        {
            var outputText = line.Text;
            string? outputValue = null;
            if (!inRandomDailyInfo)
            {
                inRandomDailyInfo = RandomDailyInfoStartPattern.IsMatch(line.Text);
            }
            else if (currentSectionId is null && LuaSectionStartPattern.Match(line.Text) is { Success: true } sectionMatch)
            {
                currentSectionId = sectionMatch.Groups["id"].Value;
                targetSections++;
            }
            else if (currentSectionId is not null && LuaStringFieldPattern.Match(line.Text) is { Success: true } fieldMatch)
            {
                targetRows++;
                var key = fieldMatch.Groups["key"].Value;
                var luaValue = fieldMatch.Groups["value"].Value;
                var value = UnescapeLuaString(luaValue);
                outputValue = value;
                if (NeedsTranslation(value))
                {
                    rowsNeedingTranslation++;
                    var replacement = FindReplacement(
                        currentSectionId,
                        key,
                        value,
                        sourceCatalog,
                        fallbackCatalog,
                        missingSourceKeys);
                    if (replacement.Status == ReplacementStatus.Found)
                    {
                        sourceMatchedRows++;
                        switch (replacement.Kind)
                        {
                            case ReplacementKind.SameFormat:
                                sameFormatMatchedRows++;
                                break;
                            case ReplacementKind.CompoundNpc:
                                compoundNpcMatchedRows++;
                                break;
                            case ReplacementKind.Anchor:
                                anchorMatchedRows++;
                                break;
                            case ReplacementKind.Fallback:
                                fallbackMatchedRows++;
                                break;
                        }

                        outputValue = replacement.Text;
                        outputText = fieldMatch.Groups["prefix"].Value +
                            EscapeLuaString(replacement.Text) +
                            fieldMatch.Groups["suffix"].Value;
                        if (!StringComparer.Ordinal.Equals(line.Text, outputText))
                        {
                            changedRows++;
                        }
                    }
                    else if (replacement.Status == ReplacementStatus.Conflict)
                    {
                        conflictRows++;
                    }
                    else
                    {
                        unmatchedRows++;
                    }
                }
            }
            else if (currentSectionId is not null && line.Text.Trim().Equals("},", StringComparison.Ordinal))
            {
                currentSectionId = null;
            }
            else if (currentSectionId is null && line.Text.Trim().Equals("}", StringComparison.Ordinal))
            {
                inRandomDailyInfo = false;
            }

            if (outputValue is not null && NeedsTranslation(outputValue))
            {
                remainingCjkRows++;
            }

            builder.Append(outputText);
            builder.Append(line.LineEnding);
        }

        updatedText = builder.ToString();
        return new LegacyQuestInfoRandomTranslationResult(
            sourceCatalog.SourceRows,
            sourceCatalog.Values.Count,
            sourceCatalog.Conflicts.Order(StringComparer.Ordinal).ToArray(),
            targetSections,
            targetRows,
            rowsNeedingTranslation,
            sourceMatchedRows,
            sameFormatMatchedRows,
            compoundNpcMatchedRows,
            anchorMatchedRows,
            fallbackMatchedRows,
            conflictRows,
            unmatchedRows,
            missingSourceKeys.Order(StringComparer.Ordinal).ToArray(),
            changedRows,
            remainingCjkRows,
            !StringComparer.Ordinal.Equals(targetText, updatedText));
    }

    public static IReadOnlyList<LegacyQuestInfoRandomSourceRow> ReadSourceRows(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!ClientTextSourcePathFence.IsAllowedSourcePath(path))
        {
            return [];
        }

        var fullPath = Path.GetFullPath(path);
        if (Directory.Exists(fullPath))
        {
            return ExpandSourceDirectory(fullPath)
                .SelectMany(ReadSourceRows)
                .Concat(ReadMonsterGeneratorAnchorSourceRows(fullPath))
                .Concat(ReadNpcAnchorSourceRows(fullPath))
                .ToArray();
        }

        if (!File.Exists(fullPath))
        {
            return [];
        }

        var text = DecodeSourceText(File.ReadAllBytes(fullPath));
        var fileName = Path.GetFileName(fullPath);
        var sources = new List<LegacyQuestInfoRandomSourceRow>();
        if (fileName.Equals("QuestinfoRandom.lua", StringComparison.OrdinalIgnoreCase))
        {
            AddSameFormatSourceRows(fullPath, text, sources);
        }
        else if (fileName.Equals("QuestinfoRandomPassion.lua", StringComparison.OrdinalIgnoreCase))
        {
            AddSameFormatSourceRows(fullPath, text, sources);
        }
        else if (fileName.Equals("Questinfo.ini", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("QuestinfoPassionServer.ini", StringComparison.OrdinalIgnoreCase))
        {
            AddQuestInfoSourceRows(fullPath, text, sources);
        }

        return sources;
    }

    private static IEnumerable<string> ExpandSourceDirectory(string path)
    {
        yield return Path.Combine(path, "QuestinfoRandom.lua");
        yield return Path.Combine(path, "QuestinfoRandomPassion.lua");
        yield return Path.Combine(path, "Questinfo.ini");
        yield return Path.Combine(path, "QuestinfoPassionServer.ini");
        yield return Path.Combine(path, "ini", "QuestinfoRandom.lua");
        yield return Path.Combine(path, "ini", "QuestinfoRandomPassion.lua");
        yield return Path.Combine(path, "ini", "Questinfo.ini");
        yield return Path.Combine(path, "ini", "QuestinfoPassionServer.ini");
        yield return Path.Combine(path, "GameServer", "DBServer", "Questinfo.ini");
        yield return Path.Combine(path, "GameServer", "DBServer");
        yield return Path.Combine(path, "lqtxfwd", "GameServer", "DBServer", "Questinfo.ini");
        yield return Path.Combine(path, "lqtxfwd", "GameServer", "DBServer");
    }

    private static IReadOnlyList<LegacyQuestInfoRandomSourceRow> ReadMonsterGeneratorAnchorSourceRows(string path)
    {
        var generatorDirectory = Path.Combine(path, "cq_generator");
        var monsterDirectory = Path.Combine(path, "Monsters");
        if (!Directory.Exists(generatorDirectory) || !Directory.Exists(monsterDirectory))
        {
            return [];
        }

        var monsterNamesByTypeId = ReadMonsterNamesByTypeId(monsterDirectory);
        if (monsterNamesByTypeId.Count == 0)
        {
            return [];
        }

        var sources = new List<LegacyQuestInfoRandomSourceRow>();
        foreach (var generatorPath in Directory.EnumerateFiles(generatorDirectory, "*.ini"))
        {
            try
            {
                var document = IniDocument.Parse(DecodeSourceText(File.ReadAllBytes(generatorPath)));
                var spawn = MonsterSpawnParser.Parse(document, sourceRelativePath: generatorPath);
                if (!monsterNamesByTypeId.TryGetValue(spawn.MonsterTypeId, out var monsterName))
                {
                    continue;
                }

                sources.Add(new LegacyQuestInfoRandomSourceRow(
                    BuildAnchorKey("mob", spawn.MapId + "," + spawn.BoundX + "," + spawn.BoundY),
                    monsterName,
                    generatorPath + ":cq_generator"));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or KeyNotFoundException)
            {
                continue;
            }
        }

        return sources;
    }

    private static IReadOnlyDictionary<int, string> ReadMonsterNamesByTypeId(string path)
    {
        var result = new Dictionary<int, string>();
        foreach (var monsterPath in Directory.EnumerateFiles(path, "*.ini"))
        {
            try
            {
                var document = IniDocument.Parse(DecodeSourceText(File.ReadAllBytes(monsterPath)));
                var section = document.GetSection("cq_monstertype");
                if (!section.TryGetValue("id", out var rawId) ||
                    !section.TryGetValue("name", out var name) ||
                    !int.TryParse(rawId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ||
                    !IsUsableEnglishSource(name))
                {
                    continue;
                }

                result.TryAdd(id, NormalizeSourceText(name));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or KeyNotFoundException)
            {
                continue;
            }
        }

        return result;
    }

    private static void AddSameFormatSourceRows(
        string path,
        string text,
        List<LegacyQuestInfoRandomSourceRow> sources)
    {
        var inRandomDailyInfo = false;
        string? currentSectionId = null;
        foreach (var line in SplitLines(text))
        {
            if (!inRandomDailyInfo)
            {
                inRandomDailyInfo = RandomDailyInfoStartPattern.IsMatch(line.Text);
                continue;
            }

            if (currentSectionId is null && LuaSectionStartPattern.Match(line.Text) is { Success: true } sectionMatch)
            {
                currentSectionId = sectionMatch.Groups["id"].Value;
                continue;
            }

            if (currentSectionId is not null && LuaStringFieldPattern.Match(line.Text) is { Success: true } fieldMatch)
            {
                var key = fieldMatch.Groups["key"].Value;
                var value = UnescapeLuaString(fieldMatch.Groups["value"].Value);
                sources.Add(new LegacyQuestInfoRandomSourceRow(
                    BuildFieldKey(currentSectionId, key),
                    value,
                    path + ":RandomDailyInfo"));
                AddAnchorSourceRows(path, key, value, sources);
                continue;
            }

            if (currentSectionId is not null && line.Text.Trim().Equals("},", StringComparison.Ordinal))
            {
                currentSectionId = null;
                continue;
            }

            if (currentSectionId is null && line.Text.Trim().Equals("}", StringComparison.Ordinal))
            {
                return;
            }
        }
    }

    private static void AddQuestInfoSourceRows(
        string path,
        string text,
        List<LegacyQuestInfoRandomSourceRow> sources)
    {
        foreach (var line in SplitLines(text))
        {
            if (IniSectionPattern.IsMatch(line.Text))
            {
                continue;
            }

            var match = IniKeyValuePattern.Match(line.Text);
            if (!match.Success)
            {
                continue;
            }

            var key = match.Groups["key"].Value;
            var value = match.Groups["value"].Value;
            if (IsCompoundNpcField(key))
            {
                AddCompoundNpcSourceRows(path, key, value, sources);
            }

            AddAnchorSourceRows(path, key, value, sources);
        }
    }

    private static IReadOnlyList<LegacyQuestInfoRandomSourceRow> ReadNpcAnchorSourceRows(string path)
    {
        var npcPath = Path.Combine(path, "Npcs.txt");
        if (!File.Exists(npcPath))
        {
            return [];
        }

        var npcNamesById = LegacyServerKeyResourceTranslator.ReadNpcIdEnumNames(ResolveNpcIdEnumSourcePaths(path));
        if (npcNamesById.Count == 0)
        {
            return [];
        }

        var sources = new List<LegacyQuestInfoRandomSourceRow>();
        var lineNumber = 0;
        foreach (var line in SplitLines(DecodeSourceText(File.ReadAllBytes(npcPath))))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line.Text))
            {
                continue;
            }

            try
            {
                var npc = NpcParser.ParseLine(line.Text);
                if (!npcNamesById.TryGetValue(npc.Id, out var name))
                {
                    continue;
                }

                sources.Add(new LegacyQuestInfoRandomSourceRow(
                    BuildAnchorKey("npc", npc.Id + "," + npc.MapId + "," + npc.X + "," + npc.Y),
                    name,
                    npcPath + ":" + lineNumber + "+NpcID.cs"));
            }
            catch (FormatException)
            {
                continue;
            }
        }

        return sources;
    }

    private static IEnumerable<string> ResolveNpcIdEnumSourcePaths(string path)
    {
        var directory = Path.GetFullPath(path);
        while (!string.IsNullOrEmpty(directory))
        {
            yield return Path.Combine(directory, "NpcID.cs");
            yield return Path.Combine(
                directory,
                "DecompiledSource",
                "GameServer",
                "MrRayzo.Game.MsgNpc",
                "NpcID.cs");

            directory = Directory.GetParent(directory)?.FullName;
        }
    }

    private static SourceCatalog BuildSourceCatalog(IEnumerable<LegacyQuestInfoRandomSourceRow> sources)
    {
        var sourceRows = 0;
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var conflicts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            sourceRows++;
            if (string.IsNullOrWhiteSpace(source.Key) || conflicts.Contains(source.Key))
            {
                continue;
            }

            var text = NormalizeSourceText(source.Text);
            if (!IsUsableEnglishSource(text))
            {
                continue;
            }

            if (values.TryGetValue(source.Key, out var existing))
            {
                if (!StringComparer.Ordinal.Equals(existing, text))
                {
                    values.Remove(source.Key);
                    conflicts.Add(source.Key);
                }

                continue;
            }

            values.Add(source.Key, text);
        }

        return new SourceCatalog(sourceRows, values, conflicts);
    }

    private static ReplacementCandidate FindReplacement(
        string sectionId,
        string key,
        string value,
        SourceCatalog sourceCatalog,
        LegacyQuestInfoNameFallbackCatalog fallbackCatalog,
        ISet<string> missingSourceKeys)
    {
        var fieldKey = BuildFieldKey(sectionId, key);
        if (sourceCatalog.Values.TryGetValue(fieldKey, out var fieldReplacement))
        {
            if (IsSafeFullReplacement(value, fieldReplacement))
            {
                return ReplacementCandidate.Found(fieldReplacement, ReplacementKind.SameFormat);
            }

            return ReplacementCandidate.Missing();
        }

        if (IsCompoundNpcField(key))
        {
            var compoundReplacement = TryBuildCompoundNpcReplacement(value, sourceCatalog, missingSourceKeys);
            if (compoundReplacement.Status != ReplacementStatus.Missing)
            {
                return compoundReplacement;
            }
        }

        var anchorReplacement = TryReplaceAnchors(value, sourceCatalog, missingSourceKeys);
        if (anchorReplacement.Status != ReplacementStatus.Missing)
        {
            return anchorReplacement;
        }

        var fallbackReplacement = TryBuildFallbackReplacement(key, value, fallbackCatalog);
        if (fallbackReplacement.Status != ReplacementStatus.Missing)
        {
            return fallbackReplacement;
        }

        if (sourceCatalog.Conflicts.Contains(fieldKey))
        {
            return ReplacementCandidate.Conflict();
        }

        missingSourceKeys.Add(fieldKey);
        return ReplacementCandidate.Missing();
    }

    private static ReplacementCandidate TryBuildFallbackReplacement(
        string key,
        string value,
        LegacyQuestInfoNameFallbackCatalog fallbackCatalog)
    {
        if (IsCompoundNpcField(key) &&
            fallbackCatalog.TryReplaceCompoundNpcValue(value, out var compoundReplacement) &&
            IsSafePartialReplacement(value, compoundReplacement))
        {
            return ReplacementCandidate.Found(compoundReplacement, ReplacementKind.Fallback);
        }

        // The shared fallback catalog has broader quest-info heuristics. RandomDailyInfo uses
        // [mob name,map,x,y] and unnamed [item id,...] anchors, so only exact source anchors may
        // change those names here.
        if (!ContainsUnsafeFallbackAnchor(value) &&
            fallbackCatalog.TryReplaceVisibleValue(key, value, out var visibleReplacement) &&
            IsSafePartialReplacement(value, visibleReplacement))
        {
            return ReplacementCandidate.Found(visibleReplacement, ReplacementKind.Fallback);
        }

        return ReplacementCandidate.Missing();
    }

    private static bool ContainsUnsafeFallbackAnchor(string value)
    {
        return value.Contains("[mob ", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("[monster ", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("[item ", StringComparison.OrdinalIgnoreCase);
    }

    private static ReplacementCandidate TryBuildCompoundNpcReplacement(
        string value,
        SourceCatalog sourceCatalog,
        ISet<string> missingSourceKeys)
    {
        if (!TryReadCompoundNpcTuple(value, out var tuple, out var tailParts))
        {
            return ReplacementCandidate.Missing();
        }

        var sourceKey = BuildCompoundNpcKey(tuple);
        if (sourceCatalog.Conflicts.Contains(sourceKey))
        {
            return ReplacementCandidate.Conflict();
        }

        if (!sourceCatalog.Values.TryGetValue(sourceKey, out var sourceTail))
        {
            missingSourceKeys.Add(sourceKey);
            return ReplacementCandidate.Missing();
        }

        var sourceTailParts = sourceTail.Split(',', StringSplitOptions.TrimEntries);
        if (sourceTailParts.Length != tailParts.Length)
        {
            return ReplacementCandidate.Missing();
        }

        var originalParts = value.Split(',');
        var replacementParts = new string[originalParts.Length];
        for (var index = 0; index < 4; index++)
        {
            replacementParts[index] = originalParts[index];
        }

        var changed = false;
        for (var index = 0; index < tailParts.Length; index++)
        {
            var sourcePart = sourceTailParts[index];
            if (ContainsCjkOrReplacement(tailParts[index]) &&
                IsUsableEnglishSource(sourcePart))
            {
                replacementParts[index + 4] = sourcePart;
                changed = true;
                continue;
            }

            replacementParts[index + 4] = originalParts[index + 4];
        }

        if (!changed)
        {
            return ReplacementCandidate.Missing();
        }

        return ReplacementCandidate.Found(string.Join(',', replacementParts), ReplacementKind.CompoundNpc);
    }

    private static ReplacementCandidate TryReplaceAnchors(
        string value,
        SourceCatalog sourceCatalog,
        ISet<string> missingSourceKeys)
    {
        var changed = false;
        var conflict = false;
        var replaced = AnchorPattern.Replace(
            value,
            match =>
            {
                if (!TryBuildAnchorKey(
                    match.Groups["kind"].Value,
                    match.Groups["payload"].Value,
                    out var key))
                {
                    return match.Value;
                }

                if (sourceCatalog.Conflicts.Contains(key))
                {
                    conflict = true;
                    return match.Value;
                }

                if (!sourceCatalog.Values.TryGetValue(key, out var replacementName))
                {
                    missingSourceKeys.Add(key);
                    return match.Value;
                }

                var currentName = match.Groups["name"].Value;
                if (StringComparer.Ordinal.Equals(currentName, replacementName))
                {
                    return match.Value;
                }

                changed = true;
                return "[" + match.Groups["kind"].Value + " " + replacementName + "," + match.Groups["payload"].Value + "]";
            });

        if (changed && IsSafePartialReplacement(value, replaced))
        {
            return ReplacementCandidate.Found(replaced, ReplacementKind.Anchor);
        }

        return conflict ? ReplacementCandidate.Conflict() : ReplacementCandidate.Missing();
    }

    private static void AddCompoundNpcSourceRows(
        string path,
        string key,
        string value,
        List<LegacyQuestInfoRandomSourceRow> sources)
    {
        if (!TryReadCompoundNpcTuple(value, out var tuple, out var tailParts))
        {
            return;
        }

        var source = path + ":" + key;
        var tail = string.Join(',', tailParts);
        sources.Add(new LegacyQuestInfoRandomSourceRow(BuildCompoundNpcKey(tuple), tail, source));
        if (tailParts.Length > 0)
        {
            sources.Add(new LegacyQuestInfoRandomSourceRow(
                BuildAnchorKey("npc", tuple.NpcId + "," + tuple.MapId + "," + tuple.X + "," + tuple.Y),
                tailParts[0],
                source));
        }
    }

    private static void AddAnchorSourceRows(
        string path,
        string key,
        string value,
        List<LegacyQuestInfoRandomSourceRow> sources)
    {
        foreach (Match match in AnchorPattern.Matches(value))
        {
            var name = match.Groups["name"].Value;
            if (!TryBuildAnchorKey(match.Groups["kind"].Value, match.Groups["payload"].Value, out var anchorKey))
            {
                continue;
            }

            sources.Add(new LegacyQuestInfoRandomSourceRow(anchorKey, name, path + ":" + key));
        }
    }

    private static bool TryReadCompoundNpcTuple(
        string value,
        out CompoundNpcTuple tuple,
        out string[] tailParts)
    {
        tuple = default;
        tailParts = [];
        var parts = value.Split(',');
        if (parts.Length < 5 ||
            !TryParseLong(parts[0], out var npcId) ||
            !TryParseLong(parts[1], out var mapId) ||
            !TryParseLong(parts[2], out var x) ||
            !TryParseLong(parts[3], out var y))
        {
            return false;
        }

        tailParts = parts
            .Skip(4)
            .Select(static part => part.Trim())
            .ToArray();
        tuple = new CompoundNpcTuple(npcId, mapId, x, y);
        return true;
    }

    private static bool IsCompoundNpcField(string key)
    {
        return key.Equals("BeginNpcId", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("FinishNpcId", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildFieldKey(string sectionId, string key)
    {
        return "field|" + sectionId.Trim() + "|" + key.Trim().ToLowerInvariant();
    }

    private static string BuildCompoundNpcKey(CompoundNpcTuple tuple)
    {
        return "compound-npc|" + tuple.NpcId + "|" + tuple.MapId + "|" + tuple.X + "|" + tuple.Y;
    }

    private static string BuildAnchorKey(string kind, string payload)
    {
        return "anchor|" + NormalizeAnchorKind(kind) + "|" + NormalizeAnchorPayload(kind, payload);
    }

    private static bool TryBuildAnchorKey(string kind, string payload, out string key)
    {
        var normalizedPayload = NormalizeAnchorPayload(kind, payload);
        if (normalizedPayload.Length == 0)
        {
            key = string.Empty;
            return false;
        }

        key = "anchor|" + NormalizeAnchorKind(kind) + "|" + normalizedPayload;
        return true;
    }

    private static string NormalizeAnchorKind(string kind)
    {
        return kind.Equals("monster", StringComparison.OrdinalIgnoreCase)
            ? "mob"
            : kind.Trim().ToLowerInvariant();
    }

    private static string NormalizeAnchorPayload(string kind, string payload)
    {
        var parts = payload.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return string.Empty;
        }

        var length = kind.Equals("npc", StringComparison.OrdinalIgnoreCase)
            ? Math.Min(parts.Length, 4)
            : parts.Length;
        var normalizedParts = new string[length];
        for (var index = 0; index < length; index++)
        {
            if (parts[index].Length == 0)
            {
                return string.Empty;
            }

            normalizedParts[index] = TryParseLong(parts[index], out var number)
                ? number.ToString(CultureInfo.InvariantCulture)
                : parts[index];
        }

        return string.Join(',', normalizedParts);
    }

    private static bool TryParseLong(string value, out long result)
    {
        return long.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    private static bool IsSafeFullReplacement(string current, string replacement)
    {
        return IsUsableEnglishSource(replacement) && IsSafePartialReplacement(current, replacement);
    }

    private static bool IsSafePartialReplacement(string current, string replacement)
    {
        return PlaceholderSignature(current).SequenceEqual(PlaceholderSignature(replacement), StringComparer.Ordinal) &&
            ColorSignature(current).SequenceEqual(ColorSignature(replacement), StringComparer.OrdinalIgnoreCase);
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

    private static string NormalizeSourceText(string value)
    {
        return value
            .Replace('~', ' ')
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
    }

    private static bool IsUsableEnglishSource(string value)
    {
        var normalized = NormalizeSourceText(value);
        if (normalized.Length == 0 || !ContainsAsciiLetter(normalized))
        {
            return false;
        }

        foreach (var character in normalized)
        {
            if (IsBlockedClientTextCharacter(character) || char.IsControl(character))
            {
                return false;
            }
        }

        try
        {
            ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936).GetByteCount(normalized);
            return true;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    private static bool NeedsTranslation(string value)
    {
        return ContainsCjkOrReplacement(value);
    }

    private static bool ContainsCjkOrReplacement(string value)
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

    private static bool IsBlockedClientTextCharacter(char character)
    {
        return character is '\uFFFD' or >= '\u2e80' and <= '\u9fff' or >= '\uf900' and <= '\ufaff' or >= '\uff00' and <= '\uffef';
    }

    private static bool ContainsAsciiLetter(string value)
    {
        foreach (var character in value)
        {
            if (char.IsAsciiLetter(character))
            {
                return true;
            }
        }

        return false;
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

    private enum ReplacementStatus
    {
        Found,
        Conflict,
        Missing,
    }

    private enum ReplacementKind
    {
        None,
        SameFormat,
        CompoundNpc,
        Anchor,
        Fallback,
    }

    private sealed record SourceCatalog(
        int SourceRows,
        IReadOnlyDictionary<string, string> Values,
        IReadOnlySet<string> Conflicts);

    private readonly record struct CompoundNpcTuple(long NpcId, long MapId, long X, long Y);

    private sealed record DecodedTargetText(
        string Text,
        Encoding Encoding,
        bool HadUtf8Bom);

    private sealed record ReplacementCandidate(
        ReplacementStatus Status,
        ReplacementKind Kind,
        string Text)
    {
        public static ReplacementCandidate Found(string text, ReplacementKind kind)
        {
            return new ReplacementCandidate(ReplacementStatus.Found, kind, text);
        }

        public static ReplacementCandidate Conflict()
        {
            return new ReplacementCandidate(ReplacementStatus.Conflict, ReplacementKind.None, string.Empty);
        }

        public static ReplacementCandidate Missing()
        {
            return new ReplacementCandidate(ReplacementStatus.Missing, ReplacementKind.None, string.Empty);
        }
    }
}
