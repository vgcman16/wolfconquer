using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WolfConquer.Core.Client;

public sealed record LegacyServerKeyResourceTranslationResult(
    int KeyCount,
    int TranslatedKeys,
    int UnmatchedKeys,
    int ChangedKeys,
    bool Changed);

public sealed record LegacyServerKeyResourceCatalog(
    string KeyPrefix,
    IReadOnlyDictionary<int, string> Names,
    bool ReplaceOnlyCjk = false);

public sealed record LegacyServerKeyNamedResourceCatalog(
    IReadOnlyDictionary<string, string> Names,
    bool ReplaceOnlyCjk = false);

public static class LegacyServerKeyResourceTranslator
{
    private static readonly string[] ExactServerKeyResourceCatalogPrefixes =
    [
        "STR_NPC_ID_",
        "STR_MONSTER_ID_",
        "STR_MAP_ID_",
        "STR_INSTANCE_ID_",
        "STR_MAGIC_TYPE_",
        "STR_ITEM_TYPE_",
    ];

    private static readonly IReadOnlyDictionary<string, string> CatalogPrefixAliases =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["STR_SHOOT_MONSTER_"] = "STR_MONSTER_ID_",
        };

    private static readonly Regex ServerKeyPattern = new(
        "^(?<prefix>\\s*\\[\"(?<key>STR_[A-Z0-9_]+)\"\\]\\s*=\\s*\")(?<name>(?:\\\\.|[^\"\\\\])*)(?<suffix>\"\\s*,.*)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex QuestNpcLinkPattern = new(
        "\\[npc\\s+(?<name>[^,\\]]+),(?<id>\\d+),(?<map>\\d+),(?<x>\\d+),(?<y>\\d+)\\]",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex CSharpEnumMemberPattern = new(
        "^\\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\\s*=\\s*(?<id>\\d+)u?\\s*,?",
        RegexOptions.CultureInvariant | RegexOptions.Multiline);

    public static LegacyServerKeyResourceTranslationResult Translate(
        string serverKeyText,
        IEnumerable<LegacyServerKeyResourceCatalog> catalogs,
        out string updatedText)
    {
        return Translate(serverKeyText, catalogs, [], out updatedText);
    }

    public static LegacyServerKeyResourceTranslationResult Translate(
        string serverKeyText,
        IEnumerable<LegacyServerKeyResourceCatalog> catalogs,
        IEnumerable<LegacyServerKeyNamedResourceCatalog> namedCatalogs,
        out string updatedText)
    {
        ArgumentNullException.ThrowIfNull(serverKeyText);
        ArgumentNullException.ThrowIfNull(catalogs);
        ArgumentNullException.ThrowIfNull(namedCatalogs);

        var catalogList = catalogs
            .Where(catalog => catalog.Names.Count > 0)
            .ToArray();
        var namedCatalogList = namedCatalogs
            .Where(catalog => catalog.Names.Count > 0)
            .ToArray();
        var builder = new StringBuilder(serverKeyText.Length);
        var keyCount = 0;
        var translatedKeys = 0;
        var unmatchedKeys = 0;
        var changedKeys = 0;

        foreach (var line in SplitLines(serverKeyText))
        {
            var outputText = line.Text;
            var match = ServerKeyPattern.Match(line.Text);
            if (match.Success)
            {
                var key = match.Groups["key"].Value;
                if (TryGetReplacement(key, match.Groups["name"].Value, catalogList, namedCatalogList, out var name))
                {
                    keyCount++;
                    translatedKeys++;
                    var translatedLine = match.Groups["prefix"].Value + EscapeLuaString(NormalizeName(name)) + match.Groups["suffix"].Value;
                    if (!StringComparer.Ordinal.Equals(line.Text, translatedLine))
                    {
                        changedKeys++;
                    }

                    outputText = translatedLine;
                }
                else
                {
                    if (IsTrackedKey(key, catalogList, namedCatalogList))
                    {
                        keyCount++;
                        unmatchedKeys++;
                    }
                }
            }

            builder.Append(outputText);
            builder.Append(line.LineEnding);
        }

        updatedText = builder.ToString();
        return new LegacyServerKeyResourceTranslationResult(
            keyCount,
            translatedKeys,
            unmatchedKeys,
            changedKeys,
            !StringComparer.Ordinal.Equals(serverKeyText, updatedText));
    }

    public static IReadOnlyList<LegacyServerKeyResourceCatalog> BuildExactServerKeyResourceCatalogs(
        IEnumerable<string> serverKeySourcePaths)
    {
        return BuildExactServerKeyResourceCatalogs(
            serverKeySourcePaths,
            ExactServerKeyResourceCatalogPrefixes,
            replaceOnlyCjk: true);
    }

    public static IReadOnlyList<LegacyServerKeyResourceCatalog> BuildExactServerKeyResourceCatalogs(
        IEnumerable<string> serverKeySourcePaths,
        IEnumerable<string> keyPrefixes,
        bool replaceOnlyCjk = true)
    {
        ArgumentNullException.ThrowIfNull(serverKeySourcePaths);
        ArgumentNullException.ThrowIfNull(keyPrefixes);

        var prefixList = keyPrefixes
            .Where(static prefix => !string.IsNullOrWhiteSpace(prefix))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (prefixList.Length == 0)
        {
            return [];
        }

        var candidates = prefixList.ToDictionary(
            static prefix => prefix,
            static _ => new Dictionary<int, string>(),
            StringComparer.Ordinal);
        var conflicts = prefixList.ToDictionary(
            static prefix => prefix,
            static _ => new HashSet<int>(),
            StringComparer.Ordinal);

        foreach (var path in ResolveServerKeySourceFiles(serverKeySourcePaths))
        {
            var sourceText = DecodeLenientCodePage936(File.ReadAllBytes(path));
            foreach (var line in SplitLines(sourceText))
            {
                var match = ServerKeyPattern.Match(line.Text);
                if (!match.Success ||
                    !TryGetTrailingId(match.Groups["key"].Value, out var id))
                {
                    continue;
                }

                var prefix = prefixList.FirstOrDefault(candidate =>
                    match.Groups["key"].Value.StartsWith(candidate, StringComparison.Ordinal));
                if (prefix is null)
                {
                    continue;
                }

                var name = NormalizeName(UnescapeLuaString(match.Groups["name"].Value));
                if (IsUsableName(name))
                {
                    AddConsistentCandidate(candidates[prefix], conflicts[prefix], id, name);
                }
            }
        }

        var catalogs = new List<LegacyServerKeyResourceCatalog>();
        foreach (var prefix in prefixList)
        {
            foreach (var conflict in conflicts[prefix])
            {
                candidates[prefix].Remove(conflict);
            }

            if (candidates[prefix].Count > 0)
            {
                catalogs.Add(new LegacyServerKeyResourceCatalog(prefix, candidates[prefix], replaceOnlyCjk));
            }
        }

        return catalogs;
    }

    public static IReadOnlyDictionary<int, string> ReadExactServerKeyResourceNames(
        IEnumerable<string> serverKeySourcePaths,
        string keyPrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPrefix);

        return BuildExactServerKeyResourceCatalogs(serverKeySourcePaths, [keyPrefix], replaceOnlyCjk: true)
            .SingleOrDefault()?.Names ??
            new Dictionary<int, string>();
    }

    public static LegacyServerKeyResourceCatalog BuildSeedMonsterFallbackCatalog(IEnumerable<string> monsterSeedSourcePaths)
    {
        ArgumentNullException.ThrowIfNull(monsterSeedSourcePaths);

        return new LegacyServerKeyResourceCatalog(
            "STR_MONSTER_ID_",
            ReadSeedMonsterNames(monsterSeedSourcePaths),
            ReplaceOnlyCjk: true);
    }

    public static IReadOnlyDictionary<int, string> ReadSeedMonsterNames(IEnumerable<string> monsterSeedSourcePaths)
    {
        ArgumentNullException.ThrowIfNull(monsterSeedSourcePaths);

        var candidates = new Dictionary<int, string>();
        var conflicts = new HashSet<int>();
        foreach (var path in ResolveSeedMonsterSourceFiles(monsterSeedSourcePaths)
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            using var document = JsonDocument.Parse(
                File.ReadAllBytes(path),
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                });

            foreach (var monster in EnumerateSeedMonsterElements(document.RootElement))
            {
                if (TryReadSeedMonsterName(monster, out var id, out var name))
                {
                    AddConsistentCandidate(candidates, conflicts, id, name);
                }
            }
        }

        foreach (var conflict in conflicts)
        {
            candidates.Remove(conflict);
        }

        return candidates;
    }

    private static bool TryGetReplacement(
        string key,
        string currentName,
        IReadOnlyList<LegacyServerKeyResourceCatalog> catalogs,
        IReadOnlyList<LegacyServerKeyNamedResourceCatalog> namedCatalogs,
        out string name)
    {
        foreach (var namedCatalog in namedCatalogs)
        {
            if (namedCatalog.Names.TryGetValue(key, out var namedValue) &&
                IsUsableName(namedValue) &&
                (!namedCatalog.ReplaceOnlyCjk ||
                    ContainsCjk(currentName) ||
                    StringComparer.Ordinal.Equals(NormalizeName(currentName), NormalizeName(namedValue))))
            {
                name = namedValue;
                return true;
            }
        }

        if (TryGetTrailingId(key, out var id))
        {
            foreach (var catalog in catalogs)
            {
                if (!MatchesCatalogPrefix(key, catalog.KeyPrefix) ||
                    !catalog.Names.TryGetValue(id, out var catalogValue) ||
                    !IsUsableName(catalogValue) ||
                    (catalog.ReplaceOnlyCjk &&
                        !ContainsCjk(currentName) &&
                        !StringComparer.Ordinal.Equals(NormalizeName(currentName), NormalizeName(catalogValue))))
                {
                    continue;
                }

                name = catalogValue;
                return true;
            }
        }

        name = string.Empty;
        return false;
    }

    private static bool IsTrackedKey(
        string key,
        IReadOnlyList<LegacyServerKeyResourceCatalog> catalogs,
        IReadOnlyList<LegacyServerKeyNamedResourceCatalog> namedCatalogs)
    {
        return namedCatalogs.Any(catalog => catalog.Names.ContainsKey(key)) ||
            (TryGetTrailingId(key, out _) && catalogs.Any(catalog => MatchesCatalogPrefix(key, catalog.KeyPrefix)));
    }

    private static bool MatchesCatalogPrefix(string key, string catalogPrefix)
    {
        if (key.StartsWith(catalogPrefix, StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var alias in CatalogPrefixAliases)
        {
            if (key.StartsWith(alias.Key, StringComparison.Ordinal) &&
                StringComparer.Ordinal.Equals(alias.Value, catalogPrefix))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetTrailingId(string key, out int id)
    {
        var separatorIndex = key.LastIndexOf('_');
        if (separatorIndex >= 0 &&
            separatorIndex < key.Length - 1 &&
            int.TryParse(key[(separatorIndex + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out id))
        {
            return true;
        }

        id = 0;
        return false;
    }

    private static string NormalizeName(string value)
    {
        return value
            .Replace('~', ' ')
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
    }

    private static bool IsUsableName(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length != 0 && !ContainsCjkOrReplacement(trimmed);
    }

    public static LegacyServerKeyResourceCatalog BuildQuestAnchorNpcFallbackCatalog(IEnumerable<string> questInfoPaths)
    {
        ArgumentNullException.ThrowIfNull(questInfoPaths);

        return new LegacyServerKeyResourceCatalog(
            "STR_NPC_ID_",
            ReadQuestAnchorNpcNames(questInfoPaths),
            ReplaceOnlyCjk: true);
    }

    public static IReadOnlyDictionary<int, string> ReadQuestAnchorNpcNames(IEnumerable<string> questInfoPaths)
    {
        ArgumentNullException.ThrowIfNull(questInfoPaths);

        var candidates = new Dictionary<int, string>();
        var conflicts = new HashSet<int>();
        foreach (var path in questInfoPaths)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            foreach (var line in File.ReadLines(path))
            {
                if (TryParseQuestAnchorNpcName(line, out var npcId, out var name))
                {
                    AddConsistentCandidate(candidates, conflicts, npcId, name);
                }
            }
        }

        foreach (var conflict in conflicts)
        {
            candidates.Remove(conflict);
        }

        return candidates;
    }

    public static LegacyServerKeyResourceCatalog BuildQuestLinkedNpcFallbackCatalog(IEnumerable<string> questInfoPaths)
    {
        ArgumentNullException.ThrowIfNull(questInfoPaths);

        var paths = questInfoPaths.ToArray();
        var names = ReadQuestLinkedNpcNames(paths);
        var npcIdEnumNames = ReadNpcIdEnumNames(ResolveNpcIdEnumReferencePaths(paths));
        if (npcIdEnumNames.Count > 0)
        {
            names = MergeConsistentNpcNameSources(names, npcIdEnumNames);
        }

        return new LegacyServerKeyResourceCatalog(
            "STR_NPC_ID_",
            names,
            ReplaceOnlyCjk: true);
    }

    public static LegacyServerKeyResourceCatalog BuildSpellIdEnumFallbackCatalog(IEnumerable<string> sourcePaths)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);

        return new LegacyServerKeyResourceCatalog(
            "STR_MAGIC_TYPE_",
            ReadSpellIdEnumNames(sourcePaths),
            ReplaceOnlyCjk: true);
    }

    public static IReadOnlyDictionary<int, string> ReadSpellIdEnumNames(IEnumerable<string> sourcePaths)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);

        return ReadCSharpEnumNames(ResolveSpellIdEnumReferencePaths(sourcePaths), "SpellID");
    }

    public static LegacyServerKeyResourceCatalog BuildMapInfoFallbackCatalog(IEnumerable<string> sourcePaths)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);

        return new LegacyServerKeyResourceCatalog(
            "STR_MAP_ID_",
            ReadMapInfoNames(sourcePaths),
            ReplaceOnlyCjk: true);
    }

    public static LegacyServerKeyResourceCatalog BuildClientNpcNameFallbackCatalog(IEnumerable<string> sourcePaths)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);

        return new LegacyServerKeyResourceCatalog(
            "STR_NPC_ID_",
            ReadClientNpcIniNames(sourcePaths),
            ReplaceOnlyCjk: true);
    }

    public static IReadOnlyDictionary<int, string> ReadClientNpcIniNames(IEnumerable<string> sourcePaths)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);

        var candidates = new Dictionary<int, string>();
        var conflicts = new HashSet<int>();
        foreach (var path in ResolveClientNpcIniSourceFiles(sourcePaths)
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var (npcId, name) in ReadClientNpcIniNames(path))
            {
                AddConsistentCandidate(candidates, conflicts, npcId, name);
            }
        }

        foreach (var conflict in conflicts)
        {
            candidates.Remove(conflict);
        }

        return candidates;
    }

    public static IReadOnlyDictionary<int, string> ReadMapInfoNames(IEnumerable<string> sourcePaths)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);

        var candidates = new Dictionary<int, string>();
        var conflicts = new HashSet<int>();
        foreach (var path in ResolveMapInfoSourceFiles(sourcePaths)
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (TryReadMapInfoName(path, out var mapId, out var name))
            {
                AddConsistentCandidate(candidates, conflicts, mapId, name);
            }
        }

        foreach (var conflict in conflicts)
        {
            candidates.Remove(conflict);
        }

        return candidates;
    }

    public static IReadOnlyDictionary<int, string> ReadQuestLinkedNpcNames(IEnumerable<string> questInfoPaths)
    {
        ArgumentNullException.ThrowIfNull(questInfoPaths);

        var candidates = new Dictionary<int, string>();
        var conflicts = new HashSet<int>();
        foreach (var path in questInfoPaths)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            foreach (var line in File.ReadLines(path))
            {
                foreach (Match match in QuestNpcLinkPattern.Matches(line))
                {
                    if (TryParseQuestLinkedNpcName(match, out var npcId, out var name))
                    {
                        AddConsistentCandidate(candidates, conflicts, npcId, name);
                    }
                }
            }
        }

        foreach (var conflict in conflicts)
        {
            candidates.Remove(conflict);
        }

        return candidates;
    }

    public static IReadOnlyDictionary<int, string> ReadNpcIdEnumNames(IEnumerable<string> csharpPaths)
    {
        ArgumentNullException.ThrowIfNull(csharpPaths);

        return ReadCSharpEnumNames(csharpPaths, "NpcID");
    }

    public static IReadOnlyDictionary<int, string> ReadCSharpEnumNames(IEnumerable<string> csharpPaths, string enumName)
    {
        ArgumentNullException.ThrowIfNull(csharpPaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(enumName);

        var candidates = new Dictionary<int, string>();
        var conflicts = new HashSet<int>();
        foreach (var path in csharpPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(path))
            {
                continue;
            }

            foreach (var (id, name) in EnumerateCSharpEnumMembers(File.ReadAllText(path), enumName))
            {
                AddConsistentCandidate(candidates, conflicts, id, name);
            }
        }

        foreach (var conflict in conflicts)
        {
            candidates.Remove(conflict);
        }

        return candidates;
    }

    private static IEnumerable<(int Id, string Name)> EnumerateCSharpEnumMembers(string text, string enumName)
    {
        var enumDeclarationPattern = new Regex(
            "\\benum\\s+" + Regex.Escape(enumName) + "\\b",
            RegexOptions.CultureInvariant);
        var insideTargetEnum = false;
        var waitingForOpenBrace = false;

        foreach (var rawLine in SplitLines(text))
        {
            var line = RemoveCSharpLineComment(rawLine.Text);
            if (!insideTargetEnum)
            {
                if (!waitingForOpenBrace && !enumDeclarationPattern.IsMatch(line))
                {
                    continue;
                }

                waitingForOpenBrace = true;
                var openBraceIndex = line.IndexOf('{');
                if (openBraceIndex < 0)
                {
                    continue;
                }

                insideTargetEnum = true;
                waitingForOpenBrace = false;
                line = line[(openBraceIndex + 1)..];
            }

            var closeBraceIndex = line.IndexOf('}');
            var memberLine = closeBraceIndex >= 0 ? line[..closeBraceIndex] : line;
            var match = CSharpEnumMemberPattern.Match(memberLine);
            if (match.Success &&
                long.TryParse(match.Groups["id"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var longId) &&
                longId is >= 0 and <= int.MaxValue &&
                NormalizeName(match.Groups["name"].Value) is { } name &&
                IsEnglishQuestAnchorName(name))
            {
                yield return ((int)longId, name);
            }

            if (closeBraceIndex >= 0)
            {
                insideTargetEnum = false;
            }
        }
    }

    private static string RemoveCSharpLineComment(string line)
    {
        var commentIndex = line.IndexOf("//", StringComparison.Ordinal);
        return commentIndex >= 0 ? line[..commentIndex] : line;
    }

    private static IReadOnlyDictionary<int, string> MergeConsistentNpcNameSources(
        IReadOnlyDictionary<int, string> first,
        IReadOnlyDictionary<int, string> second)
    {
        var candidates = new Dictionary<int, string>();
        var conflicts = new HashSet<int>();
        foreach (var entry in first)
        {
            AddConsistentCandidate(candidates, conflicts, entry.Key, entry.Value);
        }

        foreach (var entry in second)
        {
            AddConsistentCandidate(candidates, conflicts, entry.Key, entry.Value);
        }

        foreach (var conflict in conflicts)
        {
            candidates.Remove(conflict);
        }

        return candidates;
    }

    private static IEnumerable<string> ResolveClientNpcIniSourceFiles(IEnumerable<string> sourcePaths)
    {
        foreach (var rawPath in sourcePaths)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                continue;
            }

            var path = Path.GetFullPath(rawPath);
            if (File.Exists(path))
            {
                yield return path;
                continue;
            }

            if (!Directory.Exists(path))
            {
                continue;
            }

            foreach (var candidate in new[]
            {
                Path.Combine(path, "npc.ini"),
                Path.Combine(path, "NpcX.ini"),
                Path.Combine(path, "terrainnpc.ini"),
                Path.Combine(path, "ini", "npc.ini"),
                Path.Combine(path, "ini", "NpcX.ini"),
                Path.Combine(path, "ini", "terrainnpc.ini"),
            })
            {
                if (File.Exists(candidate))
                {
                    yield return candidate;
                }
            }
        }
    }

    private static IEnumerable<(int NpcId, string Name)> ReadClientNpcIniNames(string path)
    {
        int? currentId = null;
        foreach (var rawLine in SplitLines(DecodeLenientCodePage936(File.ReadAllBytes(path))))
        {
            var line = rawLine.Text.Trim();
            if (TryParseClientNpcSectionId(line, out var npcId))
            {
                currentId = npcId;
                continue;
            }

            if (currentId is { } id &&
                line.StartsWith("Name=", StringComparison.OrdinalIgnoreCase) &&
                NormalizeName(line["Name=".Length..]) is { } name &&
                IsEnglishQuestAnchorName(name))
            {
                yield return (id, name);
            }
        }
    }

    private static bool TryParseClientNpcSectionId(string value, out int npcId)
    {
        if (value.Length > 2 &&
            value[0] == '[' &&
            value[^1] == ']')
        {
            var sectionName = value[1..^1];
            const string npcTypePrefix = "NpcType";
            if (sectionName.StartsWith(npcTypePrefix, StringComparison.OrdinalIgnoreCase))
            {
                sectionName = sectionName[npcTypePrefix.Length..];
            }

            return int.TryParse(sectionName, NumberStyles.None, CultureInfo.InvariantCulture, out npcId);
        }

        npcId = 0;
        return false;
    }

    private static IEnumerable<string> ResolveNpcIdEnumReferencePaths(IEnumerable<string> questInfoPaths)
    {
        foreach (var questInfoPath in questInfoPaths)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(questInfoPath));
            while (!string.IsNullOrEmpty(directory))
            {
                yield return Path.Combine(
                    directory,
                    "DecompiledSource",
                    "GameServer",
                    "MrRayzo.Game.MsgNpc",
                    "NpcID.cs");
                directory = Directory.GetParent(directory)?.FullName;
            }
        }
    }

    private static IEnumerable<string> ResolveSpellIdEnumReferencePaths(IEnumerable<string> sourcePaths)
    {
        foreach (var rawPath in sourcePaths)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                continue;
            }

            var path = Path.GetFullPath(rawPath);
            if (File.Exists(path))
            {
                yield return path;
                continue;
            }

            if (!Directory.Exists(path))
            {
                continue;
            }

            foreach (var candidate in new[]
            {
                Path.Combine(path, "Flags.cs"),
                Path.Combine(path, "MrRayzo.Role", "Flags.cs"),
            })
            {
                if (File.Exists(candidate))
                {
                    yield return candidate;
                }
            }

            var directory = path;
            while (!string.IsNullOrEmpty(directory))
            {
                yield return Path.Combine(
                    directory,
                    "DecompiledSource",
                    "GameServer",
                    "MrRayzo.Role",
                    "Flags.cs");
                directory = Directory.GetParent(directory)?.FullName;
            }
        }
    }

    private static IEnumerable<string> ResolveMapInfoSourceFiles(IEnumerable<string> sourcePaths)
    {
        foreach (var rawPath in sourcePaths)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                continue;
            }

            var path = Path.GetFullPath(rawPath);
            if (File.Exists(path))
            {
                yield return path;
                continue;
            }

            if (!Directory.Exists(path))
            {
                continue;
            }

            if (Path.GetFileName(path).Equals("maps", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var mapInfoPath in Directory.EnumerateFiles(path, "*.ini", SearchOption.TopDirectoryOnly))
                {
                    yield return mapInfoPath;
                }
            }

            foreach (var candidateDirectory in new[]
            {
                Path.Combine(path, "maps"),
                Path.Combine(path, "GameServer", "DBServer", "maps"),
                Path.Combine(path, "lqtxfwd", "GameServer", "DBServer", "maps"),
            })
            {
                if (!Directory.Exists(candidateDirectory))
                {
                    continue;
                }

                foreach (var mapInfoPath in Directory.EnumerateFiles(candidateDirectory, "*.ini", SearchOption.TopDirectoryOnly))
                {
                    yield return mapInfoPath;
                }
            }
        }
    }

    private static bool TryReadMapInfoName(string path, out int mapId, out string name)
    {
        mapId = 0;
        name = string.Empty;
        int? parsedMapId = null;
        string? parsedName = null;
        var inInfoSection = false;

        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length > 2 && line[0] == '[' && line[^1] == ']')
            {
                inInfoSection = line[1..^1].Equals("info", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inInfoSection)
            {
                continue;
            }

            if (line.StartsWith("id=", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(line["id=".Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var parsedId))
            {
                parsedMapId = parsedId;
                continue;
            }

            if (line.StartsWith("name=", StringComparison.OrdinalIgnoreCase))
            {
                parsedName = NormalizeName(line["name=".Length..]);
            }
        }

        if (parsedMapId is not { } id ||
            parsedName is not { Length: > 0 } safeName ||
            !IsEnglishQuestAnchorName(safeName))
        {
            return false;
        }

        mapId = id;
        name = safeName;
        return true;
    }

    private static bool TryParseQuestAnchorNpcName(string line, out int npcId, out string name)
    {
        npcId = 0;
        name = string.Empty;

        var trimmed = line.TrimStart();
        if (!StartsWithQuestAnchorKey(trimmed))
        {
            return false;
        }

        var equalsIndex = trimmed.IndexOf('=');
        if (equalsIndex <= 0)
        {
            return false;
        }

        var value = trimmed[(equalsIndex + 1)..].Trim();
        if (value.Length > 1 && value[0] == '"')
        {
            var closingQuoteIndex = value.IndexOf('"', 1);
            if (closingQuoteIndex > 1)
            {
                value = value[1..closingQuoteIndex];
            }
        }

        var fields = value.Split(',', 6, StringSplitOptions.TrimEntries);
        if (fields.Length < 5 ||
            !int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out npcId) ||
            !int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ||
            !int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ||
            !int.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            npcId = 0;
            return false;
        }

        name = NormalizeName(fields[4]);
        return IsEnglishQuestAnchorName(name);
    }

    private static bool TryParseQuestLinkedNpcName(Match match, out int npcId, out string name)
    {
        npcId = 0;
        name = string.Empty;

        if (!match.Success ||
            !int.TryParse(match.Groups["id"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out npcId) ||
            !int.TryParse(match.Groups["map"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ||
            !int.TryParse(match.Groups["x"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ||
            !int.TryParse(match.Groups["y"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            npcId = 0;
            return false;
        }

        name = NormalizeName(match.Groups["name"].Value);
        return IsEnglishQuestAnchorName(name);
    }

    private static bool StartsWithQuestAnchorKey(string value)
    {
        return StartsWithQuestAnchorKey(value, "BeginNpcId") ||
            StartsWithQuestAnchorKey(value, "FinishNpcId");
    }

    private static bool StartsWithQuestAnchorKey(string value, string key)
    {
        if (!value.StartsWith(key, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var index = key.Length;
        if (index < value.Length && value[index] == '_')
        {
            index++;
            var digitStart = index;
            while (index < value.Length && char.IsAsciiDigit(value[index]))
            {
                index++;
            }

            if (index == digitStart)
            {
                return false;
            }
        }

        while (index < value.Length && char.IsWhiteSpace(value[index]))
        {
            index++;
        }

        return index < value.Length && value[index] == '=';
    }

    private static bool IsEnglishQuestAnchorName(string value)
    {
        var containsAsciiLetter = false;
        foreach (var character in value)
        {
            if (character > '\u007f')
            {
                return false;
            }

            containsAsciiLetter |= char.IsAsciiLetter(character);
        }

        return containsAsciiLetter;
    }

    private static void AddConsistentCandidate(
        Dictionary<int, string> candidates,
        HashSet<int> conflicts,
        int id,
        string name)
    {
        if (conflicts.Contains(id))
        {
            return;
        }

        if (candidates.TryGetValue(id, out var existing))
        {
            if (!StringComparer.Ordinal.Equals(existing, name))
            {
                candidates.Remove(id);
                conflicts.Add(id);
            }

            return;
        }

        candidates[id] = name;
    }

    private static bool ContainsCjk(string value)
    {
        foreach (var character in value)
        {
            if (character is >= '\u3400' and <= '\u9fff')
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsCjkOrReplacement(string value)
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

    private static IEnumerable<JsonElement> EnumerateSeedMonsterElements(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in root.EnumerateArray())
            {
                yield return element;
            }

            yield break;
        }

        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("monsters", out var monsters) &&
            monsters.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in monsters.EnumerateArray())
            {
                yield return element;
            }
        }
    }

    private static bool TryReadSeedMonsterName(JsonElement monster, out int id, out string name)
    {
        id = 0;
        name = string.Empty;
        if (monster.ValueKind != JsonValueKind.Object ||
            !monster.TryGetProperty("id", out var idElement) ||
            !TryGetJsonInt32(idElement, out id) ||
            !monster.TryGetProperty("name", out var nameElement) ||
            nameElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        name = NormalizeName(nameElement.GetString() ?? string.Empty);
        return IsSafeSeedMonsterName(name);
    }

    private static bool IsSafeSeedMonsterName(string value)
    {
        var containsAsciiLetter = false;
        foreach (var character in value)
        {
            if (character is '\uFFFD' or >= '\u3400' and <= '\u9fff' ||
                char.IsControl(character))
            {
                return false;
            }

            containsAsciiLetter |= char.IsAsciiLetter(character);
        }

        return containsAsciiLetter;
    }

    private static bool TryGetJsonInt32(JsonElement element, out int value)
    {
        if (element.ValueKind == JsonValueKind.Number &&
            element.TryGetInt32(out value))
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.String &&
            int.TryParse(element.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    private static IEnumerable<string> ResolveSeedMonsterSourceFiles(IEnumerable<string> sourcePaths)
    {
        foreach (var rawPath in sourcePaths)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                continue;
            }

            var path = Path.GetFullPath(rawPath);
            if (File.Exists(path))
            {
                if (Path.GetFileName(path).Equals("monsters.json", StringComparison.OrdinalIgnoreCase))
                {
                    yield return path;
                }

                continue;
            }

            if (!Directory.Exists(path))
            {
                continue;
            }

            foreach (var candidate in new[]
            {
                Path.Combine(path, "monsters.json"),
                Path.Combine(path, "seed-json", "monsters.json"),
                Path.Combine(path, ".setup_work", "seed-json", "monsters.json"),
            })
            {
                if (File.Exists(candidate))
                {
                    yield return candidate;
                }
            }
        }
    }

    private static IEnumerable<string> ResolveServerKeySourceFiles(IEnumerable<string> sourcePaths)
    {
        foreach (var rawPath in sourcePaths)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                continue;
            }

            var path = Path.GetFullPath(rawPath);
            if (File.Exists(path))
            {
                yield return path;
                continue;
            }

            if (!Directory.Exists(path))
            {
                continue;
            }

            foreach (var candidate in new[]
            {
                Path.Combine(path, "Server_Key.lua"),
                Path.Combine(path, "Script", "Server_Key.lua"),
                Path.Combine(path, "ini", "Server_Key.lua"),
            })
            {
                if (File.Exists(candidate))
                {
                    yield return candidate;
                }
            }
        }
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
