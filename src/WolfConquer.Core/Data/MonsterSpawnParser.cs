using System.Globalization;

namespace WolfConquer.Core.Data;

public static class MonsterSpawnParser
{
    private const string LegacyLoadMapMonstersSource = "LoadMapMonsters";
    private const string LegacyLoadMyMonstersSource = "LoadMyMonsters";

    public static MonsterSpawn Parse(
        IniDocument document,
        int? sourceMapFolderId = null,
        string? sourceMonsterName = null,
        string sourceRelativePath = "")
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(sourceRelativePath);

        var attributes = new Dictionary<string, string>(document.GetSection("cq_generator"), StringComparer.OrdinalIgnoreCase);
        var declaredMapId = ParseRequiredInt(attributes, "mapid");

        return new MonsterSpawn(
            ParseRequiredInt(attributes, "id"),
            sourceMapFolderId ?? declaredMapId,
            ParseRequiredInt(attributes, "npctype"),
            ParseRequiredInt(attributes, "bound_x"),
            ParseRequiredInt(attributes, "bound_y"),
            ParseRequiredInt(attributes, "bound_cx"),
            ParseRequiredInt(attributes, "bound_cy"),
            ParseRequiredInt(attributes, "maxnpc"),
            ParseRequiredInt(attributes, "rest_secs"),
            ParseRequiredInt(attributes, "max_per_gen"),
            ParseRequiredInt(attributes, "timer_begin"),
            ParseRequiredInt(attributes, "timer_end"),
            ParseRequiredInt(attributes, "born_x"),
            ParseRequiredInt(attributes, "born_y"),
            sourceMapFolderId,
            sourceMonsterName,
            sourceRelativePath,
            attributes);
    }

    public static IReadOnlyList<MonsterSpawn> ParseLegacyMapMonsterFile(
        string content,
        IReadOnlyDictionary<string, int> monsterTypeIdsByName,
        string sourceRelativePath = "")
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(monsterTypeIdsByName);
        ArgumentNullException.ThrowIfNull(sourceRelativePath);

        var spawns = new List<MonsterSpawn>();
        using var reader = new StringReader(content);
        var lineNumber = 0;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                spawns.Add(ParseLegacyMapMonsterLine(
                    line,
                    monsterTypeIdsByName,
                    sourceRelativePath,
                    lineNumber));
            }
            catch (FormatException ex) when (IsUnknownLegacyMapMonsterName(ex))
            {
                continue;
            }
            catch (FormatException ex) when (IsSkippedLegacyMapMonsterName(ex))
            {
                continue;
            }
        }

        return spawns;
    }

    public static MonsterSpawn ParseLegacyMapMonsterLine(
        string line,
        IReadOnlyDictionary<string, int> monsterTypeIdsByName,
        string sourceRelativePath = "",
        int sourceLine = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(line);
        ArgumentNullException.ThrowIfNull(monsterTypeIdsByName);
        ArgumentNullException.ThrowIfNull(sourceRelativePath);

        var fields = line.Split(',', StringSplitOptions.TrimEntries);
        if (fields.Length != 6)
        {
            throw new FormatException($"Legacy map monster spawn row has {fields.Length} fields; expected 6.");
        }

        var legacyUid = ParseInt(fields[0], "legacy map monster unique id");
        _ = ParseInt(fields[1], "legacy map monster lookface");
        var monsterName = fields[2];
        if (monsterName.Length == 0)
        {
            throw new FormatException("Legacy map monster name is required.");
        }

        if (monsterName.Contains("Titan", StringComparison.Ordinal))
        {
            throw new FormatException($"Skipped legacy map monster name '{monsterName}'.");
        }

        if (!TryGetLegacyMapMonsterTypeId(monsterTypeIdsByName, monsterName, out var monsterTypeId))
        {
            throw new FormatException($"Unknown legacy map monster name '{monsterName}'.");
        }

        var x = ParseInt(fields[3], "legacy map monster x");
        var y = ParseInt(fields[4], "legacy map monster y");
        var mapId = ParseInt(fields[5], "legacy map monster map id");
        var attributes = CreateAttributes(
            generatorId: legacyUid,
            mapId,
            monsterTypeId,
            boundX: x,
            boundY: y,
            boundWidth: 1,
            boundHeight: 1,
            maxNpc: 1,
            restSeconds: 0,
            maxPerGenerator: 1,
            timerBegin: 0,
            timerEnd: 0,
            bornX: 0,
            bornY: 0);
        attributes["legacy_uid"] = fields[0];
        attributes["legacy_lookface"] = fields[1];
        attributes["legacy_monster_name"] = monsterName;
        attributes["legacy_source"] = LegacyLoadMapMonstersSource;
        AddSourceLine(attributes, sourceLine);

        return new MonsterSpawn(
            legacyUid,
            mapId,
            monsterTypeId,
            x,
            y,
            1,
            1,
            1,
            0,
            1,
            0,
            0,
            0,
            0,
            null,
            monsterName,
            sourceRelativePath,
            attributes);
    }

    public static IReadOnlyList<MonsterSpawn> ParseLegacyMyMonstersFile(
        string content,
        string sourceRelativePath = "")
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(sourceRelativePath);

        var spawns = new List<MonsterSpawn>();
        using var reader = new StringReader(content);
        var lineNumber = 0;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            spawns.Add(ParseLegacyMyMonsterLine(line, sourceRelativePath, lineNumber));
        }

        return spawns;
    }

    public static MonsterSpawn ParseLegacyMyMonsterLine(
        string line,
        string sourceRelativePath = "",
        int sourceLine = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(line);
        ArgumentNullException.ThrowIfNull(sourceRelativePath);

        var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fields.Length != 10)
        {
            throw new FormatException($"Legacy myMonsters spawn row has {fields.Length} fields; expected 10.");
        }

        var generatorId = ParseInt(fields[0], "legacy myMonsters generator id");
        var mapId = ParseInt(fields[1], "legacy myMonsters map id");
        var x = ParseInt(fields[2], "legacy myMonsters x");
        var y = ParseInt(fields[3], "legacy myMonsters y");
        var width = ParseInt(fields[4], "legacy myMonsters width");
        var height = ParseInt(fields[5], "legacy myMonsters height");
        var maxNpc = ParseInt(fields[6], "legacy myMonsters max npc");
        var restSeconds = ParseInt(fields[7], "legacy myMonsters rest seconds");
        var maxPerGenerator = ParseInt(fields[8], "legacy myMonsters max per generator");
        var monsterTypeId = ParseInt(fields[9], "legacy myMonsters monster type id");
        var attributes = CreateAttributes(
            generatorId,
            mapId,
            monsterTypeId,
            x,
            y,
            width,
            height,
            maxNpc,
            restSeconds,
            maxPerGenerator,
            timerBegin: 0,
            timerEnd: 0,
            bornX: 0,
            bornY: 0);
        attributes["legacy_source"] = LegacyLoadMyMonstersSource;
        AddSourceLine(attributes, sourceLine);

        return new MonsterSpawn(
            generatorId,
            mapId,
            monsterTypeId,
            x,
            y,
            width,
            height,
            maxNpc,
            restSeconds,
            maxPerGenerator,
            0,
            0,
            0,
            0,
            null,
            null,
            sourceRelativePath,
            attributes);
    }

    private static int ParseRequiredInt(IReadOnlyDictionary<string, string> attributes, string key)
    {
        if (!attributes.TryGetValue(key, out var value))
        {
            throw new FormatException($"Missing monster spawn key '{key}'.");
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new FormatException($"Invalid monster spawn key '{key}': '{value}'.");
        }

        return parsed;
    }

    private static Dictionary<string, string> CreateAttributes(
        int generatorId,
        int mapId,
        int monsterTypeId,
        int boundX,
        int boundY,
        int boundWidth,
        int boundHeight,
        int maxNpc,
        int restSeconds,
        int maxPerGenerator,
        int timerBegin,
        int timerEnd,
        int bornX,
        int bornY)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = FormatInt(generatorId),
            ["mapid"] = FormatInt(mapId),
            ["npctype"] = FormatInt(monsterTypeId),
            ["bound_x"] = FormatInt(boundX),
            ["bound_y"] = FormatInt(boundY),
            ["bound_cx"] = FormatInt(boundWidth),
            ["bound_cy"] = FormatInt(boundHeight),
            ["maxnpc"] = FormatInt(maxNpc),
            ["rest_secs"] = FormatInt(restSeconds),
            ["max_per_gen"] = FormatInt(maxPerGenerator),
            ["timer_begin"] = FormatInt(timerBegin),
            ["timer_end"] = FormatInt(timerEnd),
            ["born_x"] = FormatInt(bornX),
            ["born_y"] = FormatInt(bornY)
        };
    }

    private static void AddSourceLine(IDictionary<string, string> attributes, int sourceLine)
    {
        if (sourceLine > 0)
        {
            attributes["source_line"] = FormatInt(sourceLine);
        }
    }

    private static int ParseInt(string value, string fieldName)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new FormatException($"Invalid {fieldName}: '{value}'.");
        }

        return parsed;
    }

    private static string FormatInt(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static bool TryGetLegacyMapMonsterTypeId(
        IReadOnlyDictionary<string, int> monsterTypeIdsByName,
        string monsterName,
        out int monsterTypeId)
    {
        foreach (var entry in monsterTypeIdsByName)
        {
            if (string.Equals(entry.Key, monsterName, StringComparison.Ordinal))
            {
                monsterTypeId = entry.Value;
                return true;
            }
        }

        monsterTypeId = 0;
        return false;
    }

    private static bool IsUnknownLegacyMapMonsterName(FormatException ex)
    {
        return ex.Message.StartsWith("Unknown legacy map monster name '", StringComparison.Ordinal);
    }

    private static bool IsSkippedLegacyMapMonsterName(FormatException ex)
    {
        return ex.Message.StartsWith("Skipped legacy map monster name '", StringComparison.Ordinal);
    }
}
