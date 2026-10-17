using WolfConquer.Core.Client;
using WolfConquer.Core.Data;

internal static partial class Program
{
    static IReadOnlyList<string> ResolveIncludedGameMapExPaths(string[] args)
    {
        var includedPaths = new List<string>();
        for (var index = 1; index < args.Length; index++)
        {
            if (!string.Equals(args[index], "--include-game-map-ex", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            index++;
            if (index >= args.Length)
            {
                throw new ArgumentException("Missing path after --include-game-map-ex.");
            }

            includedPaths.Add(Path.GetFullPath(args[index]));
        }

        return includedPaths;
    }

    static int ExportLegacyGameMaps(string[] args)
    {
        var positional = new List<string>();
        var includedInputs = new List<string>();
        for (var i = 1; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--include", StringComparison.OrdinalIgnoreCase))
            {
                if (++i >= args.Length)
                {
                    Console.Error.WriteLine("Missing path after --include.");
                    return 1;
                }

                includedInputs.Add(args[i]);
                continue;
            }

            positional.Add(args[i]);
        }

        var inputPaths = new List<string>();
        if (positional.Count > 0)
        {
            inputPaths.Add(positional[0]);
        }
        else
        {
            inputPaths.Add(@"data/DBServer");
        }

        inputPaths.AddRange(positional.Skip(2));
        inputPaths.AddRange(includedInputs);

        var outputPath = positional.Count > 1
            ? Path.GetFullPath(positional[1])
            : Path.GetFullPath(Path.Combine(".setup_work", "seed-json"));

        Directory.CreateDirectory(outputPath);

        var gameMapPaths = inputPaths.Select(ResolveLegacyGameMapDatPath).ToArray();
        var legacyGameMaps = ReadMergedLegacyGameMaps(gameMapPaths);
        WriteJson(Path.Combine(outputPath, "legacyGameMaps.json"), legacyGameMaps);

        foreach (var gameMapPath in gameMapPaths)
        {
            Console.WriteLine($"GameMap.dat: {gameMapPath}");
        }

        Console.WriteLine($"Output: {outputPath}");
        Console.WriteLine($"legacyGameMaps: {legacyGameMaps.Length:N0}");
        return 0;
    }

    static string ResolveLegacyGameMapDatPath(string inputPath)
    {
        var fullPath = Path.GetFullPath(inputPath);
        return Directory.Exists(fullPath)
            ? Path.Combine(fullPath, "ini", "GameMap.dat")
            : fullPath;
    }

    static LegacyGameMapEntry[] ReadMergedLegacyGameMaps(IReadOnlyList<string> gameMapPaths)
    {
        var seenMapIds = new HashSet<int>();
        var entries = new List<LegacyGameMapEntry>();
        foreach (var gameMapPath in gameMapPaths)
        {
            foreach (var entry in LegacyGameMapParser.ParseGameMapDatFile(gameMapPath))
            {
                if (seenMapIds.Add(entry.MapId))
                {
                    entries.Add(entry);
                }
            }
        }

        return entries.ToArray();
    }

    static void ProbeMaps(string path, ImportProbeResult result)
    {
        if (!Directory.Exists(path))
        {
            result.RecordFailure($"maps: missing directory {path}");
            return;
        }

        try
        {
            var count = ReadMaps(path).Count;
            Console.WriteLine($"maps: {count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"maps: {ex.Message}");
        }
    }

    static void ProbeMapNames(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"mapNames: {MapNameParser.ParseFile(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"mapNames: {ex.Message}");
        }
    }

    static void ProbeLegacyGameMaps(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"legacyGameMaps: {LegacyGameMapParser.ParseGameMapDatFile(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"legacyGameMaps: {ex.Message}");
        }
    }

    static IReadOnlyList<MapMetadata> ReadMaps(string rootPath)
    {
        if (!Directory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException($"Missing maps directory: {rootPath}");
        }

        var maps = new List<MapMetadata>();
        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        foreach (var path in Directory.EnumerateFiles(rootPath, "*.ini").Order(StringComparer.OrdinalIgnoreCase))
        {
            var document = IniDocument.Parse(encoding.GetString(File.ReadAllBytes(path)));
            maps.Add(MapMetadataParser.Parse(document, Path.GetFileName(path)));
        }

        return maps;
    }
}
