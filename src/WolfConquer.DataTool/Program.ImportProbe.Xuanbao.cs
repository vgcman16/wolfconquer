using WolfConquer.Core.Data;

internal static partial class Program
{
    static void ProbeXuanbaoAdditionAttributes(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"xuanbaoAdditionAttributes: {ReadXuanbaoAdditionAttributes(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"xuanbaoAdditionAttributes: {ex.Message}");
        }
    }

    static IReadOnlyList<XuanbaoAdditionAttribute> ReadXuanbaoAdditionAttributes(string path)
    {
        return ReadRequiredRows(
            path,
            (sourceFile, lineNumber, line) => XuanbaoParser.ParseAdditionAttributeRow(sourceFile, lineNumber, line));
    }

    static void ProbeXuanbaoComposeAttributeLimits(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"xuanbaoComposeAttributeLimits: {ReadXuanbaoComposeAttributeLimits(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"xuanbaoComposeAttributeLimits: {ex.Message}");
        }
    }

    static IReadOnlyList<XuanbaoComposeAttributeLimit> ReadXuanbaoComposeAttributeLimits(string path)
    {
        return ReadRequiredRows(
            path,
            (sourceFile, lineNumber, line) => XuanbaoParser.ParseComposeAttributeLimitRow(sourceFile, lineNumber, line));
    }

    static void ProbeXuanbaoRandomAttributes(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"xuanbaoRandomAttributes: {ReadXuanbaoRandomAttributes(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"xuanbaoRandomAttributes: {ex.Message}");
        }
    }

    static IReadOnlyList<XuanbaoRandomAttribute> ReadXuanbaoRandomAttributes(string path)
    {
        return ReadRequiredRows(
            path,
            (sourceFile, lineNumber, line) => XuanbaoParser.ParseRandomAttributeRow(sourceFile, lineNumber, line));
    }
}
