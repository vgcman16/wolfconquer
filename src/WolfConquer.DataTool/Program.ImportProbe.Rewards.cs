using WolfConquer.Core.Data;

internal static partial class Program
{
    static void ProbeLotteryDictionary(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"lotteryDictionary: {ReadLotteryDictionary(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"lotteryDictionary: {ex.Message}");
        }
    }

    static void ProbeCollectionRewardTiers(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"collectionRewardTiers: {ReadCollectionRewardTiers(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"collectionRewardTiers: {ex.Message}");
        }
    }

    static IReadOnlyList<LotteryDictionaryEntry> ReadLotteryDictionary(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        var rows = new List<LotteryDictionaryEntry>();
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
                rows.Add(LotteryParser.ParseDictionaryRow(Path.GetFileName(path), lineNumber, line));
            }
            catch (Exception ex)
            {
                throw new FormatException($"{Path.GetFileName(path)} line {lineNumber}: {ex.Message}", ex);
            }
        }

        return rows;
    }

    static IReadOnlyList<CollectionRewardTier> ReadCollectionRewardTiers(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        var rows = new List<CollectionRewardTier>();
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
                rows.Add(CollectionParser.ParseRewardTierRow(Path.GetFileName(path), lineNumber, line));
            }
            catch (Exception ex)
            {
                throw new FormatException($"{Path.GetFileName(path)} line {lineNumber}: {ex.Message}", ex);
            }
        }

        return rows;
    }

    static void ProbeTaskRewardTypes(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"taskRewardTypes: {ReadTaskRewardTypes(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"taskRewardTypes: {ex.Message}");
        }
    }

    static IReadOnlyList<TaskRewardTypeEntry> ReadTaskRewardTypes(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        var rows = new List<TaskRewardTypeEntry>();
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
                rows.Add(TaskRewardParser.ParseTypeRow(Path.GetFileName(path), lineNumber, line));
            }
            catch (Exception ex)
            {
                throw new FormatException($"{Path.GetFileName(path)} line {lineNumber}: {ex.Message}", ex);
            }
        }

        return rows;
    }

    static void ProbeTaskRewardTypeIniEntries(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"taskRewardTypeIniEntries: {ReadTaskRewardTypeIniEntries(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"taskRewardTypeIniEntries: {ex.Message}");
        }
    }

    static IReadOnlyList<TaskRewardTypeIniEntry> ReadTaskRewardTypeIniEntries(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        var rows = new List<TaskRewardTypeIniEntry>();
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
                rows.Add(TaskRewardParser.ParseTypeIniRow(Path.GetFileName(path), lineNumber, line));
            }
            catch (Exception ex)
            {
                throw new FormatException($"{Path.GetFileName(path)} line {lineNumber}: {ex.Message}", ex);
            }
        }

        return rows;
    }
}
