using System.Globalization;
using WolfConquer.Core.Data;

public static class MonsterSpawnImport
{
    private static readonly HashSet<string> LegacyStartupMapMonsterFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Monsters1002.txt",
        "Monsters1020.txt",
        "Monsters1011.txt",
        "Monsters1768.txt",
        "Monsters1000.txt",
        "Monsters1001.txt",
        "Monsters1779.txt",
        "Monsters1780.txt",
        "Monsters3935.txt",
        "Monsters3998.txt",
        "Monsters1043.txt",
        "Monsters1044.txt",
        "Monsters1045.txt",
        "Monsters1046.txt",
        "Monsters1047.txt",
        "Monsters1048.txt",
        "Monsters8822.txt",
        "Monsters1015.txt",
        "Monsters1016.txt",
        "Monsters3845.txt",
        "Monsters10250.txt",
        "Monsters10166.txt",
        "Monsters10089.txt",
        "Monsters11127.txt",
        "Monsters10137.txt",
        "Monsters3071.txt",
        "Monsters10341.txt",
        "Monsters10342.txt",
        "Monsters10323.txt",
        "Monsters10324.txt",
        "Monsters10322.txt",
        "Monsters10343.txt",
    };

    public static IReadOnlyList<MonsterSpawn> ReadMonsterSpawns(
        string rootPath,
        IReadOnlyDictionary<string, int> monsterTypeIdsByName,
        MonsterSpawnImportMode mode = MonsterSpawnImportMode.LegacyStartup)
    {
        ArgumentNullException.ThrowIfNull(monsterTypeIdsByName);

        if (!Directory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException($"Missing monster spawns directory: {rootPath}");
        }

        var monsterTypeIds = monsterTypeIdsByName.Values.ToHashSet();
        var spawns = new List<MonsterSpawn>();
        if (mode == MonsterSpawnImportMode.FullInventory)
        {
            foreach (var path in Directory.EnumerateFiles(rootPath, "*.ini", SearchOption.AllDirectories).Order(StringComparer.OrdinalIgnoreCase))
            {
                var document = IniDocument.Parse(File.ReadAllText(path));
                if (!document.Sections.ContainsKey("cq_generator"))
                {
                    continue;
                }

                var spawn = MonsterSpawnParser.Parse(
                    document,
                    sourceMapFolderId: TryGetSourceMapFolderId(rootPath, path),
                    sourceMonsterName: TryGetSourceMonsterName(rootPath, path),
                    sourceRelativePath: Path.GetRelativePath(rootPath, path));
                if (!monsterTypeIds.Contains(spawn.MonsterTypeId))
                {
                    continue;
                }

                spawns.Add(spawn);
            }
        }

        foreach (var path in Directory.EnumerateFiles(rootPath, "Monsters*.txt", SearchOption.TopDirectoryOnly).Order(StringComparer.OrdinalIgnoreCase))
        {
            var fileName = Path.GetFileName(path);
            if (mode == MonsterSpawnImportMode.LegacyStartup &&
                !LegacyStartupMapMonsterFiles.Contains(fileName))
            {
                continue;
            }

            spawns.AddRange(MonsterSpawnParser.ParseLegacyMapMonsterFile(
                File.ReadAllText(path),
                monsterTypeIdsByName,
                Path.GetRelativePath(rootPath, path)));
        }

        var myMonstersPath = Path.Combine(rootPath, "myMonsters.txt");
        if (mode == MonsterSpawnImportMode.FullInventory &&
            File.Exists(myMonstersPath))
        {
            spawns.AddRange(MonsterSpawnParser.ParseLegacyMyMonstersFile(
                    File.ReadAllText(myMonstersPath),
                    Path.GetRelativePath(rootPath, myMonstersPath))
                .Where(spawn => monsterTypeIds.Contains(spawn.MonsterTypeId)));
        }

        return spawns;
    }

    private static int? TryGetSourceMapFolderId(string rootPath, string path)
    {
        var relative = Path.GetRelativePath(rootPath, path);
        var firstSegment = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).FirstOrDefault();
        return int.TryParse(firstSegment, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string? TryGetSourceMonsterName(string rootPath, string path)
    {
        var relative = Path.GetRelativePath(rootPath, path);
        var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Length >= 3 ? segments[1] : null;
    }
}

public enum MonsterSpawnImportMode
{
    LegacyStartup = 0,
    FullInventory = 1,
}
