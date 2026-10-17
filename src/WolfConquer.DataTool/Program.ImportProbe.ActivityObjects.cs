using WolfConquer.Core.Client;
using WolfConquer.Core.Data;

internal static partial class Program
{
    static void ProbePokerTables(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"pokerTables: {ReadPokerTables(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"pokerTables: {ex.Message}");
        }
    }

    static IReadOnlyList<PokerTable> ReadPokerTables(string path)
    {
        return ReadRequiredRows(path, ActivityObjectParser.ParsePokerTableRow, IsSemicolonCommentRow);
    }

    static void ProbeRouletteTables(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"rouletteTables: {ReadRouletteTables(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"rouletteTables: {ex.Message}");
        }
    }

    static IReadOnlyList<RouletteTable> ReadRouletteTables(string path)
    {
        return ReadRequiredRows(path, ActivityObjectParser.ParseRouletteTableRow, IsSemicolonCommentRow);
    }

    static void ProbeNpcTimeEntries(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"npcTimeEntries: {ReadNpcTimeEntries(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"npcTimeEntries: {ex.Message}");
        }
    }

    static IReadOnlyList<NpcTimeEntry> ReadNpcTimeEntries(string path)
    {
        return ReadRequiredRows(path, ActivityObjectParser.ParseNpcTimeRow, IsSemicolonCommentRow);
    }

    static void ProbeActivityTasks(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"activityTasks: {ReadActivityTasks(path).Tasks.Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"activityTasks: {ex.Message}");
        }
    }

    static ActivityTaskCatalog ReadActivityTasks(string path)
    {
        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        return ActivityTaskParser.Parse(encoding.GetString(File.ReadAllBytes(path)), Path.GetFileName(path));
    }

    static bool IsSemicolonCommentRow(string row)
    {
        return row.TrimStart().StartsWith(';');
    }
}
