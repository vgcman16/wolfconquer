using WolfConquer.Core.Client;

namespace WolfConquer.Core.Data;

public static partial class WorldDataStartup
{
    public const string Chinese7937ComposePathEnvironmentVariable = "WOLFCONQUER_7937_XUANBAO_COMPOSE_PATH";
    public const string Chinese7937ComposeSha256EnvironmentVariable = "WOLFCONQUER_7937_XUANBAO_COMPOSE_SHA256";
    public const string Chinese7937RandomPathEnvironmentVariable = "WOLFCONQUER_7937_XUANBAO_RANDOM_PATH";
    public const string Chinese7937RandomSha256EnvironmentVariable = "WOLFCONQUER_7937_XUANBAO_RANDOM_SHA256";

    internal static bool TryBindConfiguredChinese7937Rules(WorldDataCatalog catalog, out string reason,
        Func<string, string?>? readEnvironment = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        readEnvironment ??= Environment.GetEnvironmentVariable;
        var composePath = readEnvironment(Chinese7937ComposePathEnvironmentVariable);
        var composePin = readEnvironment(Chinese7937ComposeSha256EnvironmentVariable);
        var randomPath = readEnvironment(Chinese7937RandomPathEnvironmentVariable);
        var randomPin = readEnvironment(Chinese7937RandomSha256EnvironmentVariable);
        reason = string.Empty;
        if (composePath is null && composePin is null && randomPath is null && randomPin is null) return true;
        if (string.IsNullOrWhiteSpace(composePath) || string.IsNullOrWhiteSpace(randomPath) ||
            !FullPin(composePin) || !FullPin(randomPin))
        { reason = "selected7937ComposeAndRandomPathsAndPinsRequired"; return false; }
        if (!Chinese7937XuanbaoRuleCatalogs.TryLoadPackedFiles(composePath, randomPath, 7937,
                Convert.FromHexString(composePin!), Convert.FromHexString(randomPin!), out var source, out var failure))
        { reason = "selected7937PackedRuleFilesRefused:" + failure; return false; }
        return catalog.TryBindChinese7937XuanbaoRules(source!, out reason);
    }

    private static bool FullPin(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
}
