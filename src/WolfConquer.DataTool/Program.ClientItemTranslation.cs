using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Client;

internal static partial class Program
{
    private const string DefaultLegacyItemTypePath = @"data/DBServer/itemtype.txt";
    private const string DefaultLegacyDbServerPath = @"data/DBServer";
    private static readonly Regex ServerKeyTrapIdPattern = new(
        "\\[\"STR_TRAP_ID_(?<id>\\d+)\"\\]",
        RegexOptions.CultureInvariant);

    static int PatchClientEmoneyShopV2(string[] args)
    {
        var clientPath = ResolveClientPath(args);
        var itemTypePath = ResolveEmoneyShopItemTypePath(args);
        var shopPath = Path.Combine(clientPath, "ini", "emoneyShopV2.ini");

        if (!Directory.Exists(clientPath))
        {
            Console.Error.WriteLine($"Client path not found: {clientPath}");
            return 1;
        }

        if (!File.Exists(shopPath))
        {
            Console.Error.WriteLine($"Client emoney shop file not found: {shopPath}");
            return 1;
        }

        if (!File.Exists(itemTypePath))
        {
            Console.Error.WriteLine($"Legacy itemtype source not found: {itemTypePath}");
            return 1;
        }

        if (!ClientTextSourcePathFence.IsAllowedSourcePath(itemTypePath))
        {
            Console.Error.WriteLine($"Legacy itemtype source is blocked because it is from a non-Conquer client: {itemTypePath}");
            return 1;
        }

        var result = LegacyEmoneyShopTranslator.PatchFile(shopPath, itemTypePath);
        Console.WriteLine($"Client emoney shop: {shopPath}");
        Console.WriteLine($"Legacy itemtype source: {itemTypePath}");
        Console.WriteLine($"Text rows scanned: {result.RowCount}");
        Console.WriteLine($"Translated rows: {result.TranslatedRows}");
        Console.WriteLine($"Changed rows: {result.ChangedRows}");
        Console.WriteLine($"Unmatched rows: {result.UnmatchedRows}");
        Console.WriteLine($"Unique item IDs: {result.UniqueItemIds}");
        Console.WriteLine($"Matched unique item IDs: {result.MatchedUniqueItemIds}");
        Console.WriteLine($"Unmatched unique item IDs: {result.UnmatchedItemIds.Count}");
        Console.WriteLine($"Changed file: {result.Changed}");
        if (result.UnmatchedItemIds.Count > 0)
        {
            Console.WriteLine("First unmatched item IDs: " + string.Join(", ", result.UnmatchedItemIds.Take(25)));
        }

        return result.TranslatedRows > 0 ? 0 : 2;
    }

    static int PatchClientEmoneyShopV2SourceBacked(string[] args)
    {
        var clientPath = ResolveClientPath(args);
        var shopPath = Path.Combine(clientPath, "ini", "emoneyShopV2.ini");

        if (!Directory.Exists(clientPath))
        {
            Console.Error.WriteLine($"Client path not found: {clientPath}");
            return 1;
        }

        if (!File.Exists(shopPath))
        {
            Console.Error.WriteLine($"Client emoney shop file not found: {shopPath}");
            return 1;
        }

        var sourcePaths = ResolveSourceBackedEmoneyShopV2Paths(args, clientPath, shopPath);
        if (sourcePaths.Count == 0)
        {
            Console.Error.WriteLine("No source-backed emoney shop source files were found.");
            return 1;
        }

        var result = SourceBackedEmoneyShopV2Translator.PatchFile(shopPath, sourcePaths);
        Console.WriteLine($"Client source-backed emoney shop: {shopPath}");
        Console.WriteLine($"Source-backed emoney shop sources: {string.Join("; ", sourcePaths)}");
        Console.WriteLine($"Text rows scanned: {result.RowCount}");
        Console.WriteLine($"Rows needing translation: {result.RowsNeedingTranslation}");
        Console.WriteLine($"Source-matched rows: {result.SourceMatchedRows}");
        Console.WriteLine($"Changed rows: {result.ChangedRows}");
        Console.WriteLine($"Skipped rows without source: {result.SkippedRowsWithoutSource}");
        Console.WriteLine($"Unique item IDs needing translation: {result.UniqueItemIdsNeedingTranslation}");
        Console.WriteLine($"Matched unique item IDs: {result.MatchedUniqueItemIds}");
        Console.WriteLine($"Missing unique item IDs: {result.MissingItemIds.Count}");
        Console.WriteLine($"Conflicted source item IDs: {result.ConflictedItemIds.Count}");
        Console.WriteLine($"Changed file: {result.Changed}");
        if (result.MissingItemIds.Count > 0)
        {
            Console.WriteLine("First missing item IDs: " + string.Join(", ", result.MissingItemIds.Take(25)));
        }

        return result.RowCount > 0 ? 0 : 2;
    }

