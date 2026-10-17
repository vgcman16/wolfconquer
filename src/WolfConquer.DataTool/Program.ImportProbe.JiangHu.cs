using WolfConquer.Core.Data;

internal static partial class Program
{
    static void ProbeJiangHuAttributes(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"jiangHuAttributes: {ReadJiangHuAttributes(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"jiangHuAttributes: {ex.Message}");
        }
    }

    static IReadOnlyList<JiangHuAttribute> ReadJiangHuAttributes(string path)
    {
        return JiangHuParser.ParseAttributesFile(path);
    }

    static void ProbeJiangHuCultivateStatuses(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"jiangHuCultivateStatuses: {ReadJiangHuCultivateStatuses(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"jiangHuCultivateStatuses: {ex.Message}");
        }
    }

    static IReadOnlyList<JiangHuCultivateStatus> ReadJiangHuCultivateStatuses(string path)
    {
        return JiangHuParser.ParseCultivateStatusesFile(path);
    }
}
