using System.Globalization;
using System.Text.Json;
using WolfConquer.Core.Data;

internal static partial class Program
{
    private const string MonsterFamilyAuditUsage =
        "Usage: WolfConquer.DataTool monster-family-audit [--world-data-root path] [--db-server-root path] [--json]";

    private static readonly JsonSerializerOptions MonsterFamilyAuditJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private static readonly JsonSerializerOptions MonsterFamilyAuditCompactJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    static int MonsterFamilyAudit(string[] args)
    {
        if (!TryParseMonsterFamilyAuditOptions(args, out var options, out var error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine(MonsterFamilyAuditUsage);
            return 1;
        }

        MonsterFamilyAuditReport report;
        try
        {
            report = MonsterFamilyAuditLoader.LoadAndAudit(options);
        }
        catch (Exception ex) when (ex is
            ArgumentException or
            DirectoryNotFoundException or
            FileNotFoundException or
            FormatException or
            IOException or
            JsonException or
            NotSupportedException or
            UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Could not audit monster families: {ex.Message}");
            Console.Error.WriteLine(MonsterFamilyAuditUsage);
            return 1;
        }

        if (options.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(report, MonsterFamilyAuditJsonOptions));
        }
        else
        {
            PrintMonsterFamilyAuditReport(report);
        }

        return report.IsClean ? 0 : 2;
    }

    private static bool TryParseMonsterFamilyAuditOptions(
        string[] args,
        out MonsterFamilyAuditOptions options,
        out string error)
    {
        var worldDataRoot = WorldDataOptions.FromEnvironment().RootPath;
        var dbServerRoot = @"data/DBServer";
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

            if (TryReadMonsterFamilyAuditOption(args, ref index, "--world-data-root", out var value, out error))
            {
                worldDataRoot = value;
                continue;
            }

            if (error.Length > 0)
            {
                options = default!;
                return false;
            }

            if (TryReadMonsterFamilyAuditOption(args, ref index, "--db-server-root", out value, out error))
            {
                dbServerRoot = value;
                continue;
            }

            if (error.Length > 0)
            {
                options = default!;
                return false;
            }

            options = default!;
            error = $"Unknown monster-family-audit option: {argument}";
            return false;
        }