    static int PatchClientServerKeyItems(string[] args)
    {
        var clientPath = ResolveClientPath(args);
        var itemTypePath = ResolveLegacyItemTypePath(args);

        if (!Directory.Exists(clientPath))
        {
            Console.Error.WriteLine($"Client path not found: {clientPath}");
            return 1;
        }

        var serverKeyPaths = ResolveClientServerKeyPaths(clientPath).ToArray();
        if (serverKeyPaths.Length == 0)
        {
            Console.Error.WriteLine($"Client server key file not found under: {clientPath}");
            return 1;
        }

        if (!File.Exists(itemTypePath))
        {
            Console.Error.WriteLine($"Legacy itemtype source not found: {itemTypePath}");
            return 1;
        }

        if (!ClientTextSourcePathFence.IsAllowedSourcePath(itemTypePath))
        {
            Console.Error.WriteLine($"Legacy itemtype source is blocked because it is from a non-Conquer client: {itemTypePath}");
            return 1;
        }

        var sourcePaths = ResolveServerKeyItemSourcePaths(args, clientPath, itemTypePath);
        if (sourcePaths.Count == 0)
        {
            Console.Error.WriteLine("No Server_Key item text source files were found.");
            return 1;
        }

        Console.WriteLine($"Legacy itemtype source: {itemTypePath}");
        Console.WriteLine($"Server_Key item text sources: {string.Join("; ", sourcePaths)}");
        var keyCount = 0;
        var translatedKeys = 0;
        var changedKeys = 0;
        var unmatchedKeys = 0;
        var uniqueItemIds = 0;
        var matchedUniqueItemIds = 0;
        var changedFiles = 0;
        var unmatchedItemIds = new HashSet<int>();
        var conflictedItemIds = new HashSet<int>();

        foreach (var serverKeyPath in serverKeyPaths)
        {
            var result = LegacyServerKeyItemNameTranslator.PatchFile(serverKeyPath, sourcePaths);
            PrintClientServerKeyItemResult(serverKeyPath, result);
            keyCount += result.KeyCount;
            translatedKeys += result.TranslatedKeys;
            changedKeys += result.ChangedKeys;
            unmatchedKeys += result.UnmatchedKeys;
            uniqueItemIds += result.UniqueItemIds;
            matchedUniqueItemIds += result.MatchedUniqueItemIds;
            if (result.Changed)
            {
                changedFiles++;
            }

            foreach (var itemId in result.UnmatchedItemIds)
            {
                unmatchedItemIds.Add(itemId);
            }

            foreach (var itemId in result.ConflictingItemIds)
            {
                conflictedItemIds.Add(itemId);
            }
        }

        if (serverKeyPaths.Length > 1)
        {
            Console.WriteLine($"Server key files scanned: {serverKeyPaths.Length}");
            Console.WriteLine($"Total item name keys scanned: {keyCount}");
            Console.WriteLine($"Total translated keys: {translatedKeys}");
            Console.WriteLine($"Total changed keys: {changedKeys}");
            Console.WriteLine($"Total unmatched keys: {unmatchedKeys}");
            Console.WriteLine($"Total unique item ID references: {uniqueItemIds}");
            Console.WriteLine($"Total matched unique item ID references: {matchedUniqueItemIds}");
            Console.WriteLine($"Unique unmatched item IDs across files: {unmatchedItemIds.Count}");
            Console.WriteLine($"Unique conflicted item source IDs across files: {conflictedItemIds.Count}");
            Console.WriteLine($"Changed files: {changedFiles}");
            if (unmatchedItemIds.Count > 0)
            {
                Console.WriteLine("First unmatched item IDs across files: " + string.Join(", ", unmatchedItemIds.Order().Take(25)));
            }

            if (conflictedItemIds.Count > 0)
            {
                Console.WriteLine("First conflicted item source IDs across files: " + string.Join(", ", conflictedItemIds.Order().Take(25)));
            }
        }

        return translatedKeys > 0 ? 0 : 2;
    }

