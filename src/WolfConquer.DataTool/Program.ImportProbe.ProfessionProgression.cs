using WolfConquer.Core.Data;

internal static partial class Program
{
    static void ProbeClassLevelStats(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"classLevelStats: {ReadClassLevelStats(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"classLevelStats: {ex.Message}");
        }
    }

    static IReadOnlyList<ClassLevelStat> ReadClassLevelStats(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        return ClassLevelStatParser.Parse(File.ReadAllText(path), Path.GetFileName(path));
    }

    static void ProbeProfessionLevelUps(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"professionLevelUps: {ReadProfessionLevelUps(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"professionLevelUps: {ex.Message}");
        }
    }

    static IReadOnlyList<ProfessionLevelUp> ReadProfessionLevelUps(string path)
    {
        return ReadProfessionRows(path, ProfessionParser.ParseLevelUpRow);
    }

    static void ProbeProfessionLevelBenefits(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"professionLevelBenefits: {ReadProfessionLevelBenefits(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"professionLevelBenefits: {ex.Message}");
        }
    }

    static IReadOnlyList<ProfessionLevelBenefit> ReadProfessionLevelBenefits(string path)
    {
        return ReadProfessionRows(path, ProfessionParser.ParseLevelBenefitRow);
    }

    static void ProbeProfessionTitleBenefits(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"professionTitleBenefits: {ReadProfessionTitleBenefits(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"professionTitleBenefits: {ex.Message}");
        }
    }

    static IReadOnlyList<ProfessionTitleBenefit> ReadProfessionTitleBenefits(string path)
    {
        return ReadProfessionRows(path, ProfessionParser.ParseTitleBenefitRow);
    }

    static IReadOnlyList<T> ReadProfessionRows<T>(
        string path,
        Func<string, int, string, T> parse)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        var rows = new List<T>();
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
                rows.Add(parse(Path.GetFileName(path), lineNumber, line));
            }
            catch (Exception ex)
            {
                throw new FormatException($"{Path.GetFileName(path)} line {lineNumber}: {ex.Message}", ex);
            }
        }

        return rows;
    }
}
