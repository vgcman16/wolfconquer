using WolfConquer.Core.Data;

internal static partial class Program
{
    static void ProbeRuneLevelExperience(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"runeLevelExperience: {ReadRuneLevelExperience(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"runeLevelExperience: {ex.Message}");
        }
    }

    static IReadOnlyList<RuneLevelExperience> ReadRuneLevelExperience(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        var rows = new List<RuneLevelExperience>();
        var lineNumber = 0;
        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                rows.Add(RuneParser.ParseLevelExperienceRow(Path.GetFileName(path), lineNumber, line));
            }
            catch (Exception ex)
            {
                throw new FormatException($"{Path.GetFileName(path)} line {lineNumber}: {ex.Message}", ex);
            }
        }

        return rows;
    }

    static void ProbeRuneStorageAttributes(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"runeStorageAttributes: {ReadRuneStorageAttributes(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"runeStorageAttributes: {ex.Message}");
        }
    }

    static IReadOnlyList<RuneStorageAttribute> ReadRuneStorageAttributes(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        var rows = new List<RuneStorageAttribute>();
        var lineNumber = 0;
        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                rows.Add(RuneParser.ParseStorageAttributeRow(Path.GetFileName(path), lineNumber, line));
            }
            catch (Exception ex)
            {
                throw new FormatException($"{Path.GetFileName(path)} line {lineNumber}: {ex.Message}", ex);
            }
        }

        return rows;
    }
}
