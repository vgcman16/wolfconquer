using System.Globalization;
using System.Text.Json;
using WolfConquer.Core.World;

internal static partial class Program
{
    private static int InspectOwnedPlayer(string[] args)
    {
        if (args.Length != 3 || !uint.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out var account) || account == 0)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { success = false, reason = "ownedPlayerInspectionUsage" }));
            return 1;
        }
        if (!LegacyOwnedPlayerInspection.TryInspect(args[1], account, out var report, out var reason))
        {
            Console.WriteLine(JsonSerializer.Serialize(new { success = false, reason }));
            return 1;
        }
        Console.WriteLine(JsonSerializer.Serialize(new { success = true, report },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }));
        return 0;
    }
}
