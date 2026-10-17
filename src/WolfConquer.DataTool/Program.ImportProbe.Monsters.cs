using WolfConquer.Core.Data;

internal static partial class Program
{
    static IReadOnlyDictionary<string, int> ProbeMonsters(string path, ImportProbeResult result)
    {
        if (!Directory.Exists(path))
        {
            result.RecordFailure($"monsters: missing directory {path}");
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        var count = 0;
        var monsterTypeIdsByName = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(path, "*.ini"))
        {
            try
            {
                var monster = MonsterParser.Parse(IniDocument.Parse(File.ReadAllText(file)));
                monsterTypeIdsByName.TryAdd(monster.Name, monster.Id);
                count++;
            }
            catch (Exception ex)
            {
                result.RecordFailure($"monsters: {Path.GetFileName(file)}: {ex.Message}");
            }
        }

        Console.WriteLine($"monsters: {count:N0}");
        return monsterTypeIdsByName;
    }

    static void ProbeMonsterSpawns(
        string path,
        IReadOnlyDictionary<string, int> monsterTypeIdsByName,
        ImportProbeResult result)
    {
        ArgumentNullException.ThrowIfNull(monsterTypeIdsByName);

        if (!Directory.Exists(path))
        {
            result.RecordFailure($"monsterSpawns: missing directory {path}");
            return;
        }

        try
        {
            var count = MonsterSpawnImport.ReadMonsterSpawns(path, monsterTypeIdsByName).Count;
            Console.WriteLine($"monsterSpawns: {count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"monsterSpawns: {ex.Message}");
        }
    }

    static IReadOnlyDictionary<string, int> BuildMonsterTypeIdsByName(IEnumerable<MonsterSeed> monsters)
    {
        ArgumentNullException.ThrowIfNull(monsters);

        var monsterTypeIdsByName = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var monster in monsters)
        {
            monsterTypeIdsByName.TryAdd(monster.Name, monster.Id);
        }

        return monsterTypeIdsByName;
    }
}