    static int PatchClientServerKeyResources(string[] args)
    {
        var clientPath = ResolveClientPath(args);
        var dbServerPath = ResolveLegacyDbServerPath(args);
        var serverKeyPaths = ResolveClientServerKeyPaths(clientPath).ToArray();

        if (!Directory.Exists(clientPath))
        {
            Console.Error.WriteLine($"Client path not found: {clientPath}");
            return 1;
        }

        if (!Directory.Exists(dbServerPath))
        {
            Console.Error.WriteLine($"Legacy DBServer path not found: {dbServerPath}");
            return 1;
        }

        if (!ClientTextSourcePathFence.IsAllowedSourcePath(dbServerPath))
        {
            Console.Error.WriteLine($"Legacy DBServer source is blocked because it is from a non-Conquer client: {dbServerPath}");
            return 1;
        }

        if (serverKeyPaths.Length == 0)
        {
            Console.Error.WriteLine($"Client server key file not found under: {clientPath}");
            return 1;
        }

        var catalogs = BuildServerKeyResourceCatalogs(clientPath, dbServerPath).ToList();
        var exactResourceSourcePaths = ResolveServerKeyResourceSourcePaths(args, clientPath);
        catalogs.AddRange(LegacyServerKeyResourceTranslator.BuildExactServerKeyResourceCatalogs(exactResourceSourcePaths));
        var namedCatalogs = BuildServerKeyNamedResourceCatalogs(dbServerPath);
        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        var keyCount = 0;
        var translatedKeys = 0;
        var unmatchedKeys = 0;
        var changedKeys = 0;
        var changedFiles = 0;

        Console.WriteLine($"Legacy DBServer: {dbServerPath}");
        if (exactResourceSourcePaths.Count > 0)
        {
            Console.WriteLine($"Exact Server_Key resource sources: {string.Join("; ", exactResourceSourcePaths)}");
        }

        foreach (var serverKeyPath in serverKeyPaths)
        {
            var originalBytes = File.ReadAllBytes(serverKeyPath);
            var originalText = encoding.GetString(originalBytes);
            var result = LegacyServerKeyResourceTranslator.Translate(originalText, catalogs, namedCatalogs, out var updatedText);
            if (result.Changed)
            {
                ClientTextPatchWriter.WritePatchedTextPreservingUnchangedBytes(
                    serverKeyPath,
                    originalBytes,
                    originalText,
                    updatedText,
                    encoding);
                changedFiles++;
            }

            Console.WriteLine($"Client server key: {serverKeyPath}");
            Console.WriteLine($"Resource keys scanned: {result.KeyCount}");
            Console.WriteLine($"Translated keys: {result.TranslatedKeys}");
            Console.WriteLine($"Changed keys: {result.ChangedKeys}");
            Console.WriteLine($"Unmatched keys: {result.UnmatchedKeys}");
            Console.WriteLine($"Changed file: {result.Changed}");
            keyCount += result.KeyCount;
            translatedKeys += result.TranslatedKeys;
            unmatchedKeys += result.UnmatchedKeys;
            changedKeys += result.ChangedKeys;
        }

        Console.WriteLine($"Server key files scanned: {serverKeyPaths.Length}");
        Console.WriteLine($"Total resource keys scanned: {keyCount}");
        Console.WriteLine($"Total translated keys: {translatedKeys}");
        Console.WriteLine($"Total changed keys: {changedKeys}");
        Console.WriteLine($"Total unmatched keys: {unmatchedKeys}");
        Console.WriteLine($"Changed files: {changedFiles}");
        return translatedKeys > 0 ? 0 : 2;
    }

    private static IReadOnlyList<string> ResolveServerKeyResourceSourcePaths(string[] args, string clientPath)
    {
        var paths = ResolveClientServerKeyPaths(clientPath).ToList();
        if (args.Length > 3)
        {
            for (var index = 3; index < args.Length; index++)
            {
                AddServerKeyResourceSourcePath(paths, args[index]);
            }
        }

        return ExistingDistinctPaths(paths);
    }

    private static void AddServerKeyResourceSourcePath(List<string> paths, string rawPath)
    {
        var path = Path.GetFullPath(rawPath);
        if (!Directory.Exists(path))
        {
            paths.Add(path);
            return;
        }

        paths.Add(Path.Combine(path, "Server_Key.lua"));
        paths.Add(Path.Combine(path, "Script", "Server_Key.lua"));
        paths.Add(Path.Combine(path, "ini", "Server_Key.lua"));
    }

