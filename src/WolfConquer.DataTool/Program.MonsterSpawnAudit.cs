using System.Globalization;
using System.Text.Json;
using WolfConquer.Core.Data;

internal static partial class Program
{
    private const string MonsterSpawnAuditUsage =
        "Usage: WolfConquer.DataTool monster-spawn-audit [--world-data-root path] [--db-server-root path] [--map mapId] [--json]";

    private static readonly JsonSerializerOptions MonsterSpawnAuditJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private static readonly JsonSerializerOptions MonsterSpawnAuditCompactJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    static int MonsterSpawnAudit(string[] args)
    {
        if (!TryParseMonsterSpawnAuditOptions(args, out var options, out var error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine(MonsterSpawnAuditUsage);
            return 1;
        }

        MonsterSpawnAuditReport report;
        try
        {
            report = MonsterSpawnAuditLoader.LoadAndAudit(options);
        }
        catch (Exception ex) when (ex is
            ArgumentException or
            DirectoryNotFoundException or
            FileNotFoundException or
            FormatException or
            IOException or
            JsonException or
            UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Could not audit monster spawns: {ex.Message}");
            Console.Error.WriteLine(MonsterSpawnAuditUsage);
            return 1;
        }

        if (options.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(report, MonsterSpawnAuditJsonOptions));
        }
        else
        {
            PrintMonsterSpawnAuditReport(report);
        }

        return report.IsClean ? 0 : 2;
    }

    private static bool TryParseMonsterSpawnAuditOptions(
        string[] args,
        out MonsterSpawnAuditOptions options,
        out string error)
    {
        var worldDataRoot = WorldDataOptions.FromEnvironment().RootPath;
        var dbServerRoot = @"data/DBServer";
        int? mapId = null;
        var json = false;
        error = "";

        for (var index = 1; index < args.Length; index++)
        {
            var argument = args[index];
            if (string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase))
            {
                json = true;
                continue;
            }

            if (TryReadMonsterSpawnAuditOption(args, ref index, "--world-data-root", out var value, out error))
            {
                worldDataRoot = value;
                continue;
            }

            if (error.Length > 0)
            {
                options = default!;
                return false;
            }

            if (TryReadMonsterSpawnAuditOption(args, ref index, "--db-server-root", out value, out error))
            {
                dbServerRoot = value;
                continue;
            }

            if (error.Length > 0)
            {
                options = default!;
                return false;
            }

            if (TryReadMonsterSpawnAuditOption(args, ref index, "--map", out value, out error))
            {
                if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedMapId) ||
                    parsedMapId <= 0)
                {
                    options = default!;
                    error = $"Invalid --map value: {value}";
                    return false;
                }

