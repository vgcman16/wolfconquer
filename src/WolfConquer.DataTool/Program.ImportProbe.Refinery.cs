using WolfConquer.Core.Data;

internal static partial class Program
{
    static void ProbeRefineryBoxes(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"refineryBoxes: {ReadRefineryBoxes(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"refineryBoxes: {ex.Message}");
        }
    }

    static IReadOnlyList<RefineryBox> ReadRefineryBoxes(string path)
    {
        return RefineryParser.ParseBoxesFile(path);
    }

    static void ProbeRefinerySouls(string dbServerPath, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"refinerySouls: {ReadRefinerySouls(dbServerPath).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"refinerySouls: {ex.Message}");
        }
    }

    static IReadOnlyList<RefinerySoul> ReadRefinerySouls(string dbServerPath)
    {
        var rows = new List<RefinerySoul>();
        for (var level = 1; level <= 7; level++)
        {
            rows.AddRange(RefineryParser.ParseSoulsFile(Path.Combine(dbServerPath, $"Souls{level}.ini")));
        }

        return rows;
    }

    static void ProbeRefineryMaterials(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"refineryMaterials: {ReadRefineryMaterials(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"refineryMaterials: {ex.Message}");
        }
    }

    static IReadOnlyList<RefineryMaterial> ReadRefineryMaterials(string path)
    {
        return RefineryParser.ParseMaterialsFile(path);
    }
}
