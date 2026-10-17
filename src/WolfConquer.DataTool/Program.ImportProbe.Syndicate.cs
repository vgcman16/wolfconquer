using WolfConquer.Core.Data;

internal static partial class Program
{
    static void ProbeSyndicateLevels(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"syndicateLevels: {ReadSyndicateLevels(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"syndicateLevels: {ex.Message}");
        }
    }

    static IReadOnlyList<SyndicateLevel> ReadSyndicateLevels(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        var rows = new List<SyndicateLevel>();
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
                rows.Add(SyndicateParser.ParseLevelRow(Path.GetFileName(path), lineNumber, line));
            }
            catch (Exception ex)
            {
                throw new FormatException($"{Path.GetFileName(path)} line {lineNumber}: {ex.Message}", ex);
            }
        }

        return rows;
    }

    static void ProbeSyndicateFormationTypes(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"syndicateFormationTypes: {ReadSyndicateFormationTypes(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"syndicateFormationTypes: {ex.Message}");
        }
    }

    static IReadOnlyList<SyndicateFormationType> ReadSyndicateFormationTypes(string path)
    {
        return ReadRequiredRows(path, SyndicateFormationTypeParser.ParseRow);
    }

    static void ProbeSyndicateSkillTypes(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"syndicateSkillTypes: {ReadSyndicateSkillTypes(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"syndicateSkillTypes: {ex.Message}");
        }
    }

    static IReadOnlyList<SyndicateSkillType> ReadSyndicateSkillTypes(string path)
    {
        return ReadRequiredRows(path, SyndicateSkillTypeParser.ParseRow);
    }
}