                mapId = parsedMapId;
                continue;
            }

            if (error.Length > 0)
            {
                options = default!;
                return false;
            }

            options = default!;
            error = $"Unknown monster-spawn-audit option: {argument}";
            return false;
        }

        try
        {
            options = new MonsterSpawnAuditOptions(
                Path.GetFullPath(worldDataRoot),
                Path.GetFullPath(dbServerRoot),
                mapId,
                json);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            options = default!;
            error = $"Invalid monster-spawn-audit path: {ex.Message}";
            return false;
        }
    }

    private static bool TryReadMonsterSpawnAuditOption(
        string[] args,
        ref int index,
        string optionName,
        out string value,
        out string error)
    {
        var argument = args[index];
        value = "";
        error = "";

        if (string.Equals(argument, optionName, StringComparison.OrdinalIgnoreCase))
        {
            if (index + 1 >= args.Length)
            {
                error = $"Missing value for {optionName}.";
                return false;
            }

            value = args[++index];
            if (value.Length == 0)
            {
                error = $"Missing value for {optionName}.";
                return false;
            }

            return true;
        }

        var prefix = optionName + "=";
        if (!argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        value = argument[prefix.Length..];
        if (value.Length == 0)
        {
            error = $"Missing value for {optionName}.";
            return false;
        }

        return true;
    }

    private static void PrintMonsterSpawnAuditReport(MonsterSpawnAuditReport report)
    {
        Console.WriteLine($"monster-spawn-audit schemaVersion={report.SchemaVersion.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"activeWorldDataRoot={JsonSerializer.Serialize(report.ActiveWorldDataRoot)}");
        Console.WriteLine($"originalDbServerRoot={JsonSerializer.Serialize(report.OriginalDbServerRoot)}");
        Console.WriteLine($"mapId={report.MapId?.ToString(CultureInfo.InvariantCulture) ?? "all"}");
        Console.WriteLine(
            $"summary activeSpawns={report.ActiveSpawnCount.ToString(CultureInfo.InvariantCulture)} " +
            $"originalSpawns={report.OriginalSpawnCount.ToString(CultureInfo.InvariantCulture)} " +
            $"activeFamilies={report.ActiveFamilyCount.ToString(CultureInfo.InvariantCulture)} " +
            $"originalFamilies={report.OriginalFamilyCount.ToString(CultureInfo.InvariantCulture)} " +
            $"missingSpawns={report.MissingSpawns.Count.ToString(CultureInfo.InvariantCulture)} " +
            $"excessSpawns={report.ExcessSpawns.Count.ToString(CultureInfo.InvariantCulture)} " +
            $"guardStacks={report.GuardStacks.Count.ToString(CultureInfo.InvariantCulture)} " +
            $"missingFamilies={report.MissingFamilies.Count.ToString(CultureInfo.InvariantCulture)} " +
            $"coordinateDuplicates={report.CoordinateDuplicates.Count.ToString(CultureInfo.InvariantCulture)}");

        foreach (var finding in report.MissingSpawns)
        {
            Console.WriteLine(
                "missingSpawn " +
                JsonSerializer.Serialize(finding, MonsterSpawnAuditCompactJsonOptions));
        }

        foreach (var finding in report.ExcessSpawns)
        {
            Console.WriteLine(
                "excessSpawn " +
                JsonSerializer.Serialize(finding, MonsterSpawnAuditCompactJsonOptions));
        }

        foreach (var finding in report.GuardStacks)
        {
            Console.WriteLine(
                "guardStack " +
                JsonSerializer.Serialize(finding, MonsterSpawnAuditCompactJsonOptions));
        }

        foreach (var finding in report.MissingFamilies)
        {
            Console.WriteLine(
                "missingFamily " +
                JsonSerializer.Serialize(finding, MonsterSpawnAuditCompactJsonOptions));
        }

        foreach (var finding in report.CoordinateDuplicates)
        {
            Console.WriteLine(
                "coordinateDuplicate " +
                JsonSerializer.Serialize(finding, MonsterSpawnAuditCompactJsonOptions));
        }

        Console.WriteLine($"result isClean={report.IsClean.ToString().ToLowerInvariant()}");
    }
}

internal sealed record MonsterSpawnAuditOptions(
    string ActiveWorldDataRoot,
    string OriginalDbServerRoot,
    int? MapId,
    bool Json);

internal sealed record MonsterSpawnAuditReport(
    int SchemaVersion,
    string ActiveWorldDataRoot,
    string OriginalDbServerRoot,
    int? MapId,
    int ActiveSpawnCount,
    int OriginalSpawnCount,
    int ActiveFamilyCount,
    int OriginalFamilyCount,
    IReadOnlyList<MonsterSpawnAuditProjectionFinding> MissingSpawns,
    IReadOnlyList<MonsterSpawnAuditProjectionFinding> ExcessSpawns,
    IReadOnlyList<MonsterSpawnAuditCoordinateFinding> GuardStacks,
    IReadOnlyList<MonsterSpawnAuditMissingFamilyFinding> MissingFamilies,
    IReadOnlyList<MonsterSpawnAuditCoordinateFinding> CoordinateDuplicates)
{
    public bool IsClean =>
        ActiveSpawnCount == OriginalSpawnCount &&
        ActiveFamilyCount == OriginalFamilyCount &&
        MissingSpawns.Count == 0 &&
        ExcessSpawns.Count == 0 &&
        GuardStacks.All(static finding => finding.SourceBacked) &&
        MissingFamilies.Count == 0 &&
        CoordinateDuplicates.All(static finding => finding.SourceBacked);
}

internal sealed record MonsterSpawnAuditProjectionFinding(
    int GeneratorId,
    int MapId,
    int FamilyId,
    string FamilyName,
    int X,
    int Y,
    int BoundWidth,
    int BoundHeight,
    int MaxNpc,
    int RestSeconds,
    int MaxPerGenerator,
    int TimerBegin,
    int TimerEnd,
    int BornX,
    int BornY,
    int Count,
    IReadOnlyList<MonsterSpawnAuditSourceReference> Sources);

internal sealed record MonsterSpawnAuditCoordinateFinding(
    int MapId,
    int X,
    int Y,
    int ActiveCount,
    int OriginalCount,
    IReadOnlyList<MonsterSpawnAuditFamilyCount> ActiveFamilies,
    IReadOnlyList<MonsterSpawnAuditFamilyCount> OriginalFamilies,
    IReadOnlyList<MonsterSpawnAuditSourceReference> ActiveSources,
    IReadOnlyList<MonsterSpawnAuditSourceReference> OriginalSources)
{
    public int ExcessCount => Math.Max(0, ActiveCount - OriginalCount);

    public bool SourceBacked => OriginalCount >= ActiveCount;
}

internal sealed record MonsterSpawnAuditMissingFamilyFinding(
    int MapId,
    int FamilyId,
    string FamilyName,
    int OriginalSpawnCount,
    IReadOnlyList<MonsterSpawnAuditSourceReference> OriginalSources);

internal sealed record MonsterSpawnAuditFamilyCount(
    int FamilyId,
    string FamilyName,
    int SpawnCount);

internal sealed record MonsterSpawnAuditSourceReference(
    int GeneratorId,
    int FamilyId,
    string FamilyName,
    string SourceRelativePath,
    int? SourceLine);

internal static class MonsterSpawnAuditLoader
{
    private static readonly JsonSerializerOptions SeedJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static MonsterSpawnAuditReport LoadAndAudit(MonsterSpawnAuditOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var active = LoadActiveProjection(options.ActiveWorldDataRoot);
        var original = LoadOriginalDbServerProjection(options.OriginalDbServerRoot);
        return MonsterSpawnAuditor.Audit(
            active.Monsters,
            active.Spawns,
            original.Monsters,
            original.Spawns,
            options.ActiveWorldDataRoot,
            options.OriginalDbServerRoot,
            options.MapId);
    }

    private static MonsterSpawnAuditProjection LoadActiveProjection(string rootPath)
    {
        if (!Directory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException($"Active world-data root not found: {rootPath}");
        }

        return new MonsterSpawnAuditProjection(
            ReadRequiredJsonArray<Monster>(Path.Combine(rootPath, "monsters.json")),
            ReadRequiredJsonArray<MonsterSpawn>(Path.Combine(rootPath, "monsterSpawns.json")));
    }

    private static MonsterSpawnAuditProjection LoadOriginalDbServerProjection(string rootPath)
    {
        if (!Directory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException($"Original DBServer root not found: {rootPath}");
        }

        var monstersRoot = Path.Combine(rootPath, "Monsters");
        var spawnsRoot = Path.Combine(rootPath, "MonsterSpawns");
        if (!Directory.Exists(monstersRoot))
        {
            throw new DirectoryNotFoundException($"Original monster-family directory not found: {monstersRoot}");
        }

        var monsters = Directory.EnumerateFiles(monstersRoot, "*.ini", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(path => MonsterParser.Parse(IniDocument.Parse(File.ReadAllText(path))))
            .ToArray();
        var monsterTypeIdsByName = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var monster in monsters)
        {
            monsterTypeIdsByName.TryAdd(monster.Name, monster.Id);
        }

        var spawns = MonsterSpawnImport.ReadMonsterSpawns(spawnsRoot, monsterTypeIdsByName);
        return new MonsterSpawnAuditProjection(monsters, spawns);
    }

    private static T[] ReadRequiredJsonArray<T>(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Active world-data file not found: {path}", path);
        }

        return JsonSerializer.Deserialize<T[]>(File.ReadAllText(path), SeedJsonOptions) ??
            throw new FormatException($"Active world-data file is empty: {path}");
    }

    private sealed record MonsterSpawnAuditProjection(
        IReadOnlyList<Monster> Monsters,
        IReadOnlyList<MonsterSpawn> Spawns);
}