    private static IEnumerable<string> ResolveClientServerKeyPaths(string clientPath)
    {
        var candidates = new[]
        {
            Path.Combine(clientPath, "Script", "Server_Key.lua"),
            Path.Combine(clientPath, "ini", "Server_Key.lua"),
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                yield return candidate;
            }
        }
    }

    private static IReadOnlyList<string> ResolveSourceBackedEmoneyShopV2Paths(
        string[] args,
        string clientPath,
        string shopPath)
    {
        var paths = new List<string>();
        if (args.Length > 2)
        {
            for (var index = 2; index < args.Length; index++)
            {
                AddEmoneyShopV2SourcePath(paths, args[index]);
            }
        }
        else
        {
            paths.Add(shopPath);
            paths.Add(Path.Combine(clientPath, "Script", "Server_Key.lua"));
            paths.Add(Path.Combine(clientPath, "ini", "Server_Key.lua"));
            paths.Add(Path.Combine(DefaultLegacyDbServerPath, "itemtype.txt"));
            paths.Add(Path.Combine(DefaultLegacyDbServerPath, "shops", "emoneyShopV2.ini"));
            foreach (var discovered in DiscoverWorkspaceEmoneyShopV2Sources(Directory.GetCurrentDirectory(), clientPath))
            {
                paths.Add(discovered);
            }
        }

        return paths
            .Where(File.Exists)
            .Select(Path.GetFullPath)
            .Where(ClientTextSourcePathFence.IsAllowedSourcePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddEmoneyShopV2SourcePath(List<string> paths, string rawPath)
    {
        var path = Path.GetFullPath(rawPath);
        if (!Directory.Exists(path))
        {
            paths.Add(path);
            return;
        }

        paths.Add(Path.Combine(path, "emoneyShopV2.ini"));
        paths.Add(Path.Combine(path, "ini", "emoneyShopV2.ini"));
        paths.Add(Path.Combine(path, "shops", "emoneyShopV2.ini"));
        paths.Add(Path.Combine(path, "itemtype.txt"));
        paths.Add(Path.Combine(path, "Server_Key.lua"));
        paths.Add(Path.Combine(path, "Script", "Server_Key.lua"));
        paths.Add(Path.Combine(path, "GameServer", "DBServer", "shops", "emoneyShopV2.ini"));
        paths.Add(Path.Combine(path, "lqtxfwd", "GameServer", "DBServer", "shops", "emoneyShopV2.ini"));
    }

    private static IEnumerable<string> DiscoverWorkspaceEmoneyShopV2Sources(string workspaceRoot, string clientPath)
    {
        if (!Directory.Exists(workspaceRoot) ||
            !ClientTextSourcePathFence.IsAllowedSourcePath(workspaceRoot))
        {
            yield break;
        }

        var clientRoot = Path.GetFullPath(clientPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var setupWorkRoot = Path.Combine(Path.GetFullPath(workspaceRoot), ".setup_work") +
            Path.DirectorySeparatorChar;
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.System,
        };

        foreach (var path in Directory.EnumerateFiles(workspaceRoot, "emoneyShopV2.ini", options))
        {
            var fullPath = Path.GetFullPath(path);
            if (fullPath.StartsWith(clientRoot, StringComparison.OrdinalIgnoreCase) ||
                fullPath.StartsWith(setupWorkRoot, StringComparison.OrdinalIgnoreCase) ||
                !ClientTextSourcePathFence.IsAllowedSourcePath(fullPath))
            {
                continue;
            }

            yield return fullPath;
        }
    }

    private static void PrintClientServerKeyItemResult(
        string serverKeyPath,
        LegacyServerKeyItemNameTranslationResult result)
    {
        Console.WriteLine($"Client server key: {serverKeyPath}");
        Console.WriteLine($"Item name keys scanned: {result.KeyCount}");
        Console.WriteLine($"Translated keys: {result.TranslatedKeys}");
        Console.WriteLine($"Changed keys: {result.ChangedKeys}");
        Console.WriteLine($"Unmatched keys: {result.UnmatchedKeys}");
        Console.WriteLine($"Unique item IDs: {result.UniqueItemIds}");
        Console.WriteLine($"Matched unique item IDs: {result.MatchedUniqueItemIds}");
        Console.WriteLine($"Unmatched unique item IDs: {result.UnmatchedItemIds.Count}");
        Console.WriteLine($"Changed file: {result.Changed}");
        if (result.UnmatchedItemIds.Count > 0)
        {
            Console.WriteLine("First unmatched item IDs: " + string.Join(", ", result.UnmatchedItemIds.Take(25)));
        }

        if (result.ConflictingItemIds.Count > 0)
        {
            Console.WriteLine("First conflicted item source IDs: " + string.Join(", ", result.ConflictingItemIds.Take(25)));
        }
    }

    private static IReadOnlyList<string> ResolveServerKeyItemSourcePaths(
        string[] args,
        string clientPath,
        string itemTypePath)
    {
        var paths = new List<string>
        {
            itemTypePath,
            Path.Combine(clientPath, "ini", "RuneDesc.ini"),
        };
        paths.AddRange(ResolveClientServerKeyPaths(clientPath));

        if (args.Length > 3)
        {
            for (var index = 3; index < args.Length; index++)
            {
                AddServerKeyItemSourcePath(paths, args[index]);
            }
        }
        else
        {
            paths.AddRange(DiscoverWorkspaceServerKeyItemSources(Directory.GetCurrentDirectory(), clientPath));
        }

        return ExistingDistinctPaths(paths);
    }

    private static void AddServerKeyItemSourcePath(List<string> paths, string rawPath)
    {
        var path = Path.GetFullPath(rawPath);
        if (!Directory.Exists(path))
        {
            paths.Add(path);
            return;
        }

        paths.Add(Path.Combine(path, "itemtype.txt"));
        paths.Add(Path.Combine(path, "Server_Key.lua"));
        paths.Add(Path.Combine(path, "Script", "Server_Key.lua"));
        paths.Add(Path.Combine(path, "ini", "Server_Key.lua"));
        paths.Add(Path.Combine(path, "RuneDesc.ini"));
        paths.Add(Path.Combine(path, "ini", "RuneDesc.ini"));
        paths.Add(Path.Combine(path, "GameServer", "DBServer", "itemtype.txt"));
        paths.Add(Path.Combine(path, "lqtxfwd", "GameServer", "DBServer", "itemtype.txt"));
    }

    private static IEnumerable<string> DiscoverWorkspaceServerKeyItemSources(string workspaceRoot, string clientPath)
    {
        if (!Directory.Exists(workspaceRoot) ||
            !ClientTextSourcePathFence.IsAllowedSourcePath(workspaceRoot))
        {
            yield break;
        }

        var excludedRoots = new[]
        {
            Path.GetFullPath(clientPath),
            Path.Combine(Path.GetFullPath(workspaceRoot), ".setup_work"),
            Path.Combine(Path.GetFullPath(workspaceRoot), ".codex-test-artifacts"),
            Path.Combine(Path.GetFullPath(workspaceRoot), ".artifacts"),
            Path.Combine(Path.GetFullPath(workspaceRoot), ".worktrees"),
            Path.Combine(Path.GetFullPath(workspaceRoot), "TestResults"),
        }
            .Select(static path => path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar)
            .ToArray();

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.System,
        };

        foreach (var fileName in new[] { "itemtype.txt", "Server_Key.lua", "RuneDesc.ini" })
        {
            foreach (var path in Directory.EnumerateFiles(workspaceRoot, fileName, options))
            {
                var fullPath = Path.GetFullPath(path);
                if (excludedRoots.Any(root => fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)) ||
                    !ClientTextSourcePathFence.IsAllowedSourcePath(fullPath))
                {
                    continue;
                }

                yield return fullPath;
            }
        }
    }

    private static IReadOnlyList<LegacyServerKeyResourceCatalog> BuildServerKeyResourceCatalogs(
        string clientPath,
        string dbServerPath)
    {
        var monsterNames = ReadMonsterNames(
            Path.Combine(dbServerPath, "Monsters"),
            Path.Combine(dbServerPath, "MonsterSpawns"));
        var catalogs = new List<LegacyServerKeyResourceCatalog>
        {
            new LegacyServerKeyResourceCatalog("STR_NPC_ID_", ReadClientNpcNames(clientPath, dbServerPath), ReplaceOnlyCjk: true),
            new LegacyServerKeyResourceCatalog("STR_MONSTER_ID_", monsterNames, ReplaceOnlyCjk: true),
            new LegacyServerKeyResourceCatalog("STR_MAGIC_TYPE_", ReadMagicNames(dbServerPath), ReplaceOnlyCjk: true),
            new LegacyServerKeyResourceCatalog("STR_MAGIC_TYPE_", monsterNames, ReplaceOnlyCjk: true),
            new LegacyServerKeyResourceCatalog("STR_MAP_ID_", ReadMapNames(dbServerPath, clientPath), ReplaceOnlyCjk: true),
            new LegacyServerKeyResourceCatalog("STR_TRAP_ID_", ReadClientTrapFallbackNames(clientPath), ReplaceOnlyCjk: true),
        };

        var seedMonsterFallback = LegacyServerKeyResourceTranslator.BuildSeedMonsterFallbackCatalog(
            [dbServerPath, Directory.GetCurrentDirectory()]);
        if (seedMonsterFallback.Names.Count > 0)
        {
            catalogs.Add(seedMonsterFallback);
        }

        var spellIdEnumFallback = LegacyServerKeyResourceTranslator.BuildSpellIdEnumFallbackCatalog(
            [dbServerPath, Directory.GetCurrentDirectory()]);
        if (spellIdEnumFallback.Names.Count > 0)
        {
            catalogs.Add(spellIdEnumFallback);
        }

        var mapInfoFallback = LegacyServerKeyResourceTranslator.BuildMapInfoFallbackCatalog([dbServerPath]);
        if (mapInfoFallback.Names.Count > 0)
        {
            catalogs.Add(mapInfoFallback);
        }

        var questInfoReferencePaths = ResolveServerKeyQuestInfoReferencePaths(clientPath, dbServerPath).ToArray();
        var questAnchorNpcFallback = LegacyServerKeyResourceTranslator.BuildQuestAnchorNpcFallbackCatalog(
            questInfoReferencePaths);
        if (questAnchorNpcFallback.Names.Count > 0)
        {
            catalogs.Add(questAnchorNpcFallback);
        }

        var questLinkedNpcFallback = LegacyServerKeyResourceTranslator.BuildQuestLinkedNpcFallbackCatalog(
            questInfoReferencePaths);
        if (questLinkedNpcFallback.Names.Count > 0)
        {
            catalogs.Add(questLinkedNpcFallback);
        }

        return catalogs;
    }

    private static IEnumerable<string> ResolveServerKeyQuestInfoReferencePaths(string clientPath, string dbServerPath)
    {
        yield return Path.Combine(dbServerPath, "Questinfo.ini");
        yield return Path.Combine(clientPath, "ini", "Questinfo.ini");
    }

    private static IReadOnlyList<LegacyServerKeyNamedResourceCatalog> BuildServerKeyNamedResourceCatalogs(string dbServerPath)
    {
        var catalogs = new List<LegacyServerKeyNamedResourceCatalog>
        {
            new(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["STR_OFFICIAL_TYPE_KING"] = "King",
                ["STR_OFFICIAL_TYPE_LZAIXIANG"] = "Left Chancellor",
                ["STR_OFFICIAL_TYPE_RZAIXIANG"] = "Right Chancellor",
                ["STR_OFFICIAL_TYPE_LYUANSHUAI"] = "Left Marshal",
                ["STR_OFFICIAL_TYPE_RYUANSHUAI"] = "Right Marshal",
                ["STR_OFFICIAL_TYPE_HUGUO"] = "Guardian General",
                ["STR_OFFICIAL_TYPE_WEIGUO"] = "Defender General",
                ["STR_OFFICIAL_TYPE_ZHENBANG"] = "Stabilizing General",
                ["STR_OFFICIAL_TYPE_ZHENGYI"] = "Expedition General",
                ["STR_OFFICIAL_TYPE_YULINJUN"] = "Imperial Guard",
                ["STR_OFFICIAL_TYPE_HUANGHOU"] = "Queen",
                ["STR_OFFICIAL_TYPE_GUIFEI"] = "Imperial Consort",
                ["STR_OFFICIAL_TYPE_JIAREN"] = "Lady",
            }),
        };

        var titleTypeNames = ReadTitleTypeResourceNames(Path.Combine(dbServerPath, "title_type.txt"));
        if (titleTypeNames.Count > 0)
        {
            catalogs.Add(new LegacyServerKeyNamedResourceCatalog(titleTypeNames));
        }

        var hairfaceNames = ReadHairfaceResourceNames(Path.Combine(dbServerPath, "hairface_storage_type.txt"));
        if (hairfaceNames.Count > 0)
        {
            catalogs.Add(new LegacyServerKeyNamedResourceCatalog(hairfaceNames));
        }

        return catalogs;
    }

    private static IReadOnlyDictionary<int, string> ReadClientNpcNames(string clientPath, string dbServerPath)
    {
        var names = new Dictionary<int, string>(
            LegacyServerKeyResourceTranslator.ReadClientNpcIniNames([clientPath]));
        MergeResourceNames(names, ReadSobNpcNames(Path.Combine(dbServerPath, "SobNpcs.txt")));
        return names;
    }

    private static IReadOnlyDictionary<int, string> ReadNpcTypeNames(string path)
    {
        var names = new Dictionary<int, string>();
        if (!File.Exists(path))
        {
            return names;
        }

        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        int? currentId = null;
        foreach (var rawLine in SplitTextLines(encoding.GetString(File.ReadAllBytes(path))))
        {
            var line = rawLine.Trim();
            if (TryParseNpcTypeSection(line, out var npcId))
            {
                currentId = npcId;
                continue;
            }

            if (currentId is { } id &&
                line.StartsWith("Name=", StringComparison.OrdinalIgnoreCase) &&
                !names.ContainsKey(id))
            {
                var value = NormalizeResourceName(line["Name=".Length..]);
                if (IsLikelyEnglishResourceName(value))
                {
                    names[id] = value;
                }
            }
        }

        return names;
    }

    private static IReadOnlyDictionary<int, string> ReadBracketSectionNames(string path)
    {
        var names = new Dictionary<int, string>();
        if (!File.Exists(path))
        {
            return names;
        }

        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        int? currentId = null;
        foreach (var rawLine in SplitTextLines(encoding.GetString(File.ReadAllBytes(path))))
        {
            var line = rawLine.Trim();
            if (line.Length > 2 &&
                line[0] == '[' &&
                line[^1] == ']' &&
                int.TryParse(line[1..^1], out var npcId))
            {
                currentId = npcId;
                continue;
            }

            if (currentId is { } id &&
                line.StartsWith("Name=", StringComparison.OrdinalIgnoreCase) &&
                !names.ContainsKey(id))
            {
                var value = NormalizeResourceName(line["Name=".Length..]);
                if (IsLikelyEnglishResourceName(value))
                {
                    names[id] = value;
                }
            }
        }

        return names;
    }

    private static IReadOnlyDictionary<int, string> ReadSobNpcNames(string path)
    {
        var names = new Dictionary<int, string>();
        if (!File.Exists(path))
        {
            return names;
        }

        var encoding = GetLenientCodePage936Encoding();
        foreach (var rawLine in SplitTextLines(encoding.GetString(File.ReadAllBytes(path))))
        {
            var fields = rawLine.Split(',');
            if (fields.Length < 2 ||
                !int.TryParse(fields[0].TrimStart('0'), out var id))
            {
                continue;
            }

            var name = NormalizeResourceName(fields[1]);
            if (IsLikelyEnglishResourceName(name))
            {
                names.TryAdd(id, name);
            }
        }

        return names;
    }

    private static IReadOnlyDictionary<int, string> ReadClientTrapFallbackNames(string clientPath)
    {
        var names = new Dictionary<int, string>();
        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        foreach (var path in ResolveClientServerKeyPaths(clientPath))
        {
            var text = encoding.GetString(File.ReadAllBytes(path));
            foreach (var line in SplitTextLines(text))
            {
                var match = ServerKeyTrapIdPattern.Match(line);
                if (match.Success &&
                    int.TryParse(match.Groups["id"].Value, out var trapId))
                {
                    names.TryAdd(trapId, "trap");
                }
            }
        }

        return names;
    }

    private static IReadOnlyDictionary<int, string> ReadMonsterNames(string directory, string spawnDirectory)
    {
        var names = new Dictionary<int, string>();
        var conflicts = new HashSet<int>();
        if (Directory.Exists(directory))
        {
            foreach (var path in Directory.EnumerateFiles(directory, "*.ini", SearchOption.TopDirectoryOnly))
            {
                int? id = null;
                string? name = null;
                foreach (var rawLine in File.ReadLines(path))
                {
                    var line = rawLine.Trim();
                    if (line.StartsWith("id=", StringComparison.OrdinalIgnoreCase) &&
                        int.TryParse(line["id=".Length..], out var parsedId))
                    {
                        id = parsedId;
                        continue;
                    }

                    if (line.StartsWith("name=", StringComparison.OrdinalIgnoreCase))
                    {
                        name = NormalizeResourceName(line["name=".Length..]);
                    }
                }

                if (id is { } monsterId &&
                    name is { Length: > 0 } &&
                    IsLikelyEnglishResourceName(name))
                {
                    AddConsistentCandidate(names, conflicts, monsterId, name);
                }
            }
        }

        if (Directory.Exists(spawnDirectory))
        {
            var encoding = GetLenientCodePage936Encoding();
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.System,
            };

            foreach (var path in Directory.EnumerateFiles(spawnDirectory, "*.txt", options))
            {
                foreach (var rawLine in SplitTextLines(encoding.GetString(File.ReadAllBytes(path))))
                {
                    var fields = rawLine.Split(',');
                    if (fields.Length < 6 ||
                        !TryParseLenientNonNegativeInt(fields[1], out var monsterId))
                    {
                        continue;
                    }

                    var name = NormalizeResourceName(fields[2]);
                    if (IsLikelyEnglishResourceName(name))
                    {
                        AddConsistentCandidate(names, conflicts, monsterId, name);
                    }
                }
            }
        }

        foreach (var conflict in conflicts)
        {
            names.Remove(conflict);
        }

        return names;
    }

    private static IReadOnlyDictionary<int, string> ReadMagicNames(string dbServerPath)
    {
        var names = new Dictionary<int, string>();
        var aliasCandidates = new Dictionary<int, string>();
        var aliasConflicts = new HashSet<int>();
        var path = Path.Combine(dbServerPath, "magictype.txt");
        if (!File.Exists(path))
        {
            return names;
        }

        foreach (var line in File.ReadLines(path))
        {
            var fields = line.Split("@@", StringSplitOptions.None);
            if (fields.Length > 3 &&
                int.TryParse(fields[0], out var id) &&
                NormalizeResourceName(fields[3]) is { Length: > 0 } name &&
                IsLikelyEnglishResourceName(name))
            {
                names.TryAdd(id, name);
            }

            if (fields.Length > 3 &&
                int.TryParse(fields[1], out var aliasId) &&
                aliasId > 0 &&
                NormalizeResourceName(fields[3]) is { Length: > 0 } aliasName &&
                IsLikelyEnglishResourceName(aliasName))
            {
                AddConsistentCandidate(aliasCandidates, aliasConflicts, aliasId, aliasName);
            }
        }

        foreach (var candidate in aliasCandidates)
        {
            if (!aliasConflicts.Contains(candidate.Key))
            {
                names.TryAdd(candidate.Key, candidate.Value);
            }
        }

        foreach (var candidate in ReadMagicEffectNames(Path.Combine(dbServerPath, "MagicEffect.ini")))
        {
            names.TryAdd(candidate.Key, candidate.Value);
        }

        return names;
    }

    private static IReadOnlyDictionary<int, string> ReadMagicEffectNames(string path)
    {
        var names = new Dictionary<int, string>();
        var conflicts = new HashSet<int>();
        if (!File.Exists(path))
        {
            return names;
        }

        var encoding = GetLenientCodePage936Encoding();
        int? currentId = null;
        foreach (var rawLine in SplitTextLines(encoding.GetString(File.ReadAllBytes(path))))
        {
            var line = rawLine.Trim();
            if (line.Length > 2 &&
                line[0] == '[' &&
                line[^1] == ']' &&
                int.TryParse(line[1..^1], out var sectionId))
            {
                currentId = sectionId > 0 && sectionId % 100 == 0 ? sectionId / 100 : null;
                continue;
            }

            if (currentId is { } id &&
                line.StartsWith("Name=", StringComparison.OrdinalIgnoreCase))
            {
                var value = NormalizeResourceName(line["Name=".Length..]);
                if (IsLikelyEnglishResourceName(value))
                {
                    AddConsistentCandidate(names, conflicts, id, value);
                }
            }
        }

        foreach (var conflict in conflicts)
        {
            names.Remove(conflict);
        }

        return names;
    }

    private static IReadOnlyDictionary<int, string> ReadMapNames(string dbServerPath, string clientPath)
    {
        var names = new Dictionary<int, string>();
        var path = Path.Combine(dbServerPath, "GameMapEx.ini");
        if (!File.Exists(path))
        {
            return names;
        }

        int? currentId = null;
        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length > 2 &&
                line[0] == '[' &&
                line[^1] == ']' &&
                int.TryParse(line[1..^1], out var mapId))
            {
                currentId = mapId;
                continue;
            }

            if (currentId is { } id &&
                line.StartsWith("Name=", StringComparison.OrdinalIgnoreCase) &&
                !names.ContainsKey(id))
            {
                var value = NormalizeResourceName(line["Name=".Length..]);
                if (IsLikelyEnglishResourceName(value))
                {
                    names[id] = value;
                }
            }
        }

        MergeResourceNames(
            names,
            ReadGameMapAssetNames(
                Path.Combine(dbServerPath, "ini", "GameMap.txt"),
                [dbServerPath, clientPath]));
        MergeResourceNames(
            names,
            ReadGameMapAssetNames(
                Path.Combine(clientPath, "ini", "GameMap.txt"),
                [clientPath, dbServerPath]));
        return names;
    }

    private static IReadOnlyDictionary<int, string> ReadGameMapAssetNames(string path, IReadOnlyList<string> rootPaths)
    {
        var names = new Dictionary<int, string>();
        var conflicts = new HashSet<int>();
        if (!File.Exists(path))
        {
            return names;
        }

        foreach (var rawLine in File.ReadLines(path))
        {
            var fields = rawLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 2 ||
                !int.TryParse(fields[0], out var id))
            {
                continue;
            }

            var relativePath = fields[1].Replace('/', Path.DirectorySeparatorChar);
            if (!rootPaths.Any(rootPath => MapAssetExists(rootPath, relativePath)))
            {
                continue;
            }

            var name = NormalizeResourceName(Path.GetFileNameWithoutExtension(relativePath));
            if (IsLikelyEnglishResourceName(name))
            {
                AddConsistentCandidate(names, conflicts, id, name);
            }
        }

        foreach (var conflict in conflicts)
        {
            names.Remove(conflict);
        }

        var ambiguousMapIds = names
            .GroupBy(entry => entry.Value, StringComparer.Ordinal)
            .Where(group => group.Select(entry => entry.Key).Distinct().Skip(1).Any())
            .SelectMany(group => group.Select(entry => entry.Key))
            .ToArray();
        foreach (var ambiguousMapId in ambiguousMapIds)
        {
            names.Remove(ambiguousMapId);
        }

        return names;
    }

    private static bool MapAssetExists(string rootPath, string relativePath)
    {
        var candidate = Path.Combine(rootPath, relativePath);
        if (File.Exists(candidate))
        {
            return true;
        }

        var directory = Path.GetDirectoryName(candidate);
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(candidate);
        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(nameWithoutExtension))
        {
            return false;
        }

        foreach (var extension in new[] { ".DMap", ".dmap", ".7z", ".OtherData" })
        {
            if (File.Exists(Path.Combine(directory, nameWithoutExtension + extension)))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyDictionary<string, string> ReadTitleTypeResourceNames(string path)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(path))
        {
            return names;
        }

        foreach (var line in File.ReadLines(path))
        {
            var fields = line.Split("@@", StringSplitOptions.None);
            if (fields.Length > 2 &&
                int.TryParse(fields[0], out var titleTypeId) &&
                int.TryParse(fields[1], out var titleSubId))
            {
                var name = NormalizeResourceName(fields[2]);
                if (IsLikelyEnglishResourceName(name))
                {
                    names.TryAdd($"STR_TITLE_TYPE_{titleTypeId}_{titleSubId}", name);
                }
            }
        }

        return names;
    }

    private static IReadOnlyDictionary<string, string> ReadHairfaceResourceNames(string path)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(path))
        {
            return names;
        }

        foreach (var line in File.ReadLines(path))
        {
            var fields = line.Split("@@", StringSplitOptions.None);
            if (fields.Length > 3 &&
                int.TryParse(fields[0], out var category) &&
                int.TryParse(fields[1], out var id))
            {
                var name = NormalizeResourceName(fields[3]);
                if (IsLikelyEnglishResourceName(name))
                {
                    names.TryAdd($"STR_HAIRFACE_TYPE_{id}_{category}", name);
                }
            }
        }

        return names;
    }

    private static void MergeResourceNames(Dictionary<int, string> target, IReadOnlyDictionary<int, string> source)
    {
        foreach (var entry in source)
        {
            target.TryAdd(entry.Key, entry.Value);
        }
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

    private static bool TryParseLenientNonNegativeInt(string value, out int number)
    {
        var trimmed = value.Trim().TrimStart('0');
        if (trimmed.Length == 0)
        {
            trimmed = "0";
        }

        return int.TryParse(trimmed, out number) && number >= 0;
    }

    private static string NormalizeResourceName(string value)
    {
        return value
            .Replace('~', ' ')
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
    }

    private static bool IsLikelyEnglishResourceName(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (character is '\uFFFD' or >= '\u3400' and <= '\u9fff' or >= '\u0600' and <= '\u06ff')
            {
                return false;
            }
        }

        return true;
    }

    private static Encoding GetLenientCodePage936Encoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(
            936,
            EncoderFallback.ExceptionFallback,
            DecoderFallback.ReplacementFallback);
    }

    private static bool TryParseNpcTypeSection(string value, out int npcId)
    {
        const string prefix = "[NpcType";
        if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            value.EndsWith(']') &&
            int.TryParse(value[prefix.Length..^1], out npcId))
        {
            return true;
        }

        npcId = 0;
        return false;
    }

    private static IEnumerable<string> SplitTextLines(string text)
    {
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }

    private static string ResolveEmoneyShopItemTypePath(string[] args)
    {
        return ResolveLegacyItemTypePath(args);
    }

    private static string ResolveLegacyItemTypePath(string[] args)
    {
        if (args.Length <= 2)
        {
            return Path.GetFullPath(DefaultLegacyItemTypePath);
        }

        var path = Path.GetFullPath(args[2]);
        return Directory.Exists(path)
            ? Path.Combine(path, "itemtype.txt")
            : path;
    }

    private static string ResolveLegacyDbServerPath(string[] args)
    {
        if (args.Length <= 2)
        {
            return Path.GetFullPath(DefaultLegacyDbServerPath);
        }

        return Path.GetFullPath(args[2]);
    }
}
