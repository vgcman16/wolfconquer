using WolfConquer.Core.Data;

internal static partial class Program
{
    static void ProbeTutorTypes(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"tutorTypes: {ReadTutorTypes(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"tutorTypes: {ex.Message}");
        }
    }

    static IReadOnlyList<TutorType> ReadTutorTypes(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        var rows = new List<TutorType>();
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
                rows.Add(TutorParser.ParseTypeRow(Path.GetFileName(path), lineNumber, line));
            }
            catch (Exception ex)
            {
                throw new FormatException($"{Path.GetFileName(path)} line {lineNumber}: {ex.Message}", ex);
            }
        }

        return rows;
    }

    static void ProbeTutorBattleLimitTypes(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"tutorBattleLimitTypes: {ReadTutorBattleLimitTypes(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"tutorBattleLimitTypes: {ex.Message}");
        }
    }

    static IReadOnlyList<TutorBattleLimitType> ReadTutorBattleLimitTypes(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        var rows = new List<TutorBattleLimitType>();
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
                rows.Add(TutorParser.ParseBattleLimitTypeRow(Path.GetFileName(path), lineNumber, line));
            }
            catch (Exception ex)
            {
                throw new FormatException($"{Path.GetFileName(path)} line {lineNumber}: {ex.Message}", ex);
            }
        }

        return rows;
    }
}