internal static class MonsterSpawnAuditor
{
    private const int SchemaVersion = 1;
    private const uint LegacyGuardMonsterSettingsFlag = 0xA4;

    public static MonsterSpawnAuditReport Audit(
        IReadOnlyList<Monster> activeMonsters,
        IReadOnlyList<MonsterSpawn> activeSpawns,
        IReadOnlyList<Monster> originalMonsters,
        IReadOnlyList<MonsterSpawn> originalSpawns,
        string activeWorldDataRoot,
        string originalDbServerRoot,
        int? mapId = null)
    {
        ArgumentNullException.ThrowIfNull(activeMonsters);
        ArgumentNullException.ThrowIfNull(activeSpawns);
        ArgumentNullException.ThrowIfNull(originalMonsters);
        ArgumentNullException.ThrowIfNull(originalSpawns);
        ArgumentNullException.ThrowIfNull(activeWorldDataRoot);
        ArgumentNullException.ThrowIfNull(originalDbServerRoot);

        var filteredActiveSpawns = FilterAndOrder(activeSpawns, mapId);
        var filteredOriginalSpawns = FilterAndOrder(originalSpawns, mapId);
        var activeFamilies = BuildFamiliesById(activeMonsters);
        var originalFamilies = BuildFamiliesById(originalMonsters);
        var familyNames = BuildFamilyNames(activeFamilies, originalFamilies, filteredActiveSpawns, filteredOriginalSpawns);
        var guardFamilyIds = BuildGuardFamilyIds(activeFamilies, originalFamilies);

        var missingSpawns = BuildProjectionFindings(
            filteredOriginalSpawns,
            filteredActiveSpawns,
            familyNames);
        var excessSpawns = BuildProjectionFindings(
            filteredActiveSpawns,
            filteredOriginalSpawns,
            familyNames);

        var activeCoordinateGroups = GroupByCoordinate(filteredActiveSpawns);
        var originalCoordinateGroups = GroupByCoordinate(filteredOriginalSpawns);
        var activeGuardCoordinateGroups = GroupByCoordinate(filteredActiveSpawns
            .Where(spawn => guardFamilyIds.Contains(spawn.MonsterTypeId))
            .ToArray());
        var originalGuardCoordinateGroups = GroupByCoordinate(filteredOriginalSpawns
            .Where(spawn => guardFamilyIds.Contains(spawn.MonsterTypeId))
            .ToArray());

        var guardStacks = BuildCoordinateFindings(
            activeGuardCoordinateGroups,
            originalGuardCoordinateGroups,
            familyNames);
        var coordinateDuplicates = BuildCoordinateFindings(
            activeCoordinateGroups,
            originalCoordinateGroups,
            familyNames);
        var missingFamilies = BuildMissingFamilyFindings(
            filteredActiveSpawns,
            filteredOriginalSpawns,
            familyNames);

        return new MonsterSpawnAuditReport(
            SchemaVersion,
            activeWorldDataRoot,
            originalDbServerRoot,
            mapId,
            filteredActiveSpawns.Count,
            filteredOriginalSpawns.Count,
            filteredActiveSpawns.Select(static spawn => spawn.MonsterTypeId).Distinct().Count(),
            filteredOriginalSpawns.Select(static spawn => spawn.MonsterTypeId).Distinct().Count(),
            missingSpawns,
            excessSpawns,
            guardStacks,
            missingFamilies,
            coordinateDuplicates);
    }