        try
        {
            options = new MonsterFamilyAuditOptions(
                Path.GetFullPath(worldDataRoot),
                Path.GetFullPath(dbServerRoot),
                json);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            options = default!;
            error = $"Invalid monster-family-audit path: {ex.Message}";
            return false;
        }
    }

    private static bool TryReadMonsterFamilyAuditOption(
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

    private static void PrintMonsterFamilyAuditReport(MonsterFamilyAuditReport report)
    {
        Console.WriteLine($"monster-family-audit schemaVersion={report.SchemaVersion.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"activeWorldDataRoot={JsonSerializer.Serialize(report.ActiveWorldDataRoot)}");
        Console.WriteLine($"originalDbServerRoot={JsonSerializer.Serialize(report.OriginalDbServerRoot)}");
        Console.WriteLine(
            $"summary activeFamilies={report.ActiveFamilyCount.ToString(CultureInfo.InvariantCulture)} " +
            $"originalFamilies={report.OriginalFamilyCount.ToString(CultureInfo.InvariantCulture)} " +
            $"missingFamilies={report.MissingFamilies.Count.ToString(CultureInfo.InvariantCulture)} " +
            $"excessFamilies={report.ExcessFamilies.Count.ToString(CultureInfo.InvariantCulture)} " +
            $"fieldMismatches={report.FieldMismatches.Count.ToString(CultureInfo.InvariantCulture)}");

        foreach (var finding in report.MissingFamilies)
        {
            Console.WriteLine(
                "missingFamily " +
                JsonSerializer.Serialize(finding, MonsterFamilyAuditCompactJsonOptions));
        }

        foreach (var finding in report.ExcessFamilies)
        {
            Console.WriteLine(
                "excessFamily " +
                JsonSerializer.Serialize(finding, MonsterFamilyAuditCompactJsonOptions));
        }

        foreach (var finding in report.FieldMismatches)
        {
            Console.WriteLine(
                "fieldMismatch " +
                JsonSerializer.Serialize(finding, MonsterFamilyAuditCompactJsonOptions));
        }

        Console.WriteLine($"result isClean={report.IsClean.ToString().ToLowerInvariant()}");
    }
}

internal sealed record MonsterFamilyAuditOptions(
    string ActiveWorldDataRoot,
    string OriginalDbServerRoot,
    bool Json);

internal sealed record MonsterFamilyAuditReport(
    int SchemaVersion,
    string ActiveWorldDataRoot,
    string OriginalDbServerRoot,
    int ActiveFamilyCount,
    int OriginalFamilyCount,
    IReadOnlyList<MonsterFamilyAuditFamilyFinding> MissingFamilies,
    IReadOnlyList<MonsterFamilyAuditFamilyFinding> ExcessFamilies,
    IReadOnlyList<MonsterFamilyAuditFieldMismatch> FieldMismatches)
{
    public bool IsClean =>
        ActiveFamilyCount == OriginalFamilyCount &&
        MissingFamilies.Count == 0 &&
        ExcessFamilies.Count == 0 &&
        FieldMismatches.Count == 0;
}

internal sealed record MonsterFamilyAuditFamilyFinding(
    int FamilyId,
    string FamilyName,
    string SourceRelativePath);

internal sealed record MonsterFamilyAuditFieldMismatch(
    int FamilyId,
    string ActiveFamilyName,
    string OriginalFamilyName,
    string Field,
    string? ActiveValue,
    string? OriginalValue,
    string OriginalSourceRelativePath);

internal sealed record MonsterFamilyAuditOriginalFamily(
    Monster Family,
    string SourceRelativePath);

internal static class MonsterFamilyAuditLoader
{
    private static readonly JsonSerializerOptions SeedJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static MonsterFamilyAuditReport LoadAndAudit(MonsterFamilyAuditOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var activeFamilies = LoadActiveFamilies(options.ActiveWorldDataRoot);
        var originalFamilies = LoadOriginalFamilies(options.OriginalDbServerRoot);
        return MonsterFamilyAuditor.Audit(
            activeFamilies,
            originalFamilies,
            options.ActiveWorldDataRoot,
            options.OriginalDbServerRoot);
    }

    private static IReadOnlyList<Monster> LoadActiveFamilies(string rootPath)
    {
        if (!Directory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException($"Active world-data root not found: {rootPath}");
        }

        var path = Path.Combine(rootPath, "monsters.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Active world-data file not found: {path}", path);
        }

        return JsonSerializer.Deserialize<Monster[]>(File.ReadAllText(path), SeedJsonOptions) ??
            throw new FormatException($"Active world-data file is empty: {path}");
    }

    private static IReadOnlyList<MonsterFamilyAuditOriginalFamily> LoadOriginalFamilies(string rootPath)
    {
        if (!Directory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException($"Original DBServer root not found: {rootPath}");
        }

        var monstersRoot = Path.Combine(rootPath, "Monsters");
        if (!Directory.Exists(monstersRoot))
        {
            throw new DirectoryNotFoundException($"Original monster-family directory not found: {monstersRoot}");
        }

        return Directory.EnumerateFiles(monstersRoot, "*.ini", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(path => new MonsterFamilyAuditOriginalFamily(
                MonsterParser.Parse(IniDocument.Parse(File.ReadAllText(path))),
                Path.GetRelativePath(rootPath, path)))
            .ToArray();
    }
}

internal static class MonsterFamilyAuditor
{
    private const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions CompactJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly IReadOnlyList<MonsterFamilyAuditFieldDefinition> ComparedFields =
    [
        new("attack_min", ["attack_min"]),
        new("attack_max", ["attack_max"]),
        new("life", ["life"]),
        new("defence", ["defense", "defence"]),
        new("defence2", ["defence2"]),
        new("level", ["level"]),
        new("lookface", ["lookface"]),
        new("attack_range", ["attack_range"]),
        new("dodge", ["dodge"]),
        new("Boss", ["Boss"]),
        new("move_speed", ["move_speed"]),
        new("attack_speed", ["attack_speed"]),
        new("magic_type", ["magic_type"]),
        new("critical", ["critical"]),
        new("break", ["break"]),
        new("extra_battlelev", ["extra_battlelev"]),
        new("extra_exp", ["extra_exp"]),
        new("extra_damage", ["extra_damage"]),
    ];

    public static MonsterFamilyAuditReport Audit(
        IReadOnlyList<Monster> activeFamilies,
        IReadOnlyList<MonsterFamilyAuditOriginalFamily> originalFamilies,
        string activeWorldDataRoot,
        string originalDbServerRoot)
    {
        ArgumentNullException.ThrowIfNull(activeFamilies);
        ArgumentNullException.ThrowIfNull(originalFamilies);
        ArgumentNullException.ThrowIfNull(activeWorldDataRoot);
        ArgumentNullException.ThrowIfNull(originalDbServerRoot);

        var activeById = BuildActiveFamiliesById(activeFamilies);
        var originalById = BuildOriginalFamiliesById(originalFamilies);
        var missingFamilies = originalById.Values
            .Where(original => !activeById.ContainsKey(original.Family.Id))
            .OrderBy(static original => original.Family.Id)
            .Select(static original => new MonsterFamilyAuditFamilyFinding(
                original.Family.Id,
                original.Family.Name,
                original.SourceRelativePath))
            .ToArray();
        var excessFamilies = activeById.Values
            .Where(active => !originalById.ContainsKey(active.Id))
            .OrderBy(static active => active.Id)
            .Select(static active => new MonsterFamilyAuditFamilyFinding(
                active.Id,
                active.Name,
                "monsters.json"))
            .ToArray();

        var fieldMismatches = new List<MonsterFamilyAuditFieldMismatch>();
        foreach (var familyId in activeById.Keys.Intersect(originalById.Keys).Order())
        {
            var active = activeById[familyId];
            var original = originalById[familyId];
            AddMismatchIfDifferent(
                fieldMismatches,
                active,
                original,
                "name",
                active.Name,
                original.Family.Name);

            foreach (var field in ComparedFields)
            {
                AddMismatchIfDifferent(
                    fieldMismatches,
                    active,
                    original,
                    field.Name,
                    ReadNumericAttribute(active, field.Aliases),
                    ReadNumericAttribute(original.Family, field.Aliases));
            }

            AddMismatchIfDifferent(
                fieldMismatches,
                active,
                original,
                "specialDrops",
                SerializeSpecialDrops(active),
                SerializeSpecialDrops(original.Family));
        }

        return new MonsterFamilyAuditReport(
            SchemaVersion,
            activeWorldDataRoot,
            originalDbServerRoot,
            activeFamilies.Count,
            originalFamilies.Count,
            missingFamilies,
            excessFamilies,
            fieldMismatches);
    }

    private static IReadOnlyDictionary<int, Monster> BuildActiveFamiliesById(IReadOnlyList<Monster> families)
    {
        var result = new Dictionary<int, Monster>();
        foreach (var family in families)
        {
            if (!result.TryAdd(family.Id, family))
            {
                throw new FormatException($"Duplicate active monster family id {family.Id.ToString(CultureInfo.InvariantCulture)}.");
            }
        }

        return result;
    }

    private static IReadOnlyDictionary<int, MonsterFamilyAuditOriginalFamily> BuildOriginalFamiliesById(
        IReadOnlyList<MonsterFamilyAuditOriginalFamily> families)
    {
        var result = new Dictionary<int, MonsterFamilyAuditOriginalFamily>();
        foreach (var family in families)
        {
            if (!result.TryAdd(family.Family.Id, family))
            {
                throw new FormatException(
                    $"Duplicate original monster family id {family.Family.Id.ToString(CultureInfo.InvariantCulture)}.");
            }
        }

        return result;
    }

    private static void AddMismatchIfDifferent(
        ICollection<MonsterFamilyAuditFieldMismatch> mismatches,
        Monster active,
        MonsterFamilyAuditOriginalFamily original,
        string field,
        string? activeValue,
        string? originalValue)
    {
        if (string.Equals(activeValue, originalValue, StringComparison.Ordinal))
        {
            return;
        }

        mismatches.Add(new MonsterFamilyAuditFieldMismatch(
            active.Id,
            active.Name,
            original.Family.Name,
            field,
            activeValue,
            originalValue,
            original.SourceRelativePath));
    }

    private static string? ReadNumericAttribute(Monster family, IReadOnlyList<string> aliases)
    {
        foreach (var alias in aliases)
        {
            if (TryReadAttribute(family.Attributes, alias, out var value))
            {
                var trimmed = value.Trim();
                return long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed.ToString(CultureInfo.InvariantCulture)
                    : trimmed;
            }
        }

        return null;
    }

    private static bool TryReadAttribute(
        IReadOnlyDictionary<string, string> attributes,
        string key,
        out string value)
    {
        if (attributes.TryGetValue(key, out value!))
        {
            return true;
        }

        foreach (var attribute in attributes)
        {
            if (string.Equals(attribute.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = attribute.Value;
                return true;
            }
        }

        value = "";
        return false;
    }

    private static string SerializeSpecialDrops(Monster family)
    {
        return JsonSerializer.Serialize(family.SpecialDrops ?? [], CompactJsonOptions);
    }

    private sealed record MonsterFamilyAuditFieldDefinition(
        string Name,
        IReadOnlyList<string> Aliases);
}