    private static IReadOnlyList<MonsterSpawn> FilterAndOrder(
        IReadOnlyList<MonsterSpawn> spawns,
        int? mapId)
    {
        return spawns
            .Where(spawn => !mapId.HasValue || spawn.MapId == mapId.Value)
            .OrderBy(static spawn => spawn.MapId)
            .ThenBy(static spawn => spawn.BoundX)
            .ThenBy(static spawn => spawn.BoundY)
            .ThenBy(static spawn => spawn.MonsterTypeId)
            .ThenBy(static spawn => spawn.GeneratorId)
            .ThenBy(static spawn => spawn.SourceRelativePath, StringComparer.Ordinal)
            .ThenBy(static spawn => ReadSourceLine(spawn))
            .ToArray();
    }

    private static IReadOnlyDictionary<int, Monster> BuildFamiliesById(IReadOnlyList<Monster> monsters)
    {
        var result = new Dictionary<int, Monster>();
        foreach (var monster in monsters.OrderBy(static monster => monster.Id).ThenBy(static monster => monster.Name, StringComparer.Ordinal))
        {
            result.TryAdd(monster.Id, monster);
        }

        return result;
    }

    private static IReadOnlyList<MonsterSpawnAuditProjectionFinding> BuildProjectionFindings(
        IReadOnlyList<MonsterSpawn> expectedSpawns,
        IReadOnlyList<MonsterSpawn> actualSpawns,
        IReadOnlyDictionary<int, string> familyNames)
    {
        var actualCounts = actualSpawns
            .GroupBy(CreateProjectionKey)
            .ToDictionary(static group => group.Key, static group => group.Count());

        return expectedSpawns
            .GroupBy(CreateProjectionKey)
            .Select(group =>
            {
                actualCounts.TryGetValue(group.Key, out var actualCount);
                return (Group: group, MissingCount: group.Count() - actualCount);
            })
            .Where(static entry => entry.MissingCount > 0)
            .OrderBy(static entry => entry.Group.Key.MapId)
            .ThenBy(static entry => entry.Group.Key.GeneratorId)
            .ThenBy(static entry => entry.Group.Key.MonsterTypeId)
            .ThenBy(static entry => entry.Group.Key.BoundX)
            .ThenBy(static entry => entry.Group.Key.BoundY)
            .Select(entry => new MonsterSpawnAuditProjectionFinding(
                entry.Group.Key.GeneratorId,
                entry.Group.Key.MapId,
                entry.Group.Key.MonsterTypeId,
                ResolveFamilyName(entry.Group.Key.MonsterTypeId, familyNames),
                entry.Group.Key.BoundX,
                entry.Group.Key.BoundY,
                entry.Group.Key.BoundWidth,
                entry.Group.Key.BoundHeight,
                entry.Group.Key.MaxNpc,
                entry.Group.Key.RestSeconds,
                entry.Group.Key.MaxPerGenerator,
                entry.Group.Key.TimerBegin,
                entry.Group.Key.TimerEnd,
                entry.Group.Key.BornX,
                entry.Group.Key.BornY,
                entry.MissingCount,
                BuildSourceReferences(entry.Group.ToArray(), familyNames)))
            .ToArray();
    }

    private static MonsterSpawnAuditProjectionKey CreateProjectionKey(MonsterSpawn spawn)
    {
        return new MonsterSpawnAuditProjectionKey(
            spawn.GeneratorId,
            spawn.MapId,
            spawn.MonsterTypeId,
            spawn.BoundX,
            spawn.BoundY,
            spawn.BoundWidth,
            spawn.BoundHeight,
            spawn.MaxNpc,
            spawn.RestSeconds,
            spawn.MaxPerGenerator,
            spawn.TimerBegin,
            spawn.TimerEnd,
            spawn.BornX,
            spawn.BornY);
    }

    private static IReadOnlySet<int> BuildGuardFamilyIds(
        IReadOnlyDictionary<int, Monster> activeFamilies,
        IReadOnlyDictionary<int, Monster> originalFamilies)
    {
        return originalFamilies.Values
            .Concat(activeFamilies.Values)
            .Where(IsGuardFamily)
            .Select(static family => family.Id)
            .ToHashSet();
    }

    private static IReadOnlyDictionary<int, string> BuildFamilyNames(
        IReadOnlyDictionary<int, Monster> activeFamilies,
        IReadOnlyDictionary<int, Monster> originalFamilies,
        IReadOnlyList<MonsterSpawn> activeSpawns,
        IReadOnlyList<MonsterSpawn> originalSpawns)
    {
        var result = new Dictionary<int, string>();
        foreach (var family in originalFamilies.Values.OrderBy(static family => family.Id))
        {
            result.TryAdd(family.Id, family.Name);
        }

        foreach (var family in activeFamilies.Values.OrderBy(static family => family.Id))
        {
            result[family.Id] = family.Name;
        }

        foreach (var spawn in activeSpawns.Concat(originalSpawns))
        {
            if (!result.ContainsKey(spawn.MonsterTypeId) &&
                !string.IsNullOrWhiteSpace(spawn.SourceMonsterName))
            {
                result.Add(spawn.MonsterTypeId, spawn.SourceMonsterName);
            }
        }

        return result;
    }

    private static IReadOnlyDictionary<MonsterSpawnAuditCoordinateKey, IReadOnlyList<MonsterSpawn>> GroupByCoordinate(
        IReadOnlyList<MonsterSpawn> spawns)
    {
        return spawns
            .GroupBy(static spawn => new MonsterSpawnAuditCoordinateKey(spawn.MapId, spawn.BoundX, spawn.BoundY))
            .ToDictionary(
                static group => group.Key,
                static group => (IReadOnlyList<MonsterSpawn>)group.ToArray());
    }

    private static IReadOnlyList<MonsterSpawnAuditCoordinateFinding> BuildCoordinateFindings(
        IReadOnlyDictionary<MonsterSpawnAuditCoordinateKey, IReadOnlyList<MonsterSpawn>> activeGroups,
        IReadOnlyDictionary<MonsterSpawnAuditCoordinateKey, IReadOnlyList<MonsterSpawn>> originalGroups,
        IReadOnlyDictionary<int, string> familyNames)
    {
        return activeGroups
            .Where(static entry => entry.Value.Count > 1)
            .OrderBy(static entry => entry.Key.MapId)
            .ThenBy(static entry => entry.Key.X)
            .ThenBy(static entry => entry.Key.Y)
            .Select(entry =>
            {
                var original = originalGroups.TryGetValue(entry.Key, out var matchingOriginal)
                    ? matchingOriginal
                    : [];
                return new MonsterSpawnAuditCoordinateFinding(
                    entry.Key.MapId,
                    entry.Key.X,
                    entry.Key.Y,
                    entry.Value.Count,
                    original.Count,
                    BuildFamilyCounts(entry.Value, familyNames),
                    BuildFamilyCounts(original, familyNames),
                    BuildSourceReferences(entry.Value, familyNames),
                    BuildSourceReferences(original, familyNames));
            })
            .ToArray();
    }

    private static IReadOnlyList<MonsterSpawnAuditMissingFamilyFinding> BuildMissingFamilyFindings(
        IReadOnlyList<MonsterSpawn> activeSpawns,
        IReadOnlyList<MonsterSpawn> originalSpawns,
        IReadOnlyDictionary<int, string> familyNames)
    {
        var activeKeys = activeSpawns
            .Select(static spawn => new MonsterSpawnAuditFamilyKey(spawn.MapId, spawn.MonsterTypeId))
            .ToHashSet();

        return originalSpawns
            .GroupBy(static spawn => new MonsterSpawnAuditFamilyKey(spawn.MapId, spawn.MonsterTypeId))
            .Where(group => !activeKeys.Contains(group.Key))
            .OrderBy(static group => group.Key.MapId)
            .ThenBy(static group => group.Key.FamilyId)
            .Select(group => new MonsterSpawnAuditMissingFamilyFinding(
                group.Key.MapId,
                group.Key.FamilyId,
                ResolveFamilyName(group.Key.FamilyId, familyNames),
                group.Count(),
                BuildSourceReferences(group.ToArray(), familyNames)))
            .ToArray();
    }

    private static IReadOnlyList<MonsterSpawnAuditFamilyCount> BuildFamilyCounts(
        IReadOnlyList<MonsterSpawn> spawns,
        IReadOnlyDictionary<int, string> familyNames)
    {
        return spawns
            .GroupBy(static spawn => spawn.MonsterTypeId)
            .OrderBy(static group => group.Key)
            .Select(group => new MonsterSpawnAuditFamilyCount(
                group.Key,
                ResolveFamilyName(group.Key, familyNames),
                group.Count()))
            .ToArray();
    }

    private static IReadOnlyList<MonsterSpawnAuditSourceReference> BuildSourceReferences(
        IReadOnlyList<MonsterSpawn> spawns,
        IReadOnlyDictionary<int, string> familyNames)
    {
        return spawns
            .OrderBy(static spawn => spawn.MonsterTypeId)
            .ThenBy(static spawn => spawn.GeneratorId)
            .ThenBy(static spawn => spawn.SourceRelativePath, StringComparer.Ordinal)
            .ThenBy(static spawn => ReadSourceLine(spawn))
            .Select(spawn => new MonsterSpawnAuditSourceReference(
                spawn.GeneratorId,
                spawn.MonsterTypeId,
                ResolveFamilyName(spawn.MonsterTypeId, familyNames),
                spawn.SourceRelativePath,
                ReadSourceLine(spawn)))
            .ToArray();
    }

    private static bool IsGuardFamily(Monster family)
    {
        if (TryReadMonsterSettings(family, out var settings) &&
            (settings & LegacyGuardMonsterSettingsFlag) == LegacyGuardMonsterSettingsFlag)
        {
            return true;
        }

        return (family.Name.Contains("Guard", StringComparison.Ordinal) &&
                !family.Name.Contains("Chaos", StringComparison.Ordinal)) ||
            family.Name.Contains("CitySoldier", StringComparison.Ordinal);
    }

    private static bool TryReadMonsterSettings(Monster family, out uint settings)
    {
        settings = 0;
        foreach (var key in new[] { "settings", "Settings" })
        {
            if (family.Attributes.TryGetValue(key, out var value) &&
                uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out settings))
            {
                return true;
            }
        }

        return false;
    }

    private static string ResolveFamilyName(int familyId, IReadOnlyDictionary<int, string> familyNames)
    {
        return familyNames.TryGetValue(familyId, out var name)
            ? name
            : $"#{familyId.ToString(CultureInfo.InvariantCulture)}";
    }

    private static int? ReadSourceLine(MonsterSpawn spawn)
    {
        return spawn.Attributes.TryGetValue("source_line", out var value) &&
            int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
    }

    private readonly record struct MonsterSpawnAuditCoordinateKey(int MapId, int X, int Y);

    private readonly record struct MonsterSpawnAuditFamilyKey(int MapId, int FamilyId);

    private readonly record struct MonsterSpawnAuditProjectionKey(
        int GeneratorId,
        int MapId,
        int MonsterTypeId,
        int BoundX,
        int BoundY,
        int BoundWidth,
        int BoundHeight,
        int MaxNpc,
        int RestSeconds,
        int MaxPerGenerator,
        int TimerBegin,
        int TimerEnd,
        int BornX,
        int BornY);
}
